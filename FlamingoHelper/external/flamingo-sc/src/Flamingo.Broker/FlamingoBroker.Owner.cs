using System.Numerics;
using Flamingo.Broker.Utils;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Broker
{
    public partial class FlamingoBroker
    {
        /// <summary>
        /// Transfer the ownership of the contract to the new owner.
        /// </summary>
        /// <param name="newOwner">The new owner of the contract.</param>
        public static bool ChangeOwner(UInt160 newOwner)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(Runtime.CheckWitness(newOwner), "No new owner witness");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(newOwner) && newOwner != UInt160.Zero && newOwner != null, "Invalid new owner");

            OwnerStorage.Put(newOwner);
            return true;
        }

        public static bool AddModerator(UInt160 moderator)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(moderator) && moderator != UInt160.Zero && moderator != null, "Invalid moderator");

            ModeratorsStorage.Put(moderator, true);
            return true;
        }

        public static bool RemoveModerator(UInt160 moderator)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(moderator) && moderator != UInt160.Zero && moderator != null, "Invalid moderator");

            ModeratorsStorage.Put(moderator, false);
            return true;
        }

        /// <summary>
        /// Change the address of the fee collector
        /// </summary>
        /// <param name="collector">New fee collector address.</param>
        public static bool ChangeFeeCollector(UInt160 collector)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(collector) && collector != UInt160.Zero && collector != null, "Invalid collector");

            FeeCollectorStorage.Put(collector);
            return true;
        }

        /// <summary>
        /// Change the address of the FUSD fund
        /// </summary>
        /// <param name="fusdFundAddress">New fusd fund address.</param>
        public static bool SetFusdFundAddress(UInt160 fusdFundAddress)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(fusdFundAddress) && fusdFundAddress != UInt160.Zero && fusdFundAddress != null, "Invalid fusdFundAddress");

            FusdFundAddressStorage.Put(fusdFundAddress);
            return true;
        }

        /// <summary>
        /// Remove the address of the FUSD fund
        /// </summary>
        /// <param name="fusdFundAddress">New fusd fund address.</param>
        public static bool RemoveFusdFundAddress()
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");

            FusdFundAddressStorage.Put(UInt160.Zero);
            return true;
        }

        /// <summary>
        /// Change the address that can collect GAS from bNEO
        /// </summary>
        /// <param name="collector">New gas collector address.</param>
        public static bool ChangeGasCollector(UInt160 collector)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(collector) && collector != UInt160.Zero && collector != null, "Invalid collector");

            GasCollectorStorage.Put(collector);
            return true;
        }

        /// <summary>
        /// Change the address of the amm router
        /// </summary>
        /// <param name="router">New amm router address.</param>
        public static bool ChangeAMMRouter(UInt160 router)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(router) && router != UInt160.Zero && router != null, "Invalid router");

            AMMRouterStorage.Put(router);
            return true;
        }

        /// <summary>
        /// Change the address of the amm factory
        /// </summary>
        /// <param name="factory">New amm factory address.</param>
        public static bool ChangeAMMFactory(UInt160 factory)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(factory) && factory != UInt160.Zero && factory != null, "Invalid factory");

            AMMFactoryStorage.Put(factory);
            return true;
        }

        /// <summary>
        /// Enable token deposit.
        /// </summary>
        /// <param name="token">The hash of the token</param>
        public static void EnableTokenDeposit(UInt160 token)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsContract(token), "Token not a contract");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(token), "Invalid token address");

            TokenDepositEnabledStorage.Put(token, true);
        }

        /// <summary>
        /// Disable token deposit.
        /// </summary>
        /// <param name="token">The hash of the token</param>
        public static void DisableTokenDeposit(UInt160 token)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsContract(token), "Token not a contract");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(token), "Invalid token address");

            TokenDepositEnabledStorage.Put(token, false);
        }

        /// <summary>
        /// Enable token withdraw.
        /// </summary>
        /// <param name="token">The hash of the token</param>
        public static void EnableTokenWithdraw(UInt160 token)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsContract(token), "Token not a contract");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(token), "Invalid token address");

            TokenWithdrawEnabledStorage.Put(token, true);
        }

        /// <summary>
        /// Disable token withdraw.
        /// </summary>
        /// <param name="token">The hash of the token</param>
        /// <param name="sender">The address of the moderator or owner that is signing the transaction</param>
        public static void DisableTokenWithdraw(UInt160 token, UInt160 sender)
        {
            ExecutionEngine.Assert(HasOwnerWitness() || HasModeratorWitness(sender), "No owner or moderator witness");
            ExecutionEngine.Assert(ContractUtils.IsContract(token), "Token not a contract");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(token), "Invalid token address");

            TokenWithdrawEnabledStorage.Put(token, false);
        }

        /// <summary>
        /// Add a new pair to be traded, aka create its orderbook setting all of its variables.
        /// </summary>
        /// <param name="baseToken">The base token of the pair.</param>
        /// <param name="quoteToken">The quote token of the pair.</param>
        /// <param name="treeBitLength">The tree bit length of the orderbook's pair.</param>
        /// <param name="pricePrecision">The price precision to be used for the pair.</param>
        public static void AddPair(UInt160 baseToken, UInt160 quoteToken, BigInteger treeBitLength, BigInteger pricePrecision)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsContract(baseToken) && ContractUtils.IsContract(quoteToken), "Token not a contract");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(baseToken, quoteToken), "Invalid token address");
            ExecutionEngine.Assert(treeBitLength > 0 && treeBitLength < 100, "Invalid tree bit length");
            ExecutionEngine.Assert(pricePrecision > 0, "Invalid price precision");

            var baseTokenDecimalPlaces = NEP17Helper.GetDecimals(baseToken);
            var quoteTokenDecimalPlaces = NEP17Helper.GetDecimals(quoteToken);
            TokenDecimalPlacesStorage.Put(baseToken, baseTokenDecimalPlaces);
            TokenDecimalPlacesStorage.Put(quoteToken, quoteTokenDecimalPlaces);

            BigInteger pairId = PairCounterStorage.Increase();

            BaseTokenStorage.Put(pairId, baseToken);
            QuoteTokenStorage.Put(pairId, quoteToken);
            TreeBitLengthStorage.Put(pairId, treeBitLength);
            PricePrecisionStorage.Put(pairId, pricePrecision);
            OrderTradingPausedStorage.Put(pairId, true);
            OrderManagementPausedStorage.Put(pairId, true);
            GasToBurnStorage.Put(pairId, 1);

            PairAdded(pairId, baseToken, quoteToken, treeBitLength, pricePrecision);
        }

        /// <summary>
        /// Pause order trading for a pair.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="sender">The address of the moderator or owner that is signing the transaction</param>
        public static void PausePairOrderTrading(BigInteger pairId, UInt160 sender)
        {
            ExecutionEngine.Assert(HasOwnerWitness() || HasModeratorWitness(sender), "No owner or moderator witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");

            OrderTradingPausedStorage.Put(pairId, true);
            PairOrderTradingPaused(pairId);
        }

        /// <summary>
        /// Unpause order trading for a pair.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        public static void UnpausePairOrderTrading(BigInteger pairId)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");

            OrderTradingPausedStorage.Put(pairId, false);
            PairOrderTradingResumed(pairId);
        }

        /// <summary>
        /// Pause order management for a pair.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="sender">The address of the moderator or owner that is signing the transaction</param>
        public static void PausePairOrderManagement(BigInteger pairId, UInt160 sender)
        {
            ExecutionEngine.Assert(HasOwnerWitness() || HasModeratorWitness(sender), "No owner or moderator witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");

            OrderManagementPausedStorage.Put(pairId, true);
            PairOrderManagementPaused(pairId);
        }

        /// <summary>
        /// Unpause order management for a pair.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        public static void UnpausePairOrderManagement(BigInteger pairId)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");

            OrderManagementPausedStorage.Put(pairId, false);
            PairOrderManagementResumed(pairId);
        }

        /// <summary>
        /// Set pair maker fee
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="makerFee">New fee value.</param>
        public static void SetPairMakerFee(BigInteger pairId, BigInteger makerFee)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");
            ExecutionEngine.Assert(makerFee >= 0 && makerFee <= FeeRoundingPrecision, "Invalid maker fee");

            MakerFeeStorage.Put(pairId, makerFee);
            PairMakerFeeChanged(pairId, makerFee);
        }

        /// <summary>
        /// Set pair taker fee
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="takerFee">New fee value.</param>
        public static void SetPairTakerFee(BigInteger pairId, BigInteger takerFee)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");
            ExecutionEngine.Assert(takerFee >= 0 && takerFee <= FeeRoundingPrecision, "Invalid taker fee");

            TakerFeeStorage.Put(pairId, takerFee);
            PairTakerFeeChanged(pairId, takerFee);
        }

        /// <summary>
        /// Set percentage of fees that goes to the FUSD fund.
        /// </summary>
        /// <param name="fusdFundFeePercentage">New fee value.</param>
        public static void SetFusdFundFeePercentage(BigInteger fusdFundFeePercentage)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(fusdFundFeePercentage >= 0 && fusdFundFeePercentage <= FeeRoundingPrecision, "Invalid fusd fund fee percentage");

            FusdFundFeePercentageStorage.Put(fusdFundFeePercentage);
            FusdFundFeePercentageChanged(fusdFundFeePercentage);
        }

        /// <summary>
        /// Claim GAS from bNEO
        /// </summary>
        /// <param name="receiver">The address to which send the GAS</param>
        public static void ClaimGASFromBNEO(UInt160 receiver)
        {
            ExecutionEngine.Assert(HasGasCollectorWitness(), "No owner witness");
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(receiver) && receiver != UInt160.Zero, "Invalid receiver");

            var thisContract = Runtime.ExecutingScriptHash;

            // Execute a transfer of 0 BNEO to trigger GAS claiming
            BigInteger beforeBalance = NEP17Helper.SafeBalanceOf(GAS.Hash, thisContract);
            NEP17Helper.SafeTransfer(BNEOContract, thisContract, BNEOContract, 0);
            BigInteger afterBalance = NEP17Helper.SafeBalanceOf(GAS.Hash, thisContract);
            // Calculate GAS claimed comparin before and after balance
            var claimedGASAmount = afterBalance - beforeBalance;

            // Transfer claimed GAS to the receiver
            if(claimedGASAmount > 0) NEP17Helper.SafeTransfer(GAS.Hash, thisContract, receiver, claimedGASAmount);
        }

        /// Set the amount of GAS to burn when the AMM is not executing any trades.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="gasAmount">The amount of GAS to burn.</param>
        public static void SetGasToBurn(BigInteger pairId, int gasAmount)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");
            ExecutionEngine.Assert(gasAmount >= 0, "Invalid gas amount");

            GasToBurnStorage.Put(pairId, gasAmount);
            GasToBurnChanged(pairId, gasAmount);
        }

        /// <summary>
        /// Update the contract.
        /// </summary>
        /// <param name="nefFile">The nef file used for the update.</param>
        /// <param name="manifest">The manifest of the contract.</param>
        /// <param name="data">The data along the update.</param>
        public static void Update(ByteString nefFile, string manifest, object data)
        {
            ExecutionEngine.Assert(HasOwnerWitness(), "No owner witness");
            ContractManagement.Update(nefFile, manifest, data);
        }

        [Safe]
        public static UInt160 GetOwner()
        {
            return OwnerStorage.Get();
        }

        [Safe]
        public static UInt160 GetFeeCollector()
        {
            return FeeCollectorStorage.Get();
        }

        [Safe]
        public static UInt160 GetFusdFundAddress()
        {
            return FusdFundAddressStorage.Get();
        }

        [Safe]
        public static UInt160 GetGasCollector()
        {
            return GasCollectorStorage.Get();
        }

        [Safe]
        public static UInt160 GetAMMRouter()
        {
            return AMMRouterStorage.Get();
        }

        [Safe]
        public static UInt160 GetAMMFactory()
        {
            return AMMFactoryStorage.Get();
        }

        [Safe]
        public static bool IsTokenDepositEnabled(UInt160 token)
        {
            return TokenDepositEnabledStorage.Get(token);
        }

        [Safe]
        public static bool IsTokenWithdrawEnabled(UInt160 token)
        {
            return TokenWithdrawEnabledStorage.Get(token);
        }

        [Safe]
        public static BigInteger GetPairCounter()
        {
            return PairCounterStorage.Get();
        }

        [Safe]
        public static BigInteger GetMakerFee(BigInteger pairId)
        {
            return MakerFeeStorage.Get(pairId);
        }

        [Safe]
        public static BigInteger GetTakerFee(BigInteger pairId)
        {
            return TakerFeeStorage.Get(pairId);
        }

        [Safe]
        public static BigInteger GetFusdFundFeePercentage()
        {
            return FusdFundFeePercentageStorage.Get();
        }

        [Safe]
        public static bool IsPairOrderTradingPaused(BigInteger pairId)
        {
            return OrderTradingPausedStorage.Get(pairId);
        }

        [Safe]
        public static bool IsPairOrderManagementPaused(BigInteger pairId)
        {
            return OrderManagementPausedStorage.Get(pairId);
        }

        [Safe]
        public static bool IsPairExisting(BigInteger pairId)
        {
            return GetTreeBitLength(pairId) > 0;
        }

        private static bool HasOwnerWitness()
        {
            return Runtime.CheckWitness(GetOwner());
        }

        private static bool HasGasCollectorWitness()
        {
            return Runtime.CheckWitness(GetGasCollector());
        }

        private static bool HasModeratorWitness(UInt160 moderator)
        {
            var isModeratorByWitness = Runtime.CheckWitness(moderator) || moderator.Equals(Runtime.CallingScriptHash);
            var isModeratorByStorage = ModeratorsStorage.Get(moderator);
            return isModeratorByWitness && isModeratorByStorage;
        }
    }
}
