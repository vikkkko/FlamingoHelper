using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System.Numerics;
using System.Text;

namespace FlamingoHelper
{
    public class Broker : BaseContract
    {
        private static Broker _instance;
        private static readonly object _lock = new object();

        public override string fileName => "FlamingoBroker";
        public override string selfPath => "Flamingo.Broker";

        private Broker(RpcClient _rpcClient, KeyPair keyPair) : base(_rpcClient, keyPair)
        {
        }

        public static Broker GetInstance(RpcClient _rpcClient, KeyPair keyPair)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Broker(_rpcClient, keyPair);
                    }
                }
            }
            return _instance;
        }

        #region get
        public BigInteger GetPairCounter()
        {   
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "getPairCounter");
                script = sb.ToArray();
            }
            var result = Util.InvokeScript(_rpcClient, script);
            return BigInteger.Parse(((Neo.Json.JString)result).Value);
        } 
        #endregion

        public override void Update(string network = "testnet", string _fileName = null)
        {
            _fileName = _fileName ?? fileName;
            Util.UpdateContract(Hash, 0, _Path, _fileName, _rpcClient, keyPair);
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

        public string GetAMMRouter()
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "getAMMRouter");
                script = sb.ToArray();
            }
            return Util.GetUInt160FromBase64String((string)Util.InvokeScript(_rpcClient, script)).ToString();
        }

        public string GetAMMFactory()
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "getAMMFactory");
                script = sb.ToArray();
            }
            return Util.GetUInt160FromBase64String((string)Util.InvokeScript(_rpcClient, script)).ToString();
        }

        public string GetOwner()
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "getOwner");
                script = sb.ToArray();
            }
            return Util.GetUInt160FromBase64String((string)Util.InvokeScript(_rpcClient, script)).ToString();
        }

        #endregion

        public byte[] ChangeAMMFactory(UInt160 newAMMFactory, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "changeAMMFactory", newAMMFactory);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }   

        public byte[] ChangeAMMRouter(UInt160 newAMMRouter, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "changeAMMRouter", newAMMRouter);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)    
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }

        public byte[] SetGasToBurn(BigInteger pairId, int gasAmount, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "setGasToBurn", pairId, gasAmount);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)        
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }

        public byte[] AddPair(UInt160 baseToken, UInt160 quoteToken, BigInteger treeBitLength, BigInteger pricePrecision, bool send = true,  byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "addPair", baseToken, quoteToken, treeBitLength, pricePrecision);
                //script添加sb的script
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            } 
            return script;
        }   


        public byte[] EnableTokenDeposit(UInt160 token, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "enableTokenDeposit", token);  
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)    
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }   
        
        public byte[] DisableTokenDeposit(UInt160 token, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "disableTokenDeposit", token);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)        
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        } 

        public byte[] EnableTokenWithdraw(UInt160 token, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "enableTokenWithdraw", token);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)        
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }   

        public byte[] DisableTokenWithdraw(UInt160 token, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "disableTokenWithdraw", token);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)            
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }   

        public byte[] PausePairOrderTrading(BigInteger pairId, UInt160 sender, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "pausePairOrderTrading", pairId, sender);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)                
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }   

        public byte[] UnpausePairOrderTrading(BigInteger pairId, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "unpausePairOrderTrading", pairId);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)
            {    
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }   

        public byte[] UnpausePairOrderManagement(BigInteger pairId, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "unpausePairOrderManagement", pairId);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)    
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }      

        public byte[] SetPairMakerFee(BigInteger pairId, BigInteger makerFee, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "setPairMakerFee", pairId, makerFee);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)        
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair); 
            }   
            return script;
        }

        public byte[] Deposit(UInt160 user, UInt160 token, BigInteger amount, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "deposit", user, token, amount);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }

        public byte[] Withdraw(UInt160 user, UInt160 token, BigInteger amount, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "withdraw", user, token, amount);  
                script = script.Concat(sb.ToArray()).ToArray();     
            }
            if(send)
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }

        public byte[] CreateLimitSellOrderUsingBase(UInt160 user, BigInteger pairId, BigInteger baseAmount, BigInteger limitPrice, BigInteger userDefinedId, bool useAMM, bool debug, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "createLimitSellOrderUsingBase", user, pairId, baseAmount, limitPrice, userDefinedId, useAMM, debug);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }     

        public byte[] ExecuteLimitSellOrderUsingBase(UInt160 user, BigInteger pairId, BigInteger baseAmount, BigInteger limitPrice, BigInteger userDefinedId, bool useAMM, bool debug, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "executeLimitSellOrderUsingBase", user, pairId, baseAmount, limitPrice, userDefinedId, useAMM, debug);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }
            return script;
        }

        public byte[] AddModerator(UInt160 moderator, bool send = true, byte[] _script = null)
        {
            byte[] script = _script ?? new byte[0];
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(Hash, "addModerator", moderator);
                script = script.Concat(sb.ToArray()).ToArray();
            }
            if(send)
            {
                Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };
                Util.SignAndSendTx(_rpcClient, script, signers, null, keyPair);
            }   
            return script;
        }
    }
}