using Neo.Network.RPC;
using Neo.Wallets;
using Newtonsoft.Json;
using Neo.SmartContract.Native;
using Neo;
using Neo.IO;
using Neo.SmartContract;
using System.Text;
using Neo.SmartContract.Manifest;
using Neo.VM;
using Neo.Network.P2P.Payloads;
using System.Numerics;

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

        public byte[] SetWhiteListContract(UInt160 whiteListContract, bool send = true, byte[] _script = null)
        {
            var script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "setWhiteListContract", whiteListContract);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }
    }           
}