using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using NyaLang.Antlr;

namespace NyaLang
{
    public class Stage2Visitor : NyaBaseVisitor<object>
    {
        const string NullKeyword = "nil";

        TypeBuilder _currTypeBuilder = null;
        ILGenerator _ilg = null;
        Label returnLabel;
        int _stackDepth = 0;

        string[] _currentUsing = new[]
        {
            "System",
            "System.Collections.Generic",
            "System.Linq"
        };


        ScopeManager _scopeManager = new ScopeManager();

        public AssemblyBuilder _asmBuilder;
        private IEnumerable<ClassDescriptor> _classDescriptors;
        private ModuleBuilder _moduleBuilder;
        private string _outPath;

        private Dictionary<string, Type> typeAliases = new Dictionary<string, Type>()
        {
            { "string", typeof(String) }, { "bool", typeof(Boolean) }, { "float", typeof(Single) }, { "double", typeof(Double) },
            { "sbyte", typeof(SByte) }, { "short", typeof(Int16) }, { "int", typeof(Int32) }, { "long", typeof(Int64) },
            { "byte", typeof(Byte) }, { "ushort", typeof(UInt16) }, { "uint", typeof(UInt32) }, { "ulong", typeof(UInt64) },
            { "decimal", typeof(decimal) }, { "object", typeof(object) }
        };

        public Stage2Visitor(AssemblyBuilder asmBuilder, ModuleBuilder modBuilder, IEnumerable<ClassDescriptor> descriptors)
        {
            _asmBuilder = asmBuilder;
            _classDescriptors = descriptors;
            _moduleBuilder = modBuilder;
        }

        public void SetEntryPoint(MethodBuilder builder, PEFileKinds kind)
        {
            _asmBuilder.SetEntryPoint(builder, kind);
        }

        public void Visit(IParseTree tree, TypeBuilder builder)
        {
            _currTypeBuilder = builder;
            Visit(tree);
        }

        public override object VisitCompilation_unit([NotNull] NyaParser.Compilation_unitContext context)
        {
            _scopeManager.Push(ScopeLevel.Global);

            foreach(var descriptor in _classDescriptors)
            {
                _scopeManager.Push(ScopeLevel.Class);

                _currTypeBuilder = descriptor.Builder;

                VisitChildren(descriptor.Context);

                // TODO: Check constructor visibility

                _currTypeBuilder.CreateType();

                _currTypeBuilder = null;

                _scopeManager.Pop();

            }

            foreach (var child in context.children.Where(x => !( x is NyaParser.Class_declarationContext)))
            {
                Visit(child);
            }

            _moduleBuilder.CreateGlobalFunctions();

            SetAssemblyVersionInfo();

            _scopeManager.Pop();
            return null;
        }

        public override object VisitClass_declaration([NotNull] NyaParser.Class_declarationContext context)
        {
            return null;
        }

        public override object VisitInterface_declaration([NotNull] NyaParser.Interface_declarationContext context)
        {
            return null;
        }

        private Type FindType(string name, string[] namespaces = null)
        {
            if (namespaces == null)
                namespaces = _currentUsing ?? new string[] { };
            Type t = _asmBuilder.GetType(name) ?? Type.GetType(name);

            if (t == null)
            {
                foreach (string ns in namespaces)
                {
                    t = _asmBuilder.GetType(ns + '.' + name) ?? Type.GetType(ns + '.' + name);
                    if (t != null)
                        break;
                }
            }

            return t;
        }

        private void SetAssemblyVersionInfo()
        {
            // TODO: Others magic assembly stuff
            AssemblyDetail detail = new AssemblyDetail(_asmBuilder);
            _asmBuilder.DefineVersionInfoResource();
        }

