using System;
using System.IO;
using Neo.Network.RPC;
using Neo;
using Neo.Wallets;
using Newtonsoft.Json;

namespace FlamingoHelper
{
    public class Deploy
    {
        private RpcClient rpcClient;
        private KeyPair keyPair;
        public Deploy(dynamic helperConfig, Dictionary<string, string> envConfigDict)
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
            if(contractName == "all")
            {
                Do(network, "Broker");
                Do(network, "SwapPairWhiteList");
                Do(network, "SwapFactory");
                Do(network, "SwapRouter");
            }
            else if (contractName == "Broker")
            {
                Broker.GetInstance(rpcClient, keyPair).Deploy(network);
            }
            else if (contractName == "SwapPairWhiteList")
            {
                WhiteList.GetInstance(rpcClient, keyPair).Deploy(network);
            }
            else if (contractName == "SwapFactory")
            {
                Factory.GetInstance(rpcClient, keyPair).Deploy(network);
            }
            else if (contractName == "SwapRouter")
            {
                Router.GetInstance(rpcClient, keyPair).Deploy(network);
            }
            else if (contractName == "Flocks")
            {
                Flocks.GetInstance(rpcClient, keyPair).Deploy(network);
            }
        }


    }
}