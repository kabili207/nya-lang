using Antlr4.Runtime.Misc;
using NyaLang.Antlr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{
    public class NyaCompiler
    {
        private AppDomain _appDomain;
        private AssemblyBuilder _asmBuilder;
        private ModuleBuilder _moduleBuilder;

        private string _outPath;

        public NyaCompiler(string assemblyName, string output)
        {
            AssemblyName an = new AssemblyName();
            an.Name = assemblyName;
            _outPath = output;
            _appDomain = AppDomain.CurrentDomain;
            _asmBuilder = _appDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Save);
            _moduleBuilder = _asmBuilder.DefineDynamicModule(an.Name, output);
        }

        public TypeBuilder CreateType(string typeName, TypeAttributes attributes)
        {
            return _moduleBuilder.DefineType(typeName, attributes);
        }

        public MethodBuilder CreateMain(TypeBuilder typeBuilder)
        {
            return typeBuilder.DefineMethod("Main",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(int), new Type[] { typeof(string[]) });
        }

        public void SetEntryPoint(MethodBuilder builder, PEFileKinds kind)
        {
            _asmBuilder.SetEntryPoint(builder, kind);
        }

        public void Save()
        {
            _asmBuilder.Save(_outPath);
        }
    }
}
