using System.Numerics;
using Flamingo.Broker.Utils;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Broker
{
    public partial class FlamingoBroker
    {
        /// <summary>
        /// Deposit an amount of tokens to the contract for the user.
        /// </summary>
        /// <param name="user">The user related to the operation.</param>
        /// <param name="token">The token used.</param>
        /// <param name="amount">The amount to be transferred.</param>
        [NoReentrant]
        public static void Deposit(UInt160 user, UInt160 token, BigInteger amount)
        {
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(user, token), "Invalid addresses");
            ExecutionEngine.Assert(Runtime.CheckWitness(user) || user.Equals(Runtime.CallingScriptHash), "No user witness");
            ExecutionEngine.Assert(amount > 0, "Amount must be greater than 0");
            ExecutionEngine.Assert(IsTokenDepositEnabled(token), "Token deposit not enabled");

            UserDeposited(user, token, amount);

            NEP17Helper.SafeTransfer(token, user, Runtime.ExecutingScriptHash, amount);
            IncreaseUserBalance(user, token, amount);
        }

        /// <summary>
        /// Withdraw an amount of tokens from the contract to the user.
        /// </summary>
        /// <param name="user">The user related to the operation.</param>
        /// <param name="token">The token used.</param>
        /// <param name="amount">The amount to be transferred.</param>
        [NoReentrant]
        public static void Withdraw(UInt160 user, UInt160 token, BigInteger amount)
        {
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(user, token), "Invalid addresses");
            ExecutionEngine.Assert(Runtime.CheckWitness(user) || user.Equals(Runtime.CallingScriptHash), "No user witness");
            ExecutionEngine.Assert(amount > 0, "Amount must be greater than 0");
            ExecutionEngine.Assert(IsTokenWithdrawEnabled(token), "Token withdraw not enabled");

            BigInteger availableAmount = AccountStorage.Get(user, token);
            ExecutionEngine.Assert(availableAmount >= amount, "Not enough to withdraw");

            UserWithdrew(user, token, amount);

            NEP17Helper.SafeTransfer(token, Runtime.ExecutingScriptHash, user, amount);
            DecreaseUserBalance(user, token, amount);
        }

        [Safe]
        public static BigInteger GetAccountBalance(UInt160 user, UInt160 token)
        {
            return AccountStorage.Get(user, token);
        }

        [Safe]
        public static Map<UInt160, BigInteger> GetAccountBalanceAll(UInt160 user)
        {
            return AccountStorage.GetAll(user);
        }

        private static void IncreaseUserBalance(UInt160 user, UInt160 token, BigInteger amount)
        {
            var currentBalance = TokenBalanceStorage.Increase(token, amount);
            var brokerBalance = NEP17Helper.SafeBalanceOf(token, Runtime.ExecutingScriptHash);
            ExecutionEngine.Assert(currentBalance <= brokerBalance, "Invalid balance in contract [IncreaseUserBalance]");
            BigInteger newBalance = AccountStorage.Increase(user, token, amount);
            AccountUpdated(user, token, newBalance, amount);
        }

        private static void DecreaseUserBalance(UInt160 user, UInt160 token, BigInteger amount)
        {
            var currentBalance = TokenBalanceStorage.Decrease(token, amount);
            ExecutionEngine.Assert(currentBalance >= 0, $"Invalid balance in contract [DecreaseUserBalance] {currentBalance}");
            AccountStorage.Decrease(user, token, amount);
            BigInteger newBalance = AccountStorage.Get(user, token);
            AccountUpdated(user, token, newBalance, -amount);
        }
    }
}
