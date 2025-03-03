using Neo;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.Wallets;
using System.Numerics;
using Neo.Network.RPC.Models;
using Neo.VM;

namespace FlamingoHelper
{
    public static class Util
    {
        public static string GetProjectDirectory()
        {
            string currentPath = Directory.GetCurrentDirectory();
            while (currentPath != null && !File.Exists(Path.Combine(currentPath, "FlamingoHelper.csproj")))
            {
                currentPath = Directory.GetParent(currentPath)?.FullName;
            }

            if (currentPath == null)
            {
                throw new DirectoryNotFoundException("Could not find FlamingoHelper project directory");
            }

            return currentPath;
        }

        public static UInt160 GetScriptHash(this KeyPair keyPair) => Neo.SmartContract.Contract.CreateSignatureContract(keyPair.PublicKey).ScriptHash;

        public static object InvokeScript(RpcClient _rpcClient, byte[] script, params Signer[] signers)
        {
            RpcInvokeResult invokeResult = _rpcClient.InvokeScriptAsync(script, signers).Result;

            // Console.WriteLine($"Invoke result: {invokeResult.ToJson()}");
            if(invokeResult.ToJson()["stack"][0]["type"].GetString() == "ByteString"){
                return invokeResult.ToJson()["stack"][0]["value"].GetString();
            } else if (invokeResult.ToJson()["stack"][0]["type"].GetString() == "Boolean"){
                return invokeResult.ToJson()["stack"][0]["value"].GetBoolean();
            } else {
                return invokeResult.ToJson()["stack"][0]["value"];
            }
        } 

        public static Neo.Json.JArray InvokeScript_Array(RpcClient _rpcClient, byte[] script, params Signer[] signers)
        {
            RpcInvokeResult invokeResult = _rpcClient.InvokeScriptAsync(script, signers).Result;
            Console.WriteLine($"Invoke result: {invokeResult.ToJson()}");
            return invokeResult.ToJson()["stack"][0]["value"] as Neo.Json.JArray;
        }

        public static void SignAndSendTx(RpcClient _rpcClient, byte[] script, Signer[] signers, TransactionAttribute[] transactionAttributes = null, params KeyPair[] keyPair)
        {

            try
            {
                TransactionManagerFactory factory = new TransactionManagerFactory(_rpcClient);
                TransactionManager manager = factory.MakeTransactionAsync(script, signers, transactionAttributes).Result;

                foreach (var kp in keyPair)
                {
                    manager.AddSignature(kp);
                }

                Transaction invokeTx = manager.SignAsync().Result;
                var resut =  _rpcClient.SendRawTransactionAsync(invokeTx).Result;

                Console.WriteLine($"Transaction {resut} is broadcasted!");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static UInt160 DeployContract(string path, string fileName, RpcClient _rpcClient, KeyPair keyPair)
        {
            Console.WriteLine("deploy contract.");
            string nefFilePath = path + fileName + ".nef";
            string manifestFilePath = path + fileName + ".manifest.json";

            NefFile nefFile = File.ReadAllBytes(nefFilePath).AsSerializable<NefFile>();
            var mani = File.ReadAllBytes(manifestFilePath);
            ContractManifest manifest = ContractManifest.Parse(mani);

            ContractClient contractClient = new ContractClient(_rpcClient);
            var tx = contractClient.CreateDeployContractTxAsync(nefFile.ToArray(), manifest, keyPair).Result;
            var contractHash = Neo.SmartContract.Helper.GetContractHash(tx.Sender, nefFile.CheckSum, manifest.Name);

            Console.WriteLine($"Transaction {_rpcClient.SendRawTransactionAsync(tx.ToArray()).Result} is broadcasted!");

            Console.WriteLine($"{fileName} contract hash:   " + contractHash);


            return contractHash;
        }


        public static void UpdateContract(UInt160 contract, object data, string path, string fileName, RpcClient _rpcClient, KeyPair keyPair)
        {
            Console.WriteLine("update contract.");

            string nefFilePath = path + fileName + ".nef";
            string manifestFilePath = path + fileName + ".manifest.json";

            NefFile nefFile = File.ReadAllBytes(nefFilePath).AsSerializable<NefFile>();
            var mani = File.ReadAllBytes(manifestFilePath);
            ContractManifest manifest = ContractManifest.Parse(mani);

            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                if (data != null){
                    sb.EmitDynamicCall(contract, "update", nefFile.ToArray(), manifest.ToJson().ToString(), data);
                } else {
                    sb.EmitDynamicCall(contract, "update", nefFile.ToArray(), manifest.ToJson().ToString());
                }
                script = sb.ToArray();
            }

            Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = keyPair.GetScriptHash() } };

            SignAndSendTx(_rpcClient, script, signers, null, keyPair);
        }

        public static UInt160 GetUInt160FromBase64String(string str)
        {
            return new UInt160(Convert.FromBase64String(str));
        }
    }
}
