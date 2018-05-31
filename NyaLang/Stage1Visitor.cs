using Antlr4.Runtime.Misc;
using NyaLang.Antlr;
using NyaLang.TopoSort;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{

    public class Stage1Visitor : StagedVisitor
    {
        List<ClassDescriptor> classes = new List<ClassDescriptor>();
        private string _currentNamespace = null;
        private TypeBuilder _currTypeBuilder = null;
        private ClassDescriptor _currentDescriptor;

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
                Type = ClassDescriptor.ClassType.Class,
                Methods = new List<MethodDescriptor>()
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
                Type = ClassDescriptor.ClassType.Interface,
                Methods = new List<MethodDescriptor>()
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

            ClassDescriptors = sortedClasses;

            ProcessMethods(sortedClasses, builder);

            _currentDescriptor = null;
            _currTypeBuilder = null;

            foreach (MethodDescriptor meth in GlobalMethods)
            {
                var methContext = meth.Context as NyaParser.Method_declarationContext;
                if (methContext != null)
                {
                    BuildMethod(builder, meth, methContext);
                    continue;
                }
            }

            return sortedClasses;

        }

        public List<ClassDescriptor> SortDescriptors()
        {
            return classes.TopoSort(x => x.Name, x => x.DependentTypeNames).ToList();
        }

        public override object VisitMethod_declaration([NotNull] NyaParser.Method_declarationContext context)
        {
            // TODO: Break out constructor logic to new method

            string methodName = context.identifier().GetText();

            MethodDescriptor descriptor = new MethodDescriptor()
            {
                Name = methodName,
                Context = context
            };

            if (_currentDescriptor != null)
                _currentDescriptor.Methods.Add(descriptor);
            else
                GlobalMethods.Add(descriptor);

            return descriptor;
        }

        public override object VisitInterface_method_declaration([NotNull] NyaParser.Interface_method_declarationContext context)
        {
            // TODO: Break out constructor logic to new method

            string methodName = context.identifier().GetText();

            MethodDescriptor descriptor = new MethodDescriptor()
            {
                Name = methodName,
                Context = context
            };

            _currentDescriptor.Methods.Add(descriptor);

            return descriptor;
        }

        private void ProcessMethods(IList<ClassDescriptor> classes, ModuleBuilder moduleBuilder)
        {
            AssemblyBuilder = moduleBuilder.Assembly as AssemblyBuilder;

            foreach (ClassDescriptor cls in classes)
            {
                _currentDescriptor = cls;
                _currTypeBuilder = cls.Builder;

                VisitChildren(cls.Context);

                foreach (MethodDescriptor meth in cls.Methods)
                {
                    var methContext = meth.Context as NyaParser.Method_declarationContext;
                    if (methContext != null)
                    {
                        BuildMethod(moduleBuilder, meth, methContext);
                        continue;
                    }

                    var intContext = meth.Context as NyaParser.Interface_method_declarationContext;
                    if (intContext != null)
                    {
                        BuildMethod(moduleBuilder, meth, intContext);
                        continue;
                    }
                }
            }
        }

        private void BuildMethod(ModuleBuilder moduleBuilder, MethodDescriptor meth, NyaParser.Method_declarationContext context)
        {
            string methodName = context.identifier().GetText();
            bool bIsEntry = false;
            meth.IsStatic = context.IsStatic != null;
            meth.IsConstructor = methodName == "New";

            MethodAttributes methAttrs = MethodAttributes.Private | MethodAttributes.HideBySig;

            if (meth.IsStatic)
            {
                methAttrs |= MethodAttributes.Static;
            }

            if (meth.IsConstructor)
            {
                methodName = meth.IsStatic ? ".cctor" : ".ctor";
                methAttrs |= MethodAttributes.SpecialName;
            }

            meth.IsFinal = true;

            foreach (var attr in context.attributes()?.children?
                .OfType<NyaParser.AttributeContext>() ?? new NyaParser.AttributeContext[] { })
            {
                string attrName = attr.identifier().GetText();
                switch (attrName)
                {
                    case "entry":
                        bIsEntry = true;
                        break;
                    case "public":
                        methAttrs ^= MethodAttributes.Private;
                        methAttrs |= MethodAttributes.Public;
                        break;
                    case "virtual":
                        methAttrs |= MethodAttributes.Virtual;
                        break;
                    case "abstract":
                        methAttrs |= MethodAttributes.Abstract;
                        break;
                }
            }

            var parameters = context.fixed_parameters()?.children?
                .OfType<NyaParser.Fixed_parameterContext>() ?? new NyaParser.Fixed_parameterContext[] { };

            meth.ParameterTypes = parameters.Select(x => ParseTypeDescriptor(x.type_descriptor())).ToArray();
            Type returnType = ParseTypeDescriptor(context.type_descriptor());

            MethodInfo baseMethod = FindParentDefinition(_currTypeBuilder, methodName, meth.ParameterTypes);

            MethodBuilder methodBuilder;

            if (baseMethod != null)
            {
                if (baseMethod.IsVirtual && !baseMethod.IsFinal)
                {
                    if ((methAttrs & MethodAttributes.Virtual) != MethodAttributes.Virtual)
                        methAttrs |= MethodAttributes.Final;
                    methAttrs |= MethodAttributes.Virtual;
                    if (baseMethod.DeclaringType.IsInterface)
                        methAttrs |= MethodAttributes.NewSlot;

                    if ((methAttrs & MethodAttributes.Public) != MethodAttributes.Public)
                        throw new Exception("Oy! This needs to be public!");
                }
                if (methodName == ".ctor" && !baseMethod.IsPublic)
                {
                    throw new Exception("Oy! The base constructor needs to be public!");
                }
            }

            if (_currTypeBuilder != null)
            {
                methodBuilder = _currTypeBuilder.DefineMethod(methodName, methAttrs, returnType, meth.ParameterTypes);
            }
            else
            {
                methAttrs |= MethodAttributes.Static;
                methodBuilder = moduleBuilder.DefineGlobalMethod(methodName, methAttrs, returnType, meth.ParameterTypes);
            }

            for (int i = 0; i < parameters.Count(); i++)
            {
                var param = parameters.ElementAt(i);
                string paramName = param.identifier().GetText();

                ParameterAttributes pAttrs = ParameterAttributes.None;

                ParameterBuilder pb = methodBuilder.DefineParameter(i + 1, pAttrs, paramName);
                meth.Parameters.Add(pb);

                if (param.Question() != null || param.literal() != null)
                {
                    pb.SetCustomAttribute(
                        new CustomAttributeBuilder(
                            typeof(OptionalAttribute).GetConstructor(new Type[] { }),
                            new object[] { })
                    );

                    if (param.literal() != null)
                    {
                        object o = Visit(param.literal());

                        if (o != null)
                        {
                            pb.SetConstant(o);
                        }
                    }
                }
            }

            if (bIsEntry)
            {
                AssemblyBuilder.SetEntryPoint(methodBuilder, PEFileKinds.ConsoleApplication);
            }

            meth.Builder = methodBuilder;
        }

        private void BuildMethod(ModuleBuilder moduleBuilder, MethodDescriptor meth, NyaParser.Interface_method_declarationContext context)
        {
            string methodName = context.identifier().GetText();

            MethodAttributes methAttrs = MethodAttributes.Public | MethodAttributes.HideBySig |
                MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot;


            foreach (var attr in context.attributes()?.children?
                .OfType<NyaParser.AttributeContext>() ?? new NyaParser.AttributeContext[] { })
            {
                string attrName = attr.identifier().GetText();
                switch (attrName)
                {
                    case "public":
                        break;
                }
            }

            var parameters = context.fixed_parameters()?.children?
                .OfType<NyaParser.Fixed_parameterContext>() ?? new NyaParser.Fixed_parameterContext[] { };

            meth.ParameterTypes = parameters.Select(x => ParseTypeDescriptor(x.type_descriptor())).ToArray();
            Type returnType = ParseTypeDescriptor(context.type_descriptor());

            MethodBuilder methodBuilder;

            methodBuilder = _currTypeBuilder.DefineMethod(methodName, methAttrs, returnType, meth.ParameterTypes);

            for (int i = 0; i < parameters.Count(); i++)
            {
                var param = parameters.ElementAt(i);
                string paramName = param.identifier().GetText();

                ParameterAttributes pAttrs = ParameterAttributes.None;

                ParameterBuilder pb = methodBuilder.DefineParameter(i + 1, pAttrs, paramName);
                meth.Parameters.Add(pb);
                if (param.Question() != null || param.literal() != null)
                {
                    pb.SetCustomAttribute(
                        new CustomAttributeBuilder(
                            typeof(OptionalAttribute).GetConstructor(new Type[] { }),
                            new object[] { })
                    );

                    if (param.literal() != null)
                    {
                        object o = Visit(param.literal());

                        if (o != null)
                        {
                            pb.SetConstant(o);
                        }
                    }
                }
            }

            meth.Builder = methodBuilder;
        }
    }
}
