using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.FLM
{
    partial class FLM
    {
        [DisplayName("Transfer")]
        public static event Action<UInt160, UInt160, BigInteger> OnTransfer;

        [Safe]
        public static BigInteger TotalSupply() => TotalSupplyStorage.Get();

        [Safe]
        public static BigInteger BalanceOf(UInt160 usr)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, usr), "BalanceOf: invalid usr, usr");
            return BalanceStorage.Get(usr);
        }

        public static bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, from, to), "transfer: invalid from or to, owner");
            ExecutionEngine.Assert(Runtime.CheckWitness(from) || from.Equals(Runtime.CallingScriptHash), "transfer: CheckWitness failed, from");
            return TransferInternal(from, to, amount, data);
        }

        private static bool TransferInternal(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            ExecutionEngine.Assert(amount >= 0, "transferInternal: invalid amount");

            bool result = true;
            if (from != UInt160.Zero && amount != 0)
            {
                result = BalanceStorage.Reduce(from, amount);
                ExecutionEngine.Assert(result, "transferInternal:invalid balance");
            }
            else if (from == UInt160.Zero)
            {
                TotalSupplyStorage.Increase(amount);
            }
            if (to != UInt160.Zero && amount != 0)
            {
                BalanceStorage.Increase(to, amount);
            }
            else if (to == UInt160.Zero)
            {
                TotalSupplyStorage.Reduce(amount);
            }

            // Validate payable
            if (ContractManagement.GetContract(to) != null)
                Contract.Call(to, "onNEP17Payment", CallFlags.All, new object[] { from, amount, data });

            if (result)
            {
                OnTransfer(from, to, amount);
            }
            return result;
        }
    }
}