        public override object VisitGlobal_attribute([NotNull] NyaParser.Global_attributeContext context)
        {
            string typeName = context.identifier().GetText();
            if (!typeName.EndsWith("Attribute"))
                typeName += "Attribute";

            Type type = FindType(typeName, new[] { "System", "System.Reflection", "System.InteropServices" });

            List<object> conArgs = new List<object>();

            foreach(var arg in context.attribute_arguments().children.OfType<NyaParser.Attribute_argumentContext>())
            {
                var identifier = arg.identifier();
                if(identifier == null)
                {
                    conArgs.Add(Visit(arg.literal()));
                }
            }

            ConstructorInfo cInfo = type.GetConstructor(conArgs.Select(x => x.GetType()).ToArray());

            if(cInfo != null)
            {
                _asmBuilder.SetCustomAttribute(new CustomAttributeBuilder(cInfo, conArgs.ToArray()));
            }

            return base.VisitGlobal_attribute(context);
        }

        public override object VisitMethod_declaration([NotNull] NyaParser.Method_declarationContext context)
        {
            // TODO: Break out constructor logic to new method

            string methodName = context.identifier().GetText();
            bool bIsEntry = false;
            bool isStatic = context.IsStatic != null;
            bool isConstructor = methodName == "New";

            _scopeManager.Push(ScopeLevel.Method);

            MethodAttributes methAttrs = MethodAttributes.Private | MethodAttributes.HideBySig;

            if (isStatic)
            {
                methAttrs |= MethodAttributes.Static;
            }

            if (isConstructor)
            {
                methodName = isStatic ? ".cctor" : ".ctor";
                methAttrs |= MethodAttributes.SpecialName;
            }

            bool isFinal = true;

            foreach (var attr in context.attributes()?.children?
                .OfType<NyaParser.AttributeContext>() ?? new NyaParser.AttributeContext[] { })
            {
                string attrName = attr.identifier().GetText();
                switch (attrName)
                {
                    case "entry":
                        bIsEntry = true;
                        break;
                    case "public":
                        methAttrs ^= MethodAttributes.Private;
                        methAttrs |= MethodAttributes.Public;
                        break;
                    case "virtual":
                        methAttrs |= MethodAttributes.Virtual;
                        break;
                    case "abstract":
                        methAttrs |= MethodAttributes.Abstract;
                        break;
                }
            }

            var parameters = context.fixed_parameters()?.children?
                .OfType<NyaParser.Fixed_parameterContext>() ?? new NyaParser.Fixed_parameterContext[] { };

            Type[] paramTypes = parameters.Select(x => ParseTypeDescriptor(x.type_descriptor())).ToArray();
            Type returnType = ParseTypeDescriptor(context.type_descriptor());

            MethodInfo baseMethod = FindParentDefinition(_currTypeBuilder, methodName, paramTypes);

            MethodBuilder methodBuilder;

            if(baseMethod != null)
            {
                if (baseMethod.IsVirtual && !baseMethod.IsFinal)
                {
                    if ((methAttrs & MethodAttributes.Virtual) != MethodAttributes.Virtual)
                        methAttrs |= MethodAttributes.Final;
                    methAttrs |= MethodAttributes.Virtual;
                    if (baseMethod.DeclaringType.IsInterface)
                        methAttrs |= MethodAttributes.NewSlot;

                    if ((methAttrs & MethodAttributes.Public) != MethodAttributes.Public)
                        throw new Exception("Oy! This needs to be public!");
                }
                if (methodName == ".ctor" && !baseMethod.IsPublic)
                {
                    throw new Exception("Oy! The base constructor needs to be public!");
                }
            }

            if (_currTypeBuilder != null)
            {
                methodBuilder = _currTypeBuilder.DefineMethod(methodName, methAttrs, returnType, paramTypes);
            }
            else
            {
                methAttrs |= MethodAttributes.Static;
                methodBuilder = _moduleBuilder.DefineGlobalMethod(methodName, methAttrs, returnType, paramTypes);
            }

            for (int i = 0; i < parameters.Count(); i++)
            {
                var param = parameters.ElementAt(i);
                string paramName = param.identifier().GetText();

                ParameterAttributes pAttrs = ParameterAttributes.None;

                ParameterBuilder pb = methodBuilder.DefineParameter(i + 1, pAttrs, paramName);
                _scopeManager.AddVariable(paramName, pb, paramTypes[i]);

                if (param.Question() != null || param.literal() != null)
                {
                    pb.SetCustomAttribute(
                        new CustomAttributeBuilder(
                            typeof(OptionalAttribute).GetConstructor(new Type[] { }),
                            new object[] { })
                    );

                    if (param.literal() != null)
                    {
                        object o = Visit(param.literal());

                        if (o != null)
                        {
                            pb.SetConstant(o);
                        }
                    }
                }
            }

            if (bIsEntry)
                SetEntryPoint(methodBuilder, PEFileKinds.ConsoleApplication);

            _ilg = methodBuilder.GetILGenerator();

            returnLabel = _ilg.DefineLabel();

            var block = context.block();

            if (isConstructor && !isStatic)
            {
                _ilg.Emit(OpCodes.Ldarg_0);
                ConstructorInfo ci = _currTypeBuilder.BaseType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { },
                    null
                    );

                _ilg.Emit(OpCodes.Callvirt, ci);
                _ilg.Emit(OpCodes.Nop);
            }

