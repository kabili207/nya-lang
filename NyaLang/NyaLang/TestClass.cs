using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{
    interface IFace { }
    class TestBase {
        public TestBase()
        {

        }
    }

    class TestClass : TestBase, IFace
    {
        const string waffle = "DLKFDJSLFDSJDS";

        public TestClass()
        {
            object w = null;
        }

        public decimal SetCall(string[] args)
        {
            int a = 12;
            double b = Math.Log10(10 + a * 35 + (5.4 - 7.4));
            decimal c = 13e-3m;
            return a + c;
        }

        public ulong ReturnUlong()
        {
            decimal a = 1e-3m;

            return 12385935794567457587UL;
        }

        decimal ConvertDec(int i)
        {
            return i + 13e-3m;
        }

    }
}
