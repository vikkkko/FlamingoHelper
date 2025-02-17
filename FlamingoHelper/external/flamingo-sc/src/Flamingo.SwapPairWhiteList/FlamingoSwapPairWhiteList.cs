using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.SwapPairWhiteList
{
    [DisplayName("FlamingoSwapPairWhiteList")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "Flamingo's Swap-Pair's WhiteList")]
    [ContractTrust("02738f9efdd954f8436d91ff5f373ae8af14641abc6511de3d1a2ab40665e9a21f")]
    [ContractPermission("*")] //avoid native contract hash change
    public partial class FlamingoSwapPairWhiteList : SmartContract
    {
        public static string GetVersion()
        {
            return "1.0.0-RC6";
        }

        /// <summary>
        /// 白名单存储区前缀，只允许一字节
        /// todo:换成0xff?
        /// </summary>
        private static readonly byte[] WhiteListPrefix = new byte[] { 0x77 };

        /// <summary>
        /// 增加router白名单，加过白名单的router才能完成burn
        /// </summary>
        /// <param name="router">Nep5 tokenA</param>
        /// <returns></returns>
        public static bool AddRouter(UInt160 router)
        {
            Assert(router.IsValid, "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            var key = WhiteListPrefix.Concat((byte[])router);
            StoragePut(key, 1);
            onAddRouter(router);
            return true;
        }

        /// <summary>
        /// 移除router白名单，加过白名单的router才能完成burn
        /// </summary>
        /// <param name="router">Nep5 tokenA</param>
        /// <returns></returns>
        public static bool RemoveRouter(UInt160 router)
        {
            Assert(router.IsValid, "Invalid Address");
            Assert(Runtime.CheckWitness(GetAdmin()), "Forbidden");
            var key = WhiteListPrefix.Concat((byte[])router);
            StorageDelete(key);
            onRemoveRouter(router);
            return true;
        }

        /// <summary>
        /// 检查router白名单
        /// </summary>
        /// <param name="router">Nep17 tokenA</param>
        /// <returns></returns>
        public static bool CheckRouter(UInt160 router)
        {
            Assert(router.IsValid, "Invalid Address");
            var key = WhiteListPrefix.Concat(router);
            var getraw = StorageGet(key);
            if (getraw == null)
                return false;
            var value = new BigInteger((byte[])getraw);
            return value > 0;
        }

        /// <summary>
        /// 查询router白名单
        /// </summary>
        /// <returns></returns>
        public static UInt160[] GetAllRouter()
        {
            var iterator = (Iterator<KeyValue>)StorageFind(WhiteListPrefix);
            var result = new UInt160[0];
            while (iterator.Next())
            {
                var keyValue = iterator.Value;
                if (keyValue.Value > 0)
                {
                    Append(result, keyValue.Key);
                }
            }

            return result;
        }
    }
}
