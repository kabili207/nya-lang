using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NyaLang.Antlr;
using Antlr4.Runtime;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

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

	                @override
	                Foo(string message, string s1?, string s2?) {

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
            visitor.Visit(context);
            visitor.Save();

            //Console.WriteLine(visitor.Visit(expressionContext));
        }

        public static void VoidReturn()
        {
            return;
        }

        private void Foo(string message, [Optional] string s1, string s2 = "Bacon")
        {

        }

        public static double EmitTest()
        {
            return Math.Sqrt(10 + 1 * 35 + (5.4 - 7.4));
        }
    }
}
