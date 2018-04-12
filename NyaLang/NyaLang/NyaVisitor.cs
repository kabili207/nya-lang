using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using NyaLang.Antlr;

namespace NyaLang
{
    public class NyaVisitor : NyaBaseVisitor<object>
    {
        MethodBuilder _currMethodBuilder = null;
        ILGenerator _ilg = null;

        Dictionary<string, LocalBuilder> locals = new Dictionary<string, LocalBuilder>();

        public void Visit(IParseTree tree, MethodBuilder builder)
        {
            _currMethodBuilder = builder;
            _ilg = builder.GetILGenerator();
            Visit(tree);
            _ilg.Emit(OpCodes.Ret);
        }

        public override object VisitBlock_list([NotNull] NyaParser.Block_listContext context)
        {
            VisitChildren(context);
            return null;
        }

        public override object VisitBlock([NotNull] NyaParser.BlockContext context)
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
            string sLocal = context.Name().GetText();
            LocalBuilder local = locals[sLocal];
            _ilg.Emit(OpCodes.Ldloc, local);
            return null;
        }

        public override object VisitAssignment([NotNull] NyaParser.AssignmentContext context)
        {
            // TODO: Types?
            string sLocal = context.Name().GetText();
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
            String name = context.Name().GetText();
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
