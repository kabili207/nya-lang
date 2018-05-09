using NyaLang.Antlr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NyaLang
{
    static class LiteralHelper
    {
        public static Type EmitLiteral(ILGenerator _ilg, object o)
        {
            if (o == null)
            {
                _ilg.Emit(OpCodes.Ldnull);
                return null;
            }

            if (o is bool)
                EmitBool(_ilg, (bool)o);
            else if (o is string)
                EmitString(_ilg, (string)o);
            else if (o is float)
                EmitFloat(_ilg, (float)o);
            else if (o is double)
                EmitDouble(_ilg, (double)o);
            else if (o is decimal)
                EmitDecimal(_ilg, (decimal)o);
            else if (o is int)
                EmitInt(_ilg, (int)o);
            else if (o is short)
                EmitInt(_ilg, (short)o);
            else if (o is long)
                EmitInt(_ilg, (long)o);
            else if (o is byte)
                EmitInt(_ilg, (byte)o);
            else if (o is sbyte)
                EmitInt(_ilg, (sbyte)o);
            else if (o is ushort)
                EmitInt(_ilg, (ushort)o);
            else if (o is uint)
                EmitInt(_ilg, (uint)o);
            else if (o is ulong)
                EmitInt(_ilg, (ulong)o);
            else if (o is Regex)
                EmitRegex(_ilg, (Regex)o);

            return o.GetType();
        }

        public static object VisitLiteral(NyaParser.NullLiteralContext context)
        {
            return null;
        }

        private static void EmitBool(ILGenerator _ilg, bool b)
        {
            if (b)
            {
                _ilg.Emit(OpCodes.Ldc_I4_1);
            }
            else
            {
                _ilg.Emit(OpCodes.Ldc_I4_0);
            }
        }

        public static bool VisitLiteral(NyaParser.BoolLiteralContext context)
        {
            string boolText = context.GetText();

            return boolText == "true";
        }

        private static void EmitString(ILGenerator _ilg, string s)
        {
            _ilg.Emit(OpCodes.Ldstr, s);
        }

        public static object VisitLiteral(NyaParser.StringLiteralContext context)
        {
            string rawString = context.GetText();
            rawString = rawString.Substring(1, rawString.Length - 2);
            string unescaped = StringHelper.StringFromCSharpLiteral(rawString);
            return unescaped;
        }

        private static void EmitFloat(ILGenerator _ilg, float f)
        {
            _ilg.Emit(OpCodes.Ldc_R4, f);
        }

        private static void EmitDouble(ILGenerator _ilg, double d)
        {
            _ilg.Emit(OpCodes.Ldc_R8, d);
        }

        private static void EmitDecimal(ILGenerator _ilg, decimal d)
        {
            ConstructorInfo ctor1 = typeof(Decimal).GetConstructor(
                new Type[] {
                            typeof(Int32),
                            typeof(Int32),
                            typeof(Int32),
                            typeof(Boolean),
                            typeof(Byte)
                }
            );

            int[] parts = Decimal.GetBits(d);
            bool sign = (parts[3] & 0x80000000) != 0;

            byte scale = (byte)((parts[3] >> 16) & 0x7F);

            _ilg.Emit(OpCodes.Nop);
            _ilg.Emit(OpCodes.Ldc_I4, parts[0]);
            _ilg.Emit(OpCodes.Ldc_I4, parts[1]);
            _ilg.Emit(OpCodes.Ldc_I4, parts[2]);
            _ilg.Emit(OpCodes.Ldc_I4, sign ? 1 : 0);
            _ilg.Emit(OpCodes.Ldc_I4, (int)scale);
            _ilg.Emit(OpCodes.Newobj, ctor1);
        }

        public static object VisitLiteral(NyaParser.RealLiteralContext context)
        {
            string value = context.GetText().ToLower();
            string suffix = "";
            if (new[] { 'f', 'd', 'm' }.Contains(value[value.Length - 1]))
            {
                suffix = value[value.Length - 1].ToString();
                value = value.Substring(0, value.Length - 1);
            }

            switch (suffix)
            {
                case "m":
                    return decimal.Parse(value, value.Contains("e") ?
                        System.Globalization.NumberStyles.Float : System.Globalization.NumberStyles.Number);
                case "d":
                    return double.Parse(value);
                case "f":
                default:
                    return float.Parse(value);
            }
        }

        private static void EmitInt(ILGenerator _ilg, byte b)
        {
            _ilg.Emit(OpCodes.Ldc_I4, b);
        }

        private static void EmitInt(ILGenerator _ilg, short s)
        {
            _ilg.Emit(OpCodes.Ldc_I4, s);
        }

        private static void EmitInt(ILGenerator _ilg, int i)
        {
            _ilg.Emit(OpCodes.Ldc_I4, i);
        }

        private static void EmitInt(ILGenerator _ilg, long l)
        {
            _ilg.Emit(OpCodes.Ldc_I8, l);
        }

        private static void EmitInt(ILGenerator _ilg, sbyte b)
        {
            _ilg.Emit(OpCodes.Ldc_I4, b);
        }

        private static void EmitInt(ILGenerator _ilg, ushort s)
        {
            _ilg.Emit(OpCodes.Ldc_I4, s);
        }

        private static void EmitInt(ILGenerator _ilg, uint s)
        {
            _ilg.Emit(OpCodes.Ldc_I4, s);
        }

        private static void EmitInt(ILGenerator _ilg, ulong s)
        {
            _ilg.Emit(OpCodes.Ldc_I8, (long)s);
        }

        public static object VisitLiteral(NyaParser.IntegerLiteralContext context)
        {
            string text = context.GetText();
            var regex = Regex.Match(text, @"(\d+)(\w+)?");
            string value = regex.Groups[1].Value;
            string suffix = regex.Groups[2].Value.ToLower();

            switch (suffix)
            {
                case "b":
                    return byte.Parse(value);
                case "s":
                    return short.Parse(value);
                case "l":
                    return long.Parse(value);
                case "u":
                    return uint.Parse(value);
                case "lu":
                case "ul":
                    return ulong.Parse(value);
                case "su":
                case "us":
                    return ushort.Parse(value);
                case "sb":
                case "bs":
                    return sbyte.Parse(value);
                default:
                    return int.Parse(value);
            }
        }

        private static void EmitRegex(ILGenerator _ilg, Regex r)
        {
            ConstructorInfo ctor1 = typeof(Regex).GetConstructor(
                new Type[] {
                    typeof(String),
                    typeof(RegexOptions)
                }
            );

            _ilg.Emit(OpCodes.Nop);
            _ilg.Emit(OpCodes.Ldstr, r.ToString());
            _ilg.Emit(OpCodes.Ldc_I4, (int)r.Options);
            _ilg.Emit(OpCodes.Newobj, ctor1);
        }

        public static Regex VisitLiteral(NyaParser.RegexLiteralContext context)
        {
            string raw = context.GetText();
            int start = raw.IndexOf('/');
            int end = raw.LastIndexOf('/');
            string regex = raw.Substring(start + 1, end - 1);
            string flags = raw.Substring(end + 1);

            RegexOptions options = RegexOptions.None;
            foreach (var flag in flags)
            {
                switch (flag)
                {
                    case 'm':
                        options |= RegexOptions.Multiline;
                        break;
                    case 'i':
                        options |= RegexOptions.IgnoreCase;
                        break;
                    case 's':
                        options |= RegexOptions.Singleline;
                        break;
                    case 'x':
                        options |= RegexOptions.IgnorePatternWhitespace;
                        break;
                    case 'n':
                        options |= RegexOptions.ExplicitCapture;
                        break;
                }
            }

            return new Regex(regex, options);
        }
    }
}
