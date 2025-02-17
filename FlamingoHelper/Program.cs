using System;
using System.IO;
using Neo.Network.RPC;
using Neo;
using Neo.Wallets;
using Newtonsoft.Json;
using System.Numerics;

namespace FlamingoHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            //把args都log出来
            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }
            //args[0] 是环境 testnet 或者 mainnet
            string env = args[0];
            //检查环境是否正确
            if (env != "testnet" && env != "mainnet")
            {
                Console.WriteLine("环境错误");
                return;
            }

            //如果是testnet 用config.testnet.json 替换 config.json
            //如果是mainnet 用config.mainnet.json 替换 config.json
            if (env == "testnet")
            {
                File.Copy("config.testnet.json", "config.json", true);
            }
            else
            {
                File.Copy("config.mainnet.json", "config.json", true);
            }

            //args[1] 是操作指令 deploy代表重新部署并初始化合约, test表示运行测试程序
            string action = args[1];
            if (action != "deploy" && action != "test" && action != "deployPair" && action != "execute")
            {
                Console.WriteLine("操作错误");
                return;
            }
            if(action == "deploy")
            {
                Console.WriteLine(Path.Combine(Util.GetProjectDirectory(), $"external/flamingo-sc/src/Flamingo.{args[2]}"));
                if (args[2] != "all" && !Directory.Exists(Path.Combine(Util.GetProjectDirectory(), $"external/flamingo-sc/src/Flamingo.{ args[2]}")))
                {
                    Console.WriteLine("合约不存在");
                    return;
                }
            }

            var configFile = Path.Combine(Util.GetProjectDirectory(), $"helper.{env}.json");
            if (!File.Exists(configFile))
            {
                Console.WriteLine($"错误：配置文件 {configFile} 不存在");
                return;
            }
            // 读取配置文件
            var helperConfig = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(configFile));
            var envFile = Path.Combine(Util.GetProjectDirectory(), ".env");
            var envConfig = File.ReadAllText(envFile);
            var envConfigDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(envConfig);

            if(action == "deploy"){
                Deploy deploy = new Deploy(helperConfig, envConfigDict);
                deploy.Do(env, args[2]);
            }
            else if(action == "deployPair"){
                DeployPair deployPair = new DeployPair(helperConfig, envConfigDict);
                deployPair.Do(env,  BigInteger.Parse(args[2]), args.Length > 3 ? args[3] : "");
            }
            else if(action == "execute"){
                Execute execute = new Execute(helperConfig, envConfigDict);
                execute.Do(env, args[2], args.Length > 3 ? BigInteger.Parse(args[3]) : 0);
            }
        }
    }
}
