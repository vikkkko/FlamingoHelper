using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Staking
{
    partial class FlamingoStaking
    {
        private static bool MintFLM(UInt160 receiver, BigInteger amount, UInt160 callingScript)
        {
            object[] @params = new object[]
            {
                callingScript,
                receiver,
                amount
            };
            UInt160 flmAddress = FLMAddressStorage.Get();
            return (bool)Contract.Call(flmAddress, "mint", CallFlags.All, @params);
        }

        public static bool SetFLMAddress(UInt160 flm, UInt160 author)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(author), "SetFLMAddress: CheckWitness failed, author");
            ExecutionEngine.Assert(IsAuthor(author), "SetFLMAddress: not author");
            ExecutionEngine.Assert(flm.IsValid, "SetFLMAddress: address invalid");
            FLMAddressStorage.Put(flm);
            return true;
        }

        [Safe]
        public static UInt160 GetFLMAddress()
        {
            return FLMAddressStorage.Get();
        }
    }
}
