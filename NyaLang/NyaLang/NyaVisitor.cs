using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using NyaLang.Antlr;

namespace NyaLang
{
    public class NyaVisitor : NyaBaseVisitor<object>
    {
        TypeBuilder _currTypeBuilder = null;
        MethodBuilder _currMethodBuilder = null;
        ILGenerator _ilg = null;
        Label returnLabel;

        Dictionary<string, LocalBuilder> locals = new Dictionary<string, LocalBuilder>();

        private AppDomain _appDomain;
        public AssemblyBuilder _asmBuilder;
        private ModuleBuilder _moduleBuilder;

        private string _outPath;

        private Dictionary<string, Type> typeAliases = new Dictionary<string, Type>()
        {
            { "string", typeof(String) }, { "bool", typeof(Boolean) }, { "float", typeof(Single) }, { "double", typeof(Double) },
            { "sbyte", typeof(SByte) }, { "short", typeof(Int16) }, { "int", typeof(Int32) }, { "long", typeof(Int64) },
            { "byte", typeof(Byte) }, { "ushort", typeof(UInt16) }, { "uint", typeof(UInt32) }, { "ulong", typeof(UInt64) }
        };

        public NyaVisitor(string assemblyName, string output)
        {
            AssemblyName an = new AssemblyName();
            an.Name = assemblyName;
            _outPath = output;
            _appDomain = AppDomain.CurrentDomain;
            _asmBuilder = _appDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave);
            _moduleBuilder = _asmBuilder.DefineDynamicModule(an.Name, output);
        }

        public TypeBuilder CreateType(string typeName, TypeAttributes attributes)
        {
            return _moduleBuilder.DefineType(typeName, attributes);
        }

        public void SetEntryPoint(MethodBuilder builder, PEFileKinds kind)
        {
            _asmBuilder.SetEntryPoint(builder, kind);
        }

        public void Save()
        {
            _asmBuilder.Save(_outPath);
        }

        public void Visit(IParseTree tree, TypeBuilder builder)
        {
            _currTypeBuilder = builder;
            Visit(tree);
        }

        public override object VisitCompilation_unit([NotNull] NyaParser.Compilation_unitContext context)
        {
            object r = base.VisitCompilation_unit(context);
            _moduleBuilder.CreateGlobalFunctions();
            return r;
        }

        public override object VisitClass_declaration([NotNull] NyaParser.Class_declarationContext context)
        {
            string typeName = context.Identifier().GetText();

            TypeAttributes typeAttr = TypeAttributes.Class;
            List<Type> interfaces = new List<Type>();

            if (context.types() != null)
            {
                foreach (NyaParser.TypeContext con in context.types().children)
                {
                    Type t = _moduleBuilder.GetType(con.GetText());
                    interfaces.Add(t);
                }
            }

            Type baseClass = interfaces.FirstOrDefault();
            if(baseClass != null && !baseClass.IsInterface)
            {
                interfaces.RemoveAt(0);
            }

            _currTypeBuilder = _moduleBuilder.DefineType(typeName, typeAttr, baseClass, interfaces.ToArray());

            VisitChildren(context);

            _currTypeBuilder.CreateType();

            _currTypeBuilder = null;

            return null;
        }