            if (block != null && block.ChildCount > 0)
            {
                Visit(block);
                while(_stackDepth > 0)
                {
                    // Clean up after the user
                    _ilg.Emit(OpCodes.Pop);
                    _stackDepth--;
                }
            }
            else
            {
                // We need to do _something_
                _ilg.Emit(OpCodes.Nop);
                _ilg.Emit(OpCodes.Br_S, returnLabel);
            }
            _ilg.MarkLabel(returnLabel);
            _ilg.Emit(OpCodes.Ret);

            _ilg = null;

            _scopeManager.Pop();

            return methodBuilder;
        }

        private MethodInfo FindParentDefinition(Type t, string name, Type[] methodArgs)
        {
            if (t == null)
                return null;
            MethodInfo info = null;
            if (t.BaseType != null)
            {
                info = _currTypeBuilder.BaseType.GetMethod(name, methodArgs);
                if (info != null)
                    return info;
                info = FindParentDefinition(t.BaseType, name, methodArgs);
            } else
            {
                info = typeof(object).GetMethod(name, methodArgs);
            }
            if (info != null)
                return info;

            foreach(var iface in t.GetInterfaces())
            {
                info = iface.GetMethod(name, methodArgs);
                if (info != null)
                    return info;
                info = FindParentDefinition(iface, name, methodArgs);
                if (info != null)
                    return info;
            }
            return null;
        }

        public override object VisitInterface_method_declaration([NotNull] NyaParser.Interface_method_declarationContext context)
        {
            // TODO: Break out constructor logic to new method

            string methodName = context.identifier().GetText();

            _scopeManager.Push(ScopeLevel.Method);

            MethodAttributes methAttrs = MethodAttributes.Public | MethodAttributes.HideBySig |
                MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot;


            foreach (var attr in context.attributes()?.children?
                .OfType<NyaParser.AttributeContext>() ?? new NyaParser.AttributeContext[] { })
            {
                string attrName = attr.identifier().GetText();
                switch (attrName)
                {
                    case "public":
                        break;
                }
            }

            var parameters = context.fixed_parameters()?.children?
                .OfType<NyaParser.Fixed_parameterContext>() ?? new NyaParser.Fixed_parameterContext[] { };

            Type[] paramTypes = parameters.Select(x => ParseTypeDescriptor(x.type_descriptor())).ToArray();
            Type returnType = ParseTypeDescriptor(context.type_descriptor());

            MethodBuilder methodBuilder;

            methodBuilder = _currTypeBuilder.DefineMethod(methodName, methAttrs, returnType, paramTypes);

            for (int i = 0; i < parameters.Count(); i++)
            {
                var param = parameters.ElementAt(i);
                string paramName = param.identifier().GetText();

                ParameterAttributes pAttrs = ParameterAttributes.None;

                ParameterBuilder pb = methodBuilder.DefineParameter(i + 1, pAttrs, paramName);
                _scopeManager.AddVariable(paramName, pb, paramTypes[i]);

                if (param.Question() != null || param.literal() != null)
                {
                    pb.SetCustomAttribute(
                        new CustomAttributeBuilder(
                            typeof(OptionalAttribute).GetConstructor(new Type[] { }),
                            new object[] { })
                    );

                    if (param.literal() != null)
                    {
                        object o = Visit(param.literal());

                        if (o != null)
                        {
                            pb.SetConstant(o);
                        }
                    }
                }
            }

            _scopeManager.Pop();

            return methodBuilder;
        }

