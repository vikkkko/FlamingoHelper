using System.Numerics;
using Neo;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Testing;
using Neo.SmartContract.Testing.Coverage;
using Neo.VM.Types;

namespace Flamingo.OrderBook.Tests;

[TestClass]
public abstract class FlamingoTestSuiteBase<T> : DebugTestBase<T> where T : SmartContract, IContractInfo
{
    protected FlamingoTestSuiteBase(
        NefFile nefFile,
        ContractManifest manifestFile,
        NeoDebugInfo? debugInfo = null
    )
        : base(nefFile, manifestFile, debugInfo)
    {
    }

    public Signer SuperAdmin;

    // Tokens
    public FLM FLMContract;
    public fUSDT fUSDTContract;
    public fWBTC fWBTCContract;
    public FLPfWBTCfUSDT FLPfWBTCfUSDTContract;

    // Other Contracts
    public FlamingoSwapFactory SwapFactoryContract;
    public FlamingoSwapRouter SwapRouterContract;
    public FlamingoSwapPairWhiteList SwapPairWhiteListContract;
    public FlamingoStaking StakingContract;

    protected override TestEngine CreateTestEngine()
    {
        // Set the correct values fot the native policy contract
        var testEngine = base.CreateTestEngine();
        testEngine.SetTransactionSigners(testEngine.CommitteeAddress);
        testEngine.Native.Policy.ExecFeeFactor = 1;
        testEngine.Native.Policy.StoragePrice = 1000;
        testEngine.Native.Policy.FeePerByte = 20;

        // Reset
        testEngine.SetTransactionSigners(Alice);

        // Setup the test environment
        SetupSuperAdminAccount();
        SetupSwapFactory(testEngine);
        SetupSwapRouter(testEngine);
        SetupSwapPairWhiteList(testEngine);
        SetupTokens(testEngine);
        SetupSwapPairs(testEngine);
        SetupStaking(testEngine);

        SetupTestAccounts(testEngine);
        AddLiquidityToSwapRouter(testEngine);

        // Cleanup Signers
        testEngine.SetTransactionSigners(Alice);

        return testEngine;
    }

    private void SetupSuperAdminAccount()
    {
        // Address: NL1JGjDe22U44R57ZXVSeRa4T7Jo1HDLF4
        SuperAdmin = new Signer()
        {
            Account = UInt160.Parse("0x6288e8a5e3d92f6aa645e6f749b4cb6cc024b211"),
            Scopes = WitnessScope.Global,
        };
    }

    private void SetupSwapFactory(TestEngine testEngine)
    {
        // Arrange
        SwapFactoryContract = testEngine.Deploy<FlamingoSwapFactory>(
            FlamingoSwapFactory.Nef,
            FlamingoSwapFactory.Manifest
        );

        // Act
        var admin = SwapFactoryContract.GetAdmin();

        // Assert
        Assert.AreEqual(SuperAdmin.Account, admin);
    }

    private void SetupSwapRouter(TestEngine testEngine)
    {
        // Arrange
        testEngine.SetTransactionSigners(SuperAdmin);
        SwapRouterContract = testEngine.Deploy<FlamingoSwapRouter>(FlamingoSwapRouter.Nef, FlamingoSwapRouter.Manifest);

        // Act
        var result = SwapRouterContract.SetFactory(SwapFactoryContract.Hash);
        var newFactory = SwapRouterContract.GetFactory();
        var superAdmin = SwapFactoryContract.GetAdmin();

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(SwapFactoryContract.Hash, newFactory);
        Assert.AreEqual(SuperAdmin.Account, superAdmin);

        // Cleanup Signers
        testEngine.SetTransactionSigners(Alice);
    }

