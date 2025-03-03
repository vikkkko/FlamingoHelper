using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using Neo;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlamingoHelper
{
    public abstract class BaseContract
    {
        public RpcClient _rpcClient;
        public KeyPair keyPair;
        public UInt160 Hash;
        public string BasePath = "/external/flamingo-sc-monorepo/src/";
        public string _Path;
        public virtual string selfPath => throw new NotImplementedException();
        public virtual string fileName => throw new NotImplementedException();

        public BaseContract(RpcClient rpcClient, KeyPair keyPair)
        {
            _rpcClient = rpcClient;
            this.keyPair = keyPair;
            _Path = Util.GetProjectDirectory() + BasePath + selfPath + "/bin/sc/";
        }

        public void Init(UInt160 _Hash)
        {
            Hash = _Hash;
        }

        public UInt160 Deploy(string network = "testnet", string _fileName = null)
        {
            _fileName = _fileName ?? fileName;
            Hash = Util.DeployContract(_Path, _fileName, _rpcClient, keyPair);
            UpdateHash(network, _fileName, Hash);
            return Hash;
        }   

        public virtual void Update(string network = "testnet", string _fileName = null)
        {
            _fileName = _fileName ?? fileName;
            Util.UpdateContract(Hash, null, _Path, _fileName, _rpcClient, keyPair);
        }

        public virtual void UpdateHash(string network, string fileName, UInt160 hash)
        {
            string projectDir = Util.GetProjectDirectory();
            var helperPath = Path.Combine(projectDir, $"helper.{network}.json");
            var helper = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(helperPath));
            helper.deployedContracts[fileName] = hash.ToString();
            File.WriteAllText(helperPath, JsonConvert.SerializeObject(helper, Formatting.Indented));
        }
    }
}