        private Type ParseType(NyaParser.TypeContext context)
        {
            Type t = typeof(void);
            if (context != null)
            {
                string typeName = context.identifier().GetText();
                var typeArgs = new Type[] { };

                if (context.type_argument_list() != null)
                {
                    typeArgs = context.type_argument_list().children.OfType<NyaParser.TypeContext>().Select(x => ParseType(x)).ToArray();
                }

                if (typeAliases.ContainsKey(typeName))
                {
                    t = typeAliases[typeName];
                }
                else
                {
                    if (typeArgs.Length > 0)
                        typeName += "`" + typeArgs.Length;
                    t = FindType(typeName, _currentUsing);
                }

                if( context.type_argument_list() != null)
                {
                    t = t.MakeGenericType(typeArgs);
                }

                if (context.array_type() != null)
                {
                    t = t.MakeArrayType();
                }
            }
            return t;
        }

        private Type ParseTypeDescriptor(NyaParser.Type_descriptorContext context)
        {
            if (context != null)
            {
                return ParseType(context.type());
            }
            return typeof(void);
        }

        public override object VisitType_descriptor([NotNull] NyaParser.Type_descriptorContext context)
        {
            return ParseTypeDescriptor(context);
        }

        public override object VisitParenthesisExp([NotNull] NyaParser.ParenthesisExpContext context)
        {
            // Required to pass type
            return Visit(context.expression());
        }

        private Type EmitLiteral(object o)
        {
            _stackDepth++;
            return LiteralHelper.EmitLiteral(_ilg, o);
        }