    private void SetupSwapPairWhiteList(TestEngine testEngine)
    {
        // Arrange
        testEngine.SetTransactionSigners(SuperAdmin);

        // Act
        SwapPairWhiteListContract =
            testEngine.Deploy<FlamingoSwapPairWhiteList>(FlamingoSwapPairWhiteList.Nef,
                FlamingoSwapPairWhiteList.Manifest);
        var superAdmin = SwapPairWhiteListContract.GetAdmin();
        var addRouterResult = SwapPairWhiteListContract.AddRouter(SwapRouterContract.Hash);
        var isRouterResult = SwapPairWhiteListContract.CheckRouter(SwapRouterContract.Hash);

        // Assert
        Assert.AreEqual(SuperAdmin.Account, superAdmin);
        Assert.IsTrue(addRouterResult);
        Assert.IsTrue(isRouterResult);

        // Cleanup Signers
        testEngine.SetTransactionSigners(Alice);
    }

    private void SetupTokens(TestEngine testEngine)
    {
        BigInteger oneMillion = 1_000_000_00000000;

        // Arrange
        testEngine.SetTransactionSigners(SuperAdmin);
        fUSDTContract = testEngine.Deploy<fUSDT>(fUSDT.Nef, fUSDT.Manifest);
        fWBTCContract = testEngine.Deploy<fWBTC>(fWBTC.Nef, fWBTC.Manifest);

        // Console.WriteLine("fWBTCContract.Hash: " + fWBTCContract.Hash);
        // Console.WriteLine("fUSDTContract.Hash: " + fUSDTContract.Hash);
        // Console.WriteLine("whiteListContract.Hash: " + SwapPairWhiteListContract.Hash);

        // Act
        fUSDTContract.Mint(SuperAdmin.Account, SuperAdmin.Account, oneMillion);
        fWBTCContract.Mint(SuperAdmin.Account, SuperAdmin.Account, oneMillion);
        var fUSDTBalance = fUSDTContract.BalanceOf(SuperAdmin.Account);
        var fWBTCBalance = fWBTCContract.BalanceOf(SuperAdmin.Account);

        // Assert
        Assert.AreEqual(oneMillion, fUSDTBalance);
        Assert.AreEqual(oneMillion, fWBTCBalance);

        // Cleanup Signers
        testEngine.SetTransactionSigners(Alice);
    }

    private void SetupSwapPairs(TestEngine testEngine)
    {
        // Arrange
        testEngine.SetTransactionSigners(SuperAdmin);

        FLPfWBTCfUSDTContract = testEngine.Deploy<FLPfWBTCfUSDT>(FLPfWBTCfUSDT.Nef, FLPfWBTCfUSDT.Manifest);
        var originalToken0 = fWBTCContract.Hash < fUSDTContract.Hash ? fWBTCContract.Hash : fUSDTContract.Hash;
        var originalToken1 = fWBTCContract.Hash < fUSDTContract.Hash ? fUSDTContract.Hash : fWBTCContract.Hash;
        var token0 = FLPfWBTCfUSDTContract.Token0;
        var token1 = FLPfWBTCfUSDTContract.Token1;
        Assert.AreEqual(originalToken0, token0);
        Assert.AreEqual(originalToken1, token1);

        var result = SwapFactoryContract.RegisterExchangePair(FLPfWBTCfUSDTContract.Hash);
        Assert.IsTrue(result);

        var pairAsBytes = SwapFactoryContract.GetExchangePair(fWBTCContract.Hash, fUSDTContract.Hash);
        var pair = UInt160.Parse(pairAsBytes.ToHexString(reverse: true));

        FLPfWBTCfUSDTContract.SetWhiteListContract(SwapPairWhiteListContract.Hash);
        var whiteListContract = FLPfWBTCfUSDTContract.GetWhiteListContract();

        Assert.AreEqual(FLPfWBTCfUSDTContract.Hash, pair);
        Assert.AreEqual(SwapPairWhiteListContract.Hash, whiteListContract);
    }

