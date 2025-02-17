using System.Numerics;
using Neo;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Json;
using Neo.SmartContract.Testing;

namespace Flamingo.OrderBook.Tests;

[TestClass]
public class FlamingoFTokenVaultTests : FlamingoTestSuiteBase<FTokenVault>
{
    public FlamingoFTokenVaultTests() : base(FTokenVault.Nef, FTokenVault.Manifest)
    {
    }
    // private string SuperAdminPublicKey = "02738f9efdd954f8436d91ff5f373ae8af14641abc6511de3d1a2ab40665e9a21f";
    // private string SuperAdminPrivateKey = "0c2102738f9efdd954f8436d91ff5f373ae8af14641abc6511de3d1a2ab40665e9a21f4156e7b327";
    //
    // FUSD FUSDContract;
    // BurgerNEO BNEOContract;
    // FLUND FLUNDContract;
    // FlamingoPriceFeed PriceFeedContract;
    // List<BurgerAgent> BurgerAgents = new();
    //
    // public FlamingoFTokenVaultTests() : base(FTokenVault.Nef, FTokenVault.Manifest)
    // {
    // }
    //
    // private void Initialize()
    // {
    //     InitializeAccounts();
    //     InitializeBNEO();
    //     InitializeFLUND();
    //     InitializeFToken();
    //     InitializePriceFeed();
    //     InitializeFTokenVault();
    // }
    //
    // private void InitializeFLUND()
    // {
    //     Engine.SetTransactionSigners(SuperAdmin);
    //
    //     FLUNDContract = Engine.Deploy<FLUND>(
    //         FLUND.Nef,
    //         FLUND.Manifest
    //     );
    // }
    //
    // private void InitializeAccounts()
    // {
    //     Engine.SetTransactionSigners(Engine.ValidatorsAddress);
    //
    //     var resultNeoTransfer = Engine.Native.NEO.Transfer(Engine.ValidatorsAddress, SuperAdmin.Account, 1000);
    //     Assert.IsTrue(resultNeoTransfer);
    //     var neoBalance = Engine.Native.NEO.BalanceOf(SuperAdmin.Account);
    //     Assert.AreEqual(1000, neoBalance);
    //
    //     var resultGasTransfer = Engine.Native.GAS.Transfer(Engine.ValidatorsAddress, SuperAdmin.Account, 1000);
    //     Assert.IsTrue(resultGasTransfer);
    //     var gasBalance = Engine.Native.GAS.BalanceOf(SuperAdmin.Account);
    //     Assert.AreEqual(1000, gasBalance);
    // }
    //
    // private void InitializeBNEO()
    // {
    //     Engine.SetTransactionSigners(SuperAdmin);
    //
    //     BNEOContract = Engine.Deploy<BurgerNEO>(
    //         BurgerNEO.Nef,
    //         BurgerNEO.Manifest
    //     );
    //
    //     var bNeoOwner = BNEOContract.Owner();
    //     Assert.AreEqual(SuperAdmin.Account, bNeoOwner);
    //
    //     for (int i = 0; i <= 21; i++)
    //     {
    //         var agentAccount = TestEngine.GetNewSigner();
    //         Engine.SetTransactionSigners(agentAccount);
    //         var agentContract = Engine.Deploy<BurgerAgent>(BurgerAgent.Nef, BurgerAgent.Manifest);
    //         BurgerAgents.Add(agentContract);
    //
    //         Engine.SetTransactionSigners(SuperAdmin);
    //         BNEOContract.SetAgent(i, agentContract.Hash);
    //     }
    //
    //     Engine.SetTransactionSigners(SuperAdmin);
    //
    //     var transferResult = Engine.Native.NEO.Transfer(SuperAdmin.Account, BNEOContract.Hash, 100);
    //     Assert.IsTrue(transferResult);
    //     var bNEOBalance = BNEOContract.BalanceOf(SuperAdmin.Account);
    //     Assert.AreEqual(100_00000000, bNEOBalance);
    // }
    //
    // private void InitializeFToken()
    // {
    //     Engine.SetTransactionSigners(SuperAdmin);
    //
    //     FUSDContract = Engine.Deploy<FUSD>(
    //         FToken.Nef,
    //         FToken.Manifest
    //     );
    //
    //     FUSDContract.VaultScriptHash = Contract.Hash;
    //
    //     var vaultScriptHash = FUSDContract.VaultScriptHash;
    //
    //     Assert.AreEqual(Contract.Hash, vaultScriptHash);
    // }
    //
    // private void InitializePriceFeed()
    // {
    //     Engine.SetTransactionSigners(SuperAdmin);
    //
    //     PriceFeedContract = Engine.Deploy<FlamingoPriceFeed>(
    //         FlamingoPriceFeed.Nef,
    //         FlamingoPriceFeed.Manifest
    //     );
    //
    //     PriceFeedContract.FLMHash = FLMContract.Hash;
    //     PriceFeedContract.FLUNDHash = FLUNDContract.Hash;
    //     PriceFeedContract.SwapFactoryHash = SwapFactoryContract.Hash;
    //
    //     PriceFeedContract.SetPath(FUSDContract.Hash, fUSDTContract.Hash, new object[] {FUSDContract.Hash, fUSDTContract.Hash});
    //     PriceFeedContract.SetPath(BNEOContract.Hash, fUSDTContract.Hash, new object[] {BNEOContract.Hash, fUSDTContract.Hash});
    //     PriceFeedContract.SetPath(fWBTCContract.Hash, fUSDTContract.Hash, new object[] {fWBTCContract.Hash, fUSDTContract.Hash});
    //     PriceFeedContract.SetPath(FLMContract.Hash, fUSDTContract.Hash, new object[] {FLMContract.Hash, fUSDTContract.Hash});
    // }
    //
    // private void InitializeFTokenVault()
    // {
    //     Engine.SetTransactionSigners(SuperAdmin);
    //
    //     Contract.FLUNDHash = FLUNDContract.Hash;
    //     Contract.BNEOHash = BNEOContract.Hash;
    //     Contract.QuoteTokenHash = fUSDTContract.Hash;
    //     Contract.PriceFeedHash = PriceFeedContract.Hash;
    //
    //     ECPoint signer = ECPoint.Parse(SuperAdminPublicKey, ECCurve.Secp256r1);
    //     Contract.SetSigner(signer.ToArray());
    //
    //     Contract.GasAdmin = SuperAdmin.Account;
    //     Contract.LRBFundAdmin = SuperAdmin.Account;
    //     Contract.SecurityFundAdmin = SuperAdmin.Account;
    //
    //     Contract.SupportCollateral(fWBTCContract.Hash);
    //     Contract.SupportCollateral(BNEOContract.Hash);
    //     Contract.SupportCollateral(FLUNDContract.Hash);
    //     Contract.SupportFToken(FUSDContract.Hash);
    //
    //     Contract.SetLiquidationBonus(fWBTCContract.Hash, 5);
    //     Contract.SetLiquidationBonus(BNEOContract.Hash, 5);
    //     Contract.SetLiquidationBonus(FLUNDContract.Hash, 5);
    //
    //     Contract.SetAnnualInterest(fWBTCContract.Hash, 6);
    //     Contract.SetAnnualInterest(BNEOContract.Hash, 4);
    //     Contract.SetAnnualInterest(FLUNDContract.Hash, 6);
    // }

