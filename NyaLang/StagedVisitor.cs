using NyaLang.Antlr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{
    public abstract class StagedVisitor : NyaBaseVisitor<object>
    {
        public AssemblyBuilder AssemblyBuilder { get; protected set; }

        public ModuleBuilder ModuleBuilder { get; protected set; }

        public IEnumerable<ClassDescriptor> ClassDescriptors { get; protected set; }

        public IList<MethodDescriptor> GlobalMethods { get; protected set; } = new List<MethodDescriptor>();

        public String[] CurrentUsings { get; protected set; } = new[]
        {
            "System",
            "System.Collections.Generic",
            "System.Linq"
        };

        protected Type FindType(string name, string[] namespaces = null)
        {
            if (namespaces == null)
                namespaces = CurrentUsings ?? new string[] { };
            Type t = AssemblyBuilder.GetType(name) ?? Type.GetType(name);

            if (t == null)
            {
                foreach (string ns in namespaces)
                {
                    t = AssemblyBuilder.GetType(ns + '.' + name) ?? Type.GetType(ns + '.' + name);
                    if (t != null)
                        break;
                }
            }

            return t;
        }

        protected Type ParseType(NyaParser.TypeContext context)
        {
            Type t = typeof(void);
            if (context != null)
            {
                string typeName = context.identifier().GetText();
                var typeArgs = new Type[] { };

                if (context.type_argument_list() != null)
                {
                    typeArgs = context.type_argument_list().children.OfType<NyaParser.TypeContext>().Select(x => ParseType(x)).ToArray();
                }

                if (TypeHelper.TypeAliases.ContainsKey(typeName))
                {
                    t = TypeHelper.TypeAliases[typeName];
                }
                else
                {
                    if (typeArgs.Length > 0)
                        typeName += "`" + typeArgs.Length;
                    t = FindType(typeName, CurrentUsings);
                }

                if (context.type_argument_list() != null)
                {
                    t = t.MakeGenericType(typeArgs);
                }

                if (context.array_type() != null)
                {
                    t = t.MakeArrayType();
                }
            }
            return t;
        }

        protected Type ParseTypeDescriptor(NyaParser.Type_descriptorContext context)
        {
            if (context != null)
            {
                return ParseType(context.type());
            }
            return typeof(void);
        }

    }
}
