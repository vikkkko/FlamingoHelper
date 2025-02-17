using System.Numerics;

namespace Flamingo.SwapPair
{
    public struct PriceCumulative
    {
        public BigInteger Price0CumulativeLast;

        public BigInteger Price1CumulativeLast;

        public BigInteger BlockTimestampLast;
    }
}
