using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System.Numerics;

namespace FlamingoHelper
{
    public class Flocks : BaseContract
    {
        private static Flocks _instance;
        private static readonly object _lock = new object();

        public override string fileName => "FLOCKS";
        public override string selfPath => "Flamingo.FLOCKS";

        private Flocks(RpcClient _rpcClient, KeyPair keyPair) : base(_rpcClient, keyPair)
        {
        }

        public static Flocks GetInstance(RpcClient _rpcClient, KeyPair keyPair)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Flocks(_rpcClient, keyPair);
                    }
                }
            }
            return _instance;
        }


        public byte[] AddTokenToWhitelist(UInt160 tokenHash, int priority, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "addTokenToWhitelist", tokenHash, priority);
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }    

        public byte[] AddAccountToBlacklist(UInt160 accountHash, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "addAccountToBlacklist", accountHash);
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }

        public byte[] RemoveAccountFromBlacklist(UInt160 accountHash, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "removeAccountFromBlacklist", accountHash);
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }

        public byte[] DepositToSettlementFund(UInt160 tokenHash, BigInteger amount, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "depositToSettlementFund", tokenHash, amount);
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }

        public byte[] UnpauseMint(bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "unpauseMint");
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }      
            return script;
        }

        public byte[] UnpauseBurn(bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "unpauseBurn");    
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }       
            return script;
        }

        public byte[] UnpauseWithdraw(bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0]; 
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "unpauseWithdraw");
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }       
            return script;
        }

        public byte[] UnpauseClaim(bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "unpauseClaim");
                script = script.Concat(sb.ToArray()).ToArray();
            }

            if(send){
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };    
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair); 
            }   
            return script;
        }

        public byte[] ChangeProfitSpeedFLM(BigInteger profitSpeed, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "changeProfitSpeedFLM", profitSpeed);
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