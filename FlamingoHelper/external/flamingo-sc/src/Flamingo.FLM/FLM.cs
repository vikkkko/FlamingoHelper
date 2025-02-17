using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.FLM
{
    [ManifestExtra("Author", "")]
    [ManifestExtra("Email", "")]
    [ManifestExtra("Description", "")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "*")]
    public partial class FLM : SmartContract
    {
        private static readonly UInt160 InitialOwner = "NaBUWGCLWFZTGK4V9f4pecuXmEijtGXMNX";

        [InitialValue("00000040eaed7446d09c2c9f0c", ContractParameterType.ByteArray)]
        private static readonly BigInteger ConvertDecimal;

        public static string GetVersion()
        {
            return "1.0.0-RC6";
        }

        [Safe]
        public static string Name() => "Flamingo (RC6)";

        [Safe]
        public static string Symbol() => "FLM";

        [Safe]
        public static byte Decimals() => 8;

        public static bool Burn(UInt160 user, BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(user), "Permission Invaid");

            return TransferInternal(user, UInt160.Zero, amount);
        }
    }
}
