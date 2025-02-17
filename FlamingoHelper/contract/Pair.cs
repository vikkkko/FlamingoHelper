using Neo.Network.RPC;
using Neo.Wallets;
using Newtonsoft.Json;
using Neo.SmartContract.Native;
using Neo;

namespace FlamingoHelper
{
    public class Pair : BaseContract
    {
        private static Pair _instance;
        private static readonly object _lock = new object();

        public override string fileName => "";
        public override string selfPath => "Flamingo.SwapPair";

        private Pair(RpcClient _rpcClient, KeyPair keyPair) : base(_rpcClient, keyPair)
        {
        }
        
        public static Pair GetInstance(RpcClient _rpcClient, KeyPair keyPair)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Pair(_rpcClient, keyPair);
                    }
                }
            }
            return _instance;
        }

        public override void UpdateHash(string network, string fileName, UInt160 hash)
        {
            string projectDir = Util.GetProjectDirectory();
            var helperPath = Path.Combine(projectDir, $"helper.{network}.json");
            var helper = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(helperPath));

            foreach (var item in helper.deployedContracts.FlamingoSwapPair)
            {
                if (item.name == fileName)
                {
                    item.hash = hash.ToString();
                }
            }
            File.WriteAllText(helperPath, JsonConvert.SerializeObject(helper, Formatting.Indented));
        }
    }           
}