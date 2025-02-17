using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Staking
{
    partial class FlamingoStaking
    {
        [Safe]
        public static bool IsInWhiteList(UInt160 asset)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, asset), "IsInWhiteList: invald params");
            return AssetStorage.Get(asset);
        }

        [Safe]
        public static BigInteger GetAssetCount()
        {
            return AssetStorage.Count();
        }

        [Safe]
        public static UInt160[] GetAllAsset()
        {
            BigInteger count = AssetStorage.Count();
            return AssetStorage.Find(count);
        }

        public static bool AddAsset(UInt160 asset, UInt160 author)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, asset, author), "AddAsset: invald params");
            ExecutionEngine.Assert(Runtime.CheckWitness(author), $"AddAsset: CheckWitness failed, author: {author.ToAddress()}");
            ExecutionEngine.Assert(IsAuthor(author), $"AddAsset: not author: {author.ToAddress()}");
            AssetStorage.Put(asset);
            return true;
        }

        public static bool RemoveAsset(UInt160 asset, UInt160 author)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(author), $"RemoveAsset: CheckWitness failed, author: {author.ToAddress()}");
            ExecutionEngine.Assert(IsAuthor(author), $"RemoveAsset: not author: {author.ToAddress()}");
            ExecutionEngine.Assert(IsInWhiteList(asset), $"RemoveAsset: not whitelist asset: {asset.ToAddress()}");
            AssetStorage.Delete(asset);
            return true;
        }
    }
}
