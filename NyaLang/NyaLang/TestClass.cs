using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{
    class TestClass
    {
        public static void VoidReturn()
        {
            return;
        }

        public static int? ParseInt(string s)
        {
            int i;
            return int.TryParse(s, out i) ? (int?)i : null;
        }

        public static char ByteToChar(byte i)
        {
            return (char)i;
        }

        public static void MultiReturn(int i)
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

        public static double EmitTest()
        {
            return Math.Sqrt(10 + 1 * 35 + (5.4 - 7.4));
        }

    }
}
