using System.Numerics;

namespace Flamingo.Broker.Utils
{
    public static class MathUtils
    {
        public static BigInteger Max(BigInteger num0, BigInteger num1)
        {
            return num0 > num1 ? num0 : num1;
        }

        public static BigInteger Min(BigInteger num0, BigInteger num1)
        {
            return num0 < num1 ? num0 : num1;
        }
    }
}
