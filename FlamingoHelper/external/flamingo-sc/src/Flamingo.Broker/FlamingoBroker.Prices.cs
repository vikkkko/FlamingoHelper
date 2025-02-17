using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;

namespace Flamingo.Broker
{
    public partial class FlamingoBroker
    {
        /// <summary>
        /// Take a snapshot of the price at the current block index. The price is multiplied by the PriceRoundingPrecision to avoid floating point arithmetic.
        /// </summary>
        /// <param name="pairId">The pair id</param>
        /// <param name="baseTokenReserve">The base token reserve</param>
        /// <param name="quoteTokenReserve">The quote token reserve</param>
        /// <param name="baseTokenDecimals">The base token decimals</param>
        /// <param name="quoteTokenDecimals">The quote token decimals</param>
        private static void TakePriceSnapshot(BigInteger pairId, BigInteger baseTokenReserve, BigInteger quoteTokenReserve, BigInteger baseTokenDecimals, BigInteger quoteTokenDecimals)
        {
            var currentBlockIndex = Ledger.CurrentIndex;
            var quoteTokenDecimalMultiplier = BigInteger.Pow(10, (int) quoteTokenDecimals);
            var baseTokenDecimalMultiplier = BigInteger.Pow(10, (int) baseTokenDecimals);
            var price = quoteTokenReserve * baseTokenDecimalMultiplier * PriceRoundingPrecision / baseTokenReserve / quoteTokenDecimalMultiplier;
            var lastBlockIndex = LastPriceBlockIndexStorage.Get(pairId);
            PriceSnapshotStorage.Put(pairId, currentBlockIndex, lastBlockIndex, price);
            LastPriceBlockIndexStorage.Put(pairId, currentBlockIndex);
        }

        /// <summary>
        /// Get the last block index for which a price snapshot was taken.
        /// If the pair has never had a price snapshot, this will return 0.
        /// </summary>
        /// <param name="pairId">The pair id</param>
        /// <returns>The last block index for which a price snapshot was taken</returns>
        [Safe]
        public static BigInteger GetLastIndexForPrice(BigInteger pairId)
        {
            return LastPriceBlockIndexStorage.Get(pairId);
        }

        /// <summary>
        /// Get the price at the given block index.
        /// If the pair has never had a price snapshot, this will return (0, 0).
        /// The return value contains two values:
        /// - The previous block index for which a price snapshot was taken (0 if there is no previous snapshot). This value can be used to traverse the price history.
        /// - The price at the given block index provided as argument. The price is multiplied by the PriceRoundingPrecision to avoid floating point arithmetic.
        /// </summary>
        /// <param name="pairId">The pair id</param>
        /// <param name="blockIndex">The block index to get the price for</param>
        /// <returns>The last block index for which a price snapshot was taken</returns>
        [Safe]
        public static (BigInteger previousBlockIndex, BigInteger price) GetPrice(BigInteger pairId, BigInteger blockIndex)
        {
            return PriceSnapshotStorage.Get(pairId, blockIndex);
        }
    }
}
