using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang
{
    static class CastHelper
    {
        static readonly Type[] signedPrimitives = new[] { typeof(SByte), typeof(Int16), typeof(Int32), typeof(Int64) };
        static readonly Type[] unsignedPrimitives = new[] { typeof(Byte), typeof(UInt16), typeof(UInt32), typeof(UInt64) };
        static readonly Type[] floatingPrimitives = new[] { typeof(Single), typeof(Double) };

        static Dictionary<Type, ConversionPair> primitiveMaps = new Dictionary<Type, ConversionPair>();

        struct ConversionPair
        {
            public OpCode[] OpCode;
            // TODO: We'll come back to these...
            //public OpCode[] SignedOverflow;
            //public OpCode[] UnsignedOverflow;
        }

        static CastHelper()
        {
            AddPrimitiveMap(typeof(SByte), OpCodes.Conv_I1);
            AddPrimitiveMap(typeof(Int16), OpCodes.Conv_I2);
            AddPrimitiveMap(typeof(Int32), OpCodes.Conv_I4);
            AddPrimitiveMap(typeof(Int64), OpCodes.Conv_I8);
            AddPrimitiveMap(typeof(Byte), OpCodes.Conv_U1);
            AddPrimitiveMap(typeof(UInt16), OpCodes.Conv_U2);
            AddPrimitiveMap(typeof(UInt32), OpCodes.Conv_U4);
            AddPrimitiveMap(typeof(UInt64), OpCodes.Conv_U8);
        }

        static void AddPrimitiveMap(Type t, OpCode signed)
        {
            AddPrimitiveMap(t, new[] { signed });
        }

        static void AddPrimitiveMap(Type t, OpCode[] opCode)
        {
            primitiveMaps.Add(t, new ConversionPair
            {
                OpCode = opCode
            });
        }

        public static bool IsPrimitive(Type t)
        {
            return signedPrimitives.Contains(t) || unsignedPrimitives.Contains(t) || floatingPrimitives.Contains(t);
        }

        private static bool TryOpConversion(ILGenerator ilg, Type src, Type dst)
        {
            // TODO: Consider caching the result of this to speed up compilation

            var infos = src.GetMethods().Concat(dst.GetMethods()).Where(
                x => (x.Name == "op_Implicit" || x.Name == "op_Explicit") && x.ReturnType == dst);

            MethodInfo miOp = null;

            foreach(MethodInfo mi in infos)
            {
                ParameterInfo[] pi = mi.GetParameters();
                if(pi.Length == 1 && pi[0].ParameterType == src)
                {
                    miOp = mi;
                    break;
                }
            }

            if(miOp != null)
            {
                ilg.Emit(OpCodes.Call, miOp);
                return true;
            }
            return false;
        }

        public static bool TryConvert(ILGenerator ilg, Type src, Type dst)
        {
            // Account for null values?
            if (src == null || dst == null)
                return true;

            if (src == dst)
            {
                // NOP?
                return true;
            }

            if (TryOpConversion(ilg, src, dst))
                return true;

            if(IsPrimitive(src) && IsPrimitive(dst))
            {
                if (dst == typeof(Single))
                {
                    if (signedPrimitives.Contains(src) || src == typeof(Double))
                        ilg.Emit(OpCodes.Conv_R4);
                    else
                        ilg.Emit(OpCodes.Conv_R_Un);
                    return true;
                }

                // CLR doesn't allow unsigned to double
                if (dst == typeof(Double) && (signedPrimitives.Contains(src) || floatingPrimitives.Contains(src)))
                {
                    ilg.Emit(OpCodes.Conv_R8);
                    return true;
                }

                if(primitiveMaps.ContainsKey(dst))
                {
                    ConversionPair pair = primitiveMaps[dst];
                    foreach (OpCode op in pair.OpCode)
                        ilg.Emit(op);
                    return true;
                }
            }

            // TODO: Byte <--> Char. Reflector seems to indicate nothing is needed for byte --> char

            // TODO: Nullable<>

            // TODO: Reference types and other magic

            return false;
        }
    }
}
