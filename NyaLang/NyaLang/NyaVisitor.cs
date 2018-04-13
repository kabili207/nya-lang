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

        Dictionary<string, LocalBuilder> locals = new Dictionary<string, LocalBuilder>();

        private AppDomain _appDomain;
        public AssemblyBuilder _asmBuilder;
        private ModuleBuilder _moduleBuilder;

        private string _outPath;

        private Dictionary<string, Type> typeAliases = new Dictionary<string, Type>()
        {
            { "string", typeof(String) }, { "int", typeof(Int32) }, { "byte", typeof(Byte) },
            { "bool", typeof(Boolean) }, { "float", typeof(Single) }
        };

        public NyaVisitor(string assemblyName, string output)
        {
            AssemblyName an = new AssemblyName();
            an.Name = assemblyName;
            _outPath = output;
            _appDomain = AppDomain.CurrentDomain;
            _asmBuilder = _appDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Save);
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

        public override object VisitClass([NotNull] NyaParser.ClassContext context)
        {
            string typeName = context.Identifier().GetText();

            TypeAttributes typeAttr = TypeAttributes.Class;

            _currTypeBuilder = _moduleBuilder.DefineType(typeName, typeAttr);

            VisitChildren(context);

            _currTypeBuilder.CreateType();

            return null;
        }

        public override object VisitMethod([NotNull] NyaParser.MethodContext context)
        {
            string methodName = context.Identifier().GetText();
            bool bIsEntry = false;

            MethodAttributes methAttrs = MethodAttributes.Private;

            if (context.Exclamation() != null)
                methAttrs |= MethodAttributes.Static;

            foreach (var attr in context.attributes()?.children.OfType<NyaParser.AttributeContext>())
            {
                string attrName = attr.Identifier().GetText();
                switch (attrName)
                {
                    case "entry":
                        bIsEntry = true;
                        break;
                    case "public":
                        methAttrs ^= MethodAttributes.Private;
                        methAttrs |= MethodAttributes.Public;
                        break;
                }
            }

            var parameters = context.fixed_parameters()?.children.OfType<NyaParser.Fixed_parameterContext>();

            _currMethodBuilder = _currTypeBuilder.DefineMethod(methodName, methAttrs,
                ParseTypeDescriptor(context.type_descriptor()),
                parameters.Select(x => ParseTypeDescriptor(x.type_descriptor())).ToArray());

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

            var block = context.block();
            if (block.ChildCount > 0)
            {
                Visit(block);
            }
            else
            {
                // We need to do _something_
                _ilg.Emit(OpCodes.Nop);
                _ilg.Emit(OpCodes.Ret);
            }

            _ilg = null;

            return _currMethodBuilder;
        }

        private Type ParseTypeDescriptor(NyaParser.Type_descriptorContext context)
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

        public override object VisitNumericAtomExp([NotNull] NyaParser.NumericAtomExpContext context)
        {
            string numText = context.Number().Symbol.Text;
            if (numText.Contains('.'))
                _ilg.Emit(OpCodes.Ldc_R4, float.Parse(numText));
            else
                _ilg.Emit(OpCodes.Ldc_I4, int.Parse(numText));

            return null;
        }

        public override object VisitNameAtomExp([NotNull] NyaParser.NameAtomExpContext context)
        {
            string sLocal = context.Identifier().GetText();
            LocalBuilder local = locals[sLocal];
            _ilg.Emit(OpCodes.Ldloc, local);
            return null;
        }

        public override object VisitReturnExp([NotNull] NyaParser.ReturnExpContext context)
        {
            Visit(context.expression());
            _ilg.Emit(OpCodes.Ret);
            return null;
        }

        public override object VisitAssignment([NotNull] NyaParser.AssignmentContext context)
        {
            // TODO: Types?
            string sLocal = context.Identifier().GetText();
            LocalBuilder local;

            Visit(context.expression());

            if (locals.ContainsKey(sLocal))
            {
                local = locals[sLocal];
            }
            else
            {
                local = _ilg.DeclareLocal(typeof(int));
                locals.Add(sLocal, local);
            }

            _ilg.Emit(OpCodes.Stloc, local);


            return null;
        }

        public override object VisitMulDivExp([NotNull] NyaParser.MulDivExpContext context)
        {
            Visit(context.expression(0));
            Visit(context.expression(1));

            if (context.Asterisk() != null)
                _ilg.Emit(OpCodes.Mul);

            if (context.Slash() != null)
                _ilg.Emit(OpCodes.Div);

            return null;
        }

        public override object VisitAddSubExp([NotNull] NyaParser.AddSubExpContext context)
        {
            Visit(context.expression(0));
            Visit(context.expression(1));

            if (context.Plus() != null)
                _ilg.Emit(OpCodes.Add);

            if (context.Minus() != null)
                _ilg.Emit(OpCodes.Sub);

            return null;
        }

        public override object VisitFunctionExp([NotNull] NyaParser.FunctionExpContext context)
        {
            String name = context.Identifier().GetText();
            int result = 0;

            Type mathType = typeof(Math);

            switch (name)
            {
                case "sqrt":
                    Visit(context.expression());
                    var miSqrt = mathType.GetMethod("Sqrt");
                    _ilg.Emit(OpCodes.Call, miSqrt);
                    break;

                case "log":
                    Visit(context.expression());
                    var miLog = mathType.GetMethod("Log10");
                    _ilg.Emit(OpCodes.Call, miLog);
                    break;
            }
            return result;
        }
    }
}
