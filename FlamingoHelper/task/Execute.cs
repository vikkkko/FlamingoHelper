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

        public void Do(string network, string action, BigInteger pairId, params object[] args)
        {
            var helperConfig = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(Path.Combine(Util.GetProjectDirectory(), $"helper.{network}.json")));
            var brokerHash = helperConfig.deployedContracts["FlamingoBroker"].ToString();
            var factoryHash = helperConfig.deployedContracts["FlamingoSwapFactory"].ToString();
            var routerHash = helperConfig.deployedContracts["FlamingoSwapRouter"].ToString();
            var whiteListHash = helperConfig.deployedContracts["FlamingoSwapPairWhiteList"].ToString();
            var flocksHash = helperConfig.deployedContracts["FLOCKS"].ToString();
            
            Broker.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(brokerHash));
            Factory.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(factoryHash));
            Router.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(routerHash));
            WhiteList.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(whiteListHash));
            Flocks.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(flocksHash));

            if (action == "reset"){
                var bytes = Broker.GetInstance(rpcClient, keyPair).ChangeAMMFactory(Factory.GetInstance(rpcClient, keyPair).Hash, false);
                bytes = Broker.GetInstance(rpcClient, keyPair).ChangeAMMRouter(Router.GetInstance(rpcClient, keyPair).Hash, false, bytes);
                bytes = Router.GetInstance(rpcClient, keyPair).SetFactory(Factory.GetInstance(rpcClient, keyPair).Hash, false, bytes);    
                bytes = Router.GetInstance(rpcClient, keyPair).SetBrokerContract(Broker.GetInstance(rpcClient, keyPair).Hash, false, bytes);
                WhiteList.GetInstance(rpcClient, keyPair).AddRouter(Router.GetInstance(rpcClient, keyPair).Hash, true, bytes);
                return;
            }

            if(action == "addRouterToWhiteList"){
                WhiteList.GetInstance(rpcClient, keyPair).AddRouter(Router.GetInstance(rpcClient, keyPair).Hash, true);
                return;
            }

            //helperConfig.deployedContracts.FlamingoSwapPair是一个数组格式
            //根据pairid找到对应的pair
            Func<dynamic, bool> predicate = p => (int)p.pairId == (int)pairId;
            var pair = ((IEnumerable<dynamic>)helperConfig.deployedContracts.FlamingoSwapPair).FirstOrDefault(predicate);
            // if(pair == null){
            //     throw new Exception("pair not found");
            // }

            if(action == "registPair"){
                var currentPairCounter = Broker.GetInstance(rpcClient, keyPair).GetPairCounter() + 1;

                if(currentPairCounter != pairId){
                    throw new Exception("pairId not match");
                }
                Pair.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(pair.hash.ToString()));
                
                
                // Factory.GetInstance(rpcClient, keyPair).CreateExchangePair(UInt160.Parse(pair.baseToken.ToString()), UInt160.Parse(pair.quoteToken.ToString()), UInt160.Parse(pair.hash.ToString()));
                // Pair.GetInstance(rpcClient, keyPair).SetWhiteListContract(UInt160.Parse(whiteListHash));
                var bytes = Broker.GetInstance(rpcClient, keyPair).AddPair(UInt160.Parse(pair.baseToken.ToString()), UInt160.Parse(pair.quoteToken.ToString()), BigInteger.Parse(pair.treeBitLength.ToString()), BigInteger.Parse(pair.pricePrecision.ToString()), false);
                bytes = Broker.GetInstance(rpcClient, keyPair).UnpausePairOrderTrading(pairId, false, bytes);
                bytes = Broker.GetInstance(rpcClient, keyPair).UnpausePairOrderManagement(pairId, false, bytes);
                bytes = Broker.GetInstance(rpcClient, keyPair).EnableTokenDeposit(UInt160.Parse(pair.baseToken.ToString()), false, bytes);
                bytes = Broker.GetInstance(rpcClient, keyPair).EnableTokenDeposit(UInt160.Parse(pair.quoteToken.ToString()), false, bytes);
                bytes = Broker.GetInstance(rpcClient, keyPair).EnableTokenWithdraw(UInt160.Parse(pair.baseToken.ToString()), false, bytes);
                bytes = Broker.GetInstance(rpcClient, keyPair).EnableTokenWithdraw(UInt160.Parse(pair.quoteToken.ToString()), true, bytes);
                // Broker.GetInstance(rpcClient, keyPair).SetGasToBurn(pairId, 3600000, true, bytes);           
                return;
            }

            if (action == "addTokenToWhitelist"){
                var bytes = Flocks.GetInstance(rpcClient, keyPair).AddTokenToWhitelist(UInt160.Parse("0x1005d400bcc2a56b7352f09e273be3f9933a5fb1"), 0, false);
                bytes = Flocks.GetInstance(rpcClient, keyPair).AddTokenToWhitelist(UInt160.Parse("0xf0151f528127558851b39c2cd8aa47da7418ab28"), 4, false, bytes);
                bytes = Flocks.GetInstance(rpcClient, keyPair).AddTokenToWhitelist(UInt160.Parse("0xd2a4cff31913016155e38e474a2c06d08be276cf"), 11, false, bytes);
                bytes = Flocks.GetInstance(rpcClient, keyPair).AddTokenToWhitelist(UInt160.Parse("0x4548a3bcb3c2b5ce42bf0559b1cf2f1ec97a51d0"), 12, false, bytes);
                bytes = Flocks.GetInstance(rpcClient, keyPair).AddTokenToWhitelist(UInt160.Parse("0xd3a41b53888a733b549f5d4146e7a98d3285fa21"), 5, false, bytes);
                bytes = Flocks.GetInstance(rpcClient, keyPair).AddTokenToWhitelist(UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a"), 14, false, bytes);
                bytes = Flocks.GetInstance(rpcClient, keyPair).AddTokenToWhitelist(UInt160.Parse("0x00fb9575f220727f71a1537f75e83af9387628ff"), 8, false, bytes);
                bytes = Flocks.GetInstance(rpcClient, keyPair).AddAccountToBlacklist(UInt160.Parse("0xec268e9c642b7d09d10fe658bcb1cc63c0895d4d"), true, bytes);
                return;
            }
             if (action == "changeFeeCollector"){
                var bytes = Broker.GetInstance(rpcClient, keyPair).ChangeFeeCollector(UInt160.Parse(flocksHash), true);
                return;
            }
            if (action == "depositToSettlementFund"){
                var bytes = Flocks.GetInstance(rpcClient, keyPair).DepositToSettlementFund(UInt160.Parse("0xad4e27f46f3a38d1ac3c381b149020142d7df290"), 100000000000, true);
                return;
            }
            if (action == "changeProfitSpeedFLM"){
                var bytes = Flocks.GetInstance(rpcClient, keyPair).ChangeProfitSpeedFLM(5000000, true);
                return;
            }
            if (action == "unpauseFlocks"){
                var bytes = Flocks.GetInstance(rpcClient, keyPair).UnpauseMint(false);
                bytes = Flocks.GetInstance(rpcClient, keyPair).UnpauseBurn(false, bytes);
                bytes = Flocks.GetInstance(rpcClient, keyPair).UnpauseWithdraw(false, bytes);
                bytes = Flocks.GetInstance(rpcClient, keyPair).UnpauseClaim(false, bytes);
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

            if(action == "resetRouterBroker"){
                var sender = keyPair.GetScriptHash().ToString();
                Router.GetInstance(rpcClient, keyPair).SetBrokerContract(Broker.GetInstance(rpcClient, keyPair).Hash);
                return;
            }

            if(action == "addLiquidity"){
                var sender = keyPair.GetScriptHash();
                Router.GetInstance(rpcClient, keyPair).AddLiquidity(sender, UInt160.Parse(pair.baseToken.ToString()), UInt160.Parse(pair.quoteToken.ToString()), BigInteger.Parse(args[0].ToString()), BigInteger.Parse(args[1].ToString()), 0, 0);
                return;
            }

            if(action == "addModerator"){
                Broker.GetInstance(rpcClient, keyPair).AddModerator(UInt160.Parse(args[0].ToString()), true);
                return;
            }

            if(action == "depositToBroker"){
                var sender = keyPair.GetScriptHash().ToString();
                Broker.GetInstance(rpcClient, keyPair).Deposit(UInt160.Parse(sender), UInt160.Parse(pair.baseToken.ToString()), BigInteger.Parse(args[0].ToString()));
                Broker.GetInstance(rpcClient, keyPair).Deposit(UInt160.Parse(sender), UInt160.Parse(pair.quoteToken.ToString()), BigInteger.Parse(args[1].ToString()));
                return;
            }

            if(action == "sell0"){
                var sender = keyPair.GetScriptHash().ToString();
                Router.GetInstance(rpcClient, keyPair).SwapTokenInForTokenOut(966735800000, 0, new UInt160[] { UInt160.Parse(pair.baseToken.ToString()), UInt160.Parse(pair.quoteToken.ToString()) }, 999999999999999);
                return;
            }

            if(action == "sell1"){
                var sender = keyPair.GetScriptHash().ToString();
                Broker.GetInstance(rpcClient, keyPair).CreateLimitSellOrderUsingBase(UInt160.Parse(sender), pairId, BigInteger.Parse(args[0].ToString()), BigInteger.Parse(args[1].ToString()), 0, true, false);
                return;
            }   

            if(action == "sell2"){
                var sender = keyPair.GetScriptHash().ToString();
                Broker.GetInstance(rpcClient, keyPair).ExecuteLimitSellOrderUsingBase(UInt160.Parse(sender), pairId, BigInteger.Parse(args[0].ToString()), BigInteger.Parse(args[1].ToString()), 0, true, false);
                return;
            }   
        }   
    }       
}