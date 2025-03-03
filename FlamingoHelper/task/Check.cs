using System;
using System.IO;
using Neo.Network.RPC;
using Neo;
using Neo.Wallets;
using Newtonsoft.Json;

namespace FlamingoHelper
{
    public class Check
    {
        private RpcClient rpcClient;
        private KeyPair keyPair;
        public Check(dynamic helperConfig, Dictionary<string, string> envConfigDict)
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
        public void Do(string network) 
        {
            var helperConfig = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(Path.Combine(Util.GetProjectDirectory(), $"helper.{network}.json")));
            var brokerHash = helperConfig.deployedContracts["FlamingoBroker"].ToString();
            var factoryHash = helperConfig.deployedContracts["FlamingoSwapFactory"].ToString();
            var routerHash = helperConfig.deployedContracts["FlamingoSwapRouter"].ToString();
            var whiteListHash = helperConfig.deployedContracts["FlamingoSwapPairWhiteList"].ToString();

            Console.WriteLine($"brokerHash: {brokerHash}");
            Console.WriteLine($"factoryHash: {factoryHash}");
            Console.WriteLine($"routerHash: {routerHash}");
            Console.WriteLine($"whiteListHash: {whiteListHash}");
            
            Broker.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(brokerHash));
            Factory.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(factoryHash));
            Router.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(routerHash));
            WhiteList.GetInstance(rpcClient, keyPair).Init(UInt160.Parse(whiteListHash));

            //检查broker的参数
            Console.WriteLine($"broker version: {Broker.GetInstance(rpcClient, keyPair).GetVersion()}");
            Console.WriteLine($"broker ammRouter: {Broker.GetInstance(rpcClient, keyPair).GetAMMRouter()}");
            Console.WriteLine($"broker ammFactory: {Broker.GetInstance(rpcClient, keyPair).GetAMMFactory()}");
            Console.WriteLine($"broker owner: {Broker.GetInstance(rpcClient, keyPair).GetOwner()}");

            //检查whiteList的参数
            Console.WriteLine($"whiteList version: {WhiteList.GetInstance(rpcClient, keyPair).GetVersion()}");  
            Console.WriteLine($"whiteList admin: {WhiteList.GetInstance(rpcClient, keyPair).GetAdmin()}");
            Console.WriteLine($"whiteList router: {WhiteList.GetInstance(rpcClient, keyPair).CheckRouter(UInt160.Parse(routerHash))}");


            //检查factory的参数
            Console.WriteLine($"factory version: {Factory.GetInstance(rpcClient, keyPair).GetVersion()}");
            Console.WriteLine($"factory admin: {Factory.GetInstance(rpcClient, keyPair).GetAdmin()}");


            //检查router的参数
            Console.WriteLine($"router version: {Router.GetInstance(rpcClient, keyPair).GetVersion()}");
            Console.WriteLine($"router factory: {Router.GetInstance(rpcClient, keyPair).GetFactory()}");
            Console.WriteLine($"router broker: {Router.GetInstance(rpcClient, keyPair).GetBroker()}");
            Console.WriteLine($"router admin: {Router.GetInstance(rpcClient, keyPair).GetAdmin()}");
        }
    }
}   