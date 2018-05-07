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
                namespaces = new string[] { };
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
            bool isStatic = context.Exclamation() != null;
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
                    if (!baseMethod.DeclaringType.IsInterface)
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
                            pb.SetCustomAttribute(
                                new CustomAttributeBuilder(
                                    typeof(DefaultParameterValueAttribute).GetConstructor(new Type[] { typeof(Object) }),
                                    new object[] { o })
                            );
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
                            pb.SetCustomAttribute(
                                new CustomAttributeBuilder(
                                    typeof(DefaultParameterValueAttribute).GetConstructor(new Type[] { typeof(Object) }),
                                    new object[] { o })
                            );
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
                if (typeAliases.ContainsKey(typeName))
                {
                    t = typeAliases[typeName];
                    if (context.array_type() != null)
                    {
                        t = t.MakeArrayType();
                    }
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
            if(o == null)
            {
                _ilg.Emit(OpCodes.Ldnull);
                return null;
            }

            if (o is bool)
                EmitBool((bool)o);
            else if (o is string)
                EmitString((string)o);
            else if (o is float)
                EmitFloat((float)o);
            else if (o is double)
                EmitDouble((double)o);
            else if (o is decimal)
                EmitDecimal((decimal)o);
            else if (o is int)
                EmitInt((int)o);
            else if (o is short)
                EmitInt((short)o);
            else if (o is long)
                EmitInt((long)o);
            else if (o is byte)
                EmitInt((byte)o);
            else if (o is sbyte)
                EmitInt((sbyte)o);
            else if (o is ushort)
                EmitInt((ushort)o);
            else if (o is uint)
                EmitInt((uint)o);
            else if (o is ulong)
                EmitInt((ulong)o);
            else if (o is Regex)
                EmitRegex((Regex)o);

            return o.GetType();
        }

        public override object VisitNullLiteral([NotNull] NyaParser.NullLiteralContext context)
        {
            return null;
        }

        private void EmitBool(bool b)
        {
            if (b)
            {
                _ilg.Emit(OpCodes.Ldc_I4_1);
            }
            else
            {
                _ilg.Emit(OpCodes.Ldc_I4_0);
            }
        }

        public override object VisitBoolLiteral([NotNull] NyaParser.BoolLiteralContext context)
        {
            string boolText = context.GetText();

            return boolText == "true";
        }

        private void EmitString(string s)
        {
            _ilg.Emit(OpCodes.Ldstr, s);
        }

        public override object VisitStringLiteral([NotNull] NyaParser.StringLiteralContext context)
        {
            string rawString = context.GetText();
            rawString = rawString.Substring(1, rawString.Length - 2);
            string unescaped = StringHelper.StringFromCSharpLiteral(rawString);
            return unescaped;
        }

        private void EmitFloat(float f)
        {
            _ilg.Emit(OpCodes.Ldc_R4, f);
        }

        private void EmitDouble(double d)
        {
            _ilg.Emit(OpCodes.Ldc_R8, d);
        }

        private void EmitDecimal(decimal d)
        {
            ConstructorInfo ctor1 = typeof(Decimal).GetConstructor(
                new Type[] {
                            typeof(Int32),
                            typeof(Int32),
                            typeof(Int32),
                            typeof(Boolean),
                            typeof(Byte)
                }
            );

            int[] parts = Decimal.GetBits(d);
            bool sign = (parts[3] & 0x80000000) != 0;

            byte scale = (byte)((parts[3] >> 16) & 0x7F);

            _ilg.Emit(OpCodes.Nop);
            _ilg.Emit(OpCodes.Ldc_I4, parts[0]);
            _ilg.Emit(OpCodes.Ldc_I4, parts[1]);
            _ilg.Emit(OpCodes.Ldc_I4, parts[2]);
            _ilg.Emit(OpCodes.Ldc_I4, sign ? 1 : 0);
            _ilg.Emit(OpCodes.Ldc_I4, (int)scale);
            _ilg.Emit(OpCodes.Newobj, ctor1);
        }

        public override object VisitRealLiteral([NotNull] NyaParser.RealLiteralContext context)
        {
            string value = context.GetText().ToLower();
            string suffix = "";
            if (new[] { 'f', 'd', 'm' }.Contains(value[value.Length - 1]))
            {
                suffix = value[value.Length - 1].ToString();
                value = value.Substring(0, value.Length - 1);
            }

            switch (suffix)
            {
                case "m":
                    return decimal.Parse(value, value.Contains("e") ?
                        System.Globalization.NumberStyles.Float : System.Globalization.NumberStyles.Number);
                case "d":
                    return double.Parse(value);
                case "f":
                default:
                    return float.Parse(value);
            }
        }

        private void EmitInt(byte b)
        {
            _ilg.Emit(OpCodes.Ldc_I4, b);
        }

        private void EmitInt(short s)
        {
            _ilg.Emit(OpCodes.Ldc_I4, s);
        }

        private void EmitInt(int i)
        {
            _ilg.Emit(OpCodes.Ldc_I4, i);
        }

        private void EmitInt(long l)
        {
            _ilg.Emit(OpCodes.Ldc_I8, l);
        }

        private void EmitInt(sbyte b)
        {
            _ilg.Emit(OpCodes.Ldc_I4, b);
        }

        private void EmitInt(ushort s)
        {
            _ilg.Emit(OpCodes.Ldc_I4, s);
        }

        private void EmitInt(uint s)
        {
            _ilg.Emit(OpCodes.Ldc_I4, s);
        }

        private void EmitInt(ulong s)
        {
            _ilg.Emit(OpCodes.Ldc_I8, (long)s);
        }

        public override object VisitIntegerLiteral([NotNull] NyaParser.IntegerLiteralContext context)
        {
            string text = context.GetText();
            var regex = Regex.Match(text, @"(\d+)(\w+)?");
            string value = regex.Groups[1].Value;
            string suffix = regex.Groups[2].Value.ToLower();

            switch (suffix)
            {
                case "b":
                    return byte.Parse(value);
                case "s":
                    return short.Parse(value);
                case "l":
                    return long.Parse(value);
                case "u":
                    return uint.Parse(value);
                case "lu":
                case "ul":
                    return ulong.Parse(value);
                case "su":
                case "us":
                    return ushort.Parse(value);
                case "sb":
                case "bs":
                    return sbyte.Parse(value);
                default:
                    return int.Parse(value);
            }
        }

        private void EmitRegex(Regex r)
        {
            ConstructorInfo ctor1 = typeof(Regex).GetConstructor(
                new Type[] {
                    typeof(String),
                    typeof(RegexOptions)
                }
            );

            _ilg.Emit(OpCodes.Nop);
            _ilg.Emit(OpCodes.Ldstr, r.ToString());
            _ilg.Emit(OpCodes.Ldc_I4, (int)r.Options);
            _ilg.Emit(OpCodes.Newobj, ctor1);
        }

        public override object VisitRegexLiteral([NotNull] NyaParser.RegexLiteralContext context)
        {
            string raw = context.GetText();
            int start = raw.IndexOf('/');
            int end = raw.LastIndexOf('/');
            string regex = raw.Substring(start+1, end - 1);
            string flags = raw.Substring(end + 1);

            RegexOptions options = RegexOptions.None;
            foreach(var flag in flags)
            {
                switch (flag)
                {
                    case 'm':
                        options |= RegexOptions.Multiline;
                        break;
                    case 'i':
                        options |= RegexOptions.IgnoreCase;
                        break;
                    case 's':
                        options |= RegexOptions.Singleline;
                        break;
                    case 'x':
                        options |= RegexOptions.IgnorePatternWhitespace;
                        break;
                    case 'n':
                        options |= RegexOptions.ExplicitCapture;
                        break;
                }
            }

            return new Regex(regex, options);
        }

        public override object VisitReturnStatement([NotNull] NyaParser.ReturnStatementContext context)
        {
            Visit(context.expression());
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

            return local.Type;
        }

        private Type DoArithmenticAssignment(NyaParser.AssignmentContext context)
        {
            string sOperator = context.assignment_operator().GetText();
            string sLocal = context.identifier().GetText();
            Variable local = _scopeManager.FindVariable(sLocal);

            local.Load(_ilg);

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

            return local.Type;
        }

        private Type DoCoalesceAssignment(NyaParser.AssignmentContext context)
        {
            string sLocal = context.identifier().GetText();
            Variable local = _scopeManager.FindVariable(sLocal);

            Label nullOp = _ilg.DefineLabel();

            local.Load(_ilg);
                    _ilg.Emit(OpCodes.Dup);
                    _ilg.Emit(OpCodes.Brtrue_S, nullOp);
                    _ilg.Emit(OpCodes.Pop);

            Type src = (Type)Visit(context.expression());
            Type dst = local.Type;

            if (!OpHelper.TryConvert(_ilg, src, local.Type))
                throw new Exception("Shit's whacked, yo");

            _ilg.MarkLabel(nullOp);

            local.Store(_ilg);

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

            return tLeft ?? tRight;
        }

        public override object VisitMulDivExp([NotNull] NyaParser.MulDivExpContext context)
        {
            Type tLeft = (Type)Visit(context.expression(0));
            Type tRight = (Type)Visit(context.expression(1));

            if (context.Asterisk() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Mul, "op_Multiply");

            if (context.Slash() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Div, "op_Division");

            if (context.Percent() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Rem, "op_Modulus");

            return tLeft;
        }

        public override object VisitAddSubExp([NotNull] NyaParser.AddSubExpContext context)
        {
            Type tLeft = (Type)Visit(context.expression(0));
            Type tRight = (Type)Visit(context.expression(1));


            if (context.Plus() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Add, "op_Addition");

            if (context.Minus() != null)
                OpHelper.DoMath(_ilg, tLeft, tRight, OpCodes.Sub, "op_Subtraction");

            return tLeft;
        }

        public override object VisitFunctionExp([NotNull] NyaParser.FunctionExpContext context)
        {
            // TODO: Account for different parameters

            String name = context.identifier().GetText();
            Type returnType = null;
            Type[] argTypes;
            Type mathType = typeof(Math);

            switch (name)
            {
                case "sqrt":
                    argTypes = new[] { typeof(double) };
                    VisitAndConvertArgs(context, argTypes);
                    var miSqrt = mathType.GetMethod("Sqrt", argTypes);
                    returnType = miSqrt.ReturnType;
                    _ilg.EmitCall(OpCodes.Call, miSqrt, new Type[] { });
                    break;

                case "log":
                    argTypes = new[] { typeof(double) };
                    VisitAndConvertArgs(context, argTypes);
                    var miLog = mathType.GetMethod("Log10", argTypes);
                    returnType = miLog.ReturnType;
                    _ilg.EmitCall(OpCodes.Call, miLog, new Type[] { });
                    break;
                case "print":
                    argTypes = new[] { typeof(string) };
                    VisitAndConvertArgs(context, argTypes);
                    var miWrite = typeof(Console).GetMethod("WriteLine", argTypes);
                    returnType = miWrite.ReturnType;
                    _ilg.EmitCall(OpCodes.Call, miWrite, new Type[] { });
                    break;
            }
            return returnType;
        }

        private void VisitAndConvertArgs(NyaParser.FunctionExpContext context, Type[] argTypes)
        {
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
