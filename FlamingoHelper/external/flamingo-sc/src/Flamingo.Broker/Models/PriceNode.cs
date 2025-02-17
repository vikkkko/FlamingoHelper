using System.Numerics;

namespace Flamingo.Broker.Models
{
    public class PriceNode
    {
        // The quantity of base token at the node.
        public BigInteger BaseAmount;
        // The quantity of quote token at the node.
        public BigInteger QuoteTotal;
        // Indicates how many times the node has been emptied.
        public BigInteger EmptiedCount;
        // Indicates how many times the sibling node has been emptied.
        public BigInteger EmptiedCountSibling;
    }
}