        public override object VisitMethod_declaration([NotNull] NyaParser.Method_declarationContext context)
        {
            string methodName = context.Identifier().GetText();
            bool bIsEntry = false;
            bool isStatic = context.Exclamation() != null;
            bool isConstructor = methodName == "New";

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

            foreach (var attr in context.attributes()?.children?
                .OfType<NyaParser.AttributeContext>() ?? new NyaParser.AttributeContext[] { })
            {
                string attrName = attr.Identifier().GetText();
                switch (attrName)
                {
                    case "entry":
                        bIsEntry = true;
                        break;
                    case "public":
                        if (isConstructor ^ isStatic)
                        {
                            methAttrs ^= MethodAttributes.Private;
                            methAttrs |= MethodAttributes.Public;
                        }
                        break;
                }
            }

            var parameters = context.fixed_parameters()?.children?
                .OfType<NyaParser.Fixed_parameterContext>() ?? new NyaParser.Fixed_parameterContext[] { };

            Type[] paramTypes = parameters.Select(x => ParseTypeDescriptor(x.type_descriptor())).ToArray();
            Type returnType = ParseTypeDescriptor(context.type_descriptor());


            if (_currTypeBuilder != null)
            {
                _currMethodBuilder = _currTypeBuilder.DefineMethod(methodName, methAttrs, returnType, paramTypes);
            }
            else
            {
                _currMethodBuilder = _moduleBuilder.DefineGlobalMethod(methodName, methAttrs, returnType, paramTypes);
            }

            for (int i = 0; i < parameters.Count(); i++)
            {
                var param = parameters.ElementAt(i);
                string paramName = param.Identifier().GetText();

                ParameterAttributes pAttrs = ParameterAttributes.None;

                ParameterBuilder pb = _currMethodBuilder.DefineParameter(i + 1, pAttrs, paramName);

                if (param.Question() != null)
                {
                    pb.SetCustomAttribute(
                        new CustomAttributeBuilder(
                            typeof(OptionalAttribute).GetConstructor(new Type[] { }),
                            new object[] { })
                    );

                    // For default values
                    //pb.SetCustomAttribute(
                    //    new CustomAttributeBuilder(
                    //        typeof(OptionalAttribute).GetConstructor(new Type[] { typeof(Object) }),
                    //        new object[]{ "Bacon" } )
                    //);

                }


            }

            if (bIsEntry)
                SetEntryPoint(_currMethodBuilder, PEFileKinds.ConsoleApplication);

            _ilg = _currMethodBuilder.GetILGenerator();

            returnLabel = _ilg.DefineLabel();

            var block = context.block();

            if (isConstructor && !isStatic)
            {
                _ilg.Emit(OpCodes.Ldarg_0);
                ConstructorInfo ci = typeof(object).GetConstructor(new Type[] { });
                _ilg.Emit(OpCodes.Call, ci);
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

            return _currMethodBuilder;
        }

        private Type ParseType(NyaParser.TypeContext context)
        {
            Type t = typeof(void);
            if (context != null)
            {
                string typeName = context.Identifier().GetText();
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

        public override object VisitBlock([NotNull] NyaParser.BlockContext context)
        {
            VisitChildren(context);
            return null;
        }

        public override object VisitExpression_list([NotNull] NyaParser.Expression_listContext context)
        {
            VisitChildren(context);
            return null;
        }

        public override object VisitParenthesisExp([NotNull] NyaParser.ParenthesisExpContext context)
        {
            // Required to pass type
            return Visit(context.expression());
        }

        public override object VisitStringExp([NotNull] NyaParser.StringExpContext context)
        {
            string rawString = context.GetText();
            rawString = rawString.Substring(1, rawString.Length - 2);
            string unescaped = StringHelper.StringFromCSharpLiteral(rawString);
            _ilg.Emit(OpCodes.Ldstr, unescaped);
            return typeof(string);
        }

        public override object VisitNumericAtomExp([NotNull] NyaParser.NumericAtomExpContext context)
        {
            string numText = context.Number().Symbol.Text;
            if (numText.Contains('.'))
            {
                _ilg.Emit(OpCodes.Ldc_R4, float.Parse(numText));
                return typeof(float);
            }
            else
            {
                _ilg.Emit(OpCodes.Ldc_I4, int.Parse(numText));
                return typeof(int);
            }
        }

        public override object VisitNameAtomExp([NotNull] NyaParser.NameAtomExpContext context)
        {
            string sLocal = context.Identifier().GetText();
            LocalBuilder local = locals[sLocal];
            _ilg.Emit(OpCodes.Ldloc, local);
            return local.LocalType;
        }

        public override object VisitReturnExp([NotNull] NyaParser.ReturnExpContext context)
        {
            Visit(context.expression());
            _ilg.Emit(OpCodes.Br_S, returnLabel);
            return _currMethodBuilder.ReturnType;
        }

        public override object VisitCastExp([NotNull] NyaParser.CastExpContext context)
        {
            Type src = (Type)Visit(context.expression());
            Type dst = ParseType(context.type());

            if (!CastHelper.TryConvert(_ilg, src, dst))
                throw new Exception("Shit failed, yo");

            return dst;
        }

        public override object VisitAssignment([NotNull] NyaParser.AssignmentContext context)
        {
            string sLocal = context.Identifier().GetText();
            string sOperator = context.assignment_operator().GetText();

            LocalBuilder local = null;
            if (locals.ContainsKey(sLocal))
            {
                local = locals[sLocal];
            }
            Label nullOp = _ilg.DefineLabel();

            if (sOperator != "=")
            {
                _ilg.Emit(OpCodes.Ldloc, local);
                if (sOperator == "?=")
                {
                    _ilg.Emit(OpCodes.Dup);
                    _ilg.Emit(OpCodes.Brtrue_S, nullOp);
                    _ilg.Emit(OpCodes.Pop);
                }
            }

            Type src = (Type)Visit(context.expression());

            if (local == null)
            {
                Type dst = null;
                if (context.type_descriptor() != null)
                    dst = ParseTypeDescriptor(context.type_descriptor());
                else
                    dst = src;

                local = _ilg.DeclareLocal(dst);
                locals.Add(sLocal, local);
            }

            if (!CastHelper.TryConvert(_ilg, src, local.LocalType))
                throw new Exception("Shit failed, yo");

            switch (sOperator)
            {
                case "+=": _ilg.Emit(OpCodes.Add); break;
                case "-=": _ilg.Emit(OpCodes.Sub); break;
                case "*=": _ilg.Emit(OpCodes.Mul); break;
                case "/=": _ilg.Emit(OpCodes.Div); break;
                case "%=": _ilg.Emit(OpCodes.Rem); break;
                case "&=": _ilg.Emit(OpCodes.And); break;
                case "|=": _ilg.Emit(OpCodes.Or); break;
                case "^=": _ilg.Emit(OpCodes.Xor); break;
                case "<<=": _ilg.Emit(OpCodes.Shl); break;
                case ">>=": _ilg.Emit(OpCodes.Shr); break;
                case "?=": _ilg.MarkLabel(nullOp); break;
            }

            _ilg.Emit(OpCodes.Stloc, local);

            return local.LocalType;
        }

        public override object VisitCoalesceExp([NotNull] NyaParser.CoalesceExpContext context)
        {
            Label jmp = _ilg.DefineLabel();

            Type tLeft = (Type)Visit(context.expression(0));

            _ilg.Emit(OpCodes.Dup);
            _ilg.Emit(OpCodes.Brtrue_S, jmp);
            _ilg.Emit(OpCodes.Pop);

            Type tRight = (Type)Visit(context.expression(1));

            if (!CastHelper.TryConvert(_ilg, tRight, tLeft))
                throw new Exception("Shit failed, yo");

            _ilg.MarkLabel(jmp);

            return tLeft;
        }

        public override object VisitMulDivExp([NotNull] NyaParser.MulDivExpContext context)
        {
            // TODO: Cast this shit
            Type tLeft = (Type)Visit(context.expression(0));
            Type tRight = (Type)Visit(context.expression(1));

            if (!CastHelper.TryConvert(_ilg, tRight, tLeft))
                throw new Exception("Shit failed, yo");

            if (context.Asterisk() != null)
                _ilg.Emit(OpCodes.Mul);

            if (context.Slash() != null)
                _ilg.Emit(OpCodes.Div);

            if (context.Percent() != null)
                _ilg.Emit(OpCodes.Rem);

            return tLeft;
        }

        public override object VisitAddSubExp([NotNull] NyaParser.AddSubExpContext context)
        {
            // TODO: Cast this shit
            Type tLeft = (Type)Visit(context.expression(0));
            Type tRight = (Type)Visit(context.expression(1));

            if (!CastHelper.TryConvert(_ilg, tRight, tLeft))
                throw new Exception("Shit failed, yo");

            if (context.Plus() != null)
                _ilg.Emit(OpCodes.Add);

            if (context.Minus() != null)
                _ilg.Emit(OpCodes.Sub);

            return tLeft;
        }

        public override object VisitFunctionExp([NotNull] NyaParser.FunctionExpContext context)
        {
            // TODO: Account for parameters

            String name = context.Identifier().GetText();
            Type returnType = null;

            Type mathType = typeof(Math);

            switch (name)
            {
                case "sqrt":
                    Visit(context.arguments());
                    var miSqrt = mathType.GetMethod("Sqrt");
                    returnType = miSqrt.ReturnType;
                    _ilg.Emit(OpCodes.Call, miSqrt);
                    break;

                case "log":
                    Visit(context.arguments());
                    var miLog = mathType.GetMethod("Log10");
                    returnType = miLog.ReturnType;
                    _ilg.Emit(OpCodes.Call, miLog);
                    break;
                case "print":
                    Visit(context.arguments());
                    var miWrite = typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) });
                    returnType = miWrite.ReturnType;
                    _ilg.Emit(OpCodes.Call, miWrite);
                    break;
            }
            return returnType;
        }
    }
}
