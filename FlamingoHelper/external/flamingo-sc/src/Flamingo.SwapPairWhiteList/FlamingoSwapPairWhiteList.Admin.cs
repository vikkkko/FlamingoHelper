using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.SwapPairWhiteList
{
    public partial class FlamingoSwapPairWhiteList
    {
        static readonly UInt160 superAdmin = "NdDvLrbtqeCVQkaLstAwh3md8SYYwqWRaE";

        const string AdminKey = nameof(superAdmin);

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
            return admin?.Length == 20 ? (UInt160)admin : superAdmin;
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
            StoragePut(AdminKey, admin);
            return true;
        }

        /// <summary>
        /// 升级
        /// </summary>
        /// <param name="nefFile"></param>
        /// <param name="manifest"></param>
        /// <param name="data"></param>
        public static void Update(ByteString nefFile, string manifest, object data)
        {
            Assert(Verify(), "No authorization.");
            ContractManagement.Update(nefFile, manifest, data);
        }
    }
}
