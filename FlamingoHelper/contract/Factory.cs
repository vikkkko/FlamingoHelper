using System.Text;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;

namespace FlamingoHelper
{
    public class Factory : BaseContract
    {
        private static Factory _instance;
        private static readonly object _lock = new object();

        public override string fileName => "FlamingoSwapFactory";
        public override string selfPath => "Flamingo.SwapFactory";  

        private Factory(RpcClient _rpcClient, KeyPair keyPair) : base(_rpcClient, keyPair)
        {
        }

        public static Factory GetInstance(RpcClient _rpcClient, KeyPair keyPair)
        {
            if (_instance == null)  
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Factory(_rpcClient, keyPair);    
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

        public string GetExchangePair(UInt160 tokenA, UInt160 tokenB)
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "getExchangePair", tokenA, tokenB);    
                script = sb.ToArray();
            }
            return Util.GetUInt160FromBase64String((string)Util.InvokeScript(_rpcClient, script)).ToString();
        }
        
        #endregion
        

        public byte[] CreateExchangePair(UInt160 tokenA, UInt160 tokenB, UInt160 exchangeContractHash, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "createExchangePair", tokenA, tokenB, exchangeContractHash);
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }      
        
        public byte[] RegisterExchangePair(UInt160 exchangeContractHash, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "registerExchangePair", exchangeContractHash);
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