using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Nep17
{
    [ManifestExtra("Author", "")]
    [ManifestExtra("Email", "")]
    [ManifestExtra("Description", "")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "*")]
    public partial class Nep17Token : SmartContract
    {
        private static readonly byte[] TotalSupplyPrefix = new byte[] { 0x01, 0x00 };

        private static readonly byte[] TotalSupplyKey = "totalSupply".ToByteArray();

        private static readonly byte[] BalancePrefix = new byte[] { 0x01, 0x01 };

        private static readonly byte[] OwnerPrefix = new byte[] { 0x03, 0x02 };

        [InitialValue("NaBUWGCLWFZTGK4V9f4pecuXmEijtGXMNX", ContractParameterType.Hash160)]
        private static readonly UInt160 InitialOwner;

        [Safe]
        public static string Name() => "Test Token";

        [Safe]
        public static string Symbol() => "TEST";

        [Safe]
        public static byte Decimals() => 8;
        [DisplayName("Transfer")]
        public static event Action<UInt160, UInt160, BigInteger> OnTransfer;

        [Safe]
        public static BigInteger TotalSupply() => TotalSupplyStorage.Get();

        [Safe]
        private static bool IsOwner() => Runtime.CheckWitness(GetOwner());

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

        public static bool Mint(UInt160 minter, UInt160 receiver, BigInteger amount)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, minter, receiver), "approve: invalid minter or receiver, usr");
            ExecutionEngine.Assert(amount >= 0, "mint:invalid amount");
            ExecutionEngine.Assert(IsOwner(), "mint: CheckWitness failed, owner");
            ExecutionEngine.Assert(Runtime.CheckWitness(minter) || minter.Equals(Runtime.CallingScriptHash), "mint: CheckWitness failed, author");

            TransferInternal(UInt160.Zero, receiver, amount);
            return true;
        }

        [Safe]
        public static UInt160 GetOwner()
        {
            return OwnerStorage.Get();
        }

        public static bool SetOwner(UInt160 owner)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), "SetOwner: CheckWitness failed, owner");
            ExecutionEngine.Assert(CheckAddrValid(true, owner), "SetOwner: invalid owner");
            OwnerStorage.Put(owner);
            return true;
        }

        public static class TotalSupplyStorage
        {
            internal static void Put(BigInteger amount)
            {
                StorageMap balanceMap = new(Storage.CurrentContext, TotalSupplyPrefix);
                balanceMap.Put(TotalSupplyKey, amount);
            }

            internal static BigInteger Get()
            {
                StorageMap balanceMap = new(Storage.CurrentReadOnlyContext, TotalSupplyPrefix);
                return (BigInteger)balanceMap.Get(TotalSupplyKey);
            }

            internal static void Increase(BigInteger amount) => Put(Get() + amount);

            internal static void Reduce(BigInteger amount) => Put(Get() - amount);
        }

        public static class BalanceStorage
        {
            internal static void Put(UInt160 usr, BigInteger amount)
            {
                StorageMap balanceMap = new(Storage.CurrentContext, BalancePrefix);
                balanceMap.Put(usr, amount);
            }

            internal static BigInteger Get(UInt160 usr)
            {
                StorageMap balanceMap = new(Storage.CurrentReadOnlyContext, BalancePrefix);
                return (BigInteger)balanceMap.Get(usr);
            }

            internal static void Delete(UInt160 usr)
            {
                StorageMap balanceMap = new(Storage.CurrentContext, BalancePrefix);
                balanceMap.Delete(usr);
            }

            internal static void Increase(UInt160 usr, BigInteger amount) => Put(usr, Get(usr) + amount);

            internal static bool Reduce(UInt160 usr, BigInteger amount)
            {
                BigInteger balance = Get(usr);
                if (balance < amount)
                {
                    return false;
                }
                else if (balance == amount)
                {
                    Delete(usr);
                }
                else
                {
                    Put(usr, balance - amount);
                }
                return true;
            }
        }

        public static class OwnerStorage
        {
            internal static void Put(UInt160 usr)
            {
                StorageMap map = new(Storage.CurrentContext, OwnerPrefix);
                map.Put("owner", usr);
            }

            internal static UInt160 Get()
            {
                StorageMap map = new(Storage.CurrentReadOnlyContext, OwnerPrefix);
                byte[] v = (byte[])map.Get("owner");
                if (v is null)
                {
                    return InitialOwner;
                }
                else if (v.Length != 20)
                {
                    return InitialOwner;
                }
                else
                {
                    return (UInt160)v;
                }
            }
        }

        static bool CheckAddrValid(bool checkZero, params UInt160[] addrs)
        {
            foreach (UInt160 addr in addrs)
            {
                if (!addr.IsValid || (checkZero && addr.IsZero)) return false;
            }
            return true;
        }
    }
}
