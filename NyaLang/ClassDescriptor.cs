using Antlr4.Runtime;
using NyaLang.Antlr;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace NyaLang
{
    public class ClassDescriptor
    {
        public enum ClassType
        {
            Class,
            Interface,
            Enum
        }

        public string Namespace { get; set; }
        public string Name { get; set; }
        public ParserRuleContext Context { get; set; }
        public List<string> DependentTypeNames { get; set; }
        public TypeBuilder Builder { get; set; }
        public NyaParser.AttributesContext Attributes { get; set; }
        public ClassType Type { get; set; }
        public List<MethodDescriptor> Methods { get; set; }


        public string FullName
        {
            get
            {
                return (string.IsNullOrWhiteSpace(Namespace) ? "" : ".") + Name;
            }
        }

        public override bool Equals(object obj)
        {
            ClassDescriptor c = obj as ClassDescriptor;
            if (c == null)
                return false;
            return Name == c.Name && Namespace == c.Namespace;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Namespace.GetHashCode();
        }

        public override string ToString()
        {
            return FullName;
        }
    }
}
