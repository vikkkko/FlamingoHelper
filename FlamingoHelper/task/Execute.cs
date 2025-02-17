using System;
using System.IO;
using Neo.Network.RPC;
using Neo;
using Neo.Wallets;
using Newtonsoft.Json;
using Neo.SmartContract.Native;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

namespace FlamingoHelper
{
    public class Execute
    {
        private RpcClient rpcClient;
        private KeyPair keyPair;

        public Execute(dynamic helperConfig, Dictionary<string, string> envConfigDict)
        {
            string protocolConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            try 
            {
                rpcClient = new RpcClient(new Uri(helperConfig.url.ToString()), null, null, ProtocolSettings.Load(protocolConfigPath));
 
                keyPair = Neo.Network.RPC.Utility.GetKeyPair(envConfigDict["key"]);

            }
            catch (Exception ex)
            {
                throw new Exception($"失败: {ex.Message}");
            }
        }

        public void Do(string network, string action, BigInteger pairId)
        {
            var helperConfig = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(Path.Combine(Util.GetProjectDirectory(), $"helper.{network}.json")));
            var brokerHash = helperConfig.deployedContracts["FlamingoBroker"].ToString();
            var factoryHash = helperConfig.deployedContracts["FlamingoSwapFactory"].ToString();
            var routerHash = helperConfig.deployedContracts["FlamingoSwapRouter"].ToString();
            var whiteListHash = helperConfig.deployedContracts["FlamingoSwapPairWhiteList"].ToString();
            
            Broker.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(brokerHash));
            Factory.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(factoryHash));
            Router.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(routerHash));
            WhiteList.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(whiteListHash));


            if (action == "reset"){
                var bytes = Broker.GetInstance(rpcClient, keyPair).ChangeAMMFactory(Factory.GetInstance(rpcClient, keyPair).Hash, false);
                bytes = Broker.GetInstance(rpcClient, keyPair).ChangeAMMRouter(Router.GetInstance(rpcClient, keyPair).Hash, false, bytes);
                bytes = Router.GetInstance(rpcClient, keyPair).SetFactory(Factory.GetInstance(rpcClient, keyPair).Hash, false, bytes);    
                bytes = Router.GetInstance(rpcClient, keyPair).SetBrokerContract(Broker.GetInstance(rpcClient, keyPair).Hash, false, bytes);
                WhiteList.GetInstance(rpcClient, keyPair).AddRouter(Router.GetInstance(rpcClient, keyPair).Hash, true, bytes);
                return;
            }

            //helperConfig.deployedContracts.FlamingoSwapPair是一个数组格式
            //根据pairid找到对应的pair
            Func<dynamic, bool> predicate = p => (int)p.pairId == (int)pairId;
            var pair = ((IEnumerable<dynamic>)helperConfig.deployedContracts.FlamingoSwapPair).FirstOrDefault(predicate);
            if(pair == null){
                throw new Exception("pair not found");
            }

            if(action == "registPair"){
                var currentPairCounter = Broker.GetInstance(rpcClient, keyPair).GetPairCounter() + 1;
                if(currentPairCounter != pairId){
                    throw new Exception("pairId not match");
                }
                var bytes = Broker.GetInstance(rpcClient, keyPair).AddPair(UInt160.Parse(pair.baseToken.ToString()), UInt160.Parse(pair.quoteToken.ToString()), BigInteger.Parse(pair.treeBitLength.ToString()), BigInteger.Parse(pair.pricePrecision.ToString()), false);
                bytes = Broker.GetInstance(rpcClient, keyPair).UnpausePairOrderTrading(pairId, false, bytes);
                bytes = Broker.GetInstance(rpcClient, keyPair).UnpausePairOrderManagement(pairId, false, bytes);
                bytes = Broker.GetInstance(rpcClient, keyPair).EnableTokenDeposit(UInt160.Parse(pair.baseToken.ToString()), false, bytes);
                bytes = Broker.GetInstance(rpcClient, keyPair).EnableTokenDeposit(UInt160.Parse(pair.quoteToken.ToString()), false, bytes);
                bytes = Broker.GetInstance(rpcClient, keyPair).EnableTokenWithdraw(UInt160.Parse(pair.baseToken.ToString()), false, bytes);
                bytes = Broker.GetInstance(rpcClient, keyPair).EnableTokenWithdraw(UInt160.Parse(pair.quoteToken.ToString()), false, bytes);
                Broker.GetInstance(rpcClient, keyPair).SetGasToBurn(pairId, 3600000, true, bytes);
                return;
            }
            if (action == "addPair"){
                var currentPairCounter = Broker.GetInstance(rpcClient, keyPair).GetPairCounter();
                if(currentPairCounter != pairId){
                    throw new Exception("pairId not match");
                }
                Broker.GetInstance(rpcClient, keyPair).AddPair(UInt160.Parse(pair.baseToken.ToString()), UInt160.Parse(pair.quoteToken.ToString()), BigInteger.Parse(pair.treeBitLength.ToString()), BigInteger.Parse(pair.pricePrecision.ToString()));
                return;
            }
            if(action == "enableTokenWithdraw"){
                Broker.GetInstance(rpcClient, keyPair).EnableTokenWithdraw(UInt160.Parse(pair.baseToken.ToString()));
                Broker.GetInstance(rpcClient, keyPair).EnableTokenWithdraw(UInt160.Parse(pair.quoteToken.ToString()));
                return;
            }
            if(action == "disableTokenWithdraw"){
                Broker.GetInstance(rpcClient, keyPair).DisableTokenWithdraw(UInt160.Parse(pair.baseToken.ToString()));
                Broker.GetInstance(rpcClient, keyPair).DisableTokenWithdraw(UInt160.Parse(pair.quoteToken.ToString()));
                return;
            }   
            if(action == "enableTokenDeposit"){
                Broker.GetInstance(rpcClient, keyPair).EnableTokenDeposit(UInt160.Parse(pair.baseToken.ToString()));
                Broker.GetInstance(rpcClient, keyPair).EnableTokenDeposit(UInt160.Parse(pair.quoteToken.ToString()));
                return;
            }
            if(action == "disableTokenDeposit"){
                Broker.GetInstance(rpcClient, keyPair).DisableTokenDeposit(UInt160.Parse(pair.baseToken.ToString()));
                Broker.GetInstance(rpcClient, keyPair).DisableTokenDeposit(UInt160.Parse(pair.quoteToken.ToString()));
                return;
            }
            if(action == "pausePairOrderTrading"){
                var sender = keyPair.GetScriptHash().ToString();
                Broker.GetInstance(rpcClient, keyPair).PausePairOrderTrading(pairId, UInt160.Parse(sender));
                return;
            }
            if(action == "unpausePairOrderTrading"){
                Broker.GetInstance(rpcClient, keyPair).UnpausePairOrderTrading(pairId);
                return;
            }
            if(action == "unpausePairOrderManagement"){
                Broker.GetInstance(rpcClient, keyPair).UnpausePairOrderManagement(pairId);
                return;
            }
            if(action == "setGasToBurn"){
                Broker.GetInstance(rpcClient, keyPair).SetGasToBurn(pairId, 3600000);
                return;
            }
        }   
    }       
}