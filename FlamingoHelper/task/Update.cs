using System;
using System.IO;
using Neo.Network.RPC;
using Neo;
using Neo.Wallets;
using Newtonsoft.Json;

namespace FlamingoHelper
{
    public class Update
    {
        private RpcClient rpcClient;
        private KeyPair keyPair;
        public Update(dynamic helperConfig, Dictionary<string, string> envConfigDict)
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
        public void Do(string network, string contractName) 
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

            if (contractName == "Broker")
            {
                Broker.GetInstance(rpcClient, keyPair).Update(network);
            }
            else if (contractName == "SwapPairWhiteList")
            {
                WhiteList.GetInstance(rpcClient, keyPair).Update(network);
            }
            else if (contractName == "SwapFactory")
            {
                Factory.GetInstance(rpcClient, keyPair).Update(network);
            }
            else if (contractName == "SwapRouter")
            {
                Router.GetInstance(rpcClient, keyPair).Update(network);
            }
        }


    }
}