        public override object VisitNullLiteral([NotNull] NyaParser.NullLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitBoolLiteral([NotNull] NyaParser.BoolLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitStringLiteral([NotNull] NyaParser.StringLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitRealLiteral([NotNull] NyaParser.RealLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitIntegerLiteral([NotNull] NyaParser.IntegerLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitRegexLiteral([NotNull] NyaParser.RegexLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitReturnStatement([NotNull] NyaParser.ReturnStatementContext context)
        {
            var exp = context.expression();
            if (exp != null)
            {
                Visit(exp);
                _stackDepth--;
            }
            _ilg.Emit(OpCodes.Br_S, returnLabel);
            return null;
        }

        public override object VisitAssignment([NotNull] NyaParser.AssignmentContext context)
        {
            switch (context.assignment_operator().GetText())
            {
                case "=":
                    return DoBasicAssignment(context);
                case "?=":
                    return DoCoalesceAssignment(context);
                default:
                    return DoArithmenticAssignment(context);
            }
        }

        private Type DoBasicAssignment(NyaParser.AssignmentContext context)
        {
            string sLocal = context.identifier().GetText();

            Variable local = _scopeManager.FindVariable(sLocal);

            Type src = (Type)Visit(context.expression());
            Type dst = null;

            if (local != null)
            {
                dst = local.Type;
            }
            else
            {
                if (context.type_descriptor() != null)
                    dst = ParseTypeDescriptor(context.type_descriptor());
                else
                    dst = src;

                // Someone did an untyped null assignment...
                if (dst == null)
                    dst = typeof(object);

                local = _ilg.DeclareLocal(dst);
                _scopeManager.AddVariable(sLocal, local);
            }

            local.Store(_ilg);
            _stackDepth--;

            return local.Type;
        }

        private Type DoArithmenticAssignment(NyaParser.AssignmentContext context)
        {
            string sOperator = context.assignment_operator().GetText();
            string sLocal = context.identifier().GetText();
            Variable local = _scopeManager.FindVariable(sLocal);

            local.Load(_ilg);
            _stackDepth++;

            Type src = (Type)Visit(context.expression());
            Type dst = local.Type;

            switch (sOperator)
            {
                case "+=": OpHelper.DoMath(_ilg, dst, src, OpCodes.Add, "op_Addition"); break;
                case "-=": OpHelper.DoMath(_ilg, dst, src, OpCodes.Sub, "op_Subtraction"); break;
                case "*=": OpHelper.DoMath(_ilg, dst, src, OpCodes.Mul, "op_Multiply"); break;
                case "/=": OpHelper.DoMath(_ilg, dst, src, OpCodes.Div, "op_Division"); break;
                case "%=": OpHelper.DoMath(_ilg, dst, src, OpCodes.Rem, "op_Modulus"); break;
                case "&=": OpHelper.DoMath(_ilg, dst, src, OpCodes.And, "op_BitwiseAnd"); break;
                case "|=": OpHelper.DoMath(_ilg, dst, src, OpCodes.Or, "op_BitwiseOr"); break;
                case "^=": OpHelper.DoMath(_ilg, dst, src, OpCodes.Xor, "op_ExclusiveOr"); break;
                case "<<=": OpHelper.DoMath(_ilg, dst, src, OpCodes.Shl, "op_LeftShift"); break;
                case ">>=": OpHelper.DoMath(_ilg, dst, src, OpCodes.Shr, "op_RightShift"); break;
            }

            local.Store(_ilg);
            _stackDepth -= 2;

            return local.Type;
        }

        private Type DoCoalesceAssignment(NyaParser.AssignmentContext context)
        {
            string sLocal = context.identifier().GetText();
            Variable local = _scopeManager.FindVariable(sLocal);

            Label nullOp = _ilg.DefineLabel();

            local.Load(_ilg);
            _stackDepth++;
            _ilg.Emit(OpCodes.Dup);
            _ilg.Emit(OpCodes.Brtrue_S, nullOp);
            _ilg.Emit(OpCodes.Pop);

            Type src = (Type)Visit(context.expression());
            Type dst = local.Type;

            if (!OpHelper.TryConvert(_ilg, src, local.Type))
                throw new Exception("Shit's whacked, yo");

            _ilg.MarkLabel(nullOp);

            local.Store(_ilg);

            _stackDepth -= 2;

            return local.Type;
        }

        public override object VisitLiteralExp([NotNull] NyaParser.LiteralExpContext context)
        {
            object o = Visit(context.children[0]);
            return EmitLiteral(o);
        }

        public override object VisitNameAtomExp([NotNull] NyaParser.NameAtomExpContext context)
        {
            string sLocal = context.identifier().GetText();
            Variable v = _scopeManager.FindVariable(sLocal);
            v.Load(_ilg);
            _stackDepth++;
            return v.Type;
        }

        public override object VisitCastExp([NotNull] NyaParser.CastExpContext context)
        {
            Type src = (Type)Visit(context.expression());
            Type dst = ParseType(context.type());

            if (!OpHelper.TryConvert(_ilg, src, dst))
                throw new Exception("Shit's whacked, yo");

            return dst;
        }

        public override object VisitCoalesceExp([NotNull] NyaParser.CoalesceExpContext context)
        {
            Label jmp = _ilg.DefineLabel();

            Type tLeft = (Type)Visit(context.expression(0));

            _ilg.Emit(OpCodes.Dup);
            _ilg.Emit(OpCodes.Brtrue_S, jmp);
            _ilg.Emit(OpCodes.Pop);

            Type tRight = (Type)Visit(context.expression(1));

            if (!OpHelper.TryConvert(_ilg, tRight, tLeft))
                throw new Exception("Shit's whacked, yo");

            _ilg.MarkLabel(jmp);
            _stackDepth--;

            return tLeft ?? tRight;
        }

        public override object VisitMulDivExp([NotNull] NyaParser.MulDivExpContext context)
        {
            Type tLeft = (Type)Visit(context.expression(0));
            Type tRight = (Type)Visit(context.expression(1));

            if (context.OpMuliply() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Mul, "op_Multiply");

            if (context.OpDivision() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Div, "op_Division");

            if (context.OpModulus() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Rem, "op_Modulus");

            _stackDepth--;
            return tLeft;
        }

        public override object VisitAddSubExp([NotNull] NyaParser.AddSubExpContext context)
        {
            Type tLeft = (Type)Visit(context.expression(0));
            Type tRight = (Type)Visit(context.expression(1));


            if (context.OpAddition() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Add, "op_Addition");

            if (context.OpSubtraction() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Sub, "op_Subtraction");

            _stackDepth--;
            return tLeft;
        }

        public override object VisitBitShiftExp([NotNull] NyaParser.BitShiftExpContext context)
        {
            Type tLeft = (Type)Visit(context.expression(0));
            Type tRight = (Type)Visit(context.expression(1));


            if (context.OpLeftShift() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Shl, "op_LeftShift");

            if (context.OpRightShift() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Shr, "op_RightShift");

            _stackDepth--;
            return tLeft;
        }

        public override object VisitBitwiseAndExp([NotNull] NyaParser.BitwiseAndExpContext context)
        {
            Type tLeft = (Type)Visit(context.expression(0));
            Type tRight = (Type)Visit(context.expression(1));

            OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.And, "op_BitwiseAnd");

            _stackDepth--;
            return tLeft;
        }

        public override object VisitBitwiseXorExp([NotNull] NyaParser.BitwiseXorExpContext context)
        {
            Type tLeft = (Type)Visit(context.expression(0));
            Type tRight = (Type)Visit(context.expression(1));

            OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Xor, "op_ExclusiveOr");

            _stackDepth--;
            return tLeft;
        }

        public override object VisitBitwiseOrExp([NotNull] NyaParser.BitwiseOrExpContext context)
        {
            Type tLeft = (Type)Visit(context.expression(0));
            Type tRight = (Type)Visit(context.expression(1));

            OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Or, "op_BitwiseOr");

            _stackDepth--;
            return tLeft;
        }

        public override object VisitAsExp([NotNull] NyaParser.AsExpContext context)
        {
            Type src = (Type)Visit(context.expression());
            Type dst = ParseType(context.type());

            _ilg.Emit(OpCodes.Isinst, dst);

            return dst;
        }

        public override object VisitNewExp([NotNull] NyaParser.NewExpContext context)
        {
            Type[] argTypes;
            Type retType;

            retType = ParseType(context.type());
            argTypes = VisitMethodArgs(context.arguments());

            ConstructorInfo cinfo = retType.GetConstructor(argTypes);
            _ilg.Emit(OpCodes.Newobj, cinfo);
            _stackDepth++;
            return retType;

        }

        public override object VisitFunctionExp([NotNull] NyaParser.FunctionExpContext context)
        {
            // TODO: Account for different parameters

            String name = context.identifier().GetText();
            Type[] argTypes;
            Type retType;

            switch (name)
            {
                case "sqrt":
                    argTypes = new[] { typeof(double) };
                    VisitAndConvertArgs(context, argTypes);
                    return CallMethod(typeof(Math), "Sqrt", argTypes, new Type[] { });
                case "log":
                    argTypes = new[] { typeof(double) };
                    VisitAndConvertArgs(context, argTypes);
                    return CallMethod(typeof(Math), "Log10", argTypes, new Type[] { });
                case "print":
                    argTypes = new[] { typeof(string) };
                    VisitAndConvertArgs(context, argTypes);
                    return CallMethod(typeof(Console), "WriteLine", argTypes, new Type[] { });
            }

            Type callingType;
            bool isThisCall = methodTypeStack.Count == 0;

            if (isThisCall)
            {
                callingType = _currTypeBuilder;
                _ilg.Emit(OpCodes.Ldarg_0);
                _stackDepth++;
            }
            else
            {
                callingType = methodTypeStack.Peek();
            }

            argTypes = VisitMethodArgs(context.arguments());
            bool wasStatic;
            retType = CallMethod(callingType, name, argTypes, new Type[] { }, out wasStatic);

            if(wasStatic && isThisCall)
            {
                // I hope this works
                _ilg.Emit(OpCodes.Pop);
                _stackDepth--;
            }

            return retType;
        }

        Stack<Type> methodTypeStack = new Stack<Type>();

        public override object VisitMemberExp([NotNull] NyaParser.MemberExpContext context)
        {
            var obj = context.expression()[0];
            var member = context.expression()[1];

            object o = Visit(obj);
            Type objType = o as Type;
            if (objType == null)
            {
                objType = EmitLiteral(o);
            }

            methodTypeStack.Push(objType);
            Type retType = (Type)Visit(member);
            methodTypeStack.Pop();
            _stackDepth--;
            return retType;
        }

        private Type CallMethod(Type t, string name, Type[] argTypes, Type[] typeParams)
        {
            bool isStatic;
            return CallMethod(t, name, argTypes, typeParams, out isStatic);
        }

        private MethodInfo FindMethod(Type t, string name, Type[] argTypes)
        {
            TypeBuilder builder = t as TypeBuilder;
            try
            {
                return t.GetMethod(name, argTypes);
            }
            catch (NotSupportedException)
            {
                // TODO: Make this actually work

                // Looks like we're doing this the hard way...
                foreach (var method in builder.DeclaredMethods)
                {
                    if (method.Name == name)
                    {
                        bool isMatch = true;
                        var pinfos = method.GetParameters();
                        if (pinfos.Length == argTypes.Length)
                        {
                            for (int i = 0; i < pinfos.Length; i++)
                            {
                                if (argTypes[i] != pinfos[i].ParameterType)
                                {
                                    isMatch = false;
                                    break;
                                }
                            }
                        }
                        if (isMatch)
                            return method;
                    }
                }
            }
            return null;
        }

        private Type CallMethod(Type t, string name, Type[] argTypes, Type[] typeParams, out bool wasStatic)
        {
            // TODO: Account for virt methods
            var method = FindMethod(t, name, argTypes);
            OpCode op = method.IsStatic ? OpCodes.Call : OpCodes.Callvirt;
            _ilg.EmitCall(op, method, typeParams);
            wasStatic = method.IsStatic;
            _stackDepth -= argTypes.Length;
            if (method.ReturnType != typeof(void))
                _stackDepth++;
            return method.ReturnType;
        }

        private Type[] VisitMethodArgs(NyaParser.ArgumentsContext context)
        {
            if (context == null)
                return new Type[] { };

            var args = context.children.OfType<NyaParser.ArgumentContext>().ToList();
            Type[] argTypes = new Type[args.Count];

            for (int i = 0; i < args.Count; i++)
            {
                argTypes[i] = (Type)Visit(args[i]);
            }
            return argTypes;
        }

        private void VisitAndConvertArgs(NyaParser.FunctionExpContext context, Type[] argTypes)
        {
            if (context.arguments() == null)
                return;

            var args = context.arguments().children.OfType<NyaParser.ArgumentContext>().ToList();
            for (int i = 0; i < args.Count; i++)
            {
                Type t = (Type)Visit(args[i]);
                if (t != argTypes[i])
                    OpHelper.TryConvert(_ilg, t, argTypes[i]);
            }
        }
    }
}
