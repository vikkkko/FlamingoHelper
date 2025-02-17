using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.SwapRouter
{
    public partial class FlamingoSwapRouterContract
    {
        #region Admin

        static readonly UInt160 superAdmin = "NdDvLrbtqeCVQkaLstAwh3md8SYYwqWRaE";

        static readonly UInt160 Factory = "0xca2d20610d7982ebe0bed124ee7e9b2d580a6efc";

        static readonly UInt160 Broker = "0x0000000000000000000000000000000000000000";

        const string AdminKey = nameof(superAdmin);

        const string FactoryKey = nameof(Factory);

        const string BrokerKey = nameof(Broker);

        // When this contract address is included in the transaction signature,
        // this method will be triggered as a VerificationTrigger to verify that the signature is correct.
        // For example, this method needs to be called when withdrawing token from the contract.
        public static bool Verify() => Runtime.CheckWitness(GetAdmin());

        /// <summary>
        /// 获取合约管理员
        /// </summary>
        /// <returns></returns>
        public static UInt160 GetAdmin()
        {
            var admin = StorageGet(AdminKey);
            return admin?.Length == 20 ? (UInt160) admin : superAdmin;
        }

        /// <summary>
        /// 设置合约管理员
        /// </summary>
        /// <param name="admin"></param>
        /// <returns></returns>
        public static bool SetAdmin(UInt160 admin)
        {
            Assert(admin.IsValid && !admin.IsZero, "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            StoragePut(AdminKey, (ByteString) admin);
            return true;
        }

        public static UInt160 GetFactory()
        {
            var factory = StorageGet(FactoryKey);
            return factory?.Length == 20 ? (UInt160) factory : Factory;
        }

        public static bool SetFactory(UInt160 factory)
        {
            Assert(factory.IsValid && !factory.IsZero, "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            StoragePut(FactoryKey, (ByteString) factory);
            return true;
        }

        public static bool SetBrokerContract(UInt160 broker)
        {
            Assert(broker.IsValid && !broker.IsZero, "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            StoragePut(BrokerKey, (ByteString) broker);
            return true;
        }

        public static UInt160 GetBrokerContract()
        {
            var broker = StorageGet(BrokerKey);
            return broker?.Length == 20 ? (UInt160) broker : Broker;
        }

        public static void CheckIsBroker()
        {
            ExecutionEngine.Assert(Runtime.CallingScriptHash == GetBrokerContract(), "Only the Broker contract can call this method.");
        }

        #endregion

        #region Upgrade

        /// <summary>
        /// 升级
        /// </summary>
        /// <param name="nefFile"></param>
        /// <param name="manifest"></param>
        /// <param name="data"></param>
        public static void Update(ByteString nefFile, string manifest)
        {
            Assert(Verify(), "No authorization.");
            ContractManagement.Update(nefFile, manifest, null);
        }

        #endregion
    }
}