    private void SetupStaking(TestEngine testEngine)
    {
        // Arrange
        testEngine.SetTransactionSigners(SuperAdmin);
        FLMContract = testEngine.Deploy<FLM>(FLM.Nef, FLM.Manifest);
        StakingContract = testEngine.Deploy<FlamingoStaking>(FlamingoStaking.Nef, FlamingoStaking.Manifest);

        // Act
        var owner = StakingContract.Owner;
        var flmOwner = FLMContract.Owner;

        StakingContract.AddAuthor(SuperAdmin.Account);
        var isAuthor = StakingContract.IsAuthor(SuperAdmin.Account);

        StakingContract.SetFLMAddress(FLMContract.Hash, SuperAdmin.Account);
        var flmAddress = StakingContract.FLMAddress;

        StakingContract.AddAsset(FLPfWBTCfUSDTContract.Hash, SuperAdmin.Account);
        var isInWhiteList = StakingContract.IsInWhiteList(FLPfWBTCfUSDTContract.Hash);

        // Assert
        Assert.AreEqual(SuperAdmin.Account, owner);
        Assert.AreEqual(SuperAdmin.Account, flmOwner);
        Assert.IsTrue(isAuthor);
        Assert.AreEqual(FLMContract.Hash, flmAddress);
        Assert.IsTrue(isInWhiteList);

        // Cleanup Signers
        testEngine.SetTransactionSigners(Alice);
    }

    private void SetupTestAccounts(TestEngine testEngine)
    {
        // Give some coins to both Alice and Bob for testing purposes
        BigInteger oneMillion = 1_000_000_00000000;

        testEngine.SetTransactionSigners(SuperAdmin);

        fUSDTContract.Mint(SuperAdmin.Account, Alice.Account, oneMillion);
        fWBTCContract.Mint(SuperAdmin.Account, Alice.Account, oneMillion);
        var fUSDTBalanceAlice = fUSDTContract.BalanceOf(Alice.Account);
        var fWBTCBalanceAlice = fWBTCContract.BalanceOf(Alice.Account);

        Assert.AreEqual(oneMillion, fUSDTBalanceAlice);
        Assert.AreEqual(oneMillion, fWBTCBalanceAlice);

        fUSDTContract.Mint(SuperAdmin.Account, Bob.Account, oneMillion);
        fWBTCContract.Mint(SuperAdmin.Account, Bob.Account, oneMillion);
        var fUSDTBalanceBob = fUSDTContract.BalanceOf(Bob.Account);
        var fWBTCBalanceBob = fWBTCContract.BalanceOf(Bob.Account);

        Assert.AreEqual(oneMillion, fUSDTBalanceBob);
        Assert.AreEqual(oneMillion, fWBTCBalanceBob);

        // Cleanup Signers
        testEngine.SetTransactionSigners(Alice);
    }

    private void AddLiquidityToSwapRouter(TestEngine testEngine)
    {
        // Arrange
        var baseLiqudityfUSDT = 100_000_000000; // 100000000000
        var baseLiqudityfWBTC = 10_00000000;// 1000000000

        // Act
        testEngine.SetTransactionSigners(SuperAdmin);

        fUSDTContract.Mint(SuperAdmin.Account, SuperAdmin.Account, baseLiqudityfUSDT);
        fWBTCContract.Mint(SuperAdmin.Account, SuperAdmin.Account, baseLiqudityfWBTC);

        fUSDTContract.Transfer(SuperAdmin.Account, Bob.Account, baseLiqudityfUSDT);
        fWBTCContract.Transfer(SuperAdmin.Account, Bob.Account, baseLiqudityfWBTC);

        testEngine.SetTransactionSigners(WitnessScope.Global, Bob.Account);

        const long deadline = 9999999999999;
        SwapRouterContract.AddLiquidity(
            Bob.Account,
            fWBTCContract.Hash,
            fUSDTContract.Hash,
            baseLiqudityfWBTC,
            baseLiqudityfUSDT,
            0,
            0,
            deadline
        );

        // Cleanup Signers
        testEngine.SetTransactionSigners(Alice);
    }
}
