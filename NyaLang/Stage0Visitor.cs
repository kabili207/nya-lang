using Antlr4.Runtime.Misc;
using NyaLang.Antlr;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{
    class Stage0Visitor : NyaBaseVisitor<object>
    {

        private Dictionary<string, object> _buildInfo = new Dictionary<string, object>();

        private Dictionary<string, Type> _globalAttrMap = new Dictionary<string, Type>()
        {
            { "Title", typeof(AssemblyTitleAttribute) },
            { "Version", typeof(AssemblyVersionAttribute) },
            { "FileVersion", typeof(AssemblyFileVersionAttribute) },
            { "InformationalVersion", typeof(AssemblyInformationalVersionAttribute) },
            { "Company", typeof(AssemblyCompanyAttribute) },
            { "Copyright", typeof(AssemblyCopyrightAttribute) },
            { "Trademark", typeof(AssemblyTrademarkAttribute) },
            { "Configuration", typeof(AssemblyConfigurationAttribute) },
            { "Culture", typeof(AssemblyCultureAttribute) },
            { "Description", typeof(AssemblyDescriptionAttribute) },
            { "Product", typeof(AssemblyProductAttribute) },
            { "Guid", typeof(GuidAttribute) },
            { "ComVisible", typeof(ComVisibleAttribute) },
        };

        public AssemblyBuilder AssemblyBuilder { get; private set; }
        public ModuleBuilder ModuleBuilder { get; private set; }

        public override object VisitCompilation_unit([NotNull] NyaParser.Compilation_unitContext context)
        {
            VisitChildren(context);

            return null;
        }

        public override object VisitBuild_info_declaration([NotNull] NyaParser.Build_info_declarationContext context)
        {
            string name = context.identifier().GetText();
            object value = Visit(context.literal());

            _buildInfo[name] = value;
            return null;
        }

        public void CreateAssembly(string output)
        {
            string baseName = Path.GetFileNameWithoutExtension(output);

            AssemblyName an = new AssemblyName();
            an.Name = _buildInfo.ContainsKey("Title") ? (string)_buildInfo["Title"] : baseName;
            an.Version = new Version( _buildInfo.ContainsKey("Version") ? (string)_buildInfo["Version"] : "0.0.0.0");

            var appDomain = AppDomain.CurrentDomain;
            AssemblyBuilder = appDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave);
            ModuleBuilder = AssemblyBuilder.DefineDynamicModule(an.Name, output);

            AddGlobalAttributes();
        }

        private void AddGlobalAttributes()
        {
            foreach (var kvp in _buildInfo)
            {
                if (_globalAttrMap.ContainsKey(kvp.Key))
                {
                    ConstructorInfo cInfo = _globalAttrMap[kvp.Key].GetConstructor(new Type[] { kvp.Value.GetType() });

                    if (cInfo != null)
                    {
                        AssemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(cInfo, new[] { kvp.Value }));
                    }
                }
            }
        }

        public override object VisitNullLiteral([NotNull] NyaParser.NullLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitBoolLiteral([NotNull] NyaParser.BoolLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitStringLiteral([NotNull] NyaParser.StringLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitRealLiteral([NotNull] NyaParser.RealLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitIntegerLiteral([NotNull] NyaParser.IntegerLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }

        public override object VisitRegexLiteral([NotNull] NyaParser.RegexLiteralContext context)
        {
            return LiteralHelper.VisitLiteral(context);
        }
    }
}
