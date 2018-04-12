using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NyaLang.Antlr;
using Antlr4.Runtime;
using System.Reflection;
using System.Reflection.Emit;

namespace NyaLang
{
    class Program
    {
        static void Main(string[] args)
        {
            string input = @"
                a = 12;
                b = log(10 + a * 35 + (5.4 - 7.4));
                c = b;
                a + c;
";

            //input = "log(10 + 1 * 35 + (5.4 - 7.4));";


            AntlrInputStream inputStream = new AntlrInputStream(input);
            NyaLexer nyaLexer = new NyaLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(nyaLexer);
            NyaParser nyaParser = new NyaParser(commonTokenStream);

            NyaParser.Block_listContext blockContext = nyaParser.block_list();

            NyaCompiler compiler = new NyaCompiler("NyaTest", "NyaTest.exe");
            TypeBuilder entry = compiler.CreateType("EntryPoint", TypeAttributes.Public | TypeAttributes.Class);
            MethodBuilder main = compiler.CreateMain(entry);

            NyaVisitor visitor = new NyaVisitor();
            visitor.Visit(blockContext, main);
            entry.CreateType();

            compiler.SetEntryPoint(main, PEFileKinds.ConsoleApplication);
            compiler.Save();

            //Console.WriteLine(visitor.Visit(expressionContext));
        }

        public static double EmitTest()
        {
            return Math.Sqrt(10 + 1 * 35 + (5.4 - 7.4));
        }
    }
}
