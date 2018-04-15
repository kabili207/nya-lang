using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{
    class Variable
    {
        object value;
        Type type;

        Variable(object value)
        {
            this.value = value;
        }

        public Type Type
        {
            get
            {
                if (value is LocalBuilder)
                    return ((LocalBuilder)value).LocalType;
                if (value is FieldBuilder)
                   return ((FieldBuilder)value).FieldType;
                 return type;
            }
            set
            {
                type = value;
            }
        }

        public void Load(ILGenerator ilg)
        {
            if (value is LocalBuilder)
                ilg.Emit(OpCodes.Ldloc, (LocalBuilder)value);
            if (value is FieldBuilder)
                ilg.Emit(OpCodes.Ldfld, (FieldBuilder)value);
            if (value is ParameterBuilder)
                ilg.Emit(OpCodes.Ldarg, ((ParameterBuilder)value).Position);
        }

        public void Store(ILGenerator ilg)
        {
            if (value is LocalBuilder)
                ilg.Emit(OpCodes.Stloc, (LocalBuilder)value);
            if (value is FieldBuilder)
                ilg.Emit(OpCodes.Stfld, (FieldBuilder)value);
            if (value is ParameterBuilder)
                ilg.Emit(OpCodes.Starg, ((ParameterBuilder)value).Position);
        }

        public static implicit operator LocalBuilder(Variable d)
        {
            return (LocalBuilder)d.value;
        }

        public static implicit operator Variable(LocalBuilder d)
        {
            return new Variable(d);
        }

        public static implicit operator FieldBuilder(Variable d)
        {
            return (FieldBuilder)d.value;
        }

        public static implicit operator Variable(FieldBuilder d)
        {
            return new Variable(d);
        }

        public static implicit operator ParameterBuilder(Variable d)
        {
            return (ParameterBuilder)d.value;
        }

        public static implicit operator Variable(ParameterBuilder d)
        {
            return new Variable(d);
        }
    }

    enum ScopeLevel
    {
        Generic,
        Method,
        Class,
        Global
    }

    class Scope
    {
        Dictionary<string, Variable> accessors = new Dictionary<string, Variable>();
        public ScopeLevel Level { get; private set; }

        public Scope(ScopeLevel level)
        {
            Level = level;
        }

        public void AddVariable(string name, Variable v)
        {
            accessors.Add(name, v);
        }

        public bool Contains(string name)
        {
            return accessors.ContainsKey(name);
        }

        public Variable GetVariable(string name)
        {
            Variable var = null;
            accessors.TryGetValue(name, out var);
            return var;
        }
    }
}
