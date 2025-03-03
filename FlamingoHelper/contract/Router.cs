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
    
        public string GetFactory()
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "getFactory"); 
                script = sb.ToArray();
            }
            return Util.GetUInt160FromBase64String((string)Util.InvokeScript(_rpcClient, script)).ToString();
        }
        
        public string GetBroker()
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "getBrokerContract");
                script = sb.ToArray();
            }
            return Util.GetUInt160FromBase64String((string)Util.InvokeScript(_rpcClient, script)).ToString();
        }
        #endregion

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

        public byte[] AddLiquidity(UInt160 sender, UInt160 tokenA, UInt160 tokenB, BigInteger amountADesired, BigInteger amountBDesired, BigInteger amountAMin, BigInteger amountBMin, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "addLiquidity", sender, tokenA, tokenB, amountADesired, amountBDesired, amountAMin, amountBMin, 999999999999999);
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){   
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }

        public byte[] SwapTokenInForTokenOut(BigInteger amountIn, BigInteger amountOutMin, UInt160[] paths, BigInteger deadLine, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitPush(deadLine);
                sb.EmitPush(paths[1]);
                sb.EmitPush(paths[0]);
                sb.EmitPush(2);
                sb.Emit(OpCode.PACK);
                sb.EmitPush(amountOutMin);
                sb.EmitPush(amountIn);
                sb.EmitPush(4);
                sb.Emit(OpCode.PACK);
                sb.EmitPush(CallFlags.All);
                sb.EmitPush("swapTokenInForTokenOut");
                sb.EmitPush(Hash);
                sb.EmitSysCall(ApplicationEngine.System_Contract_Call);

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