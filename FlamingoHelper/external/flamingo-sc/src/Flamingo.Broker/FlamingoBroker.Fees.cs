using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Broker
{
    public partial class FlamingoBroker
    {
        /// <summary>
        /// Send collected fees to the fee collector.
        /// </summary>
        /// <param name="token">The token from which claiming the fee.</param>
        [NoReentrant]
        public static void ClaimFeeToCollector(UInt160 token)
        {
            // Only Fee collector can call this method
            ExecutionEngine.Assert(Runtime.CallingScriptHash == GetFeeCollector(), "Only fee collector can call this method");
            // NB: Anyone can do this but at the end the fee goes always to fee collector
            BigInteger tokenFeeBalance = GetFeeBalance(token);

            // Claim fee to collector only if there's something for the token
            // This can also prevent using invalid tokens because they would always have 0 fee balance
            if (tokenFeeBalance > 0)
            {
                BigInteger amountToFusdFund = 0;

                if (FusdFundAddressStorage.IsSet())
                {
                    amountToFusdFund = tokenFeeBalance * GetFusdFundFeePercentage() / FeeRoundingPrecision;
                }

                BigInteger amountToCollector = tokenFeeBalance - amountToFusdFund;

                DecreaseFeeBalance(token, tokenFeeBalance);
                NEP17Helper.SafeTransfer(token, Runtime.ExecutingScriptHash, GetFeeCollector(), amountToCollector);

                if (FusdFundAddressStorage.IsSet())
                {
                    NEP17Helper.SafeTransfer(token, Runtime.ExecutingScriptHash, GetFusdFundAddress(), amountToFusdFund);
                }

                FeeClaimed(token, GetFeeCollector(), tokenFeeBalance);
            }
        }

        private static void IncreaseFeeBalance(UInt160 token, BigInteger amount)
        {
            var currentBalance = TokenBalanceStorage.Increase(token, amount);
            var brokerBalance = NEP17Helper.SafeBalanceOf(token, Runtime.ExecutingScriptHash);
            ExecutionEngine.Assert(currentBalance <= brokerBalance, "Invalid balance in contract [IncreaseFeeBalance]");
            FeeBalanceStorage.Increase(token, amount);
        }

        private static void DecreaseFeeBalance(UInt160 token, BigInteger amount)
        {
            var currentBalance = TokenBalanceStorage.Decrease(token, amount);
            ExecutionEngine.Assert(currentBalance >= 0, "Invalid balance in contract [DecreaseUserBalance]");
            FeeBalanceStorage.Decrease(token, amount);
        }
    }
}
