using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Contract = Neo.SmartContract.Framework.Services.Contract;

namespace Flamingo.Lend
{
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "Flamingo Stablecoin Token")]
    [DisplayName("FUSD")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "*")]
    public class FToken : SmartContract
    {
        private static readonly string ACTION_MINT = "MINT";

        private static readonly StorageContext ctx = Storage.CurrentContext;
        private static readonly StorageContext rtx = Storage.CurrentReadOnlyContext;

        private static readonly UInt160 INITIAL_OWNER = "14131211100f0e0d0c0b0a090807060504030201";

        private const int INITIAL_SUPPLY = 0;
        private const int DECIMALS = 8;
        private const string SYMBOL = "FUSD";

        // Keys
        private static readonly byte[] OWNER_KEY = new byte[] {0x00};
        private static readonly byte[] SUPPLY_KEY = new byte[] {0x01};
        private static readonly byte[] VAULT_HASH_KEY = new byte[] {0x02};

        private static readonly StorageMap BALANCE_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x10});

        // Events
        [DisplayName("Mint")] public static event MintDelegate OnMint;

        public delegate void MintDelegate(UInt160 account, BigInteger quantity);

        [DisplayName("Burn")] public static event BurnDelegate OnBurn;

        public delegate void BurnDelegate(UInt160 account, BigInteger quantity);

        [DisplayName("Transfer")] public static event TransferDelegate OnTransfer;

        public delegate void TransferDelegate(UInt160 from, UInt160 to, BigInteger amount);

        [DisplayName("Error")] public static event ErrorDelegate OnError;

        public delegate void ErrorDelegate(string message, string error);

        public static void _deploy(object data, bool update)
        {
            if (!update)
            {
                Storage.Put(ctx, SUPPLY_KEY, INITIAL_SUPPLY);
                Storage.Put(ctx, OWNER_KEY, INITIAL_OWNER);

                BALANCE_MAP.Put(INITIAL_OWNER, INITIAL_SUPPLY);
                OnTransfer(null, INITIAL_OWNER, INITIAL_SUPPLY);
            }
        }

        public static void Update(ByteString script, string manifest, object data)
        {
            ValidateOwner("update");
            ContractManagement.Update(script, manifest, data);
        }

        public static void Destroy()
        {
            ValidateOwner("destroy");
            ContractManagement.Destroy();
        }

        public static bool Verify()
        {
            return Runtime.CheckWitness(GetOwner());
        }

        public static void SetOwner(UInt160 owner)
        {
            ValidateOwner("setOwner");
            ValidateHash160(owner, "owner");

            Storage.Put(ctx, OWNER_KEY, owner);
        }

        [Safe]
        public static UInt160 GetOwner()
        {
            var owner = Storage.Get(rtx, OWNER_KEY);
            return owner is null ? INITIAL_OWNER : (UInt160) owner;
        }

        public static void SetVaultScriptHash(UInt160 vaultHash)
        {
            ValidateOwner("setVaultScriptHash");
            ValidateContract(vaultHash, "vaultHash");

            Storage.Put(ctx, VAULT_HASH_KEY, vaultHash);
        }

        [Safe]
        public static UInt160 GetVaultScriptHash()
        {
            return (UInt160) Storage.Get(ctx, VAULT_HASH_KEY);
        }

        [Safe]
        public static string Symbol() => SYMBOL;

        [Safe]
        public static int Decimals() => DECIMALS;

        [Safe]
        public static BigInteger BalanceOf(UInt160 account)
        {
            ValidateHash160(account, "account");
            return GetBalance(account);
        }

        [Safe]
        public static BigInteger TotalSupply()
        {
            return (BigInteger) Storage.Get(ctx, SUPPLY_KEY);
        }

        public static bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            ValidateHash160(from, "from");
            ValidateHash160(to, "to");
            ValidateNonNegativeNumber(amount, "amount");

            BigInteger fromBalance = GetBalance(from);
            if (fromBalance < amount)
                return false;
            if (!Runtime.CheckWitness(from))
                return false;

            if (!from.Equals(to) && amount != 0)
            {
                if (fromBalance == amount)
                    BALANCE_MAP.Delete(from);
                else
                    DeductFromBalance(from, amount);
                AddToBalance(to, amount);
            }

            OnTransfer(from, to, amount);
            PostTransfer(from, to, amount, data);

            return true;
        }

        public static void Mint(UInt160 account, BigInteger mintQuantity)
        {
            ValidateHash160(account, "account");
            ValidateNonNegativeNumber(mintQuantity, "mintQuantity");

            if (!CallByVault())
                FireErrorAndAbort("Not authorized", "mint");

            if (mintQuantity != 0)
            {
                AddToSupply(mintQuantity);
                AddToBalance(account, mintQuantity);

                OnTransfer(null, account, mintQuantity);
                PostTransfer(null, account, mintQuantity, new object[] {ACTION_MINT});
            }

            OnMint(account, mintQuantity);
        }

        public static void Burn(UInt160 account, BigInteger burnQuantity)
        {
            ValidateHash160(account, "account");
            ValidateNonNegativeNumber(burnQuantity, "burnQuantity");

            if (!CallByVault())
                FireErrorAndAbort("Not authorized", "burn");

            BigInteger accountBalance = GetBalance(account);
            if (accountBalance < burnQuantity)
                throw new Exception("The parameter 'burnQuantity' must be smaller than the account balance");

            if (burnQuantity != 0)
            {
                DeductFromSupply(burnQuantity);
                DeductFromBalance(account, burnQuantity);

                OnTransfer(account, null, burnQuantity);
            }

            OnBurn(account, burnQuantity);
        }

        private static void PostTransfer(UInt160 from, UInt160 to, BigInteger quantity, object data = null)
        {
            if (ContractManagement.GetContract(to) != null)
                Contract.Call(to, "onNEP17Payment", CallFlags.All, new object[] {from, quantity, data});
        }

        private static void FireErrorAndAbort(string msg, string method)
        {
            OnError(msg, method);
            ExecutionEngine.Abort();
        }

        private static void ValidateHash160(UInt160 hash, string hashName)
        {
            if (!hash.IsValid || hash.IsZero)
                throw new Exception($"The parameter '{hashName}' must be a 20-byte address");
        }

        private static void ValidateContract(UInt160 hash, string hashName)
        {
            ValidateHash160(hash, hashName);
            if (ContractManagement.GetContract(hash) == null)
                throw new Exception($"The parameter '{hashName}' must be a contract hash");
        }

        private static void ValidateNonNegativeNumber(BigInteger number, string numberName)
        {
            if (number < 0)
                throw new Exception($"The parameter '{numberName}' must be non-negative");
        }

        private static void ValidateOwner(string method)
        {
            if (!Runtime.CheckWitness(GetOwner()))
                FireErrorAndAbort("Not authorized", method);
        }

        private static void AddToSupply(BigInteger value)
        {
            Storage.Put(ctx, SUPPLY_KEY, TotalSupply() + value);
        }

        private static void DeductFromSupply(BigInteger value)
        {
            AddToSupply(-value);
        }

        private static void AddToBalance(UInt160 key, BigInteger value)
        {
            BALANCE_MAP.Put(key, GetBalance(key) + value);
        }

        private static void DeductFromBalance(UInt160 key, BigInteger value)
        {
            AddToBalance(key, -value);
        }

        private static BigInteger GetBalance(UInt160 key)
        {
            return (BigInteger) BALANCE_MAP.Get(key);
        }

        private static bool CallByVault()
        {
            return Runtime.CheckWitness(GetVaultScriptHash());
        }

        // [TESTNET ONLY] public static void MintOwner(UInt160 account, BigInteger mintQuantity)
        // [TESTNET ONLY] {
        // [TESTNET ONLY]     ValidateHash160(account, "account");
        // [TESTNET ONLY]     ValidateNonNegativeNumber(mintQuantity, "mintQuantity");
        // [TESTNET ONLY]
        // [TESTNET ONLY]     if (!Runtime.CheckWitness(GetOwner()))
        // [TESTNET ONLY]         FireErrorAndAbort("Not authorized", "mint");
        // [TESTNET ONLY]
        // [TESTNET ONLY]     if (mintQuantity != 0)
        // [TESTNET ONLY]     {
        // [TESTNET ONLY]         AddToSupply(mintQuantity);
        // [TESTNET ONLY]         AddToBalance(account, mintQuantity);
        // [TESTNET ONLY]
        // [TESTNET ONLY]         OnTransfer(null, account, mintQuantity);
        // [TESTNET ONLY]         PostTransfer(null, account, mintQuantity, new object[] {ACTION_MINT});
        // [TESTNET ONLY]     }
        // [TESTNET ONLY]
        // [TESTNET ONLY]     OnMint(account, mintQuantity);
        // [TESTNET ONLY] }
    }
}
