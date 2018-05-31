using Antlr4.Runtime;
using NyaLang.Antlr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{
    public class MethodDescriptor
    {
        public string Name { get; set; }

        public ParserRuleContext Context { get; set; }

        public MethodBuilder Builder { get; set; }
        public bool IsStatic { get; internal set; }
        public bool IsConstructor { get; internal set; }
        public bool IsFinal { get; internal set; }

        public IList<ParameterBuilder> Parameters { get; internal set; } = new List<ParameterBuilder>();

        public Type[] ParameterTypes { get; internal set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
