using System;
using System.IO;
using Neo.Network.RPC;
using Neo;
using Neo.Wallets;
using Newtonsoft.Json;
using System.Numerics;


namespace FlamingoHelper
{
    public class DeployPair
    {
        private RpcClient rpcClient;
        private KeyPair keyPair;
        public DeployPair(dynamic helperConfig, Dictionary<string, string> envConfigDict)
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
        public void Do(string network, BigInteger pairId, params string[] args) 
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

            Func<dynamic, bool> predicate = p => (int)p.pairId == (int)pairId;
            var pair = ((IEnumerable<dynamic>)helperConfig.deployedContracts.FlamingoSwapPair).FirstOrDefault(predicate);
            if(pair == null){
                throw new Exception("pair not found");
            }
            Console.WriteLine($"pair: {pair.name.ToString()}");
            var hash = Pair.GetInstance(rpcClient, keyPair).Deploy(network, pair.name.ToString());
            Factory.GetInstance(rpcClient, keyPair).CreateExchangePair(UInt160.Parse(pair.baseToken.ToString()), UInt160.Parse(pair.quoteToken.ToString()), hash);
        }


    }
}