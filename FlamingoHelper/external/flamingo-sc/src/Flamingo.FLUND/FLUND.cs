using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Flamingo.FLUND
{
    [ManifestExtra("Author", "")]
    [ManifestExtra("Email", "")]
    [ManifestExtra("Description", "")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "*")]
    public partial class FLUND : SmartContract
    {
        static readonly UInt160 Owner = "NVGUQ1qyL4SdSm7sVmGVkXetjEsvw2L3NT";
        static readonly UInt160 FLMHash = "5b53998b399d10cd25727269e865acc785ef5c1a";
        static readonly UInt160 Factory = "52bf47559436d3572a9b1cb83c056dc39cb42d0d";

        [InitialValue("00000040eaed7446d09c2c9f0c", ContractParameterType.ByteArray)]
        private static readonly BigInteger ConvertDecimal;

        private static bool IsOwner() => Runtime.CheckWitness(GetOwner());

        public static bool Verify() => IsOwner();

        public static UInt160 GetOwner() => OwnerStorage.Get();

        #region Helper

        #region BalanceHelper

        public static BigInteger GetContractAssetBalance(UInt160 AssetHash)
        {
            return (BigInteger)Contract.Call(AssetHash, "balanceOf", CallFlags.All, Runtime.ExecutingScriptHash);
        }
        #endregion

        #region CheckSignHelper
        static bool CheckAddrValid(bool checkZero, params UInt160[] addrs)
        {
            foreach (UInt160 addr in addrs)
            {
                if (!addr.IsValid || (checkZero && addr.IsZero)) return false;
            }
            return true;
        }
        #endregion

        #region StorageHelper
        private static ByteString StorageGet(ByteString key)
        {
            return Storage.Get(Storage.CurrentContext, key);
        }

        private static ByteString StorageGet(byte[] key)
        {
            return Storage.Get(Storage.CurrentContext, key);
        }

        private static void StoragePut(ByteString key, ByteString value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }

        private static void StoragePut(ByteString key, byte[] value)
        {
            Storage.Put(Storage.CurrentContext, key, (ByteString)value);
        }

        private static void StoragePut(ByteString key, BigInteger value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }

        private static void StoragePut(byte[] key, byte[] value)
        {
            Storage.Put(Storage.CurrentContext, key, (ByteString)value);
        }

        private static void StoragePut(byte[] key, BigInteger value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }

        private static void StoragePut(byte[] key, ByteString value)
        {
            Storage.Put(Storage.CurrentContext, key, value);
        }
        #endregion

        #endregion

        #region NEP-17

        [Safe]
        public static string Name() => "FLUND";

        [Safe]
        public static string Symbol() => "FLUND";

        [Safe]
        public static byte Decimals() => 8;

        [DisplayName("Transfer")]
        public static event Action<UInt160, UInt160, BigInteger> OnTransfer;

        [Safe]
        public static BigInteger TotalSupply() => TotalSupplyStorage.Get();
        [Safe]
        public static BigInteger BalanceOf(UInt160 usr)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, usr), $"BalanceOf: invalid usr, usr {usr.ToAddress()}");
            return BalanceStorage.Get(usr);
        }

        public static bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            ExecutionEngine.Assert(amount >= 0, "Invalid amount");
            ExecutionEngine.Assert(CheckAddrValid(true, from, to), $"transfer: invalid from or to, owner {from.ToAddress()} and to {to.ToAddress()}");
            ExecutionEngine.Assert(Runtime.CheckWitness(from),$"transfer: CheckWitness failed, from {from.ToAddress()}");
            return TransferInternal(from, to, amount, data);
        }

        private static bool TransferInternal(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            ExecutionEngine.Assert(amount >= 0, $"transferInternal: invalid amount {amount}");

            bool result = true;
            if (from != UInt160.Zero && amount != 0)
            {
                result = BalanceStorage.Reduce(from, amount);
                ExecutionEngine.Assert(result, $"transferInternal:invalid balance {amount}");
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

            if (result)
            {
                if (from == UInt160.Zero)
                    OnTransfer(null, to, amount);
                else if (to == UInt160.Zero)
                    OnTransfer(from, null, amount);
                else
                    OnTransfer(from, to, amount);
            }

            // Validate payable
            if (ContractManagement.GetContract(to) != null)
                Contract.Call(to, "onNEP17Payment", CallFlags.All, from, amount, data);

            return result;
        }

        private static bool Mint(UInt160 receiver, BigInteger amount)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, receiver), $"approve: invalid minter or receiver, receiver {receiver.ToAddress()}");
            ExecutionEngine.Assert(amount >= 0, $"mint:invalid amount {amount}");
            TransferInternal(UInt160.Zero, receiver, amount);
            return true;
        }

        private static bool Burn(UInt160 User, BigInteger amount)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, User), $"approve: invalid minter or receiver, User {User.ToAddress()}");
            ExecutionEngine.Assert(Runtime.CheckWitness(User), "check witeness fail");
            TransferInternal(User, UInt160.Zero, amount);
            return true;
        }
        #endregion

        #region storage

        #region StorageKey
        private static readonly byte[] TotalSupplyPrefix = new byte[] { 0x01, 0x00 };

        private static readonly byte[] TotalSupplyKey = "totalSupply".ToByteArray();

        private static readonly byte[] BalancePrefix = "balance".ToByteArray();

        private static readonly byte[] AuthorPrefix = new byte[] { 0x01, 0x03 };

        private static readonly byte[] OwnerPrefix = "owner".ToByteArray();

        private static readonly byte[] FeeAddressPrefix = "FeeAddress".ToByteArray();

        private static readonly byte[] FLMProfitSpeedPrefix = "FLMProfitSpeed".ToByteArray();

        private static readonly byte[] UpdateTimePrefix = "UpdateTime".ToByteArray();

        private static readonly byte[] AssetHashPrefix = "AssetHash".ToByteArray();

        private static readonly byte[] IsStartPrefix = "IsStart".ToByteArray();
        #endregion

        #region TokenStorage
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
        #endregion

        public static class AuthorStorage
        {
            internal static void Put(UInt160 usr)
            {
                StorageMap authorMap = new(Storage.CurrentContext, AuthorPrefix);
                authorMap.Put(usr, 1);
            }

            internal static void Delete(UInt160 usr)
            {
                StorageMap authorMap = new(Storage.CurrentContext, AuthorPrefix);
                authorMap.Delete(usr);
            }

            internal static bool Get(UInt160 usr)
            {
                StorageMap authorMap = new(Storage.CurrentReadOnlyContext, AuthorPrefix);
                return (BigInteger)authorMap.Get(usr) == 1;
            }

            internal static BigInteger Count()
            {
                StorageMap authorMap = new(Storage.CurrentReadOnlyContext, AuthorPrefix);
                var iterator = authorMap.Find();
                BigInteger count = 0;
                while (iterator.Next())
                {
                    count++;
                }
                return count;
            }

            internal static UInt160[] Find(BigInteger count)
            {
                StorageMap authorMap = new(Storage.CurrentReadOnlyContext, AuthorPrefix);
                var iterator = authorMap.Find(FindOptions.RemovePrefix | FindOptions.KeysOnly);
                UInt160[] addrs = new UInt160[(uint)count];
                uint i = 0;
                while (iterator.Next())
                {
                    addrs[i] = (UInt160)iterator.Value;
                    i++;
                }
                return addrs;
            }

            internal static Iterator Find()
            {
                StorageMap authorMap = new(Storage.CurrentReadOnlyContext, AuthorPrefix);
                return authorMap.Find();
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
                    return Owner;
                }
                else if (v.Length != 20)
                {
                    return Owner;
                }
                else
                {
                    return (UInt160)v;
                }
            }
        }

        public static class AssetStorage
        {
            public static bool IsInAssetList(UInt160 asset)
            {
                ExecutionEngine.Assert(CheckAddrValid(true, asset), "IsInWhiteList: invald params");
                return AssetStorage.Get(asset);
            }

            internal static void Put(UInt160 asset)
            {
                StorageMap map = new(Storage.CurrentContext, AssetHashPrefix);
                map.Put(asset, 1);
            }

            internal static void Delete(UInt160 asset)
            {
                StorageMap map = new(Storage.CurrentContext, AssetHashPrefix);
                map.Delete(asset);
            }

            internal static bool Get(UInt160 asset)
            {
                StorageMap map = new(Storage.CurrentReadOnlyContext, AssetHashPrefix);
                return (BigInteger)map.Get(asset) == 1;
            }

            internal static BigInteger Count()
            {
                StorageMap map = new(Storage.CurrentReadOnlyContext, AssetHashPrefix);
                var iterator = map.Find();
                BigInteger count = 0;
                while (iterator.Next())
                {
                    count++;
                }
                return count;
            }

            internal static UInt160[] Find(BigInteger count)
            {
                StorageMap map = new(Storage.CurrentReadOnlyContext, AssetHashPrefix);
                var iterator = map.Find(FindOptions.RemovePrefix | FindOptions.KeysOnly);
                UInt160[] addrs = new UInt160[(uint)count];
                uint i = 0;
                while (iterator.Next())
                {
                    addrs[i] = (UInt160)iterator.Value;
                    i++;
                }
                return addrs;
            }
        }
        #endregion

        #region MainMethod

        #region ExternalMethod
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object? data = null)
        {
            ExecutionEngine.Assert(amount >= 0, "Invalid amount");
            if (Runtime.CallingScriptHash == FLMHash && data != null)
            {
                if (IsStart())
                {
                    //执行领取flm的操作
                    ClaimCurrentFLMProfit();

                    //执行置换资产的操作
                    SwapAllAssetForFLM();

                    //mint Flund
                    BigInteger FLMAmount = GetContractAssetBalance(FLMHash) - amount;
                    Assert(FLMAmount > 0, "Amount Error");
                    BigInteger newFlund = TotalSupply() * amount / FLMAmount;
                    Mint(from, newFlund);
                }
                else
                {
                    if (((Transaction)Runtime.ScriptContainer).Sender != Owner)
                    {
                        ExecutionEngine.Abort();
                        return;
                    }
                }
                return;
            }
            return;
        }

        public static bool Withdraw(BigInteger amount, UInt160 user)
        {
            ExecutionEngine.Assert(amount >= 0, "Invalid amount");
            ExecutionEngine.Assert(user.IsValid, "Invalid user");
            ClaimCurrentFLMProfit();
            SwapAllAssetForFLM();

            BigInteger flmAmount = amount * GetContractAssetBalance(FLMHash) / TotalSupply();

            ExecutionEngine.Assert(Burn(user, amount), "burn fail");

            SafeTransfer(FLMHash, Runtime.ExecutingScriptHash, user, flmAmount);
            return true;
        }

        public static BigInteger GetRewardInfo(BigInteger amount)
        {
            ExecutionEngine.Assert(amount >= 0, "Invalid amount");
            BigInteger FLMProfit = CheckCurrentFLMProfit();
            BigInteger FLMFromSwap = CheckAllAssetsForFLM();
            BigInteger flmAmount = amount * (GetContractAssetBalance(FLMHash) + FLMProfit + FLMFromSwap) / TotalSupply();
            return flmAmount;
        }

        public static bool IsStart()
        {
            ByteString rawResult = StorageGet(IsStartPrefix);
            return rawResult is null ? false : true;
        }
        #endregion

        #region InternalMethod

        #region FLM Part
        private static bool ClaimFLM(BigInteger amount)
        {
            object[] @params = new object[]
            {
                Runtime.ExecutingScriptHash,
                Runtime.ExecutingScriptHash,
                amount
            };
            return (bool)Contract.Call(FLMHash, "mint", CallFlags.All, @params);
        }

        private static BigInteger ClaimCurrentFLMProfit()
        {
            BigInteger flmAmount = CheckCurrentFLMProfit();
            if (flmAmount > 0)
            {
                ExecutionEngine.Assert(ClaimFLM(flmAmount * ConvertDecimal), "Claim FLM fail");
                SetUpdateTime();
                return flmAmount;
            }

            return 0;
        }

        public static BigInteger CheckCurrentFLMProfit()
        {
            BigInteger lastUpdateTime = GetUpdateTime();
            BigInteger ProfitEpoch = GetRunTime() - lastUpdateTime;
            BigInteger ProfitSpeed = GetFLMProfitSpeed();

            if (ProfitEpoch != 0 && ProfitSpeed != 0)
            {
                BigInteger amount = ProfitSpeed * ProfitEpoch;
                return amount;
            }
            return 0;
        }

        private static void SetUpdateTime()
        {
            StoragePut(UpdateTimePrefix, GetRunTime());
        }

        public static BigInteger GetRunTime()
        {
            return Runtime.Time / 1000;
        }

        public static BigInteger GetUpdateTime()
        {
            ByteString rawUpdateTime = StorageGet(UpdateTimePrefix);
            return rawUpdateTime is null ? 0 : (BigInteger)rawUpdateTime;
        }
        #endregion

        #region Asset Swap Part

        //UInt160 sender, BigInteger amountIn, BigInteger amountOutMin, UInt160[] paths, BigInteger deadLine
        private static void SwapAssetForFLM(UInt160 AssetHash)
        {
            BigInteger balance = GetContractAssetBalance(AssetHash);
            BigInteger valueForFLM = CheckAssetForFLM(AssetHash, balance);

            if (balance > 0 && valueForFLM >= 100_00000000)
            {
                ExecutionEngine.Assert(SwapTokenInForTokenOut(balance, 0, new UInt160[] { AssetHash, FLMHash }, Runtime.Time + 30));
            }
        }

        private static BigInteger CheckAssetForFLM(UInt160 AssetHash, BigInteger balance)
        {
            if (balance > 0)
            {
                BigInteger[] FLMAmount = GetAmountsOut(balance, new UInt160[] { AssetHash, FLMHash });
                return FLMAmount[1];
            }
            return 0;
        }

        private static void SwapAllAssetForFLM()
        {
            UInt160[] AssetLists = GetAllAsset();
            foreach (UInt160 AssetHash in AssetLists)
            {
                SwapAssetForFLM(AssetHash);
            }
        }

        private static BigInteger CheckAllAssetsForFLM()
        {
            UInt160[] AssetLists = GetAllAsset();
            BigInteger FLMAmountTotal = 0;
            foreach (UInt160 AssetHash in AssetLists)
            {
                BigInteger balance = GetContractAssetBalance(AssetHash);
                FLMAmountTotal += CheckAssetForFLM(AssetHash, balance);
            }
            return FLMAmountTotal;
        }
        #endregion

        #endregion

        #endregion

        #region AdminMethod

        #region ParameterMethod

        public static void Start()
        {
            ExecutionEngine.Assert(IsOwner(), "verify fail");
            ExecutionEngine.Assert(!IsStart(), "Started");
            SafeTransfer(FLMHash, Owner, Runtime.ExecutingScriptHash, 10000000000);
            StoragePut(IsStartPrefix, 1);
            Mint(Runtime.ExecutingScriptHash, 10000000000);
        }

        public static bool SetFLMProfit(BigInteger amount)
        {
            if (IsOwner())
            {
                StoragePut(FLMProfitSpeedPrefix, amount);
                SetUpdateTime();
                return true;
            }
            return false;
        }

        public static BigInteger GetFLMProfitSpeed()
        {
            ByteString rawFLMProfit = StorageGet(FLMProfitSpeedPrefix);
            return rawFLMProfit is null ? BigInteger.Zero : (BigInteger)rawFLMProfit;
        }

        public static bool AddAssetHash(UInt160 assetHash)
        {
            ExecutionEngine.Assert(IsOwner(), "check owner witness fail");
            ExecutionEngine.Assert(assetHash.IsValid, "Invalid contractHash");
            ExecutionEngine.Assert(!AssetStorage.IsInAssetList(assetHash), "asset in list");
            AssetStorage.Put(assetHash);
            return true;
        }

        public static void RemoveAssetHash(UInt160 assetHash)
        {
            ExecutionEngine.Assert(IsOwner(), "check owner witness fail");
            ExecutionEngine.Assert(assetHash.IsValid, "Invalid contractHash");
            ExecutionEngine.Assert(AssetStorage.IsInAssetList(assetHash), "asset not in list");
            AssetStorage.Delete(assetHash);
        }

        public static UInt160[] GetAllAsset()
        {
            BigInteger count = AssetStorage.Count();
            return AssetStorage.Find(count);
        }
        #endregion

        #region ContractMethod

        public static bool TransferOwnership(UInt160 newOwner)
        {
            Assert(newOwner.IsValid, "The new owner address is invalid.");
            Assert(IsOwner(), "No authorization.");

            OwnerStorage.Put(newOwner);
            return true;
        }

        public static bool SetFeeAddress(UInt160 feeAddress)
        {
            if (IsOwner())
            {
                StoragePut(FeeAddressPrefix, feeAddress);
                return true;
            }
            return false;
        }

        public static UInt160 GetFeeAddress()
        {
            ByteString rawFeeAddress = StorageGet(FeeAddressPrefix);
            return rawFeeAddress is null ? UInt160.Zero : (UInt160)rawFeeAddress;
        }

        public static void _deploy(object data, bool update)
        {
            if (update) return;
            OwnerStorage.Put(Owner);
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            if (!IsOwner()) throw new Exception("No authorization.");
            ContractManagement.Update(nefFile, manifest, null);
        }

        public static void Destroy()
        {
            if (!IsOwner()) throw new Exception("No authorization.");
            ContractManagement.Destroy();
        }
        #endregion

        #endregion
    }
}
