using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Flamingo.FToken
{
    [ManifestExtra("Author", "Neo")]
    [ManifestExtra("Email", "dev@neo.org")]
    [ManifestExtra("Description", "")]
    // [DisplayName("Token Name")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "onNEP17Payment")]
    public partial class FToken : Nep17Token
    {
        private static readonly UInt160 owner = "NhGobEnuWX5rVdpnuZZAZExPoRs5J6D2Sb";
        // Prefix_TotalSupply = 0x00; Prefix_Balance = 0x01;
        private const byte Prefix_Contract = 0x02;
        public static readonly StorageMap ContractMap = new(Storage.CurrentContext, Prefix_Contract);
        private static readonly byte[] ownerKey = "owner".ToByteArray();
        private static bool IsOwner() => Runtime.CheckWitness(GetOwner());

        private const byte Prefix_Minter = 0x03;
        public static readonly StorageMap MinterMap = new(Storage.CurrentContext, Prefix_Minter);
        private static readonly byte[] minterKey = "minter".ToByteArray();
        private static bool IsMinter() => Runtime.CheckWitness(GetMinter());

        public static event Action<UInt160, UInt160> OnSetOwner;
        public static event Action<UInt160> OnSetMinter;
        public static byte Factor => 8;

        public override byte Decimals
        {
            [Safe]
            get => Factor;
        }

        public override string Symbol
        {
            [Safe]
            get => "Test Token";
        }

        private static BigInteger ReentrantFlag(UInt256 hash)
        {
            return (BigInteger)Storage.Get(Storage.CurrentContext, hash);
        }

        public static void _deploy(object data, bool update)
        {
            if (update) return;
            ContractMap.Put(ownerKey, owner);
            Nep17Token.Mint(owner, 100000000 * BigInteger.Pow(10, Factor));
        }

        public static void SetOwner(UInt160 newOwner)
        {
            ExecutionEngine.Assert(IsOwner(), "No Authorization!");
            var oldOwner = (UInt160)ContractMap.Get(ownerKey);
            ContractMap.Put(ownerKey, newOwner);
            OnSetOwner(oldOwner, newOwner);
        }

        public static UInt160 GetOwner()
        {
            return (UInt160)ContractMap.Get(ownerKey);
        }

        public static void SetMinter(UInt160 minter)
        {
            ExecutionEngine.Assert(IsOwner(), "No Authorization!");
            MinterMap.Put(minterKey, minter);
            OnSetMinter(minter);
        }

        public static UInt160 GetMinter()
        {
            return (UInt160)MinterMap.Get(minterKey);
        }

        [NoReentrant]
        public static new void Mint(UInt160 account, BigInteger amount)
        {
            ExecutionEngine.Assert(ReentrantFlag(Runtime.Transaction.Hash) != 1, "Transaction has been executed");
            ExecutionEngine.Assert(IsMinter() || IsOwner(), "No Authorization!");
            Storage.Put(Storage.CurrentContext, Runtime.Transaction.Hash, 1);
            Nep17Token.Mint(account, amount);
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(IsOwner(), "No Authorization!");
            ContractManagement.Update(nefFile, manifest);
        }

        public static void Destroy()
        {
            ExecutionEngine.Assert(IsOwner(), "No Authorization!");
            ContractManagement.Destroy();
        }
    }
}
