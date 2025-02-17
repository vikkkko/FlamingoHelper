using System.Numerics;
using Flamingo.Broker.Utils;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Broker
{
    public partial class FlamingoBroker
    {
        /// <summary>
        /// Executed on the verification trigger
        /// </summary>
        [Safe]
        public static bool Verify()
        {
            return HasOwnerWitness();
        }

        /// <summary>
        /// Called when NEP17 tokens are transferred to the contract to perform a deposit.
        /// </summary>
        /// <param name="from">The address from which the transfer is happening.</param>
        /// <param name="amount">The amount transferred.</param>
        /// <param name="data">The array of data to be passed as arguments [action: BigInteger] always first.</param>
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object[] data)
        {

        }

        /// <summary>
        /// Executed on swapping with the AMM because it's needed from its interface
        /// </summary>
        public static bool ApprovedTransfer(UInt160 asset, UInt160 to, BigInteger value, object data)
        {
            ExecutionEngine.Assert(Runtime.CallingScriptHash == GetAMMRouter(), "Only the AMM router can call this");
            NEP17Helper.SafeTransfer(asset, Runtime.ExecutingScriptHash, to, value, data);
            return true;
        }
    }
}
