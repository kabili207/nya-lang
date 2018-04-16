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
        public TestClass()
        {
            object w = null;
        }

        public void VoidReturn()
        {
            return;
        }

        public int? ParseInt(string s)
        {
            int i;
            return int.TryParse(s, out i) ? (int?)i : null;
        }

        public char ByteToChar(byte i)
        {
            long l = 12L;
            return (char)i;
        }

        public long ReturnLong(int i)
        {
            return long.MaxValue;
        }

        private void Foo(string message, [Optional] string s1, string s2 = "Bacon")
        {
            s1 = s1 ?? "Flarp";
        }

        public sbyte ReturnByte()
        {
            return (sbyte)-1;
        }

        public ulong ReturnUlong()
        {
            decimal a = 1e-3m;

            return 12385935794567457587UL;
        }

    }
}
