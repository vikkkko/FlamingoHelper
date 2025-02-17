using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Staking
{
    partial class FlamingoStaking
    {
        public static bool UpgradeStart()
        {
            if (!Runtime.CheckWitness(GetOwner())) return false;
            var t = UpgradeTimeLockStorage.Get();
            if (t != 0) return false;
            UpgradeTimeLockStorage.Put(GetCurrentTimestamp() + 86400);
            return true;
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), $"SetOwner: CheckWitness failed, owner: {GetOwner().ToAddress()}");
            ContractManagement.Update(nefFile, manifest, null);
            UpgradeEnd();
        }

        private static void UpgradeEnd()
        {
            var t = UpgradeTimeLockStorage.Get();
            ExecutionEngine.Assert(GetCurrentTimestamp() > t && t != 0, $"UpgradeEnd: timelock wrong, t: {t}");
            UpgradeTimeLockStorage.Put(0);
        }
    }
}
