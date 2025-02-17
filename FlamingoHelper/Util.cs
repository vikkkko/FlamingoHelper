using Neo;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.Wallets;
using System.Numerics;
using Neo.Network.RPC.Models;

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

        public static string InvokeScript(RpcClient _rpcClient, byte[] script, params Signer[] signers)
        {
            RpcInvokeResult invokeResult = _rpcClient.InvokeScriptAsync(script, signers).Result;

            Console.WriteLine($"Invoke result: {invokeResult.ToJson()}");
            return invokeResult.ToJson()["stack"][0]["value"].GetString();
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
    }
}
