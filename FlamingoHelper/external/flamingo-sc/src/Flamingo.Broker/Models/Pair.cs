using System.Numerics;
using Neo.SmartContract.Framework;

namespace Flamingo.Broker.Models
{
    public class Pair
    {
        public BigInteger Id;
        public UInt160 BaseToken;
        public UInt160 QuoteToken;
        public int TreeWidth;
        public BigInteger PricePrecision;
        public BigInteger BaseTokenDecimals;
        public BigInteger QuoteTokenDecimals;
    }
}
