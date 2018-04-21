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
using static NyaLang.TestClass;

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

            AssemblyName an = new AssemblyName();
            an.Name = "NyaTest";
            var appDomain = AppDomain.CurrentDomain;
            var asmBuilder = appDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = asmBuilder.DefineDynamicModule(an.Name, "NyaTest.exe");

            Stage1Visitor visitor1 = new Stage1Visitor();
            visitor1.Visit(context);
            var descriptors = visitor1.ProcessClasses(moduleBuilder);

            Stage2Visitor visitor2 = new Stage2Visitor(asmBuilder, moduleBuilder, descriptors);
            visitor2.Visit(context);

            asmBuilder.Save("NyaTest.exe");

            //Console.WriteLine(visitor.Visit(expressionContext));
        }
    }
}
