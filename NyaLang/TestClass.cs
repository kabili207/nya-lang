using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang.Test
{
    public interface Dog
    {
        int DoThing();
    }
    public class Animal
    {

        public Animal()
        {

        }

        static Animal()
        {

        }

        public virtual void Foo()
        {

        }

        public void
        Bar()
        {

        }
    }


    class CatDog : Cat, Dog {


        public int DoThing()
        {
            return 10;
        }
    }


    public class Cat : Animal
    {

        public Cat()
        {
            object w = null;
        }

        static Cat()
        {
            //var a = 12 ?? 4;
        }

        public static
        decimal SetCall(string[] args)
        {
            var a = 12;
            var b = Math.Log10(10 + a * 35 + (5.4 - 7.4));
            var c = 13e-3m;
            return a + c;
        }


        public static
        void SetCall1(string[] args)
        {
            var a = 12;
            var b = Math.Log10(10 + a * 35 + (7.4f));
        }

        decimal ConvertDec(int i)
        {
            return i + 13e-3m;
        }

        void ShiftDec(decimal d)
        {
            var q = 10e-1m;
            q += d;
        }

        void Foo(string message, string s2, string s1 = "bacon")
        {
            s1 = s1 ?? "waffle";
            //var s = 0;
            //var s ?= 12;
            var t = null ?? "herp";
        }
    }

}
