using Neo.SmartContract.Framework;

namespace Flamingo.SwapFactory.Models
{
    public struct ExchangePair
    {
        public UInt160 TokenA;
        public UInt160 TokenB;
        public UInt160 ExchangePairHash;
    }
}