    // [TestMethod]
    // public void TestInitialize()
    // {
    //     Initialize();
    // }

    // [TestMethod]
    // public void TakeOutLoanTest()
    // {
    //     Initialize();
    //
    //     BigInteger amount = 1_00000000;
    //     ECPoint signer = ECPoint.Parse(SuperAdminPublicKey, ECCurve.Secp256r1);
    //
    //     // Example payload:
    //     // {
    //     //   "payload": "eyJwcmljZXMiOiB7ImJORU8iOiAiMSIsICJGTE0iOiAiNTUwNjU1NjY1NTc1ODQxNzM1MiIsICJmV0JUQyI6ICIzNDY0OTQ1NjMxNTA4MjY1NTE1OTU0MzEwIiwgImZXQlRDX3YyIjogIjM0NjQ5NDU2MzE1MDgyNjU1MTU5NTQzMTAiLCAiR0FTIjogMSwgImZXRVRIIjogMSwgImZXRVRIX3YyIjogMX0sICJleHBpcmVzIjogMTcyNTM1MjYzMCwgImRlY2ltYWxzIjogMjB9",
    //     //   "signature": "koMJs+ZH4BJLnlg82eL+TcP4zS7Ag/kg9o9puJAfMvkte+Dv99GqU+v2twkB6qT8kSQ3jHunyqYkzqMnl+tkag==",
    //     //   "data": {
    //     //     "prices": {
    //     //       "fWBTC": "3464945631508265515954310",
    //     //     },
    //     //     "expires": 1725352630,
    //     //     "decimals": 20
    //     //   }
    //     // }
    //
    //     var jsonObject = new JObject();
    //     jsonObject["prices"] = new JObject();
    //     jsonObject["prices"]["fWBTC"] = "3464945631508265515954310";
    //     jsonObject["expires"] = 1725352630;
    //     jsonObject["decimals"] = 20;
    //
    //     var dataJsonString = System.Text.Encoding.UTF8.GetBytes(jsonObject.ToString());
    //     var tester = Neo.Wallets.Wallet.GetPrivateKeyFromWIF(SuperAdminPrivateKey);
    //     var signature = Crypto.Sign(dataJsonString, tester);
    //     var json64 = Convert.ToBase64String(dataJsonString);
    //     var signature64 = Convert.ToBase64String(signature);
    //
    //     fWBTCContract.Transfer(Alice.Account, Contract.Hash, 1_000000000);
    //     Contract.MintFToken(fWBTCContract.Hash, FUSDContract.Hash, Alice.Account, amount, json64, signature64);
    // }
}
