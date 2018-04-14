using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{
    interface IFace { }
    class TestBase { }

    class TestClass : TestBase, IFace
    {
        string classVar = "SKREEEE";

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
            return (char)i;
        }

        public void MultiReturn(int i)
        {
            i += 4;

            if (i % 12 == 0)
            {
                return;
            }
            Console.WriteLine("Vacon");
            return;
        }

        private void Foo(string message, [Optional] string s1, string s2 = "Bacon")
        {
            s1 = s1 ?? "Flarp";
        }

        public string ClassVar()
        {
            return classVar;
        }

    }
}
