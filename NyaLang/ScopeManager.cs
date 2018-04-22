using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{
    class ScopeManager
    {
        Stack<Scope> scopes = new Stack<Scope>();

        public Scope Push(ScopeLevel level)
        {
            Scope s = new Scope(level);
            scopes.Push(s);
            return s;
        }

        public void Push(Scope scope)
        {
            scopes.Push(scope);
        }

        public Scope Pop()
        {
            return scopes.Pop();
        }

        public Scope Peek()
        {
            return scopes.Peek();
        }

        public void AddVariable(string name, Variable v, Type type = null)
        {
            if (v.Type == null)
                v.Type = type;
            scopes.Peek().AddVariable(name, v);
        }

        public Variable FindVariable(string name)
        {
            Variable ret = null;

            foreach (Scope scope in scopes)
            {
                if (scope.Contains(name))
                {
                    ret = scope.GetVariable(name);
                    break;
                }
            }

            return ret;
        }

        public Variable FindVariable(string name, ScopeLevel level)
        {
            Scope s = scopes.FirstOrDefault(x => x.Level == level);
            if (s != null)
                return s.GetVariable(name);
            return null;
        }
    }
}
