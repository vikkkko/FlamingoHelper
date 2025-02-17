using System.Numerics;
using Neo.SmartContract.Framework;

namespace Flamingo.Broker
{
    public partial class FlamingoBroker
    {
        public static event OrderUpsertedDelegate OrderUpserted;

        public delegate void OrderUpsertedDelegate(
            BigInteger id,
            BigInteger pairId,
            UInt160 owner,
            BigInteger totalOrderBaseAmount,
            BigInteger totalOrderQuoteAmount,
            BigInteger placedInOrderBookBaseAmount,
            BigInteger placedInOrderBookQuoteAmount,
            BigInteger price,
            BigInteger baseAmountTradedInAmm,
            BigInteger quoteAmountTradedInAmm,
            BigInteger baseAmountTradedBeforeOrderPlaced,
            BigInteger quoteAmountTradedBeforeOrderPlaced,
            bool isBuy,
            BigInteger cancelledBaseAmount,
            BigInteger cancelledQuoteAmount,
            BigInteger claimedBaseAmount,
            BigInteger claimedQuoteAmount,
            BigInteger emptiedCountWhenInserted,
            BigInteger globalBaseAmountPlacedAtPriceWhenInserted,
            BigInteger globalQuoteAmountPlacedAtPriceWhenInserted,
            BigInteger CancelId,
            BigInteger EndingPrice,
            BigInteger FeeAmount
        );

        public static event AccountUpdatedDelegate AccountUpdated;

        public delegate void AccountUpdatedDelegate(
            UInt160 user,
            UInt160 token,
            BigInteger newBalance,
            BigInteger balanceChange
        );

        public static event UserDepositedDelegate UserDeposited;

        public delegate void UserDepositedDelegate(
            UInt160 user,
            UInt160 token,
            BigInteger amount
        );

        public static event UserWithdrewDelegate UserWithdrew;

        public delegate void UserWithdrewDelegate(
            UInt160 user,
            UInt160 token,
            BigInteger amount
        );

        public static event PairOrderTradingPausedDelegate PairOrderTradingPaused;

        public delegate void PairOrderTradingPausedDelegate(BigInteger pairId);

        public static event PairOrderManagementPausedDelegate PairOrderManagementPaused;

        public delegate void PairOrderManagementPausedDelegate(BigInteger pairId);

        public static event PairOrderTradingResumedDelegate PairOrderTradingResumed;

        public delegate void PairOrderTradingResumedDelegate(BigInteger pairId);

        public static event PairOrderManagementResumedDelegate PairOrderManagementResumed;

        public delegate void PairOrderManagementResumedDelegate(BigInteger pairId);

        public static event PairAddedDelegate PairAdded;

        public delegate void PairAddedDelegate(
            BigInteger pairId,
            UInt160 baseToken,
            UInt160 quoteToken,
            BigInteger treeBitLength,
            BigInteger pricePrecision
        );

        public static event FeeClaimedDelegate FeeClaimed;

        public delegate void FeeClaimedDelegate(
            UInt160 token,
            UInt160 feeCollector,
            BigInteger tokenFeeBalance
        );

        public static event PairMakerFeeChangedDelegate PairMakerFeeChanged;

        public delegate void PairMakerFeeChangedDelegate(
            BigInteger pairId,
            BigInteger makerFee
        );

        public static event PairTakerFeeChangedDelegate PairTakerFeeChanged;

        public delegate void PairTakerFeeChangedDelegate(
            BigInteger pairId,
            BigInteger takerFee
        );

        public static event FusdFundFeePercentageChangedDelegate FusdFundFeePercentageChanged;

        public delegate void FusdFundFeePercentageChangedDelegate(
            BigInteger fusdFundFeePercentage
        );

        public static event GasToBurnChangedDelegate GasToBurnChanged;

        public delegate void GasToBurnChangedDelegate(
            BigInteger pairId,
            BigInteger gasToBurnAmount
        );

        public static event DebugMethodCalledDelegate DebugMethodCalled;

        public delegate void DebugMethodCalledDelegate(
            UInt160 user,
            string methodName,
            List<object> parameters
        );
    }
}
