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
using System.IO;

namespace NyaLang
{
    class Program
    {
        static Program() { }

        public Program() {
            Console.WriteLine("Bacon");
        }

        static void Main(string[] args)
        {

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "NyaLang.test.nya";

            string input = "";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                input = reader.ReadToEnd();
            }

            AntlrInputStream inputStream = new AntlrInputStream(input);
            NyaLexer nyaLexer = new NyaLexer(inputStream);
            CommonTokenStream commonTokenStream = new CommonTokenStream(nyaLexer);
            NyaParser nyaParser = new NyaParser(commonTokenStream);

            var context = nyaParser.compilation_unit();

            NyaVisitor visitor = new NyaVisitor("NyaTest", "NyaTest.exe");
            visitor.Visit(context);
            visitor.Save();

            //Console.WriteLine(visitor.Visit(expressionContext));
        }

        public static void VoidReturn()
        {
            return;
        }

        public static void MultiReturn(int i)
        {
            if(i % 12 == 0)
            {
                return;
            }
            Console.WriteLine("Vacon");
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
