using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace NeoBurger
{
    [ManifestExtra("Author", "NEOBURGER")]
    [ManifestExtra("Email", "developer@neo.org")]
    [ManifestExtra("Description", "Agent Contract")]
    [ContractPermission("*", "*")]
    public class BurgerAgent : SmartContract
    {
        private static readonly UInt160 INITIAL_OWNER = "6288e8a5e3d92f6aa645e6f749b4cb6cc024b211";

        public static void Transfer(UInt160 to, BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(INITIAL_OWNER));
            ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, to, amount));
        }

        public static void Sync()
        {
            ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, Runtime.ExecutingScriptHash, 0));
        }

        public static void Claim()
        {
            ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, INITIAL_OWNER, GAS.BalanceOf(Runtime.ExecutingScriptHash), true));
        }

        public static void Vote(ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(INITIAL_OWNER));
            ExecutionEngine.Assert(NEO.Vote(Runtime.ExecutingScriptHash, target));
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness((UInt160)Contract.Call(INITIAL_OWNER, "owner", CallFlags.All)));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
