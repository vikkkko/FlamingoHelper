using Neo;
using Neo.IO;
using Neo.SmartContract;
using Neo.Wallets;
using System.Text;
using Neo.Network.RPC;
using Neo.SmartContract.Manifest;
using Neo.VM;
using Neo.Network.P2P.Payloads;
using System.Numerics;

namespace FlamingoHelper
{
    public class Router : BaseContract
    {
        private static Router _instance;
        private static readonly object _lock = new object();

        public override string fileName => "FlamingoSwapRouter";
        public override string selfPath => "Flamingo.SwapRouter";

        private Router(RpcClient _rpcClient, KeyPair keyPair) : base(_rpcClient, keyPair)
        {
        }
        
        public static Router GetInstance(RpcClient _rpcClient, KeyPair keyPair)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Router(_rpcClient, keyPair);
                    }
                }
            }
              return _instance;
        }


        public byte[] SetFactory(UInt160 newFactory, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "setFactory", newFactory);
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }   

        public byte[] SetBrokerContract(UInt160 newBroker, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "setBrokerContract", newBroker);
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