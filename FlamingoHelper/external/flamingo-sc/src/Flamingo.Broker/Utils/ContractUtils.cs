using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Broker.Utils
{
    public static class ContractUtils
    {
        public static bool IsValidAddress(params UInt160[] addresses)
        {
            foreach (UInt160 address in addresses)
            {
                if (!address.IsValid) return false;
            }
            return true;
        }

        public static bool IsContract(UInt160 hash)
        {
            Contract contract = ContractManagement.GetContract(hash);
            return contract != null;
        }
    }
}
