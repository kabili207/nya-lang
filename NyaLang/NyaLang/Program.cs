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
                class Cat {
                    @public @entry
	                int SetCall!(string[] args) {
                        a = 12;
                        b = log(10 + a * 35 + (5.4 - 7.4));
                        c = b;
                        return a + c;
                    }
                }
";

            //input = "log(10 + 1 * 35 + (5.4 - 7.4));";


            AntlrInputStream inputStream = new AntlrInputStream(input);
            NyaLexer nyaLexer = new NyaLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(nyaLexer);
            NyaParser nyaParser = new NyaParser(commonTokenStream);

            var context = nyaParser.@class();

            NyaVisitor visitor = new NyaVisitor("NyaTest", "NyaTest.exe");
            TypeBuilder entry = visitor.CreateType("EntryPoint", TypeAttributes.Public | TypeAttributes.Class);

            //MethodBuilder main = compiler.CreateMain(entry);

            visitor.Visit(context, entry);

            entry.CreateType();
            visitor.Save();

            //Console.WriteLine(visitor.Visit(expressionContext));
        }

        public static void VoidReturn()
        {
            return;
        }

        public static double EmitTest()
        {
            return Math.Sqrt(10 + 1 * 35 + (5.4 - 7.4));
        }
    }
}
