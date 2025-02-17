using System.Numerics;
using Flamingo.Broker.Models;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Broker
{
    [ManifestExtra("Author", "")]
    [ManifestExtra("Email", "")]
    [ManifestExtra("Description", "")]
    [ContractTrust("02738f9efdd954f8436d91ff5f373ae8af14641abc6511de3d1a2ab40665e9a21f")]
    [ContractPermission("*", "*")]
    public partial class FlamingoBroker : SmartContract
    {
        private static readonly UInt160 InitialOwner = "6288e8a5e3d92f6aa645e6f749b4cb6cc024b211";
        private static readonly UInt160 InitialFeeCollector = "6288e8a5e3d92f6aa645e6f749b4cb6cc024b211";
        private static readonly UInt160 InitialAMMRouter = "0x80841bd50d95007cebe45f9cb546f798641fe4c2";
        private static readonly UInt160 InitialAMMFactory = "0x875659fed1972b106ba3adbee9b2fd4f09948f25";

        private static readonly UInt160 BNEOContract = "0x48c40d4666f93408be1bef038b6722404d9a4c2a";

        private static readonly BigInteger FeeRoundingPrecision = 1000000;
        // This number is used so that we can have both sub and super precision for prices. E.g. 0.001 or 1000. as minimum price movement.
        public static readonly BigInteger PriceCoefficient = 1_000_000_000_000_000_000;
        // This number is used to perform rounding operations on prices.
        public static readonly BigInteger RoundingPrecision = BigInteger.Parse("100000000000000000000000000");
        public static readonly BigInteger PriceRoundingPrecision = BigInteger.Parse("100000000000000000000");

        private static readonly BigInteger DefaultTakerFee = 1500;
        private static readonly BigInteger DefaultMakerFee = 1500;

        private static readonly BigInteger DefaultFusdFundFeePercentage = 200000;

        private static readonly BigInteger AMMRouterSwapFee = 2500;

        private static readonly BigInteger OrdersPerPage = 20;

        [Safe]
        public static string GetVersion()
        {
            return "VERSION_PLACEHOLDER";
        }

        /// <summary>
        /// Get a price node object from the tree.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="nodeIndex">The index of the node</param>
        /// <param name="fromSellTree">If the node is from the sell tree. True = sell tree, False = buy tree</param>
        /// <returns>The price node object</returns>
        [Safe]
        public static PriceNode GetPriceNode(BigInteger pairId, BigInteger nodeIndex, bool fromSellTree)
        {
            return fromSellTree ? SellTreeStorage.Get(pairId, nodeIndex) : BuyTreeStorage.Get(pairId, nodeIndex);
        }

        /// <summary>
        /// Get the bit length of the tree. The tree bit length is the number of bits allocated to store price nodes.
        /// If we have a tree bit length of 8, we can store 256 price nodes and so on.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <returns>The tree byte length</returns>
        [Safe]
        public static BigInteger GetTreeBitLength(BigInteger pairId)
        {
            return TreeBitLengthStorage.Get(pairId);
        }

        /// <summary>
        /// Get pair base token.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <returns>The hash of the token</returns>
        [Safe]
        public static UInt160 GetBaseToken(BigInteger pairId)
        {
            return BaseTokenStorage.Get(pairId);
        }

        /// <summary>
        /// Get pair quote token.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <returns>The hash of the token</returns>
        [Safe]
        public static UInt160 GetQuoteToken(BigInteger pairId)
        {
            return QuoteTokenStorage.Get(pairId);
        }

        [Safe]
        public static BigInteger GetDecimals(UInt160 token)
        {
            return TokenDecimalPlacesStorage.Get(token);
        }

        [Safe]
        public static BigInteger GetPricePrecision(BigInteger pairId)
        {
            return PricePrecisionStorage.Get(pairId);
        }

        [Safe]
        public static BigInteger GetMaxPrice(BigInteger pairId)
        {
            return 1 << (int)GetTreeBitLength(pairId);
        }

        [Safe]
        public static BigInteger GetFeeBalance(UInt160 token)
        {
            return FeeBalanceStorage.Get(token);
        }

        [Safe]
        public static BigInteger GetPriceCoefficient()
        {
            return PriceCoefficient;
        }

        [Safe]
        public static BigInteger GetRoundingPrecision()
        {
            return RoundingPrecision;
        }

        [Safe]
        public static BigInteger GetPriceRoundingPrecision()
        {
            return PriceRoundingPrecision;
        }

        /// <summary>
        /// Turns a column and a price row into a node index that can be used to access nodes in the tree.
        /// The colum and the price is "concatenated" into a single number by shifting the column to the left of the price.
        /// </summary>
        /// <param name="column">The column index. Starts from 0.</param>
        /// <param name="priceRow">The price row index</param>
        /// <returns>The node index</returns>
        [Safe]
        public static BigInteger GetNodeIndex(int treeWidth, int column, BigInteger priceRow)
        {
            // We need to round the price index according to the column we are in.
            // Example: If we are in column 0, the price index should be ceiled to the number of nodes in the column.
            BigInteger numberOfNodesInColumn = 1 << (column + 1);
            BigInteger rightMostNumberOfNodes = 1 << treeWidth;
            var priceRangePerNode = rightMostNumberOfNodes / numberOfNodesInColumn;
            // Integer ceil division
            var nodeNumber = (priceRow + priceRangePerNode) / priceRangePerNode;
            var roundedPriceIndex = (nodeNumber * priceRangePerNode) - 1;
            var theoreticalNodeIndex = (column << treeWidth) | roundedPriceIndex;

            var nodeIndex = (column << treeWidth) | priceRow;

            ExecutionEngine.Assert(nodeIndex == theoreticalNodeIndex, "Node index is not correct!");
            return nodeIndex;
        }

        [Safe]
        public static BigInteger GetColumnFromNodeIndex(BigInteger nodeIndex, int treeWidth)
        {
            return nodeIndex >> treeWidth;
        }

        [Safe]
        public static BigInteger GetPriceRowFromNodeIndex(BigInteger nodeIndex, int treeWidth)
        {
            return nodeIndex & ((1 << treeWidth) - 1);
        }

        [Safe]
        public static ByteString GetCanceledTreeKey(BigInteger pairId, BigInteger priceRow, bool isBuy)
        {
            return StdLib.Serialize((pairId, priceRow, isBuy));
        }

        [Safe]
        public static BigInteger GetBaseAmountExecutedAtNode(bool isBuy, BigInteger pairId, BigInteger endingNodeIndex)
        {
            return isBuy ? BuyBaseAmountExecutedStorage.Get(pairId, endingNodeIndex) : SellBaseAmountExecutedStorage.Get(pairId, endingNodeIndex);
        }

        [Safe]
        public static BigInteger GetQuoteAmountExecutedAtNode(bool isBuy, BigInteger pairId, BigInteger endingNodeIndex)
        {
            return isBuy ? BuyQuoteAmountExecutedStorage.Get(pairId, endingNodeIndex) : SellQuoteAmountExecutedStorage.Get(pairId, endingNodeIndex);
        }

        [Safe]
        public static BigInteger GetUserOrderCount(UInt160 user)
        {
            return UserOrderCountStorage.Get(user);
        }

        [Safe]
        public static BigInteger GetTokenBalance(UInt160 token)
        {
            return TokenBalanceStorage.Get(token);
        }

        [Safe]
        public static bool IsModerator(UInt160 user)
        {
            return ModeratorsStorage.Get(user);
        }
    }
}
