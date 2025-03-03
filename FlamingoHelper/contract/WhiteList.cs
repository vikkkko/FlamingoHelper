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
    public class WhiteList : BaseContract
    {
        private static WhiteList _instance;
        private static readonly object _lock = new object();

        public override string fileName => "FlamingoSwapPairWhiteList";
        public override string selfPath => "Flamingo.SwapPairWhiteList";

        private WhiteList(RpcClient _rpcClient, KeyPair keyPair) : base(_rpcClient, keyPair)
        {
        }

        public static WhiteList GetInstance(RpcClient _rpcClient, KeyPair keyPair)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new WhiteList(_rpcClient, keyPair);
                    }
                }
            }
            return _instance;
        }


        #region get
        public string GetVersion()
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {   
                sb.EmitDynamicCall(Hash, "getVersion");
                script = sb.ToArray();
            }
            return Encoding.UTF8.GetString(Convert.FromBase64String((string)Util.InvokeScript(_rpcClient, script)));
        }   

        public string GetAdmin()
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "getAdmin");
                script = sb.ToArray();
            }
            return Util.GetUInt160FromBase64String((string)Util.InvokeScript(_rpcClient, script)).ToString();
        }

        public bool CheckRouter(UInt160 router)   
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "checkRouter", router);
                script = sb.ToArray();
            }   
            return (bool)Util.InvokeScript(_rpcClient, script);
        }
        #endregion

        public byte[] AddRouter(UInt160 router, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "addRouter", router);
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }   

        public byte[] RemoveRouter(UInt160 router, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "removeRouter", router);
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
