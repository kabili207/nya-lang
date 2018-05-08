using Antlr4.Runtime.Misc;
using NyaLang.Antlr;
using NyaLang.TopoSort;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{

    public class Stage1Visitor : NyaBaseVisitor<object>
    {
        List<ClassDescriptor> classes = new List<ClassDescriptor>();
        private string _currentNamespace = null;

        public override object VisitCompilation_unit([NotNull] NyaParser.Compilation_unitContext context)
        {
            _currentNamespace = "";
            object r = base.VisitCompilation_unit(context);
            _currentNamespace = null;
            return r;
        }

        public override object VisitType_delcaration([NotNull] NyaParser.Type_delcarationContext context)
        {
            var obj = base.VisitType_delcaration(context) as ClassDescriptor;
            obj.Attributes = context.attributes();
            classes.Add(obj);
            return null;
        }

        public override object VisitClass_declaration([NotNull] NyaParser.Class_declarationContext context)
        {
            string typeName = context.identifier().GetText();
            List<string> dependentTypes = new List<string>();

            if (context.types() != null)
            {
                foreach (NyaParser.TypeContext con in context.types().children.OfType<NyaParser.TypeContext>())
                {
                    dependentTypes.Add(con.GetText());
                }
            }

            return new ClassDescriptor()
            {
                Namespace = _currentNamespace,
                Name = typeName,
                Context = context,
                DependentTypeNames = dependentTypes,
                Type = ClassDescriptor.ClassType.Class
            };
        }

        public override object VisitInterface_declaration([NotNull] NyaParser.Interface_declarationContext context)
        {
            string typeName = context.identifier().GetText();
            List<string> dependentTypes = new List<string>();

            if (context.types() != null)
            {
                foreach (NyaParser.TypeContext con in context.types().children.OfType<NyaParser.TypeContext>())
                {
                    dependentTypes.Add(con.GetText());
                }
            }

            return new ClassDescriptor()
            {
                Namespace = _currentNamespace,
                Name = typeName,
                Context = context,
                DependentTypeNames = dependentTypes,
                Type = ClassDescriptor.ClassType.Class
            };
        }

        private Type ResolveType(string name)
        {
            foreach(var descriptor in classes)
            {
                if (descriptor.FullName == name)
                    return descriptor.Builder;
            }
            return null;
        }

        public IList<ClassDescriptor> ProcessClasses(ModuleBuilder builder)
        {
            var sortedClasses = classes.TopoSort(x => x.Name, x => x.DependentTypeNames).ToList();

            foreach (var descriptor in sortedClasses)
            {
                TypeAttributes typeAttr = TypeAttributes.NotPublic;

                if (descriptor.Context is NyaParser.Class_declarationContext)
                {
                    typeAttr |= TypeAttributes.Class;
                }
                else if (descriptor.Context is NyaParser.Interface_declarationContext)
                {
                    typeAttr |= TypeAttributes.Interface | TypeAttributes.Abstract;
                }

                foreach (var attr in descriptor.Attributes?.children?
                    .OfType<NyaParser.AttributeContext>() ?? new NyaParser.AttributeContext[] { })
                {
                    string attrName = attr.identifier().GetText();
                    switch (attrName)
                    {
                        case "abstract":
                            typeAttr |= TypeAttributes.Abstract;
                            break;
                        case "public":
                            typeAttr ^= TypeAttributes.NotPublic;
                            typeAttr |= TypeAttributes.Public;
                            break;
                    }
                }

                List<Type> interfaces = new List<Type>();

                foreach (var con in descriptor.DependentTypeNames)
                {
                    interfaces.Add(ResolveType(con));
                }

                Type baseClass = interfaces.FirstOrDefault();
                if ((typeAttr & TypeAttributes.Interface) != TypeAttributes.Interface &&
                    baseClass != null && !baseClass.IsInterface)
                {
                    interfaces.RemoveAt(0);
                }

                descriptor.Builder = builder.DefineType(descriptor.FullName, typeAttr, baseClass, interfaces.ToArray());
            }

            return sortedClasses;

        }

        public List<ClassDescriptor> SortDescriptors()
        {
            return classes.TopoSort(x => x.Name, x => x.DependentTypeNames).ToList();
        }
    }
}
