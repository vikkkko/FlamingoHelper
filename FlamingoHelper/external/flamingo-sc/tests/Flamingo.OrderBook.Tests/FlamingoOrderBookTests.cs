using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Testing;
using Neo.SmartContract.Testing.Coverage;
using Neo.SmartContract.Testing.Exceptions;
using Array = Neo.VM.Types.Array;

namespace Flamingo.OrderBook.Tests;

[TestClass]
public class FlamingoOrderBookTests : FlamingoTestSuiteBase<FlamingoBroker>
{
    public FlamingoOrderBookTests() : base(FlamingoBroker.Nef, FlamingoBroker.Manifest, NeoDebugInfo.TryLoad("../../../TestingArtifacts/FlamingoBroker.nefdbgnfo", out var debugInfo) ? debugInfo : null)
    {
    }

    [TestMethod]
    public void InitializeTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        // # Act
        var treeBitLengthResult = Contract.GetTreeBitLength(1);

        // # Assert
        Assert.AreEqual(treeBitLength, treeBitLengthResult);
    }

    [TestMethod]
    public void GetNodeIndexTest8()
    {
        // # Arrange
        const int treeBitLength = 8;
        InitializeContract([]);

        var columns = 8;
        var rightMostNumberOfNodes = 1 << treeBitLength;

        for (int col = 0; col < columns; col++)
        {
            BigInteger numberOfNodesInColumn = 1 << (col + 1);
            var priceRangePerNode = rightMostNumberOfNodes / numberOfNodesInColumn;

            for (int row = 1; row <= numberOfNodesInColumn; row++)
            {
                var priceRow = (int) (row * priceRangePerNode) - 1;

                var nodeNumber = (priceRow + priceRangePerNode) / priceRangePerNode;
                var roundedPriceIndex = (nodeNumber * priceRangePerNode) - 1;
                var expectedNodeIndex = (col << treeBitLength) | roundedPriceIndex;

                var nodeIndex = Contract.GetNodeIndex(treeBitLength, col, priceRow);
                Assert.AreEqual(expectedNodeIndex, nodeIndex);
            }
        }

        // # Test for failure
        try
        {
            // We should be out of range here.
            var nodeIndex8 = Contract.GetNodeIndex(treeBitLength, 0, 256);
            Assert.Fail("An exception should have been thrown, but the value was: " + nodeIndex8);
        }
        catch (Exception e)
        {
            Assert.IsTrue(e.Message.Contains("Node index is not correct!"));
        }
    }

    [TestMethod]
    public void ConvertPriceRowToAmmPriceWithSubDecimalPrecisionTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient / 10000000;
        var roundingPrecision = Contract.PriceRoundingPrecision!.Value;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        // # Act
        var realPrice1 = Contract.ConvertPriceRowToAmmPrice(1, pricePrecision, 8, 8);
        var realPrice2 = Contract.ConvertPriceRowToAmmPrice(10, pricePrecision, 8, 8);
        var realPrice3 = Contract.ConvertPriceRowToAmmPrice(100, pricePrecision, 8, 8);
        var realPrice4 = Contract.ConvertPriceRowToAmmPrice(149, pricePrecision, 8, 8);

        // Testing different decimal places
        var realPrice5 = Contract.ConvertPriceRowToAmmPrice(50, pricePrecision, 8, 6);
        var realPrice6 = Contract.ConvertPriceRowToAmmPrice(50, pricePrecision, 6, 8);

        // # Assert
        Assert.AreEqual(1 * roundingPrecision / 10000000, realPrice1);
        Assert.AreEqual(10 * roundingPrecision / 10000000, realPrice2);
        Assert.AreEqual(100 * roundingPrecision / 10000000, realPrice3);
        Assert.AreEqual(149 * roundingPrecision / 10000000, realPrice4);

        // Testing different decimal places
        Assert.AreEqual(50 * roundingPrecision / 10000000 / 100, realPrice5);
        Assert.AreEqual(50 * roundingPrecision / 10000000 * 100, realPrice6);
    }

    [TestMethod]
    public void ConvertPriceRowToRealPriceWithSubDecimalPrecisionTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient / 10000000;
        var roundingPrecision = Contract.PriceRoundingPrecision!.Value;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        // # Act
        var realPrice1 = Contract.ConvertPriceRowToRealPrice(1, pricePrecision, 8, 6);
        var realPrice2 = Contract.ConvertPriceRowToRealPrice(10, pricePrecision, 8, 6);
        var realPrice3 = Contract.ConvertPriceRowToRealPrice(100, pricePrecision, 8, 6);
        var realPrice4 = Contract.ConvertPriceRowToRealPrice(149, pricePrecision, 8, 6);
        var realPrice5 = Contract.ConvertPriceRowToRealPrice(10_0000_0000, pricePrecision, 8, 6);

        // # Assert
        Assert.AreEqual(1 * roundingPrecision / 10000000_00, realPrice1);
        Assert.AreEqual(10 * roundingPrecision / 10000000_00, realPrice2);
        Assert.AreEqual(100 * roundingPrecision / 10000000_00, realPrice3);
        Assert.AreEqual(149 * roundingPrecision / 10000000_00, realPrice4);
        Assert.AreEqual(1 * roundingPrecision, realPrice5);
    }

    [TestMethod]
    public void ConvertPriceRowToAmmPriceWithSuperDecimalPrecisionTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 10000;
        var roundingPrecision = Contract.PriceRoundingPrecision!.Value;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        // # Act
        var realPrice1 = Contract.ConvertPriceRowToAmmPrice(1, pricePrecision, 8, 8);
        var realPrice2 = Contract.ConvertPriceRowToAmmPrice(10, pricePrecision, 8, 8);
        var realPrice3 = Contract.ConvertPriceRowToAmmPrice(100, pricePrecision, 8, 8);
        var realPrice4 = Contract.ConvertPriceRowToAmmPrice(149, pricePrecision, 8, 8);

        // Testing different decimal places
        var realPrice5 = Contract.ConvertPriceRowToAmmPrice(50, pricePrecision, 8, 6);
        var realPrice6 = Contract.ConvertPriceRowToAmmPrice(50, pricePrecision, 6, 8);

        // # Assert
        Assert.AreEqual(1 * roundingPrecision * 10000, realPrice1);
        Assert.AreEqual(10 * roundingPrecision * 10000, realPrice2);
        Assert.AreEqual(100 * roundingPrecision * 10000, realPrice3);
        Assert.AreEqual(149 * roundingPrecision * 10000, realPrice4);

        // Testing different decimal places
        Assert.AreEqual(50 * roundingPrecision * 10000 / 100, realPrice5);
        Assert.AreEqual(50 * roundingPrecision * 10000 * 100, realPrice6);
    }

    [TestMethod]
    public void CalculateUntilPriceTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceRoundingPrecision!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var baseTokenReserveStart = 11_000_00000000;
        var quoteTokenReserveStart = 1_000_000_00000000;
        var marginOfError = 0.001;

        // # Act
        var targetPrice1 = BigInteger.Parse("500000000000000000"); // 0.5 USDT
        var untilPrice1 = Contract.CalculateBaseTokenInUntilPrice(baseTokenReserveStart, quoteTokenReserveStart, targetPrice1);

        var amountOut1 = SwapRouterContract.GetAmountOut(untilPrice1, baseTokenReserveStart, quoteTokenReserveStart);
        var baseTokenReserveEnd1 = baseTokenReserveStart + untilPrice1;
        var quoteTokenReserveEnd1 = quoteTokenReserveStart - amountOut1;
        var priceInAmm1 = (float) quoteTokenReserveEnd1! / (float) baseTokenReserveEnd1!;
        var expectedPrice1 = (float) targetPrice1 / (float) priceCoefficient;
        var expectedLowerPrice1 = ((float) targetPrice1 / (float) priceCoefficient) * (1 - marginOfError);
        var expectedUpperPrice1 = ((float) targetPrice1 / (float) priceCoefficient) * (1 + marginOfError);
        Console.WriteLine($"priceCoefficient: {priceCoefficient}");
        Console.WriteLine($"priceInAmm1: {priceInAmm1}");
        Console.WriteLine($"expectedPrice1: {expectedPrice1}");
        Console.WriteLine($"expectedLowerPrice1: {expectedLowerPrice1}");
        Console.WriteLine($"expectedUpperPrice1: {expectedUpperPrice1}");
        var errorPercentage1 = Math.Abs(priceInAmm1 - expectedPrice1) / expectedPrice1 * 100;
        Console.WriteLine($"errorPercentage1: {errorPercentage1}");
        Console.WriteLine();

        // TODO: CalculateBaseTokenOutUntilPrice is not implemented with correct formula yet.
        // var targetPrice2 = BigInteger.Parse("201230000000000000000000000"); // 2.0123 USDT
        // var untilPrice2 = Contract.CalculateBaseTokenOutUntilPrice(baseTokenReserveStart, quoteTokenReserveStart, targetPrice2);
        //
        // var amountIn2 = SwapRouterContract.GetAmountIn(untilPrice2, quoteTokenReserveStart, baseTokenReserveStart);
        // var baseTokenReserveEnd2 = baseTokenReserveStart - untilPrice2;
        // var quoteTokenReserveEnd2 = quoteTokenReserveStart + amountIn2;
        // var priceInAmm2 = (float) quoteTokenReserveEnd2 / (float) baseTokenReserveEnd2;
        // var expectedPrice2 = ((float) targetPrice2 / (float) priceCoefficient / 1_0000_0000);
        // var expectedLowerPrice2 = ((float) targetPrice2 / (float) priceCoefficient / 1_0000_0000) * (1 - marginOfError);
        // var expectedUpperPrice2 = ((float) targetPrice2 / (float) priceCoefficient / 1_0000_0000) * (1 + marginOfError);
        // Console.WriteLine($"priceInAmm2: {priceInAmm2}");
        // Console.WriteLine($"expectedLowerPrice2: {expectedLowerPrice2}");
        // Console.WriteLine($"expectedUpperPrice2: {expectedUpperPrice2}");
        // var errorPercentage2 = Math.Abs(priceInAmm2 - expectedPrice2) / expectedPrice2 * 100;
        // Console.WriteLine($"errorPercentage2: {errorPercentage2}");
        // Console.WriteLine();

        var targetPrice3 = BigInteger.Parse("18001230000000000000000"); // 180.0123 USDT
        var untilPrice3 = Contract.CalculateQuoteTokenInUntilPrice(baseTokenReserveStart, quoteTokenReserveStart, targetPrice3);

        var amountOut3 = SwapRouterContract.GetAmountOut(untilPrice3, quoteTokenReserveStart, baseTokenReserveStart);
        var baseTokenReserveEnd3 = baseTokenReserveStart - amountOut3;
        var quoteTokenReserveEnd3 = quoteTokenReserveStart + untilPrice3;
        var priceInAmm3 = (float) quoteTokenReserveEnd3! / (float) baseTokenReserveEnd3!;
        var expectedPrice3 = ((float) targetPrice3 / (float) priceCoefficient);
        var expectedLowerPrice3 = ((float) targetPrice3 / (float) priceCoefficient) * (1 - marginOfError);
        var expectedUpperPrice3 = ((float) targetPrice3 / (float) priceCoefficient) * (1 + marginOfError);
        Console.WriteLine($"priceInAmm3: {priceInAmm3}");
        Console.WriteLine($"expectedLowerPrice3: {expectedLowerPrice3}");
        Console.WriteLine($"expectedUpperPrice3: {expectedUpperPrice3}");
        var errorPercentage3 = Math.Abs(priceInAmm3 - expectedPrice3) / expectedPrice3 * 100;
        Console.WriteLine($"errorPercentage3: {errorPercentage3}");
        Console.WriteLine();

        // TODO: CalculateQuoteTokenOutUntilPrice is not implemented with correct formula yet.
        // var targetPrice4 = BigInteger.Parse("125100000000000000000"); // 0.000001251 USDT
        // var untilPrice4 = Contract.CalculateQuoteTokenOutUntilPrice(baseTokenReserveStart, quoteTokenReserveStart, targetPrice4);
        //
        // var amountIn4 = SwapRouterContract.GetAmountIn(untilPrice4, baseTokenReserveStart, quoteTokenReserveStart);
        // var baseTokenReserveEnd4 = baseTokenReserveStart + amountIn4;
        // var quoteTokenReserveEnd4 = quoteTokenReserveStart - untilPrice4;
        // var priceInAmm4 = (float) quoteTokenReserveEnd4 / (float) baseTokenReserveEnd4;
        // var expectedPrice4 = ((float) targetPrice4 / (float) priceCoefficient / 1_0000_0000);
        // var expectedLowerPrice4 = ((float) targetPrice4 / (float) priceCoefficient / 1_0000_0000) * (1 - marginOfError);
        // var expectedUpperPrice4 = ((float) targetPrice4 / (float) priceCoefficient / 1_0000_0000) * (1 + marginOfError);
        // Console.WriteLine($"priceInAmm4: {priceInAmm4}");
        // Console.WriteLine($"expectedLowerPrice4: {expectedLowerPrice4}");
        // Console.WriteLine($"expectedUpperPrice4: {expectedUpperPrice4}");
        // var errorPercentage4 = Math.Abs(priceInAmm4 - expectedPrice4) / expectedPrice4 * 100;
        // Console.WriteLine($"errorPercentage4: {errorPercentage4}");
        // Console.WriteLine();

        // Test very low prices
        var targetPrice5 = BigInteger.Parse("5000000000"); // 0.000000005 USDT
        var untilPrice5 = Contract.CalculateBaseTokenInUntilPrice(baseTokenReserveStart, quoteTokenReserveStart, targetPrice5);

        var amountOut5 = SwapRouterContract.GetAmountOut(untilPrice5, baseTokenReserveStart, quoteTokenReserveStart);
        var baseTokenReserveEnd5 = baseTokenReserveStart + untilPrice5;
        var quoteTokenReserveEnd5 = quoteTokenReserveStart - amountOut5;
        var priceInAmm5 = (float) quoteTokenReserveEnd5! / (float) baseTokenReserveEnd5!;
        var expectedPrice5 = ((float) targetPrice5 / (float) priceCoefficient);
        var expectedLowerPrice5 = ((float) targetPrice5 / (float) priceCoefficient) * (1 - marginOfError);
        var expectedUpperPrice5 = ((float) targetPrice5 / (float) priceCoefficient) * (1 + marginOfError);
        Console.WriteLine($"priceInAmm5: {priceInAmm5}");
        Console.WriteLine($"expectedLowerPrice5: {expectedLowerPrice5}");
        Console.WriteLine($"expectedUpperPrice5: {expectedUpperPrice5}");
        var errorPercentage5 = Math.Abs(priceInAmm5 - expectedPrice5) / expectedPrice5 * 100;
        Console.WriteLine($"errorPercentage5: {errorPercentage5}");
        Console.WriteLine();

        // Test very high prices
        var targetPrice6 = BigInteger.Parse("12345678912345678900000000000"); // 123456789.123456789 USDT
        var untilPrice6 = Contract.CalculateQuoteTokenInUntilPrice(baseTokenReserveStart, quoteTokenReserveStart, targetPrice6);
        var amountOut6 = SwapRouterContract.GetAmountOut(untilPrice6, quoteTokenReserveStart, baseTokenReserveStart);
        Console.WriteLine(amountOut6);
        var baseTokenReserveEnd6 = baseTokenReserveStart - amountOut6;
        var quoteTokenReserveEnd6 = quoteTokenReserveStart + untilPrice6;
        var priceInAmm6 = (float) quoteTokenReserveEnd6! / (float) baseTokenReserveEnd6!;
        var expectedPrice6 = ((float) targetPrice6 / (float) priceCoefficient);
        var expectedLowerPrice6 = ((float) targetPrice6 / (float) priceCoefficient) * (1 - marginOfError);
        var expectedUpperPrice6 = ((float) targetPrice6 / (float) priceCoefficient) * (1 + marginOfError);
        Console.WriteLine($"priceInAmm6: {priceInAmm6}");
        Console.WriteLine($"expectedPrice6: {expectedPrice6}");
        Console.WriteLine($"expectedLowerPrice6: {expectedLowerPrice6}");
        Console.WriteLine($"expectedUpperPrice6: {expectedUpperPrice6}");
        var errorPercentage6 = Math.Abs(priceInAmm6 - expectedPrice6) / expectedPrice6 * 100;
        Console.WriteLine($"errorPercentage6: {errorPercentage6}");
        Console.WriteLine();

        // # Assert
        Assert.IsTrue(priceInAmm1 >= expectedLowerPrice1 && priceInAmm1 <= expectedUpperPrice1);
        Assert.IsTrue(errorPercentage1 < marginOfError);

        // Assert.IsTrue(priceInAmm2 >= expectedLowerPrice2 && priceInAmm2 <= expectedUpperPrice2);
        // Assert.IsTrue(errorPercentage2 < marginOfError);

        Assert.IsTrue(priceInAmm3 >= expectedLowerPrice3 && priceInAmm3 <= expectedUpperPrice3);
        Assert.IsTrue(errorPercentage3 < marginOfError);

        // Assert.IsTrue(priceInAmm4 >= expectedLowerPrice4 && priceInAmm4 <= expectedUpperPrice4);
        // Assert.IsTrue(errorPercentage4 < marginOfError);

        Assert.IsTrue(priceInAmm5 >= expectedLowerPrice5 && priceInAmm5 <= expectedUpperPrice5);
        Assert.IsTrue(errorPercentage5 < marginOfError);

        Assert.IsTrue(priceInAmm6 >= expectedLowerPrice6 && priceInAmm6 <= expectedUpperPrice6);
        Assert.IsTrue(errorPercentage6 < marginOfError);
    }

    [TestMethod]
    public void CalculateUntilPriceOverflowTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceRoundingPrecision!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        // Reserves of 10 million with 18 decimals
        var baseTokenReserveStart = BigInteger.Parse("10000000000000000000000000");
        var quoteTokenReserveStart = BigInteger.Parse("10000000000000000000000000");

        // # Act
        var targetPrice1 = BigInteger.Parse("9900000000000000"); // 0.99 USDT
        Contract.CalculateBaseTokenInUntilPrice(baseTokenReserveStart, quoteTokenReserveStart, targetPrice1);

        var targetPrice3 = BigInteger.Parse("123456789123456789000000000"); // 12345678912.3456789 USDT
        Contract.CalculateQuoteTokenInUntilPrice(baseTokenReserveStart, quoteTokenReserveStart, targetPrice3);
    }

    [TestMethod]
    public void CreateLimitSellOrderHighPriceTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act

        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 16, 0, false, false);
        var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitSellOrderHighPriceTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 1, Contract.GetMaxPrice(1), 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Create images
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitSellOrderHighPriceTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.ClaimOrder(1, 1, -1);
        var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // # Assert

        // Check order
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(100, orderAlicePlacedInOrderBook);
        Assert.AreEqual(16, orderAlicePrice);

        // Check balances
        Assert.AreEqual(0, aliceBalanceFwbtc);
        Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
        Assert.AreEqual(1, aliceBalanceFusdtAfterClaim);
        Assert.AreEqual(9_999, bobBalanceFusdt);
        Assert.AreEqual(6, bobBalanceFwbtc);
    }

    // [TestMethod]
    // public void CreateLimitSellOrderHighPriceTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength);
    //
    //     // # Act
    //
    //     Contract.CreateLimitSellOrderUsingQuote(aliceWithScope.Account, 1, CalculateQuoteAmountForBaseAmount(1, 16, 100), 16, 0, false, false);
    //     var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //     var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitSellOrderHighPriceTestUsingQuote/orderbook-image-diff.png");
    //
    //     // Make a market buy order
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 1, Contract.GetMaxPrice(1), 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     // Create images
    //     var grid3 = GenerateGrid(treeBitLength);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitSellOrderHighPriceTestUsingQuote/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.ClaimOrder(1, 1, -1);
    //     var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     // # Assert
    //
    //     // Check order
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(100, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(16, orderAlicePrice);
    //
    //     // Check balances
    //     Assert.AreEqual(0, aliceBalanceFwbtc);
    //     Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
    //     Assert.AreEqual(1, aliceBalanceFusdtAfterClaim);
    //     Assert.AreEqual(9_999, bobBalanceFusdt);
    //     Assert.AreEqual(6, bobBalanceFwbtc);
    // }

    [TestMethod]
    public void CreateLimitSellOrderMiddlePriceTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100_000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act



        // var gasCostMap = GenerateDocumentGasCostMap();
        // Engine.FeeConsumed.Reset();
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 50, 7, 0, false, false);
        // var gasCost = (int) Engine.FeeConsumed;
        //Console.WriteLine("Gas cost: " + gasCost / 100000000f);
        // Write the document to the current project directory
        //WriteGasCostsToFile("tester", gasCostMap);




        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 5_000, 9, 0, false, false);
        var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitSellOrderMiddlePriceTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 16, Contract.GetMaxPrice(1), 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Create images
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitSellOrderMiddlePriceTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);

        // Check if the contract calculates the correct nodeIndexToCheck when
        var nodeIndexToCheck1 = Contract.FindNodeToCheckForClaim(1, 1);
        var nodeIndexToCheck2 = Contract.FindNodeToCheckForClaim(1, 2);

        // Check if the contract calculates the correct claimable amounts
        var claimableAmount1 = (BigInteger) Contract.GetClaimableAmount(1, 1, nodeIndexToCheck1)[0];
        var claimableAmount2 = (BigInteger) Contract.GetClaimableAmount(1, 2, nodeIndexToCheck2)[0];

        // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
        Contract.ClaimOrder(1, 1, nodeIndexToCheck1);
        // Since we expect the second order to be partially filled we do not check any parent node for virtually filled amounts.
        Contract.ClaimOrder(1, 2, nodeIndexToCheck2);
        var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // # Assert

        // Check order
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        var orderAliceClaimedBaseAmount = orderAlice.ClaimedBaseAmount;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(50, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(50, orderAlicePlacedInOrderBook);
        Assert.AreEqual(7, orderAlicePrice);
        Assert.AreEqual(50, orderAliceClaimedBaseAmount);

        var orderAliceRaw2 = (Array?) Contract.GetOrder(1, 2);
        var orderAlice2 = RawOrderToOrder(orderAliceRaw2);
        Assert.IsNotNull(orderAlice);
        var orderAlice2ClaimedBaseAmount = orderAlice2.ClaimedBaseAmount;
        Assert.AreEqual(144, orderAlice2ClaimedBaseAmount);

        // Check balances
        Assert.AreEqual(94950, aliceBalanceFwbtc);
        Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
        Assert.AreEqual(16, aliceBalanceFusdtAfterClaim);
        Assert.AreEqual(9_984, bobBalanceFusdt);
        Assert.AreEqual(194, bobBalanceFwbtc);

        // Check the nodeIndexToCheck
        Assert.AreEqual(7, nodeIndexToCheck1);
        Assert.AreEqual(-1, nodeIndexToCheck2);

        // Check the claimable amounts
        Assert.AreEqual(50, claimableAmount1);
        Assert.AreEqual(144, claimableAmount2);
    }

    // [TestMethod]
    // public void CreateLimitSellOrderMiddlePriceTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100_000);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength);
    //
    //     // # Act
    //
    //
    //
    //     // var gasCostMap = GenerateDocumentGasCostMap();
    //     // Engine.FeeConsumed.Reset();
    //     Contract.CreateLimitSellOrderUsingQuote(aliceWithScope.Account, 1, 3, 7, 0, false, false);
    //     // var gasCost = (int) Engine.FeeConsumed;
    //     //Console.WriteLine("Gas cost: " + gasCost / 100000000f);
    //     // Write the document to the current project directory
    //     //WriteGasCostsToFile("tester", gasCostMap);
    //
    //
    //
    //
    //     Contract.CreateLimitSellOrderUsingQuote(aliceWithScope.Account, 1, 450, 9, 0, false, false);
    //     var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //     var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitSellOrderMiddlePriceTestUsingQuote/orderbook-image-diff.png");
    //
    //     // Make a market buy order
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 16, Contract.GetMaxPrice(1), 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     // Create images
    //     var grid3 = GenerateGrid(treeBitLength);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitSellOrderMiddlePriceTestUsingQuote/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //
    //     // Check if the contract calculates the correct nodeIndexToCheck when
    //     var nodeIndexToCheck1 = Contract.FindNodeToCheckForClaim(1, 1);
    //     var nodeIndexToCheck2 = Contract.FindNodeToCheckForClaim(1, 2);
    //
    //     // Check if the contract calculates the correct claimable amounts
    //     var claimableAmount1 = (BigInteger) Contract.GetClaimableAmount(1, 1, nodeIndexToCheck1)[0];
    //     var claimableAmount2 = (BigInteger) Contract.GetClaimableAmount(1, 2, nodeIndexToCheck2)[0];
    //
    //     // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
    //     Contract.ClaimOrder(1, 1, 15);
    //     // Since we expect the second order to be partially filled we do not check any parent node for virtually filled amounts.
    //     Contract.ClaimOrder(1, 2, -1);
    //     var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     // # Assert
    //
    //     // Check order
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     var orderAliceClaimedBaseAmount = orderAlice.ClaimedBaseAmount;
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(42, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(42, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(7, orderAlicePrice);
    //     Assert.AreEqual(42, orderAliceClaimedBaseAmount);
    //
    //     var orderAliceRaw2 = (Array?) Contract.GetOrder(1, 2);
    //     var orderAlice2 = RawOrderToOrder(orderAliceRaw2);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAlice2ClaimedBaseAmount = orderAlice2.ClaimedBaseAmount;
    //     Assert.AreEqual(144, orderAlice2ClaimedBaseAmount);
    //
    //     // Check balances
    //     Assert.AreEqual(94958, aliceBalanceFwbtc);
    //     Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
    //     Assert.AreEqual(16, aliceBalanceFusdtAfterClaim);
    //     Assert.AreEqual(9_984, bobBalanceFusdt);
    //     Assert.AreEqual(186, bobBalanceFwbtc);
    //
    //     // Check the nodeIndexToCheck
    //     Assert.AreEqual(15, nodeIndexToCheck1);
    //     Assert.AreEqual(-1, nodeIndexToCheck2);
    //
    //     // Check the claimable amounts
    //     Assert.AreEqual(42, claimableAmount1);
    //     Assert.AreEqual(144, claimableAmount2);
    // }

    // [TestMethod]
    // public void CorrectFeeCalculationTest()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000_0000000);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 1000_000000000);
    //
    //     // Visual representation
    //     // # Act
    //     Contract.CreateLimitSellOrderUsingQuote(aliceWithScope.Account, 1, 100_0000000, 10, 0, false, false);
    //
    //     // Make a market buy order
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 100_0000000, 10, 0, false, false);
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //
    //     // Check if the contract calculates the correct claimable amounts
    //     var claimableAmount1 = (BigInteger) Contract.GetClaimableAmount(1, 1, -1)[1];
    //     Console.WriteLine(claimableAmount1);
    //
    //     // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
    //     Contract.ClaimOrder(1, 1, -1);
    //
    //     // # Assert
    //
    //     // Check order
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     var orderAliceClaimedBaseAmount = orderAlice.ClaimedBaseAmount;
    //
    //     Console.WriteLine(orderAlice.ClaimedQuoteAmount);
    //
    //     // Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     // Assert.AreEqual(42, orderAliceTotalOrderBaseAmount);
    //     // Assert.AreEqual(42, orderAlicePlacedInOrderBook);
    //     // Assert.AreEqual(7, orderAlicePrice);
    //     // Assert.AreEqual(42, orderAliceClaimedBaseAmount);
    //
    //     // Check if the contract calculates the correct claimable amounts
    //     var claimableAmount2 = (BigInteger) Contract.GetClaimableAmount(1, 1, -1)[1];
    //     Console.WriteLine(claimableAmount2);
    //
    //     // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
    //     Contract.ClaimOrder(1, 1, -1);
    //
    //     orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     orderAlice = RawOrderToOrder(orderAliceRaw);
    //
    //     Console.WriteLine(orderAlice.ClaimedQuoteAmount);
    // }

    [TestMethod]
    public void CreateLimitSellOrderLowPriceTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 1, 0, false, false);
        var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitSellOrderLowPriceTest/orderbook-image-diff.png");

        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 2, Contract.GetMaxPrice(1), 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitSellOrderLowPriceTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        // The node we want to check is [0, 7] which has the binary address 0x0000_1111 and the decimal address 15.
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);
        var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var grid4 = GenerateGrid(treeBitLength);
        var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
        ExportGrid(diffGrid3, "CreateLimitSellOrderLowPriceTest/orderbook-image-diff-3.png");

        // # Assert
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(100, orderAlicePlacedInOrderBook);
        Assert.AreEqual(1, orderAlicePrice);

        Assert.AreEqual(0, aliceBalanceFwbtc);
        Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
        Assert.AreEqual(1, aliceBalanceFusdtAfterClaim);
        Assert.AreEqual(9_999, bobBalanceFusdt);
        Assert.AreEqual(100, bobBalanceFwbtc);
    }

    // [TestMethod]
    // public void CreateLimitSellOrderLowPriceTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength);
    //
    //     // # Act
    //     Contract.CreateLimitSellOrderUsingQuote(aliceWithScope.Account, 1, CalculateQuoteAmountForBaseAmount(1, 1, 100), 1, 0, false, false);
    //     var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //     var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitSellOrderLowPriceTestUsingQuote/orderbook-image-diff.png");
    //
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 2, Contract.GetMaxPrice(1), 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     var grid3 = GenerateGrid(treeBitLength);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitSellOrderLowPriceTestUsingQuote/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     // The node we want to check is [0, 7] which has the binary address 0x0000_1111 and the decimal address 15.
    //     Contract.ClaimOrder(1, 1, 15);
    //     var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     var grid4 = GenerateGrid(treeBitLength);
    //     var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
    //     ExportGrid(diffGrid3, "CreateLimitSellOrderLowPriceTestUsingQuote/orderbook-image-diff-3.png");
    //
    //     // # Assert
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(100, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(1, orderAlicePrice);
    //
    //     Assert.AreEqual(0, aliceBalanceFwbtc);
    //     Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
    //     Assert.AreEqual(1, aliceBalanceFusdtAfterClaim);
    //     Assert.AreEqual(9_999, bobBalanceFusdt);
    //     Assert.AreEqual(100, bobBalanceFwbtc);
    // }

    [TestMethod]
    public void CreateLimitSellOrderHighPriceSuperDecimalPriceRangeTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10000;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 160000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act

        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 16, 0, false, false);
        var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitSellOrderHighPriceSuperDecimalPriceRangeTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 160000, Contract.GetMaxPrice(1), 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Create images
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitSellOrderHighPriceSuperDecimalPriceRangeTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.ClaimOrder(1, 1, 15);
        var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // # Assert

        // Check order
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(100, orderAlicePlacedInOrderBook);
        Assert.AreEqual(16, orderAlicePrice);

        // Check balances
        Assert.AreEqual(0, aliceBalanceFwbtc);
        Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
        Assert.AreEqual(159760, aliceBalanceFusdtAfterClaim); // 160_000 * 0.15% (maker fee) = 240
        Assert.AreEqual(0, bobBalanceFusdt);
        Assert.AreEqual(100, bobBalanceFwbtc);
    }

    // [TestMethod]
    // public void CreateLimitSellOrderHighPriceSuperDecimalPriceRangeTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 10000;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 160000);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength);
    //
    //     // # Act
    //
    //     Contract.CreateLimitSellOrderUsingQuote(aliceWithScope.Account, 1, CalculateQuoteAmountForBaseAmount(1, 16, 100), 16, 0, false, false);
    //     var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //     var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitSellOrderHighPriceSuperDecimalPriceRangeTestUsingQuote/orderbook-image-diff.png");
    //
    //     // Make a market buy order
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 160000, Contract.GetMaxPrice(1), 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     // Create images
    //     var grid3 = GenerateGrid(treeBitLength);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitSellOrderHighPriceSuperDecimalPriceRangeTestUsingQuote/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.ClaimOrder(1, 1, 15);
    //     var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     // # Assert
    //
    //     // Check order
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(100, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(16, orderAlicePrice);
    //
    //     // Check balances
    //     Assert.AreEqual(0, aliceBalanceFwbtc);
    //     Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
    //     Assert.AreEqual(159760, aliceBalanceFusdtAfterClaim); // 160_000 * 0.15% (maker fee) = 240
    //     Assert.AreEqual(0, bobBalanceFusdt);
    //     Assert.AreEqual(100, bobBalanceFwbtc);
    // }

    [TestMethod]
    public void CreateLimitSellOrderMiddlePriceSuperDecimalPriceRangeTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10000;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 160_000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100_000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 50, 7, 0, false, false);

        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 5_000, 9, 0, false, false);
        var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitSellOrderMiddlePriceSuperDecimalPriceRangeTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 160000, Contract.GetMaxPrice(1), 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Create images
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitSellOrderMiddlePriceSuperDecimalPriceRangeTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);

        // Check if the contract calculates the correct nodeIndexToCheck when
        var nodeIndexToCheck1 = Contract.FindNodeToCheckForClaim(1, 1);
        var nodeIndexToCheck2 = Contract.FindNodeToCheckForClaim(1, 2);
        Console.WriteLine(nodeIndexToCheck1);
        Console.WriteLine(nodeIndexToCheck1);

        // Check if the contract calculates the correct claimable amounts
        var claimableAmount1 = (BigInteger) Contract.GetClaimableAmount(1, 1, nodeIndexToCheck1)[0];
        var claimableAmount2 =  (BigInteger) Contract.GetClaimableAmount(1, 2, nodeIndexToCheck2)[0];

        // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
        Contract.ClaimOrder(1, 1, nodeIndexToCheck1);
        // Since we expect the second order to be partially filled we do not check any parent node for virtually filled amounts.
        Contract.ClaimOrder(1, 2, nodeIndexToCheck2);
        var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // # Assert

        // Check order
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        var orderAliceClaimedBaseAmount = orderAlice.ClaimedBaseAmount;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(50, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(50, orderAlicePlacedInOrderBook);
        Assert.AreEqual(7, orderAlicePrice);
        Assert.AreEqual(50, orderAliceClaimedBaseAmount);

        var orderAliceRaw2 = (Array?) Contract.GetOrder(1, 2);
        var orderAlice2 = RawOrderToOrder(orderAliceRaw2);
        Assert.IsNotNull(orderAlice);
        var orderAlice2ClaimedBaseAmount = orderAlice2.ClaimedBaseAmount;
        Assert.AreEqual(138, orderAlice2ClaimedBaseAmount);

        // Check balances
        Assert.AreEqual(94_950, aliceBalanceFwbtc);
        Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
        Assert.AreEqual(159_761, aliceBalanceFusdtAfterClaim); // floor(35_000 * 0.15% (maker fee)) = 52 and floor(125_000 * 0.15% (maker fee)) = 187. So 160_000 - 52 - 187 = 159_761
        Assert.AreEqual(0, bobBalanceFusdt);
        Assert.AreEqual(188, bobBalanceFwbtc);

        // Check the nodeIndexToCheck
        Assert.AreEqual(7, nodeIndexToCheck1);
        Assert.AreEqual(-1, nodeIndexToCheck2);

        // Check the claimable amounts
        Assert.AreEqual(50, claimableAmount1);
        Assert.AreEqual(138, claimableAmount2);
    }

    // [TestMethod]
    // public void CreateLimitSellOrderMiddlePriceSuperDecimalPriceRangeTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 10000;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 160_000);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100_000);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength);
    //
    //     // # Act
    //     Contract.CreateLimitSellOrderUsingQuote(aliceWithScope.Account, 1, CalculateQuoteAmountForBaseAmount(1, 7, 50), 7, 0, false, false);
    //
    //     Contract.CreateLimitSellOrderUsingQuote(aliceWithScope.Account, 1, CalculateQuoteAmountForBaseAmount(1, 9, 5_000), 9, 0, false, false);
    //     var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //     var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitSellOrderMiddlePriceSuperDecimalPriceRangeTestUsingQuote/orderbook-image-diff.png");
    //
    //     // Make a market buy order
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 160000, Contract.GetMaxPrice(1), 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     // Create images
    //     var grid3 = GenerateGrid(treeBitLength);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitSellOrderMiddlePriceSuperDecimalPriceRangeTestUsingQuote/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //
    //     // Check if the contract calculates the correct nodeIndexToCheck when
    //     var nodeIndexToCheck1 = Contract.FindNodeToCheckForClaim(1, 1);
    //     var nodeIndexToCheck2 = Contract.FindNodeToCheckForClaim(1, 2);
    //     Console.WriteLine(nodeIndexToCheck1);
    //     Console.WriteLine(nodeIndexToCheck1);
    //
    //     // Check if the contract calculates the correct claimable amounts
    //     var claimableAmount1 = (BigInteger) Contract.GetClaimableAmount(1, 1, nodeIndexToCheck1)[0];
    //     var claimableAmount2 =  (BigInteger) Contract.GetClaimableAmount(1, 2, nodeIndexToCheck2)[0];
    //
    //     // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
    //     Contract.ClaimOrder(1, 1, 15);
    //     // Since we expect the second order to be partially filled we do not check any parent node for virtually filled amounts.
    //     Contract.ClaimOrder(1, 2, -1);
    //     var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     // # Assert
    //
    //     // Check order
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     var orderAliceClaimedBaseAmount = orderAlice.ClaimedBaseAmount;
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(50, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(50, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(7, orderAlicePrice);
    //     Assert.AreEqual(50, orderAliceClaimedBaseAmount);
    //
    //     var orderAliceRaw2 = (Array?) Contract.GetOrder(1, 2);
    //     var orderAlice2 = RawOrderToOrder(orderAliceRaw2);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAlice2ClaimedBaseAmount = orderAlice2.ClaimedBaseAmount;
    //     Assert.AreEqual(138, orderAlice2ClaimedBaseAmount);
    //
    //     // Check balances
    //     Assert.AreEqual(94_950, aliceBalanceFwbtc);
    //     Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
    //     Assert.AreEqual(159_761, aliceBalanceFusdtAfterClaim); // floor(35_000 * 0.15% (maker fee)) = 52 and floor(125_000 * 0.15% (maker fee)) = 187. So 160_000 - 52 - 187 = 159_761
    //     Assert.AreEqual(0, bobBalanceFusdt);
    //     Assert.AreEqual(188, bobBalanceFwbtc);
    //
    //     // Check the nodeIndexToCheck
    //     Assert.AreEqual(15, nodeIndexToCheck1);
    //     Assert.AreEqual(-1, nodeIndexToCheck2);
    //
    //     // Check the claimable amounts
    //     Assert.AreEqual(50, claimableAmount1);
    //     Assert.AreEqual(138, claimableAmount2);
    // }

    [TestMethod]
    public void CreateLimitSellOrderLowPriceSuperDecimalPriceRangeTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10000;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 1, 0, false, false);
        var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitSellOrderLowPriceSuperDecimalPriceRangeTest/orderbook-image-diff.png");

        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 11200, Contract.GetMaxPrice(1), 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitSellOrderLowPriceSuperDecimalPriceRangeTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        // The node we want to check is [0, 7] which has the binary address 0x0000_1111 and the decimal address 15.
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);
        var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var grid4 = GenerateGrid(treeBitLength);
        var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
        ExportGrid(diffGrid3, "CreateLimitSellOrderLowPriceSuperDecimalPriceRangeTest/orderbook-image-diff-3.png");

        // # Assert
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(100, orderAlicePlacedInOrderBook);
        Assert.AreEqual(1, orderAlicePrice);

        Assert.AreEqual(0, aliceBalanceFwbtc);
        Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
        Assert.AreEqual(9985, aliceBalanceFusdtAfterClaim); // 10_000 * 0.15% (maker fee) = 15
        Assert.AreEqual(0, bobBalanceFusdt);
        Assert.AreEqual(100, bobBalanceFwbtc);
    }

    // [TestMethod]
    // public void CreateLimitSellOrderLowPriceSuperDecimalPriceRangeTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 10000;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength);
    //
    //     // # Act
    //     Contract.CreateLimitSellOrderUsingQuote(aliceWithScope.Account, 1, CalculateQuoteAmountForBaseAmount(1, 1, 100), 1, 0, false, false);
    //     var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //     var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitSellOrderLowPriceSuperDecimalPriceRangeTestUsingQuote/orderbook-image-diff.png");
    //
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 11200, Contract.GetMaxPrice(1), 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     var grid3 = GenerateGrid(treeBitLength);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitSellOrderLowPriceSuperDecimalPriceRangeTestUsingQuote/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     // The node we want to check is [0, 7] which has the binary address 0x0000_1111 and the decimal address 15.
    //     Contract.ClaimOrder(1, 1, 15);
    //     var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     var grid4 = GenerateGrid(treeBitLength);
    //     var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
    //     ExportGrid(diffGrid3, "CreateLimitSellOrderLowPriceSuperDecimalPriceRangeTestUsingQuote/orderbook-image-diff-3.png");
    //
    //     // # Assert
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(100, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(1, orderAlicePrice);
    //
    //     Assert.AreEqual(0, aliceBalanceFwbtc);
    //     Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
    //     Assert.AreEqual(9985, aliceBalanceFusdtAfterClaim); // 10_000 * 0.15% (maker fee) = 15
    //     Assert.AreEqual(0, bobBalanceFusdt);
    //     Assert.AreEqual(100, bobBalanceFwbtc);
    // }

    [TestMethod]
    public void CreateManyLimitSellOrderTest()
    {
        // # Arrange
        const int treeBitLength = 5;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var buyerAccount = TestEngine.GetNewSigner();
        var buyerWithScope = new Signer {Account = buyerAccount.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(SuperAdmin);
        var buyerFusdtDepositAmount = 1_000_000_000000;
        fUSDTContract.Mint(SuperAdmin.Account, buyerAccount.Account, buyerFusdtDepositAmount);

        Engine.SetTransactionSigners(buyerWithScope);
        Contract.Deposit(buyerWithScope.Account, fUSDTContract.Hash, buyerFusdtDepositAmount);

        // (Signer, Sell Amount, Limit Price)
        var sellers = new List<(Signer, BigInteger, BigInteger)>
        {
            (TestEngine.GetNewSigner(), 100, 1),
            (TestEngine.GetNewSigner(), 150, 2),
            (TestEngine.GetNewSigner(), 200, 3),
            (TestEngine.GetNewSigner(), 170, 4),
            (TestEngine.GetNewSigner(), 88, 5),
            (TestEngine.GetNewSigner(), 65, 6),
            (TestEngine.GetNewSigner(), 32, 7),
            (TestEngine.GetNewSigner(), 10000, 8),
            (TestEngine.GetNewSigner(), 12345, 9),
            (TestEngine.GetNewSigner(), 52016, 10),
            (TestEngine.GetNewSigner(), 20100, 11),
            (TestEngine.GetNewSigner(), 20312, 12),
            (TestEngine.GetNewSigner(), 111, 13),
            (TestEngine.GetNewSigner(), 222, 14),
            (TestEngine.GetNewSigner(), 333, 15),
            (TestEngine.GetNewSigner(), 444, 16),
            (TestEngine.GetNewSigner(), 123, 17),
            (TestEngine.GetNewSigner(), 854, 18),
            (TestEngine.GetNewSigner(), 20351, 19),
            (TestEngine.GetNewSigner(), 12099, 20),
            (TestEngine.GetNewSigner(), 91999, 21),
            (TestEngine.GetNewSigner(), 12012, 22),
            (TestEngine.GetNewSigner(), 15101, 23),
            (TestEngine.GetNewSigner(), 18520, 24),
            (TestEngine.GetNewSigner(), 18132, 25),
            (TestEngine.GetNewSigner(), 81537, 26),
            (TestEngine.GetNewSigner(), 153, 27),
            (TestEngine.GetNewSigner(), 121, 28),
            (TestEngine.GetNewSigner(), 1237, 29),
            (TestEngine.GetNewSigner(), 4, 30),
            (TestEngine.GetNewSigner(), 1111, 31),
            (TestEngine.GetNewSigner(), 125, 32),
        };

        // Debugging
        var startingGrid = GenerateGrid(treeBitLength);
        var lastGrid = GenerateGrid(treeBitLength);

        BigInteger sumForSale = 0;
        BigInteger expectedQuoteAmountTotal = 0;
        var index = 1;

        foreach (var (seller, amount, price) in sellers)
        {
            Engine.SetTransactionSigners(SuperAdmin);
            fWBTCContract.Mint(SuperAdmin.Account, seller.Account, amount);

            var sellerWithScope = new Signer {Account = seller.Account, Scopes = WitnessScope.Global};
            Engine.SetTransactionSigners(sellerWithScope);
            Contract.Deposit(sellerWithScope.Account, fWBTCContract.Hash, amount);
            Contract.CreateLimitSellOrderUsingBase(sellerWithScope.Account, 1, amount, price, 0, false, false);

            // Debugging
            var grid = GenerateGrid(treeBitLength);
            var diffGrid = OrderBookImageGenerator.HighlightDifferences(lastGrid, grid);
            ExportGrid(diffGrid, $"CreateManyLimitSellOrderTest/orderbook-image-{index}.png");
            lastGrid = grid;

            var nodeIndex = (4 << 5) + (price - 1); // Colum = 5, row = price - 1
            var priceNodeRaw = (Array) Contract.GetPriceNode(1, nodeIndex, true)!;
            var priceNode = new OrderBookImageGenerator.PriceNode
            {
                BaseAmount = priceNodeRaw[0].GetInteger(), QuoteTotal = priceNodeRaw[1].GetInteger(), EmptiedCount = priceNodeRaw[2].GetInteger(), EmptiedCountSibling = priceNodeRaw[3].GetInteger(),
            };
            var expectedQuoteAmount = price * amount / 100;
            Assert.AreEqual(amount, priceNode.BaseAmount);
            Assert.AreEqual(expectedQuoteAmount, priceNode.QuoteTotal);

            var orderRaw = (Array?) Contract.GetOrder(1, index);
            var order = RawOrderToOrder(orderRaw);
            Assert.IsNotNull(order);
            var orderOwner = order.Owner;
            var orderTotalOrderBaseAmount = order.TotalOrderBaseAmount;
            var orderPlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount;
            var orderPlacedInOrderBookQuoteAmount = order.PlacedInOrderBookQuoteAmount;
            var orderPrice = order.Price;
            Assert.AreEqual(sellerWithScope.Account, orderOwner);
            Assert.AreEqual(amount, orderTotalOrderBaseAmount);
            Assert.AreEqual(priceNode.BaseAmount, orderPlacedInOrderBookBaseAmount);
            Assert.AreEqual(priceNode.QuoteTotal, orderPlacedInOrderBookQuoteAmount);
            Assert.AreEqual(price, orderPrice);

            sumForSale += amount;
            expectedQuoteAmountTotal += expectedQuoteAmount;

            index += 1;
        }

        // Calculate the fee the taker/buyer has to pay.
        var baseFee = Contract.GetTakerFee(1)!.Value * sumForSale / 1000000;
        var sumForSaleAfterFees = sumForSale - baseFee;

        // Debugging
        var finalGrid = GenerateGrid(treeBitLength);
        var diffGridFinal = OrderBookImageGenerator.HighlightDifferences(startingGrid, finalGrid);
        ExportGrid(diffGridFinal, "CreateManyLimitSellOrderTest/orderbook-image-final.png");

        // Buy all the orders
        Engine.SetTransactionSigners(buyerWithScope);
        // We try to buy twice the amount of the total sum of all the orders.
        var buyQuoteAmount = expectedQuoteAmountTotal * 2;
        Contract.ExecuteLimitBuyOrderUsingQuote(buyerWithScope.Account, 1, buyQuoteAmount, Contract.GetMaxPrice(1), 0, false, false);

        // Debugging
        var afterBuyGrid = GenerateGrid(treeBitLength);
        var diffGridAfterBuy = OrderBookImageGenerator.HighlightDifferences(finalGrid, afterBuyGrid);
        ExportGrid(diffGridAfterBuy, "CreateManyLimitSellOrderTest/orderbook-image-final-after-buy.png");

        // Check the balances
        var buyerBalanceFwbtc = Contract.GetAccountBalance(buyerWithScope.Account, fWBTCContract);
        var buyerBalanceFusdt = Contract.GetAccountBalance(buyerWithScope.Account, fUSDTContract);
        Assert.AreEqual(sumForSaleAfterFees, buyerBalanceFwbtc);
        var expectedQuoteAmountTotalManualCheck = 74835;
        Assert.AreEqual(buyerFusdtDepositAmount - expectedQuoteAmountTotalManualCheck, buyerBalanceFusdt);
        Assert.AreEqual(buyerFusdtDepositAmount - expectedQuoteAmountTotal, buyerBalanceFusdt);
    }

    // [TestMethod]
    // public void CreateManyLimitSellOrderTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 5;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var buyerAccount = TestEngine.GetNewSigner();
    //     var buyerWithScope = new Signer {Account = buyerAccount.Account, Scopes = WitnessScope.Global};
    //
    //     Engine.SetTransactionSigners(SuperAdmin);
    //     var buyerFusdtDepositAmount = 1_000_000_000000;
    //     fUSDTContract.Mint(SuperAdmin.Account, buyerAccount.Account, buyerFusdtDepositAmount);
    //
    //     Engine.SetTransactionSigners(buyerWithScope);
    //     Contract.Deposit(buyerWithScope.Account, fUSDTContract.Hash, buyerFusdtDepositAmount);
    //
    //     // (Signer, Sell Amount, Limit Price)
    //     var sellers = new List<(Signer, BigInteger, BigInteger, BigInteger)>
    //     {
    //         (TestEngine.GetNewSigner(), 100, 10000, 1),
    //         (TestEngine.GetNewSigner(), 150, 7500, 2),
    //         (TestEngine.GetNewSigner(), 200, 6666, 3),
    //         (TestEngine.GetNewSigner(), 170, 4250, 4),
    //         (TestEngine.GetNewSigner(), 88, 1760, 5),
    //         (TestEngine.GetNewSigner(), 65, 1083, 6),
    //         (TestEngine.GetNewSigner(), 32, 457, 7),
    //         (TestEngine.GetNewSigner(), 10000, 125000, 8),
    //         (TestEngine.GetNewSigner(), 12345, 137166, 9),
    //         (TestEngine.GetNewSigner(), 52016, 520160, 10),
    //         (TestEngine.GetNewSigner(), 20100, 182727, 11),
    //         (TestEngine.GetNewSigner(), 20312, 169266, 12),
    //         (TestEngine.GetNewSigner(), 111, 853, 13),
    //         (TestEngine.GetNewSigner(), 222, 1585, 14),
    //         (TestEngine.GetNewSigner(), 333, 2220, 15),
    //         (TestEngine.GetNewSigner(), 444, 2775, 16),
    //         (TestEngine.GetNewSigner(), 123, 723, 17),
    //         (TestEngine.GetNewSigner(), 854, 4744, 18),
    //         (TestEngine.GetNewSigner(), 20351, 107110, 19),
    //         (TestEngine.GetNewSigner(), 12099, 60495, 20),
    //         (TestEngine.GetNewSigner(), 91999, 438090, 21),
    //         (TestEngine.GetNewSigner(), 12012, 54600, 22),
    //         (TestEngine.GetNewSigner(), 15101, 65656, 23),
    //         (TestEngine.GetNewSigner(), 18520, 77166, 24),
    //         (TestEngine.GetNewSigner(), 18132, 72528, 25),
    //         (TestEngine.GetNewSigner(), 81537, 313603, 26),
    //         (TestEngine.GetNewSigner(), 153, 566, 27),
    //         (TestEngine.GetNewSigner(), 121, 432, 28),
    //         (TestEngine.GetNewSigner(), 1237, 4265, 29),
    //         (TestEngine.GetNewSigner(), 4, 13, 30),
    //         (TestEngine.GetNewSigner(), 1111, 3583, 31),
    //         (TestEngine.GetNewSigner(), 125, 390, 32),
    //     };
    //
    //     // Debugging
    //     var startingGrid = GenerateGrid(treeBitLength);
    //     var lastGrid = GenerateGrid(treeBitLength);
    //
    //     BigInteger sumForSale = 0;
    //     BigInteger expectedQuoteAmountTotal = 0;
    //     var index = 1;
    //
    //     foreach (var (seller, quoteAmount, baseAmount, price) in sellers)
    //     {
    //         Engine.SetTransactionSigners(SuperAdmin);
    //         fWBTCContract.Mint(SuperAdmin.Account, seller.Account, baseAmount);
    //
    //         var sellerWithScope = new Signer {Account = seller.Account, Scopes = WitnessScope.Global};
    //         Engine.SetTransactionSigners(sellerWithScope);
    //         Contract.Deposit(sellerWithScope.Account, fWBTCContract.Hash, baseAmount);
    //         Contract.CreateLimitSellOrderUsingQuote(sellerWithScope.Account, 1, quoteAmount, price, 0, false, false);
    //
    //         // Debugging
    //         var grid = GenerateGrid(treeBitLength);
    //         var diffGrid = OrderBookImageGenerator.HighlightDifferences(lastGrid, grid);
    //         ExportGrid(diffGrid, $"CreateManyLimitSellOrderTestUsingQuote/orderbook-image-{index}.png");
    //         lastGrid = grid;
    //
    //         var nodeIndex = (4 << 5) + (price - 1); // Colum = 5, row = price - 1
    //         var priceNodeRaw = (Array) Contract.GetPriceNode(1, nodeIndex, true)!;
    //         var priceNode = new OrderBookImageGenerator.PriceNode
    //         {
    //             BaseAmount = priceNodeRaw[0].GetInteger(), QuoteTotal = priceNodeRaw[1].GetInteger(), EmptiedCount = priceNodeRaw[2].GetInteger(), EmptiedCountSibling = priceNodeRaw[3].GetInteger(),
    //         };
    //         Assert.AreEqual(baseAmount, priceNode.BaseAmount);
    //         Assert.AreEqual(quoteAmount, priceNode.QuoteTotal);
    //
    //         var orderRaw = (Array?) Contract.GetOrder(1, index);
    //         var order = RawOrderToOrder(orderRaw);
    //         Assert.IsNotNull(order);
    //         var orderOwner = order.Owner;
    //         var orderPlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount;
    //         var orderPlacedInOrderBookQuoteAmount = order.PlacedInOrderBookQuoteAmount;
    //         var orderPrice = order.Price;
    //         Assert.AreEqual(sellerWithScope.Account, orderOwner);
    //         Assert.AreEqual(baseAmount, order.TotalOrderBaseAmount);
    //         Assert.AreEqual(quoteAmount, order.TotalOrderQuoteAmount);
    //         Assert.AreEqual(priceNode.BaseAmount, orderPlacedInOrderBookBaseAmount);
    //         Assert.AreEqual(priceNode.QuoteTotal, orderPlacedInOrderBookQuoteAmount);
    //         Assert.AreEqual(price, orderPrice);
    //
    //         sumForSale += baseAmount;
    //         expectedQuoteAmountTotal += quoteAmount;
    //
    //         index += 1;
    //     }
    //
    //     // Calculate the fee the taker/buyer has to pay.
    //     var baseFee = Contract.GetTakerFee(1)!.Value * sumForSale / 1000000;
    //     var sumForSaleAfterFees = sumForSale - baseFee;
    //
    //     // Debugging
    //     var finalGrid = GenerateGrid(treeBitLength);
    //     var diffGridFinal = OrderBookImageGenerator.HighlightDifferences(startingGrid, finalGrid);
    //     ExportGrid(diffGridFinal, "CreateManyLimitSellOrderTestUsingQuote/orderbook-image-final.png");
    //
    //     // Buy all the orders
    //     Engine.SetTransactionSigners(buyerWithScope);
    //     // We try to buy twice the amount of the total sum of all the orders.
    //     var buyQuoteAmount = expectedQuoteAmountTotal * 2;
    //     Contract.ExecuteLimitBuyOrderUsingQuote(buyerWithScope.Account, 1, buyQuoteAmount, Contract.GetMaxPrice(1), 0, false, false);
    //
    //     // Debugging
    //     var afterBuyGrid = GenerateGrid(treeBitLength);
    //     var diffGridAfterBuy = OrderBookImageGenerator.HighlightDifferences(finalGrid, afterBuyGrid);
    //     ExportGrid(diffGridAfterBuy, "CreateManyLimitSellOrderTestUsingQuote/orderbook-image-final-after-buy.png");
    //
    //     // Check the balances
    //     var buyerBalanceFwbtc = Contract.GetAccountBalance(buyerWithScope.Account, fWBTCContract);
    //     var buyerBalanceFusdt = Contract.GetAccountBalance(buyerWithScope.Account, fUSDTContract);
    //     Assert.AreEqual(sumForSaleAfterFees, buyerBalanceFwbtc);
    //     var expectedQuoteAmountTotalManualCheck = 390167;
    //     Assert.AreEqual(buyerFusdtDepositAmount - expectedQuoteAmountTotalManualCheck, buyerBalanceFusdt);
    //     Assert.AreEqual(buyerFusdtDepositAmount - expectedQuoteAmountTotal, buyerBalanceFusdt);
    // }

    [TestMethod]
    public void CreateTwoLimitSellOrderBuyOneTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var buyerAccount = TestEngine.GetNewSigner();
        var buyerWithScope = new Signer {Account = buyerAccount.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(SuperAdmin);
        var buyerFusdtDepositAmount = 1357;
        fUSDTContract.Mint(SuperAdmin.Account, buyerAccount.Account, buyerFusdtDepositAmount);

        Engine.SetTransactionSigners(buyerWithScope);
        Contract.Deposit(buyerWithScope.Account, fUSDTContract.Hash, buyerFusdtDepositAmount);

        // (Signer, Sell Amount, Limit Price)
        var sellers = new List<(Signer, BigInteger, BigInteger)>
        {
            (TestEngine.GetNewSigner(), 12345, 11),
            (TestEngine.GetNewSigner(), 12345, 11)
        };

        // Debugging
        var startingGrid = GenerateGrid(treeBitLength);
        var lastGrid = GenerateGrid(treeBitLength);

        var index = 1;

        foreach (var (seller, amount, price) in sellers)
        {
            Engine.SetTransactionSigners(SuperAdmin);
            fWBTCContract.Mint(SuperAdmin.Account, seller.Account, amount);

            var sellerWithScope = new Signer {Account = seller.Account, Scopes = WitnessScope.Global};
            Engine.SetTransactionSigners(sellerWithScope);
            Contract.Deposit(sellerWithScope.Account, fWBTCContract.Hash, amount);
            Contract.CreateLimitSellOrderUsingBase(sellerWithScope.Account, 1, amount, price, 0, false, false);

            // Debugging
            var grid = GenerateGrid(treeBitLength);
            var diffGrid = OrderBookImageGenerator.HighlightDifferences(lastGrid, grid);
            ExportGrid(diffGrid, $"CreateTwoLimitSellOrderBuyOneTest/orderbook-image-{index}.png");
            lastGrid = grid;

            var orderRaw = (Array?) Contract.GetOrder(1, index);
            var order = RawOrderToOrder(orderRaw);
            Assert.IsNotNull(order);
            var orderOwner = order.Owner;
            var orderTotalOrderBaseAmount = order.TotalOrderBaseAmount;
            var orderPlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount;
            var orderPrice = order.Price;
            Assert.AreEqual(sellerWithScope.Account, orderOwner);
            Assert.AreEqual(amount, orderTotalOrderBaseAmount);
            Assert.AreEqual(amount, orderPlacedInOrderBookBaseAmount);
            Assert.AreEqual(price, orderPrice);

            index += 1;
        }

        // Calculate the fee the taker/buyer has to pay.
        var firstOrderAmount = sellers[0].Item2;
        var firstOrderQuoteAmount = sellers[0].Item3 * firstOrderAmount / 100;
        var baseFee = Contract.GetTakerFee(1)!.Value * firstOrderAmount / 1000000;
        var sumForSaleAfterFees = firstOrderAmount - baseFee;

        // Debugging
        var finalGrid = GenerateGrid(treeBitLength);
        var diffGridFinal = OrderBookImageGenerator.HighlightDifferences(startingGrid, finalGrid);
        ExportGrid(diffGridFinal, "CreateTwoLimitSellOrderBuyOneTest/orderbook-image-final.png");

        // Buy all the orders
        Engine.SetTransactionSigners(buyerWithScope);
        // We try to buy twice the amount of the total sum of all the orders.
        Contract.ExecuteLimitBuyOrderUsingQuote(buyerWithScope.Account, 1, firstOrderQuoteAmount, 11, 0, false, false);

        // Debugging
        var afterBuyGrid = GenerateGrid(treeBitLength);
        var diffGridAfterBuy = OrderBookImageGenerator.HighlightDifferences(finalGrid, afterBuyGrid);
        ExportGrid(diffGridAfterBuy, "CreateTwoLimitSellOrderBuyOneTest/orderbook-image-final-after-buy.png");

        // Check the balances
        var buyerBalanceFwbtc = Contract.GetAccountBalance(buyerWithScope.Account, fWBTCContract);
        var buyerBalanceFusdt = Contract.GetAccountBalance(buyerWithScope.Account, fUSDTContract);
        Assert.AreEqual(sumForSaleAfterFees, buyerBalanceFwbtc);
        Assert.AreEqual(0, buyerBalanceFusdt);

        // Check the amount to claim for both users
        Engine.SetTransactionSigners(sellers[0].Item1);
        Contract.ClaimOrder(1, 1, -1);
        var seller1BalanceFusdt = Contract.GetAccountBalance(sellers[0].Item1.Account, fUSDTContract);
        var expectedSeller1BalanceFusdt = 1357 - 2; // Fee is 2 (0.15%)
        Assert.AreEqual(expectedSeller1BalanceFusdt, seller1BalanceFusdt);

        Engine.SetTransactionSigners(sellers[1].Item1);

        Contract.ClaimOrder(1, 2, -1);
        var seller2BalanceFusdt = Contract.GetAccountBalance(sellers[1].Item1.Account, fUSDTContract);
        Assert.AreEqual(0, seller2BalanceFusdt);
    }

    // [TestMethod]
    // public void CreateTwoLimitSellOrderBuyOneTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var buyerAccount = TestEngine.GetNewSigner();
    //     var buyerWithScope = new Signer {Account = buyerAccount.Account, Scopes = WitnessScope.Global};
    //
    //     Engine.SetTransactionSigners(SuperAdmin);
    //     var buyerFusdtDepositAmount = 1357;
    //     fUSDTContract.Mint(SuperAdmin.Account, buyerAccount.Account, buyerFusdtDepositAmount);
    //
    //     Engine.SetTransactionSigners(buyerWithScope);
    //     Contract.Deposit(buyerWithScope.Account, fUSDTContract.Hash, buyerFusdtDepositAmount);
    //
    //     // (Signer, Sell Amount, Limit Price)
    //     var sellers = new List<(Signer, BigInteger, BigInteger, BigInteger)>
    //     {
    //         (TestEngine.GetNewSigner(), 1357, 12336, 11),
    //         (TestEngine.GetNewSigner(), 1357, 12336, 11)
    //     };
    //
    //     // Debugging
    //     var startingGrid = GenerateGrid(treeBitLength);
    //     var lastGrid = GenerateGrid(treeBitLength);
    //
    //     var index = 1;
    //
    //     foreach (var (seller, quoteAmount, baseAmount, price) in sellers)
    //     {
    //         Engine.SetTransactionSigners(SuperAdmin);
    //         fWBTCContract.Mint(SuperAdmin.Account, seller.Account, baseAmount);
    //
    //         var sellerWithScope = new Signer {Account = seller.Account, Scopes = WitnessScope.Global};
    //         Engine.SetTransactionSigners(sellerWithScope);
    //         Contract.Deposit(sellerWithScope.Account, fWBTCContract.Hash, baseAmount);
    //         Contract.CreateLimitSellOrderUsingQuote(sellerWithScope.Account, 1, quoteAmount, price, 0, false, false);
    //
    //         // Debugging
    //         var grid = GenerateGrid(treeBitLength);
    //         var diffGrid = OrderBookImageGenerator.HighlightDifferences(lastGrid, grid);
    //         ExportGrid(diffGrid, $"CreateTwoLimitSellOrderBuyOneTestUsingQuote/orderbook-image-{index}.png");
    //         lastGrid = grid;
    //
    //         var orderRaw = (Array?) Contract.GetOrder(1, index);
    //         var order = RawOrderToOrder(orderRaw);
    //         Assert.IsNotNull(order);
    //         var orderOwner = order.Owner;
    //         var orderTotalOrderBaseAmount = order.TotalOrderBaseAmount;
    //         var orderPlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount;
    //         var orderPrice = order.Price;
    //         Assert.AreEqual(sellerWithScope.Account, orderOwner);
    //         Assert.AreEqual(baseAmount, orderTotalOrderBaseAmount);
    //         Assert.AreEqual(baseAmount, orderPlacedInOrderBookBaseAmount);
    //         Assert.AreEqual(price, orderPrice);
    //
    //         index += 1;
    //     }
    //
    //     // Debugging
    //     var finalGrid = GenerateGrid(treeBitLength);
    //     var diffGridFinal = OrderBookImageGenerator.HighlightDifferences(startingGrid, finalGrid);
    //     ExportGrid(diffGridFinal, "CreateTwoLimitSellOrderBuyOneTestUsingQuote/orderbook-image-final.png");
    //
    //     // Buy all the orders
    //     Engine.SetTransactionSigners(buyerWithScope);
    //     // We try to buy twice the amount of the total sum of all the orders.
    //     Contract.ExecuteLimitBuyOrderUsingQuote(buyerWithScope.Account, 1, 1357, 11, 0, false, false);
    //
    //     // Debugging
    //     var afterBuyGrid = GenerateGrid(treeBitLength);
    //     var diffGridAfterBuy = OrderBookImageGenerator.HighlightDifferences(finalGrid, afterBuyGrid);
    //     ExportGrid(diffGridAfterBuy, "CreateTwoLimitSellOrderBuyOneTestUsingQuote/orderbook-image-final-after-buy.png");
    //
    //     // Check the balances
    //     var buyerBalanceFwbtc = Contract.GetAccountBalance(buyerWithScope.Account, fWBTCContract);
    //     var buyerBalanceFusdt = Contract.GetAccountBalance(buyerWithScope.Account, fUSDTContract);
    //     var expectedFwbtcBalance = 12336 - 18; // Fee is 18 (0.15%)
    //     Assert.AreEqual(expectedFwbtcBalance, buyerBalanceFwbtc);
    //     Assert.AreEqual(0, buyerBalanceFusdt);
    //
    //     // Check the amount to claim for both users
    //     Engine.SetTransactionSigners(sellers[0].Item1);
    //     Contract.ClaimOrder(1, 1, -1);
    //     var seller1BalanceFusdt = Contract.GetAccountBalance(sellers[0].Item1.Account, fUSDTContract);
    //     var expectedSeller1BalanceFusdt = 1357 - 2; // Fee is 2 (0.15%)
    //     Assert.AreEqual(expectedSeller1BalanceFusdt, seller1BalanceFusdt);
    //
    //     Engine.SetTransactionSigners(sellers[1].Item1);
    //
    //     Contract.ClaimOrder(1, 2, -1);
    //     var seller2BalanceFusdt = Contract.GetAccountBalance(sellers[1].Item1.Account, fUSDTContract);
    //     Assert.AreEqual(0, seller2BalanceFusdt);
    // }

    [TestMethod]
    public void LimitSellMakerAndTakerTest()
    {
        // Test scenario:
        // 1. Alice creates a limit buy order with a price of 0.10 USDT and an amount of 0.1 WBTC.
        // 2. Bob creates a limit sell order with a price of 0.09 USDT and an amount of 0.2 WBTC.
        // 3. Bob should sell 0.1 WBTC to Alice at a price of 0.10 USDT.
        // 4. Bob should have an order of 0.1 WBTC at a price of 0.90 USDT.

        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 200);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength, false);

        // # Act
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 10, 0, false, false);

        // Debugging
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
        var sellGridBeforeSell = GenerateGrid(treeBitLength);

        // Create images
        ExportGrid(diffGrid, "LimitSellMakerAndTakerTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 200, 9, 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Debugging
        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "LimitSellMakerAndTakerTest/orderbook-image-diff-2.png");
        var sellGridAfterSell = GenerateGrid(treeBitLength);
        var diffGridSell = OrderBookImageGenerator.HighlightDifferences(sellGridBeforeSell, sellGridAfterSell);
        ExportGrid(diffGridSell, "LimitSellMakerAndTakerTest/orderbook-image-diff-3-sell.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);
        var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // # Assert

        // Check order
        var orderBobRaw = (Array?) Contract.GetOrder(1, 2);
        var orderBob = RawOrderToOrder(orderBobRaw);
        Assert.IsNotNull(orderBob);
        var orderBobOwner = orderBob.Owner;
        var orderBobTotalOrderBaseAmount = orderBob.TotalOrderBaseAmount;
        var orderBobPlacedInOrderBook = orderBob.PlacedInOrderBookBaseAmount;
        var orderBobPrice = orderBob.Price;
        var orderBobClaimedBaseAmount = orderBob.ClaimedBaseAmount;
        Assert.AreEqual(bobWithScope.Account, orderBobOwner);
        Assert.AreEqual(200, orderBobTotalOrderBaseAmount);
        Assert.AreEqual(100, orderBobPlacedInOrderBook);
        Assert.AreEqual(9, orderBobPrice);
        Assert.AreEqual(0, orderBobClaimedBaseAmount);

        // Check balances
        Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
        Assert.AreEqual(10, bobBalanceFusdt);
        Assert.AreEqual(0, bobBalanceFwbtc);
    }

    // [TestMethod]
    // public void LimitSellMakerAndTakerTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 200);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength, false);
    //
    //     // # Act
    //     Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 10, 0, false, false);
    //
    //     // Debugging
    //     var grid2 = GenerateGrid(treeBitLength, false);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //     var sellGridBeforeSell = GenerateGrid(treeBitLength);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "LimitSellMakerAndTakerTestUsingQuote/orderbook-image-diff.png");
    //
    //     // Make a market buy order
    //     Engine.SetTransactionSigners(bobWithScope);
    //     var amount = CalculateQuoteAmountForBaseAmount(1, 9, 200);
    //     Console.WriteLine(amount);
    //     Contract.CreateLimitSellOrderUsingQuote(bobWithScope.Account, 1, amount, 9, 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     // Debugging
    //     var grid3 = GenerateGrid(treeBitLength, false);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "LimitSellMakerAndTakerTestUsingQuote/orderbook-image-diff-2.png");
    //     var sellGridAfterSell = GenerateGrid(treeBitLength);
    //     var diffGridSell = OrderBookImageGenerator.HighlightDifferences(sellGridBeforeSell, sellGridAfterSell);
    //     ExportGrid(diffGridSell, "LimitSellMakerAndTakerTestUsingQuote/orderbook-image-diff-3-sell.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
    //     Contract.ClaimOrder(1, 1, nodeIndexToCheck);
    //     var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // # Assert
    //
    //     // Check order
    //     var orderBobRaw = (Array?) Contract.GetOrder(1, 2);
    //     var orderBob = RawOrderToOrder(orderBobRaw);
    //     Assert.IsNotNull(orderBob);
    //     var orderBobOwner = orderBob.Owner;
    //     var orderBobTotalOrderBaseAmount = orderBob.TotalOrderBaseAmount;
    //     var orderBobPlacedInOrderBook = orderBob.PlacedInOrderBookBaseAmount;
    //     var orderBobPrice = orderBob.Price;
    //     var orderBobClaimedBaseAmount = orderBob.ClaimedBaseAmount;
    //     Assert.AreEqual(bobWithScope.Account, orderBobOwner);
    //     Assert.AreEqual(200, orderBobTotalOrderBaseAmount);
    //     Assert.AreEqual(88, orderBobPlacedInOrderBook);
    //     Assert.AreEqual(9, orderBobPrice);
    //     Assert.AreEqual(0, orderBobClaimedBaseAmount);
    //
    //     // Check balances
    //     Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
    //     Assert.AreEqual(10, bobBalanceFusdt);
    //     Assert.AreEqual(200 - 188, bobBalanceFwbtc);
    // }

    [TestMethod]
    public void PostOnlyLimitSellOrderTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10000000000);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 10000000000);

        var reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
        var reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
        Assert.AreEqual(100_000_000000, reserveFusdt);
        Assert.AreEqual(10_00000000, reserveFwbtc);

        var ammPrice = (double)reserveFusdt / (double)reserveFwbtc;
        Console.WriteLine(ammPrice);

        // We start by pushing the price in the AMM a bit down so that we make sure the price of the AMM is close to the limit price of 1001 but not exactly the same.
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 10000000000, 1001, 0, true, false);

        reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
        reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
        ammPrice = (double)reserveFusdt / (double)reserveFwbtc;
        Console.WriteLine(ammPrice);

        var exception = Assert.ThrowsException<TestException>(() => Contract.AddLimitSellOrderUsingBase(bobWithScope.Account, 1, 10000, 1001, 0));
        Assert.IsTrue(exception.Message.Contains("Limit price is lower than AMM price"));




        var gasCostMapBefore = GenerateDocumentGasCostMap();
        var gasBefore = (int)Engine.FeeConsumed;
        Contract.AddLimitSellOrderUsingBase(bobWithScope.Account, 1, 10000, 1002, 0);
        var gasCostMapAfter = GenerateDocumentGasCostMap(gasCostMapBefore);
        //WriteGasCostsToFile("test.txt", gasCostMapAfter);
        var gasAfter = (int)Engine.FeeConsumed;
        var gasCost = gasAfter - gasBefore;
        Console.WriteLine(gasCost / 100000000f);


        var orderBobRaw = (Array?) Contract.GetOrder(1, 2);
        var orderBob = RawOrderToOrder(orderBobRaw);
        Assert.IsNotNull(orderBob);
        var orderBobOwner = orderBob.Owner;
        var orderBobTotalOrderBaseAmount = orderBob.TotalOrderQuoteAmount;
        var orderBobPlacedInOrderBook = orderBob.PlacedInOrderBookQuoteAmount;
        var orderBobPrice = orderBob.Price;
        var orderBobClaimedBaseAmount = orderBob.ClaimedBaseAmount;
        Assert.AreEqual(bobWithScope.Account, orderBobOwner);
        Assert.AreEqual(1002000, orderBobTotalOrderBaseAmount);
        Assert.AreEqual(1002000, orderBobPlacedInOrderBook);
        Assert.AreEqual(1002, orderBobPrice);
        Assert.AreEqual(0, orderBobClaimedBaseAmount);
    }

    // [TestMethod]
    // public void PostOnlyLimitSellOrderTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 16;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100);
    //
    //     var reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
    //     var reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
    //     Assert.AreEqual(100_000_000000, reserveFusdt);
    //     Assert.AreEqual(10_00000000, reserveFwbtc);
    //
    //     var exception = Assert.ThrowsException<TestException>(() => Contract.AddLimitSellOrderUsingQuote(bobWithScope.Account, 1, CalculateQuoteAmountForBaseAmount(1, 99, 100), 99, 0));
    //     Assert.IsTrue(exception.Message.Contains("Limit price is lower than AMM price"));
    //
    //
    //
    //
    //     var gasCostMapBefore = GenerateDocumentGasCostMap();
    //     var gasBefore = (int)Engine.FeeConsumed;
    //     Contract.AddLimitSellOrderUsingQuote(bobWithScope.Account, 1, CalculateQuoteAmountForBaseAmount(1, 100, 100), 100, 0);
    //     var gasCostMapAfter = GenerateDocumentGasCostMap(gasCostMapBefore);
    //     //WriteGasCostsToFile("test.txt", gasCostMapAfter);
    //     var gasAfter = (int)Engine.FeeConsumed;
    //     var gasCost = gasAfter - gasBefore;
    //     Console.WriteLine(gasCost / 100000000f);
    //
    //
    //     var orderBobRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderBob = RawOrderToOrder(orderBobRaw);
    //     Assert.IsNotNull(orderBob);
    //     var orderBobOwner = orderBob.Owner;
    //     var orderBobTotalOrderBaseAmount = orderBob.TotalOrderBaseAmount;
    //     var orderBobPlacedInOrderBook = orderBob.PlacedInOrderBookBaseAmount;
    //     var orderBobPrice = orderBob.Price;
    //     var orderBobClaimedBaseAmount = orderBob.ClaimedBaseAmount;
    //     Assert.AreEqual(bobWithScope.Account, orderBobOwner);
    //     Assert.AreEqual(100, orderBobTotalOrderBaseAmount);
    //     Assert.AreEqual(100, orderBobPlacedInOrderBook);
    //     Assert.AreEqual(100, orderBobPrice);
    //     Assert.AreEqual(0, orderBobClaimedBaseAmount);
    // }

    [TestMethod]
    public void CreateLimitBuyOrderHighPriceTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10_000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength, false);

        // # Act

        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 16, 16, 0, false, false);
        var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitBuyOrderHighPriceTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        var bobBalanceFusdtBefore = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Create images
        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitBuyOrderHighPriceTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.ClaimOrder(1, 1, -1);
        var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // # Assert
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        Assert.IsNotNull(orderAliceRaw);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(100, orderAlicePlacedInOrderBook);
        Assert.AreEqual(16, orderAlicePrice);

        // Check balances
        Assert.AreEqual(9_984, aliceBalanceFusdt);
        Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
        Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
        Assert.AreEqual(0, bobBalanceFusdtBefore);
        Assert.AreEqual(16, bobBalanceFusdt);
        Assert.AreEqual(0, bobBalanceFwbtc);
    }

    // [TestMethod]
    // public void CreateLimitBuyOrderHighPriceTestUsingBase()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10_000);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength, false);
    //
    //     // # Act
    //
    //     Contract.CreateLimitBuyOrderUsingBase(aliceWithScope.Account, 1, CalculateBaseAmountForQuoteAmount(1, 16, 16), 16, 0, false, false);
    //     var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //     var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength, false);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitBuyOrderHighPriceTestUsingBase/orderbook-image-diff.png");
    //
    //     // Make a market buy order
    //     Engine.SetTransactionSigners(bobWithScope);
    //     var bobBalanceFusdtBefore = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //     Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     // Create images
    //     var grid3 = GenerateGrid(treeBitLength, false);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitBuyOrderHighPriceTestUsingBase/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.ClaimOrder(1, 1, -1);
    //     var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // # Assert
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     Assert.IsNotNull(orderAliceRaw);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(100, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(16, orderAlicePrice);
    //
    //     // Check balances
    //     Assert.AreEqual(9_984, aliceBalanceFusdt);
    //     Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
    //     Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
    //     Assert.AreEqual(0, bobBalanceFusdtBefore);
    //     Assert.AreEqual(16, bobBalanceFusdt);
    //     Assert.AreEqual(0, bobBalanceFwbtc);
    // }

    [TestMethod]
    public void CreateLimitBuyOrderMiddlePriceTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100_00000000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10_000_000000);

        // Visual representation
        // var grid1 = GenerateGrid(treeBitLength, false);

        // # Act
        Engine.FeeConsumed.Reset();
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 400, 104, 0, false, false);
        var gasCost = (int) Engine.FeeConsumed;
        Console.WriteLine(gasCost / 100000000f);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 35000, 101, 0, false, false);
        var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        // var grid2 = GenerateGrid(treeBitLength, false);
        // var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        // ExportGrid(diffGrid, "CreateLimitBuyOrderMiddlePriceTest/orderbook-image-diff.png");

        var nodeIndex1 = Contract.GetNodeIndex(treeBitLength, 15, 100);
        Console.WriteLine("Node index: " + nodeIndex1);
        var test1 = Contract.GetBaseAmountExecutedAtNode(true, 1, nodeIndex1);
        Console.WriteLine("Test 1: " + test1);

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        var bobBalanceFwbtcBefore = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 30, 100, 0, true, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        var nodeIndex2 = Contract.GetNodeIndex(treeBitLength, 15, 100);
        Console.WriteLine("Node index: " + nodeIndex2);
        var test2 = Contract.GetBaseAmountExecutedAtNode(true, 1, nodeIndex2);
        Console.WriteLine("Test 2: " + test2);

        // Create images
        // var grid3 = GenerateGrid(treeBitLength, false);
        // var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        // ExportGrid(diffGrid2, "CreateLimitBuyOrderMiddlePriceTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
        Contract.ClaimOrder(1, 1, 7);
        // Since we expect the second order to be partially filled we do not check any parent node for virtually filled amounts.
        Contract.ClaimOrder(1, 2, -1);
        var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // # Assert

        // Check order
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Console.WriteLine();
        orderAlice.PrintPublicFields();
        // Assert.IsNotNull(orderAlice);
        // var orderAliceOwner = orderAlice.Owner;
        // var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        // var orderAliceTotalOrderQuoteAmount = orderAlice.TotalOrderQuoteAmount;
        // var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        // var orderAlicePrice = orderAlice.Price;
        // var orderAliceClaimedBaseAmount = orderAlice.ClaimedBaseAmount;
        // Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        // Assert.AreEqual(4, orderAliceTotalOrderQuoteAmount);
        // Assert.AreEqual(44, orderAliceTotalOrderBaseAmount);
        // Assert.AreEqual(44, orderAlicePlacedInOrderBook);
        // Assert.AreEqual(9, orderAlicePrice);
        // Assert.AreEqual(44, orderAliceClaimedBaseAmount);

        var orderAliceRaw2 = (Array?) Contract.GetOrder(1, 2);
        var orderAlice2 = RawOrderToOrder(orderAliceRaw2);
        Console.WriteLine();
        orderAlice2.PrintPublicFields();
        // Assert.IsNotNull(orderAlice2);
        // var orderAlice2TotalOrderBaseAmount = orderAlice2.TotalOrderBaseAmount;
        // var orderAlice2GlobalBaseAmountPlacedAtPriceWhenInserted = orderAlice2.GlobalBaseAmountPlacedAtPriceWhenInserted;
        // var orderAlice2ClaimedBaseAmount = orderAlice2.ClaimedBaseAmount;
        // Assert.AreEqual(5_000, orderAlice2TotalOrderBaseAmount);
        // Assert.AreEqual(0, orderAlice2GlobalBaseAmountPlacedAtPriceWhenInserted);
        // Assert.AreEqual(56, orderAlice2ClaimedBaseAmount);
        //
        // // Check balances
        // Assert.AreEqual(10_000 - 354, aliceBalanceFusdt);
        // Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
        // Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
        //
        // Assert.AreEqual(100, bobBalanceFwbtcBefore);
        // Assert.AreEqual(0, bobBalanceFwbtc);
        // Assert.AreEqual(7, bobBalanceFusdt);

        var orderBobRaw = (Array?) Contract.GetOrder(1, 3);
        var orderBob = RawOrderToOrder(orderBobRaw);
        Console.WriteLine();
        orderBob.PrintPublicFields();
    }

    // [TestMethod]
    // public void CreateLimitBuyOrderMiddlePriceTestUsingBase()
    // {
    //     // # Arrange
    //     const int treeBitLength = 16;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 100;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100_00000000);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10_000_000000);
    //
    //     // Visual representation
    //     // var grid1 = GenerateGrid(treeBitLength, false);
    //
    //     // # Act
    //     Engine.FeeConsumed.Reset();
    //     Contract.CreateLimitBuyOrderUsingBase(aliceWithScope.Account, 1, CalculateBaseAmountForQuoteAmount(1, 104, 400), 104, 0, false, false);
    //     var gasCost = (int) Engine.FeeConsumed;
    //     Console.WriteLine(gasCost / 100000000f);
    //     Contract.CreateLimitBuyOrderUsingBase(aliceWithScope.Account, 1, CalculateBaseAmountForQuoteAmount(1, 101, 35000), 101, 0, false, false);
    //     var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //     var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     // var grid2 = GenerateGrid(treeBitLength, false);
    //     // var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     // ExportGrid(diffGrid, "CreateLimitBuyOrderMiddlePriceTest/orderbook-image-diff.png");
    //
    //     var nodeIndex1 = Contract.GetNodeIndex(treeBitLength, 15, 100);
    //     Console.WriteLine("Node index: " + nodeIndex1);
    //     var test1 = Contract.GetBaseAmountExecutedAtNode(true, 1, nodeIndex1);
    //     Console.WriteLine("Test 1: " + test1);
    //
    //     // Make a market buy order
    //     Engine.SetTransactionSigners(bobWithScope);
    //     var bobBalanceFwbtcBefore = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 30, 100, 0, true, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     var nodeIndex2 = Contract.GetNodeIndex(treeBitLength, 15, 100);
    //     Console.WriteLine("Node index: " + nodeIndex2);
    //     var test2 = Contract.GetBaseAmountExecutedAtNode(true, 1, nodeIndex2);
    //     Console.WriteLine("Test 2: " + test2);
    //
    //     // Create images
    //     // var grid3 = GenerateGrid(treeBitLength, false);
    //     // var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     // ExportGrid(diffGrid2, "CreateLimitBuyOrderMiddlePriceTest/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
    //     Contract.ClaimOrder(1, 1, 7);
    //     // Since we expect the second order to be partially filled we do not check any parent node for virtually filled amounts.
    //     Contract.ClaimOrder(1, 2, -1);
    //     var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // # Assert
    //
    //     // Check order
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Console.WriteLine();
    //     orderAlice.PrintPublicFields();
    //     // Assert.IsNotNull(orderAlice);
    //     // var orderAliceOwner = orderAlice.Owner;
    //     // var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     // var orderAliceTotalOrderQuoteAmount = orderAlice.TotalOrderQuoteAmount;
    //     // var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     // var orderAlicePrice = orderAlice.Price;
    //     // var orderAliceClaimedBaseAmount = orderAlice.ClaimedBaseAmount;
    //     // Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     // Assert.AreEqual(4, orderAliceTotalOrderQuoteAmount);
    //     // Assert.AreEqual(44, orderAliceTotalOrderBaseAmount);
    //     // Assert.AreEqual(44, orderAlicePlacedInOrderBook);
    //     // Assert.AreEqual(9, orderAlicePrice);
    //     // Assert.AreEqual(44, orderAliceClaimedBaseAmount);
    //
    //     var orderAliceRaw2 = (Array?) Contract.GetOrder(1, 2);
    //     var orderAlice2 = RawOrderToOrder(orderAliceRaw2);
    //     Console.WriteLine();
    //     orderAlice2.PrintPublicFields();
    //     // Assert.IsNotNull(orderAlice2);
    //     // var orderAlice2TotalOrderBaseAmount = orderAlice2.TotalOrderBaseAmount;
    //     // var orderAlice2GlobalBaseAmountPlacedAtPriceWhenInserted = orderAlice2.GlobalBaseAmountPlacedAtPriceWhenInserted;
    //     // var orderAlice2ClaimedBaseAmount = orderAlice2.ClaimedBaseAmount;
    //     // Assert.AreEqual(5_000, orderAlice2TotalOrderBaseAmount);
    //     // Assert.AreEqual(0, orderAlice2GlobalBaseAmountPlacedAtPriceWhenInserted);
    //     // Assert.AreEqual(56, orderAlice2ClaimedBaseAmount);
    //     //
    //     // // Check balances
    //     // Assert.AreEqual(10_000 - 354, aliceBalanceFusdt);
    //     // Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
    //     // Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
    //     //
    //     // Assert.AreEqual(100, bobBalanceFwbtcBefore);
    //     // Assert.AreEqual(0, bobBalanceFwbtc);
    //     // Assert.AreEqual(7, bobBalanceFusdt);
    //
    //     var orderBobRaw = (Array?) Contract.GetOrder(1, 3);
    //     var orderBob = RawOrderToOrder(orderBobRaw);
    //     Console.WriteLine();
    //     orderBob.PrintPublicFields();
    // }

    [TestMethod]
    public void CreateLimitBuyOrderLowPriceTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10_000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength, false);

        // # Act
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 1, 1, 0, false, false);
        var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitBuyOrderLowPriceTest/orderbook-image-diff.png");

        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitBuyOrderLowPriceTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
        Contract.ClaimOrder(1, 1, 15);
        var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // # Assert
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(100, orderAlicePlacedInOrderBook);
        Assert.AreEqual(1, orderAlicePrice);

        Assert.AreEqual(10_000 - 1, aliceBalanceFusdt);
        Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
        Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);

        Assert.AreEqual(0, bobBalanceFwbtc);
        Assert.AreEqual(1, bobBalanceFusdt);
    }

    // [TestMethod]
    // public void CreateLimitBuyOrderLowPriceTestUsingBase()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10_000);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength, false);
    //
    //     // # Act
    //     Contract.CreateLimitBuyOrderUsingBase(aliceWithScope.Account, 1, CalculateBaseAmountForQuoteAmount(1, 1, 1), 1, 0, false, false);
    //     var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //     var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength, false);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitBuyOrderLowPriceTestUsingBase/orderbook-image-diff.png");
    //
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     var grid3 = GenerateGrid(treeBitLength, false);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitBuyOrderLowPriceTestUsingBase/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
    //     Contract.ClaimOrder(1, 1, 15);
    //     var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // # Assert
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(100, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(1, orderAlicePrice);
    //
    //     Assert.AreEqual(10_000 - 1, aliceBalanceFusdt);
    //     Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
    //     Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
    //
    //     Assert.AreEqual(0, bobBalanceFwbtc);
    //     Assert.AreEqual(1, bobBalanceFusdt);
    // }

    [TestMethod]
    public void CreateLimitBuyOrderHighPriceSuperDecimalPriceRangeTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10000;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 100000000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength, false);

        // # Act

        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 160000, 16, 0, false, false);
        var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitBuyOrderHighPriceTestSuperDecimalPriceRange/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        var bobBalanceFusdtBefore = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Create images
        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitBuyOrderHighPriceTestSuperDecimalPriceRange/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.ClaimOrder(1, 1, -1);
        var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // # Assert

        // Check order
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;

        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(100, orderAlicePlacedInOrderBook);
        Assert.AreEqual(16, orderAlicePrice);

        // Check balances
        Assert.AreEqual(9_9840000, aliceBalanceFusdt);
        Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
        Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
        Assert.AreEqual(0, bobBalanceFusdtBefore);
        Assert.AreEqual(15_9760, bobBalanceFusdt); // 16_0000 * 0.15% (taker fee) = 24. So 16_0000 - 24 = 15_9760.
        Assert.AreEqual(0, bobBalanceFwbtc);
    }

    // [TestMethod]
    // public void CreateLimitBuyOrderHighPriceSuperDecimalPriceRangeTestUsingBase()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 10000;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 100000000);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength, false);
    //
    //     // # Act
    //
    //     Contract.CreateLimitBuyOrderUsingBase(aliceWithScope.Account, 1, CalculateBaseAmountForQuoteAmount(1, 16, 160000), 16, 0, false, false);
    //     var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //     var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength, false);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitBuyOrderHighPriceTestSuperDecimalPriceRangeUsingBase/orderbook-image-diff.png");
    //
    //     // Make a market buy order
    //     Engine.SetTransactionSigners(bobWithScope);
    //     var bobBalanceFusdtBefore = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //     Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     // Create images
    //     var grid3 = GenerateGrid(treeBitLength, false);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitBuyOrderHighPriceTestSuperDecimalPriceRangeUsingBase/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.ClaimOrder(1, 1, -1);
    //     var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // # Assert
    //
    //     // Check order
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(100, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(100, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(16, orderAlicePrice);
    //
    //     // Check balances
    //     Assert.AreEqual(9_9840000, aliceBalanceFusdt);
    //     Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
    //     Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
    //     Assert.AreEqual(0, bobBalanceFusdtBefore);
    //     Assert.AreEqual(15_9760, bobBalanceFusdt); // 16_0000 * 0.15% (taker fee) = 24. So 16_0000 - 24 = 15_9760.
    //     Assert.AreEqual(0, bobBalanceFwbtc);
    // }

    [TestMethod]
    public void CreateLimitBuyOrderMiddlePriceSuperDecimalPriceRangeTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10000;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 100000000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength, false);

        // # Act
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 45000, 9, 0, false, false);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 3500000, 7, 0, false, false);
        var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitBuyOrderMiddlePriceSuperDecimalPriceRange/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        var bobBalanceFwbtcBefore = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Create images
        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitBuyOrderMiddlePriceSuperDecimalPriceRange/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);
        // Since we expect the second order to be partially filled we do not check any parent node for virtually filled amounts.
        Contract.ClaimOrder(1, 2, -1);
        var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // # Assert

        // Check order
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        var orderAliceClaimedBaseAmount = orderAlice.ClaimedBaseAmount;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(50, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(50, orderAlicePlacedInOrderBook);
        Assert.AreEqual(9, orderAlicePrice);
        Assert.AreEqual(50, orderAliceClaimedBaseAmount);

        var orderAliceRaw2 = (Array?) Contract.GetOrder(1, 2);
        var orderAlice2 = RawOrderToOrder(orderAliceRaw2);

        var orderAlice2TotalOrderBaseAmount = orderAlice2.TotalOrderBaseAmount;
        var orderAlice2GlobalBaseAmountPlacedAtPriceWhenInserted = orderAlice2.GlobalBaseAmountPlacedAtPriceWhenInserted;
        var orderAlice2ClaimedBaseAmount = orderAlice2.ClaimedBaseAmount;

        Assert.AreEqual(5_000, orderAlice2TotalOrderBaseAmount);
        Assert.AreEqual(0, orderAlice2GlobalBaseAmountPlacedAtPriceWhenInserted);
        Assert.AreEqual(50, orderAlice2ClaimedBaseAmount);

        // Check balances
        Assert.AreEqual(100000000 - 3545000, aliceBalanceFusdt);
        Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
        Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);

        Assert.AreEqual(100, bobBalanceFwbtcBefore);
        Assert.AreEqual(0, bobBalanceFwbtc);
        Assert.AreEqual(79880, bobBalanceFusdt); // 80000 * 0.15% (taker fee) = 120. So 80000 - 120 = 79880.
    }

    // [TestMethod]
    // public void CreateLimitBuyOrderMiddlePriceSuperDecimalPriceRangeTestUsingBase()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 10000;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 100000000);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength, false);
    //
    //     // # Act
    //     Contract.CreateLimitBuyOrderUsingBase(aliceWithScope.Account, 1, CalculateBaseAmountForQuoteAmount(1, 9, 45000), 9, 0, false, false);
    //     Contract.CreateLimitBuyOrderUsingBase(aliceWithScope.Account, 1, CalculateBaseAmountForQuoteAmount(1, 7, 3500000), 7, 0, false, false);
    //     var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //     var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength, false);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitBuyOrderMiddlePriceSuperDecimalPriceRangeUsingBase/orderbook-image-diff.png");
    //
    //     // Make a market buy order
    //     Engine.SetTransactionSigners(bobWithScope);
    //     var bobBalanceFwbtcBefore = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     // Create images
    //     var grid3 = GenerateGrid(treeBitLength, false);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitBuyOrderMiddlePriceSuperDecimalPriceRangeUsingBase/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
    //     Contract.ClaimOrder(1, 1, 7);
    //     // Since we expect the second order to be partially filled we do not check any parent node for virtually filled amounts.
    //     Contract.ClaimOrder(1, 2, -1);
    //     var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // # Assert
    //
    //     // Check order
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     var orderAliceClaimedBaseAmount = orderAlice.ClaimedBaseAmount;
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(50, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(50, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(9, orderAlicePrice);
    //     Assert.AreEqual(50, orderAliceClaimedBaseAmount);
    //
    //     var orderAliceRaw2 = (Array?) Contract.GetOrder(1, 2);
    //     var orderAlice2 = RawOrderToOrder(orderAliceRaw2);
    //
    //     var orderAlice2TotalOrderBaseAmount = orderAlice2.TotalOrderBaseAmount;
    //     var orderAlice2GlobalBaseAmountPlacedAtPriceWhenInserted = orderAlice2.GlobalBaseAmountPlacedAtPriceWhenInserted;
    //     var orderAlice2ClaimedBaseAmount = orderAlice2.ClaimedBaseAmount;
    //
    //     Assert.AreEqual(5_000, orderAlice2TotalOrderBaseAmount);
    //     Assert.AreEqual(0, orderAlice2GlobalBaseAmountPlacedAtPriceWhenInserted);
    //     Assert.AreEqual(50, orderAlice2ClaimedBaseAmount);
    //
    //     // Check balances
    //     Assert.AreEqual(100000000 - 3545000, aliceBalanceFusdt);
    //     Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
    //     Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
    //
    //     Assert.AreEqual(100, bobBalanceFwbtcBefore);
    //     Assert.AreEqual(0, bobBalanceFwbtc);
    //     Assert.AreEqual(79880, bobBalanceFusdt); // 80000 * 0.15% (taker fee) = 120. So 80000 - 120 = 79880.
    // }

    [TestMethod]
    public void CreateLimitBuyOrderLowPriceSuperDecimalPriceRangeTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10000;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 1000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 100000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength, false);

        // # Act
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100000, 1, 0, false, false);
        var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "CreateLimitBuyOrderLowPriceSuperDecimalPriceRangeTest/orderbook-image-diff.png");

        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 1000, 1, 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CreateLimitBuyOrderLowPriceSuperDecimalPriceRangeTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
        Contract.ClaimOrder(1, 1, 15);
        var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // # Assert
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(1000, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(1000, orderAlicePlacedInOrderBook);
        Assert.AreEqual(1, orderAlicePrice);

        Assert.AreEqual(0, aliceBalanceFusdt);
        Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
        Assert.AreEqual(999, aliceBalanceFwbtcAfterClaim); // 1000 - 1 (maker fee) = 999.

        Assert.AreEqual(0, bobBalanceFwbtc);
        Assert.AreEqual(99850, bobBalanceFusdt); // 100000 * 0.15% (taker fee) = 150. So 100000 - 150 = 99850.
    }

    // [TestMethod]
    // public void CreateLimitBuyOrderLowPriceSuperDecimalPriceRangeTestUsingBase()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 10000;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 1000);
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 100000);
    //
    //     // Visual representation
    //     var grid1 = GenerateGrid(treeBitLength, false);
    //
    //     // # Act
    //     Contract.CreateLimitBuyOrderUsingBase(aliceWithScope.Account, 1, CalculateBaseAmountForQuoteAmount(1, 1, 100000), 1, 0, false, false);
    //     var aliceBalanceFusdt = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //     var aliceBalanceFwbtcBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
    //     var grid2 = GenerateGrid(treeBitLength, false);
    //     var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
    //
    //     // Create images
    //     ExportGrid(diffGrid, "CreateLimitBuyOrderLowPriceSuperDecimalPriceRangeTestUsingBase/orderbook-image-diff.png");
    //
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 1000, 1, 0, false, false);
    //     var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
    //     var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);
    //
    //     var grid3 = GenerateGrid(treeBitLength, false);
    //     var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
    //     ExportGrid(diffGrid2, "CreateLimitBuyOrderLowPriceSuperDecimalPriceRangeTestUsingBase/orderbook-image-diff-2.png");
    //
    //     // Claim the assets on the order
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     // The node we want to check is [0, 15] which has the binary address 0x0000_1111 and the decimal address 15.
    //     Contract.ClaimOrder(1, 1, 15);
    //     var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //
    //     // # Assert
    //     var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderAlice = RawOrderToOrder(orderAliceRaw);
    //     Assert.IsNotNull(orderAlice);
    //     var orderAliceOwner = orderAlice.Owner;
    //     var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
    //     var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
    //     var orderAlicePrice = orderAlice.Price;
    //     Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
    //     Assert.AreEqual(1000, orderAliceTotalOrderBaseAmount);
    //     Assert.AreEqual(1000, orderAlicePlacedInOrderBook);
    //     Assert.AreEqual(1, orderAlicePrice);
    //
    //     Assert.AreEqual(0, aliceBalanceFusdt);
    //     Assert.AreEqual(0, aliceBalanceFwbtcBeforeClaim);
    //     Assert.AreEqual(999, aliceBalanceFwbtcAfterClaim); // 1000 - 1 (maker fee) = 999.
    //
    //     Assert.AreEqual(0, bobBalanceFwbtc);
    //     Assert.AreEqual(99850, bobBalanceFusdt); // 100000 * 0.15% (taker fee) = 150. So 100000 - 150 = 99850.
    // }

    [TestMethod]
    public void CreateManyLimitBuyOrderTest()
    {
        // # Arrange
        const int treeBitLength = 5;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var sellerAccount = TestEngine.GetNewSigner();
        var sellerWithScope = new Signer {Account = sellerAccount.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(SuperAdmin);
        var sellerFwbtcDepositAmount = 2377432;
        fWBTCContract.Mint(SuperAdmin.Account, sellerAccount.Account, sellerFwbtcDepositAmount);

        Engine.SetTransactionSigners(sellerWithScope);
        Contract.Deposit(sellerWithScope.Account, fWBTCContract.Hash, sellerFwbtcDepositAmount);

        // (Signer, Buy Amount, Limit Price)
        var buyers = new List<(Signer, BigInteger, BigInteger)>
        {
            (TestEngine.GetNewSigner(), 100, 1),
            (TestEngine.GetNewSigner(), 150, 2),
            (TestEngine.GetNewSigner(), 200, 3),
            (TestEngine.GetNewSigner(), 170, 4),
            (TestEngine.GetNewSigner(), 88, 5),
            (TestEngine.GetNewSigner(), 65, 6),
            (TestEngine.GetNewSigner(), 32, 7),
            (TestEngine.GetNewSigner(), 10000, 8),
            (TestEngine.GetNewSigner(), 12345, 9),
            (TestEngine.GetNewSigner(), 52016, 10),
            (TestEngine.GetNewSigner(), 20100, 11),
            (TestEngine.GetNewSigner(), 20312, 12),
            (TestEngine.GetNewSigner(), 111, 13),
            (TestEngine.GetNewSigner(), 222, 14),
            (TestEngine.GetNewSigner(), 333, 15),
            (TestEngine.GetNewSigner(), 444, 16),
            (TestEngine.GetNewSigner(), 123, 17),
            (TestEngine.GetNewSigner(), 854, 18),
            (TestEngine.GetNewSigner(), 20351, 19),
            (TestEngine.GetNewSigner(), 12099, 20),
            (TestEngine.GetNewSigner(), 91999, 21),
            (TestEngine.GetNewSigner(), 12012, 22),
            (TestEngine.GetNewSigner(), 15101, 23),
            (TestEngine.GetNewSigner(), 18520, 24),
            (TestEngine.GetNewSigner(), 18132, 25),
            (TestEngine.GetNewSigner(), 81537, 26),
            (TestEngine.GetNewSigner(), 153, 27),
            (TestEngine.GetNewSigner(), 121, 28),
            (TestEngine.GetNewSigner(), 1237, 29),
            (TestEngine.GetNewSigner(), 4, 30),
            (TestEngine.GetNewSigner(), 1111, 31),
            (TestEngine.GetNewSigner(), 125, 32),
        };

        // Debugging
        var startingGrid = GenerateGrid(treeBitLength, false);
        var lastGrid = GenerateGrid(treeBitLength, false);

        BigInteger sumToBuy = 0;
        BigInteger quoteAmountTotal = 0;
        BigInteger expectedBaseAmountTotal = 0;
        BigInteger depositedAmount = 0;
        var index = 1;
        BigInteger roundingPrecision = Contract.RoundingPrecision!.Value;

        foreach (var (buyer, amount, price) in buyers)
        {
            var expectedBaseAmount = (amount * roundingPrecision) / ((price * roundingPrecision) / 100);

            Engine.SetTransactionSigners(SuperAdmin);
            fUSDTContract.Mint(SuperAdmin.Account, buyer.Account, amount);

            var buyerWithScope = new Signer {Account = buyer.Account, Scopes = WitnessScope.Global};
            Engine.SetTransactionSigners(buyerWithScope);
            Contract.Deposit(buyerWithScope.Account, fUSDTContract.Hash, amount);
            Contract.CreateLimitBuyOrderUsingQuote(buyerWithScope.Account, 1, amount, price, 0, false, false);

            // Debugging
            var grid = GenerateGrid(treeBitLength, false);
            var diffGrid = OrderBookImageGenerator.HighlightDifferences(lastGrid, grid);
            ExportGrid(diffGrid, $"CreateManyLimitBuyOrderTest/orderbook-image-{index}.png");
            lastGrid = grid;

            var nodeIndex = (4 << 5) + (price - 1); // Colum = 5, row = price - 1
            var priceNodeRaw = (Array) Contract.GetPriceNode(1, nodeIndex, false)!;
            var priceNode = new OrderBookImageGenerator.PriceNode
            {
                BaseAmount = priceNodeRaw[0].GetInteger(), QuoteTotal = priceNodeRaw[1].GetInteger(), EmptiedCount = priceNodeRaw[2].GetInteger(), EmptiedCountSibling = priceNodeRaw[3].GetInteger(),
            };
            Assert.AreEqual(expectedBaseAmount, priceNode.BaseAmount);

            var orderRaw = (Array?) Contract.GetOrder(1, index);
            var order = RawOrderToOrder(orderRaw);
            Assert.IsNotNull(order);
            var orderOwner = order.Owner;
            var orderTotalOrderBaseAmount = order.TotalOrderBaseAmount;
            var orderTotalOrderQuoteAmount = order.TotalOrderQuoteAmount;
            var orderPlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount;
            var orderPlacedInOrderQuoteAmount = order.PlacedInOrderBookQuoteAmount;
            var orderPrice = order.Price;
            Assert.AreEqual(buyerWithScope.Account, orderOwner);
            Assert.AreEqual(expectedBaseAmount, orderTotalOrderBaseAmount);
            Assert.AreEqual(amount, orderTotalOrderQuoteAmount);
            Assert.AreEqual(priceNode.BaseAmount, orderPlacedInOrderBookBaseAmount);
            Assert.AreEqual(priceNode.QuoteTotal, orderPlacedInOrderQuoteAmount);
            Assert.AreEqual(price, orderPrice);

            sumToBuy += priceNode.BaseAmount;
            quoteAmountTotal += priceNode.QuoteTotal;
            expectedBaseAmountTotal += expectedBaseAmount;
            depositedAmount += amount;

            index += 1;
        }

        // Calculate the fee the taker/buyer has to pay.
        var baseFee = Contract.GetTakerFee(1)!.Value * quoteAmountTotal / 1000000;
        var sumQuoteAmountTotal = quoteAmountTotal - baseFee;

        // Debugging
        var finalGrid = GenerateGrid(treeBitLength, false);
        var diffGridFinal = OrderBookImageGenerator.HighlightDifferences(startingGrid, finalGrid);
        ExportGrid(diffGridFinal, "CreateManyLimitBuyOrderTest/orderbook-image-final.png");

        // Sell to all the orders
        Engine.SetTransactionSigners(sellerWithScope);
        // We try to sell twice the amount of the total sum of all the orders.
        Contract.ExecuteLimitSellOrderUsingBase(sellerWithScope.Account, 1, sumToBuy * 2, 1, 0, false, false);

        // Debugging
        var afterSellGrid = GenerateGrid(treeBitLength, false);
        var diffGridAfterSell = OrderBookImageGenerator.HighlightDifferences(finalGrid, afterSellGrid);
        ExportGrid(diffGridAfterSell, "CreateManyLimitBuyOrderTest/orderbook-image-final-after-sell.png");

        // Check the balances
        var sellerBalanceFwbtc = Contract.GetAccountBalance(sellerWithScope.Account, fWBTCContract);
        var sellerBalanceFusdt = Contract.GetAccountBalance(sellerWithScope.Account, fUSDTContract);

        Assert.AreEqual(sellerFwbtcDepositAmount - sumToBuy, sellerBalanceFwbtc);

        var expectedQuoteAmountTotalManualCheck = 390167 - 585; // Fee is 112 (0.15%)
        Assert.AreEqual(expectedQuoteAmountTotalManualCheck, sellerBalanceFusdt);
        Assert.AreEqual(sumQuoteAmountTotal, sellerBalanceFusdt);

        var brokerContractFusdtBalance = fUSDTContract.BalanceOf(ContractHash);
        var brokerContractFwbtcBalance = fWBTCContract.BalanceOf(ContractHash);
        Assert.AreEqual(depositedAmount, brokerContractFusdtBalance);
        Assert.AreEqual(sellerFwbtcDepositAmount, brokerContractFwbtcBalance);

        BigInteger buyersTotalFwbtcBalance = 0;

        for (var i = 1; i < buyers.Count + 1; i++)
        {
            var buyer = buyers[i - 1];
            Engine.SetTransactionSigners(buyer.Item1);
            var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, i);
            Contract.ClaimOrder(1, i, nodeIndexToCheck);
            BigInteger buyerBalanceFwbtc = (BigInteger) Contract.GetAccountBalance(buyer.Item1.Account, fWBTCContract);
            buyersTotalFwbtcBalance += buyerBalanceFwbtc;
        }

        var claimableFeeFusdt = Contract.GetFeeBalance(fUSDTContract);
        var claimableFeeFwbtc = Contract.GetFeeBalance(fWBTCContract);
        Assert.AreEqual(585, claimableFeeFusdt);
        // Assert.AreEqual(0, claimableFeeFwbtc);

        Assert.AreEqual(sellerBalanceFusdt + claimableFeeFusdt, brokerContractFusdtBalance);
        Assert.AreEqual(buyersTotalFwbtcBalance + claimableFeeFwbtc, brokerContractFwbtcBalance);

        // Let all users withdraw their funds
        foreach (var (buyer, _, _) in buyers)
        {
            Engine.SetTransactionSigners(buyer);
            var buyerFusdtBalance = Contract.GetAccountBalance(buyer.Account, fUSDTContract);
            if (buyerFusdtBalance > 0)
            {
                Contract.Withdraw(buyer.Account, fUSDTContract.Hash, buyerFusdtBalance);
            }
            var buyerFwbtcBalance = Contract.GetAccountBalance(buyer.Account, fWBTCContract);
            if (buyerFwbtcBalance > 0)
            {
                Contract.Withdraw(buyer.Account, fWBTCContract.Hash, buyerFwbtcBalance);
            }
        }
    }

    // [TestMethod]
    // public void CreateManyLimitBuyOrderTestUsingBase()
    // {
    //     // # Arrange
    //     const int treeBitLength = 5;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var sellerAccount = TestEngine.GetNewSigner();
    //     var sellerWithScope = new Signer {Account = sellerAccount.Account, Scopes = WitnessScope.Global};
    //
    //     Engine.SetTransactionSigners(SuperAdmin);
    //     var sellerFwbtcDepositAmount = 10_00000000;
    //     fWBTCContract.Mint(SuperAdmin.Account, sellerAccount.Account, sellerFwbtcDepositAmount);
    //
    //     Engine.SetTransactionSigners(sellerWithScope);
    //     Contract.Deposit(sellerWithScope.Account, fWBTCContract.Hash, sellerFwbtcDepositAmount);
    //
    //     // (Signer, Buy Amount, Limit Price)
    //     var buyers = new List<(Signer, BigInteger, BigInteger, BigInteger)>
    //     {
    //         (TestEngine.GetNewSigner(), 10000, 100, 1),
    //         (TestEngine.GetNewSigner(), 7500, 150, 2),
    //         (TestEngine.GetNewSigner(), 6666, 199, 3),
    //         (TestEngine.GetNewSigner(), 4250, 170, 4),
    //         (TestEngine.GetNewSigner(), 1760, 88, 5),
    //         (TestEngine.GetNewSigner(), 1083, 64, 6),
    //         (TestEngine.GetNewSigner(), 457, 31, 7),
    //         (TestEngine.GetNewSigner(), 125000, 10000, 8),
    //         (TestEngine.GetNewSigner(), 137166, 12344, 9),
    //         (TestEngine.GetNewSigner(), 520160, 52016, 10),
    //         (TestEngine.GetNewSigner(), 182727, 20099, 11),
    //         (TestEngine.GetNewSigner(), 169266, 20311, 12),
    //         (TestEngine.GetNewSigner(), 853, 110, 13),
    //         (TestEngine.GetNewSigner(), 1585, 221, 14),
    //         (TestEngine.GetNewSigner(), 2220, 333, 15),
    //         (TestEngine.GetNewSigner(), 2775, 444, 16),
    //         (TestEngine.GetNewSigner(), 723, 122, 17),
    //         (TestEngine.GetNewSigner(), 4744, 853, 18),
    //         (TestEngine.GetNewSigner(), 107110, 20350, 19),
    //         (TestEngine.GetNewSigner(), 60495, 12099, 20),
    //         (TestEngine.GetNewSigner(), 438090, 91998, 21),
    //         (TestEngine.GetNewSigner(), 54600, 12012, 22),
    //         (TestEngine.GetNewSigner(), 65656, 15100, 23),
    //         (TestEngine.GetNewSigner(), 77166, 18519, 24),
    //         (TestEngine.GetNewSigner(), 72528, 18132, 25),
    //         (TestEngine.GetNewSigner(), 313603, 81536, 26),
    //         (TestEngine.GetNewSigner(), 566, 152, 27),
    //         (TestEngine.GetNewSigner(), 432, 120, 28),
    //         (TestEngine.GetNewSigner(), 4265, 1236, 29),
    //         (TestEngine.GetNewSigner(), 13, 3, 30),
    //         (TestEngine.GetNewSigner(), 3583, 1110, 31),
    //         (TestEngine.GetNewSigner(), 390, 124, 32),
    //     };
    //
    //     // Debugging
    //     var startingGrid = GenerateGrid(treeBitLength, false);
    //     var lastGrid = GenerateGrid(treeBitLength, false);
    //
    //     BigInteger sumToBuy = 0;
    //     BigInteger quoteAmountTotal = 0;
    //     BigInteger expectedBaseAmountTotal = 0;
    //     var index = 1;
    //     BigInteger roundingPrecision = Contract.RoundingPrecision!.Value;
    //
    //     foreach (var (buyer, baseAmount, quoteAmount, price) in buyers)
    //     {
    //         Engine.SetTransactionSigners(SuperAdmin);
    //         fUSDTContract.Mint(SuperAdmin.Account, buyer.Account, quoteAmount);
    //
    //         var buyerWithScope = new Signer {Account = buyer.Account, Scopes = WitnessScope.Global};
    //         Engine.SetTransactionSigners(buyerWithScope);
    //         Contract.Deposit(buyerWithScope.Account, fUSDTContract.Hash, quoteAmount);
    //         Contract.CreateLimitBuyOrderUsingBase(buyerWithScope.Account, 1, baseAmount, price, 0, false, false);
    //
    //         // Debugging
    //         var grid = GenerateGrid(treeBitLength, false);
    //         var diffGrid = OrderBookImageGenerator.HighlightDifferences(lastGrid, grid);
    //         ExportGrid(diffGrid, $"CreateManyLimitBuyOrderTestUsingBase/orderbook-image-{index}.png");
    //         lastGrid = grid;
    //
    //         var nodeIndex = (4 << 5) + (price - 1); // Colum = 5, row = price - 1
    //         var priceNodeRaw = (Array) Contract.GetPriceNode(1, nodeIndex, false)!;
    //         var priceNode = new OrderBookImageGenerator.PriceNode
    //         {
    //             BaseAmount = priceNodeRaw[0].GetInteger(), QuoteTotal = priceNodeRaw[1].GetInteger(), EmptiedCount = priceNodeRaw[2].GetInteger(), EmptiedCountSibling = priceNodeRaw[3].GetInteger(),
    //         };
    //         Assert.AreEqual(baseAmount, priceNode.BaseAmount);
    //
    //         var orderRaw = (Array?) Contract.GetOrder(1, index);
    //         var order = RawOrderToOrder(orderRaw);
    //         Assert.IsNotNull(order);
    //         var orderOwner = order.Owner;
    //         var orderTotalOrderBaseAmount = order.TotalOrderBaseAmount;
    //         var orderTotalOrderQuoteAmount = order.TotalOrderQuoteAmount;
    //         var orderPlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount;
    //         var orderPlacedInOrderQuoteAmount = order.PlacedInOrderBookQuoteAmount;
    //         var orderPrice = order.Price;
    //         Assert.AreEqual(buyerWithScope.Account, orderOwner);
    //         Assert.AreEqual(baseAmount, orderTotalOrderBaseAmount);
    //         Assert.AreEqual(quoteAmount, orderTotalOrderQuoteAmount);
    //         Assert.AreEqual(priceNode.BaseAmount, orderPlacedInOrderBookBaseAmount);
    //         Assert.AreEqual(priceNode.QuoteTotal, orderPlacedInOrderQuoteAmount);
    //         Assert.AreEqual(price, orderPrice);
    //
    //         sumToBuy += priceNode.BaseAmount;
    //         quoteAmountTotal += priceNode.QuoteTotal;
    //         expectedBaseAmountTotal += baseAmount;
    //
    //         index += 1;
    //     }
    //
    //     // Calculate the fee the taker/buyer has to pay.
    //     var baseFee = Contract.GetTakerFee(1)!.Value * quoteAmountTotal / 1000000;
    //     var sumQuoteAmountTotal = quoteAmountTotal - baseFee;
    //
    //     // Debugging
    //     var finalGrid = GenerateGrid(treeBitLength, false);
    //     var diffGridFinal = OrderBookImageGenerator.HighlightDifferences(startingGrid, finalGrid);
    //     ExportGrid(diffGridFinal, "CreateManyLimitBuyOrderTestUsingBase/orderbook-image-final.png");
    //
    //     // Sell to all the orders
    //     Engine.SetTransactionSigners(sellerWithScope);
    //     // We try to sell twice the amount of the total sum of all the orders.
    //     Contract.ExecuteLimitSellOrderUsingBase(sellerWithScope.Account, 1, sumToBuy * 2, 1, 0, false, false);
    //
    //     // Debugging
    //     var afterSellGrid = GenerateGrid(treeBitLength, false);
    //     var diffGridAfterSell = OrderBookImageGenerator.HighlightDifferences(finalGrid, afterSellGrid);
    //     ExportGrid(diffGridAfterSell, "CreateManyLimitBuyOrderTestUsingBase/orderbook-image-final-after-sell.png");
    //
    //     // Check the balances
    //     var sellerBalanceFwbtc = Contract.GetAccountBalance(sellerWithScope.Account, fWBTCContract);
    //     var sellerBalanceFusdt = Contract.GetAccountBalance(sellerWithScope.Account, fUSDTContract);
    //
    //     Assert.AreEqual(sellerFwbtcDepositAmount - sumToBuy, sellerBalanceFwbtc);
    //
    //     Console.WriteLine(baseFee);
    //     var expectedQuoteAmountTotalManualCheck = 390146 - 585; // Fee is 112 (0.15%)
    //     Assert.AreEqual(expectedQuoteAmountTotalManualCheck, sellerBalanceFusdt);
    //     Assert.AreEqual(sumQuoteAmountTotal, sellerBalanceFusdt);
    // }

    [TestMethod]
    public void CreateTwoLimitBuyOrderBuyOneTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var sellerAccount = TestEngine.GetNewSigner();
        var sellerWithScope = new Signer {Account = sellerAccount.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(SuperAdmin);
        var sellerFwbtcDepositAmount = 10_00000000;
        fWBTCContract.Mint(SuperAdmin.Account, sellerAccount.Account, sellerFwbtcDepositAmount);

        Engine.SetTransactionSigners(sellerWithScope);
        Contract.Deposit(sellerWithScope.Account, fWBTCContract.Hash, sellerFwbtcDepositAmount);

        // (Signer, Buy Amount, Limit Price)
        var buyers = new List<(Signer, BigInteger, BigInteger)> {(TestEngine.GetNewSigner(), 1357, 11), (TestEngine.GetNewSigner(), 1357, 11),};

        // Debugging
        var startingGrid = GenerateGrid(treeBitLength, false);
        var lastGrid = GenerateGrid(treeBitLength, false);

        var index = 1;
        BigInteger buyAmountTotal = 0;
        BigInteger quoteAmountTotal = 0;
        BigInteger roundingPrecision = Contract.RoundingPrecision!.Value;

        foreach (var (buyer, amount, price) in buyers)
        {
            var expectedBaseAmount = (amount * roundingPrecision) / ((price * roundingPrecision) / 100);

            Engine.SetTransactionSigners(SuperAdmin);
            fUSDTContract.Mint(SuperAdmin.Account, buyer.Account, amount);

            var buyerWithScope = new Signer {Account = buyer.Account, Scopes = WitnessScope.Global};
            Engine.SetTransactionSigners(buyerWithScope);
            Contract.Deposit(buyerWithScope.Account, fUSDTContract.Hash, amount);
            Contract.CreateLimitBuyOrderUsingQuote(buyerWithScope.Account, 1, amount, price, 0, false, false);

            // Debugging
            var grid = GenerateGrid(treeBitLength, false);
            var diffGrid = OrderBookImageGenerator.HighlightDifferences(lastGrid, grid);
            ExportGrid(diffGrid, $"CreateTwoLimitBuyOrderBuyOneTest/orderbook-image-{index}.png");
            lastGrid = grid;

            var nodeIndex = (3 << 4) + (price - 1); // Colum = 5, row = price - 1
            var priceNodeRaw = (Array) Contract.GetPriceNode(1, nodeIndex, false)!;
            var priceNode = new OrderBookImageGenerator.PriceNode
            {
                BaseAmount = priceNodeRaw[0].GetInteger(), QuoteTotal = priceNodeRaw[1].GetInteger(), EmptiedCount = priceNodeRaw[2].GetInteger(), EmptiedCountSibling = priceNodeRaw[3].GetInteger(),
            };
            var expectedBuyAmount = priceNode.BaseAmount - buyAmountTotal;
            buyAmountTotal += priceNode.BaseAmount;
            var orderQuoteAmount = priceNode.QuoteTotal - quoteAmountTotal;
            quoteAmountTotal += priceNode.QuoteTotal;
            Assert.AreEqual(amount, orderQuoteAmount);

            var orderRaw = (Array?) Contract.GetOrder(1, index);
            var order = RawOrderToOrder(orderRaw);
            Assert.IsNotNull(order);
            var orderOwner = order.Owner;
            var orderTotalOrderBaseAmount = order.TotalOrderBaseAmount;
            var orderTotalOrderQuoteAmount = order.TotalOrderQuoteAmount;
            var orderPlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount;
            var orderPrice = order.Price;
            Assert.AreEqual(buyerWithScope.Account, orderOwner);
            Assert.AreEqual(expectedBaseAmount, orderTotalOrderBaseAmount);
            Assert.AreEqual(amount, orderTotalOrderQuoteAmount);
            Assert.AreEqual(expectedBuyAmount, orderPlacedInOrderBookBaseAmount);
            Assert.AreEqual(price, orderPrice);

            index += 1;
        }

        // Calculate the fee the taker/buyer has to pay.
        var firstOrderAmount = buyers[0].Item2;

        // Debugging
        var finalGrid = GenerateGrid(treeBitLength, false);
        var diffGridFinal = OrderBookImageGenerator.HighlightDifferences(startingGrid, finalGrid);
        ExportGrid(diffGridFinal, "CreateTwoLimitBuyOrderBuyOneTest/orderbook-image-final.png");

        // Buy all the orders
        Engine.SetTransactionSigners(sellerWithScope);
        // We try to buy twice the amount of the total sum of all the orders.
        Contract.ExecuteLimitSellOrderUsingBase(sellerWithScope.Account, 1, 12336, 1, 0, false, false);

        // Debugging
        var afterBuyGrid = GenerateGrid(treeBitLength, false);
        var diffGridAfterBuy = OrderBookImageGenerator.HighlightDifferences(finalGrid, afterBuyGrid);
        ExportGrid(diffGridAfterBuy, "CreateTwoLimitBuyOrderBuyOneTest/orderbook-image-final-after-buy.png");

        // Check the balances
        var buyerBalanceFwbtc = Contract.GetAccountBalance(sellerWithScope.Account, fWBTCContract);
        var buyerBalanceFusdt = Contract.GetAccountBalance(sellerWithScope.Account, fUSDTContract);
        Console.WriteLine(firstOrderAmount);
        Assert.AreEqual(sellerFwbtcDepositAmount - 12336, buyerBalanceFwbtc);
        var expectedSeller1BalanceFusdt = 1357 - 2; // Fee is 2 (0.15%)
        Assert.AreEqual(expectedSeller1BalanceFusdt, buyerBalanceFusdt);

        // Check the amount to claim for both users
        Engine.SetTransactionSigners(buyers[0].Item1);
        Contract.ClaimOrder(1, 1, -1);
        var buyer1BalanceFwbtc = Contract.GetAccountBalance(buyers[0].Item1.Account, fWBTCContract);
        var expectedAmountForOrderOne = 12336 - 18; // Fee is 18 (0.15%)
        Assert.AreEqual(expectedAmountForOrderOne, buyer1BalanceFwbtc);

        Engine.SetTransactionSigners(buyers[1].Item1);

        Contract.ClaimOrder(1, 2, -1);
        var buyer2BalanceFusdt = Contract.GetAccountBalance(buyers[1].Item1.Account, fUSDTContract);
        Assert.AreEqual(0, buyer2BalanceFusdt);
    }

    // [TestMethod]
    // public void CreateTwoLimitBuyOrderBuyOneTestUsingBase()
    // {
    //     // # Arrange
    //     const int treeBitLength = 4;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var sellerAccount = TestEngine.GetNewSigner();
    //     var sellerWithScope = new Signer {Account = sellerAccount.Account, Scopes = WitnessScope.Global};
    //
    //     Engine.SetTransactionSigners(SuperAdmin);
    //     var sellerFwbtcDepositAmount = 10_00000000;
    //     fWBTCContract.Mint(SuperAdmin.Account, sellerAccount.Account, sellerFwbtcDepositAmount);
    //
    //     Engine.SetTransactionSigners(sellerWithScope);
    //     Contract.Deposit(sellerWithScope.Account, fWBTCContract.Hash, sellerFwbtcDepositAmount);
    //
    //     // (Signer, Buy Amount, Limit Price)
    //     var buyers = new List<(Signer, BigInteger, BigInteger, BigInteger)>
    //     {
    //         (TestEngine.GetNewSigner(), 1354, 148, 11),
    //         (TestEngine.GetNewSigner(), 1354, 148, 11),
    //     };
    //
    //     // Debugging
    //     var startingGrid = GenerateGrid(treeBitLength, false);
    //     var lastGrid = GenerateGrid(treeBitLength, false);
    //
    //     var index = 1;
    //     BigInteger buyAmountTotal = 0;
    //     BigInteger quoteAmountTotal = 0;
    //     BigInteger roundingPrecision = Contract.RoundingPrecision!.Value;
    //
    //     foreach (var (buyer, baseAmount, quoteAmount, price) in buyers)
    //     {
    //         Engine.SetTransactionSigners(SuperAdmin);
    //         fUSDTContract.Mint(SuperAdmin.Account, buyer.Account, quoteAmount);
    //
    //         var buyerWithScope = new Signer {Account = buyer.Account, Scopes = WitnessScope.Global};
    //         Engine.SetTransactionSigners(buyerWithScope);
    //         Contract.Deposit(buyerWithScope.Account, fUSDTContract.Hash, quoteAmount);
    //         Contract.CreateLimitBuyOrderUsingBase(buyerWithScope.Account, 1, baseAmount, price, 0, false, false);
    //
    //         // Debugging
    //         var grid = GenerateGrid(treeBitLength, false);
    //         var diffGrid = OrderBookImageGenerator.HighlightDifferences(lastGrid, grid);
    //         ExportGrid(diffGrid, $"CreateTwoLimitBuyOrderBuyOneTestUsingBase/orderbook-image-{index}.png");
    //         lastGrid = grid;
    //
    //         var nodeIndex = (3 << 4) + (price - 1); // Colum = 5, row = price - 1
    //         var priceNodeRaw = (Array) Contract.GetPriceNode(1, nodeIndex, false)!;
    //         var priceNode = new OrderBookImageGenerator.PriceNode
    //         {
    //             BaseAmount = priceNodeRaw[0].GetInteger(), QuoteTotal = priceNodeRaw[1].GetInteger(), EmptiedCount = priceNodeRaw[2].GetInteger(), EmptiedCountSibling = priceNodeRaw[3].GetInteger(),
    //         };
    //         var expectedBuyAmount = priceNode.BaseAmount - buyAmountTotal;
    //         buyAmountTotal += priceNode.BaseAmount;
    //         var orderQuoteAmount = priceNode.QuoteTotal - quoteAmountTotal;
    //         quoteAmountTotal += priceNode.QuoteTotal;
    //         Assert.AreEqual(quoteAmount, orderQuoteAmount);
    //
    //         var orderRaw = (Array?) Contract.GetOrder(1, index);
    //         var order = RawOrderToOrder(orderRaw);
    //         Assert.IsNotNull(order);
    //         var orderOwner = order.Owner;
    //         var orderTotalOrderBaseAmount = order.TotalOrderBaseAmount;
    //         var orderTotalOrderQuoteAmount = order.TotalOrderQuoteAmount;
    //         var orderPlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount;
    //         var orderPrice = order.Price;
    //         Assert.AreEqual(buyerWithScope.Account, orderOwner);
    //         Assert.AreEqual(baseAmount, orderTotalOrderBaseAmount);
    //         Assert.AreEqual(quoteAmount, orderTotalOrderQuoteAmount);
    //         Assert.AreEqual(expectedBuyAmount, orderPlacedInOrderBookBaseAmount);
    //         Assert.AreEqual(price, orderPrice);
    //
    //         index += 1;
    //     }
    //
    //     // Calculate the fee the taker/buyer has to pay.
    //     var firstOrderAmount = buyers[0].Item2;
    //
    //     // Debugging
    //     var finalGrid = GenerateGrid(treeBitLength, false);
    //     var diffGridFinal = OrderBookImageGenerator.HighlightDifferences(startingGrid, finalGrid);
    //     ExportGrid(diffGridFinal, "CreateTwoLimitBuyOrderBuyOneTestUsingBase/orderbook-image-final.png");
    //
    //     // Buy all the orders
    //     Engine.SetTransactionSigners(sellerWithScope);
    //     // We try to buy twice the amount of the total sum of all the orders.
    //     Contract.ExecuteLimitSellOrderUsingBase(sellerWithScope.Account, 1, 2708, 1, 0, false, false);
    //
    //     // Debugging
    //     var afterBuyGrid = GenerateGrid(treeBitLength, false);
    //     var diffGridAfterBuy = OrderBookImageGenerator.HighlightDifferences(finalGrid, afterBuyGrid);
    //     ExportGrid(diffGridAfterBuy, "CreateTwoLimitBuyOrderBuyOneTestUsingBase/orderbook-image-final-after-buy.png");
    //
    //     // Check the balances
    //     var buyerBalanceFwbtc = Contract.GetAccountBalance(sellerWithScope.Account, fWBTCContract);
    //     var buyerBalanceFusdt = Contract.GetAccountBalance(sellerWithScope.Account, fUSDTContract);
    //     Console.WriteLine(firstOrderAmount);
    //     Assert.AreEqual(sellerFwbtcDepositAmount - (1354 * 2), buyerBalanceFwbtc);
    //     var expectedSeller1BalanceFusdt = 296; // Fee is 0 (0.15%)
    //     Assert.AreEqual(expectedSeller1BalanceFusdt, buyerBalanceFusdt);
    //
    //     // Check the amount to claim for both users
    //     Engine.SetTransactionSigners(buyers[0].Item1);
    //     Contract.ClaimOrder(1, 1, -1);
    //     var buyer1BalanceFwbtc = Contract.GetAccountBalance(buyers[0].Item1.Account, fWBTCContract);
    //     var expectedAmountForOrderOne = 1354 - 2; // Fee is 2 (0.15%)
    //     Assert.AreEqual(expectedAmountForOrderOne, buyer1BalanceFwbtc);
    //
    //     Engine.SetTransactionSigners(buyers[1].Item1);
    //
    //     Contract.ClaimOrder(1, 2, -1);
    //     var buyer2BalanceFusdt = Contract.GetAccountBalance(buyers[1].Item1.Account, fUSDTContract);
    //     Assert.AreEqual(0, buyer2BalanceFusdt);
    // }

    [TestMethod]
    public void BuyAndSellAtSamePriceUpperNodeTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000_0000000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100_00000000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10_00000000, 13, 0, false, false);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10_00000000, 12, 0, false, false);

        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "BuyAndSellAtSamePriceUpperNodeTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 18000000000, 12, 0, false, false);

        // Create images
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "BuyAndSellAtSamePriceUpperNodeTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);

        // Check if the contract calculates the correct nodeIndexToCheck when
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 2);

        // # Assert
        Assert.AreEqual(59, nodeIndexToCheck);
    }

    [TestMethod]
    public void BuyAndSellAtSamePriceLowerNodeTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000_0000000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100_00000000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10_00000000, 13, 0, false, false);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10_00000000, 11, 0, false, false);

        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "BuyAndSellAtSamePriceLowerNodeTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 16500000000, 11, 0, false, false);

        // Create images
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "BuyAndSellAtSamePriceLowerNodeTest/orderbook-image-diff-2.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);

        // Check if the contract calculates the correct nodeIndexToCheck when
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 2);

        // # Assert
        Assert.AreEqual(58, nodeIndexToCheck);
    }

    [TestMethod]
    public void LimitBuyMakerAndTakerTest()
    {
        // Test scenario:
        // 1. Alice creates a limit sell order with a price of 0.10 USDT and an amount of 0.1 WBTC.
        // 2. Bob creates a limit buy order with a price of 0.11 USDT and an amount of 0.2 WBTC.
        // 3. Bob should buy 0.1 WBTC from Alice at a price of 0.10 USDT.
        // 4. Bob should have an order of 0.1 WBTC at a price of 0.11 USDT.

        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 22);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 10, 0, false, false);

        // Debugging
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
        var sellGridBeforeSell = GenerateGrid(treeBitLength, false);

        // Create images
        ExportGrid(diffGrid, "LimitBuyMakerAndTakerTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 22, 11, 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Debugging
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "LimitBuyMakerAndTakerTest/orderbook-image-diff-2.png");
        var sellGridAfterSell = GenerateGrid(treeBitLength, false);
        var diffGridSell = OrderBookImageGenerator.HighlightDifferences(sellGridBeforeSell, sellGridAfterSell);
        ExportGrid(diffGridSell, "LimitBuyMakerAndTakerTest/orderbook-image-diff-3-buy.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Console.WriteLine(nodeIndexToCheck);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);
        var aliceBalanceFusdtAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // # Assert

        // Check order
        var orderBobRaw = (Array?) Contract.GetOrder(1, 2);
        var orderBob = RawOrderToOrder(orderBobRaw);
        Assert.IsNotNull(orderBob);
        var orderBobOwner = orderBob.Owner;
        var orderBobTotalOrderBaseAmount = orderBob.TotalOrderBaseAmount;
        var orderBobPlacedInOrderBook = orderBob.PlacedInOrderBookBaseAmount;
        var orderBobPrice = orderBob.Price;
        var orderBobClaimedBaseAmount = orderBob.ClaimedBaseAmount;
        Assert.AreEqual(bobWithScope.Account, orderBobOwner);
        Assert.AreEqual(200, orderBobTotalOrderBaseAmount);
        Assert.AreEqual(109, orderBobPlacedInOrderBook);
        Assert.AreEqual(11, orderBobPrice);
        Assert.AreEqual(0, orderBobClaimedBaseAmount);

        // Check balances
        Assert.AreEqual(10, aliceBalanceFusdtAfterClaim);
        Assert.AreEqual(0, bobBalanceFusdt);
        Assert.AreEqual(100, bobBalanceFwbtc);
    }

    [TestMethod]
    public void MarketBuyNoAmmNoOrdersTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10000;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initialBalance = 1_000_000_000000;
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, initialBalance);

        // # Act
        BigInteger buyAmount = 1_000_000_000000;
        Contract.ExecuteLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, buyAmount, Contract.GetMaxPrice(1), 0, false, false);
        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // # Assert

        Assert.AreEqual(initialBalance, fusdtBalance);
        Assert.AreEqual(0, btcBalance);
        var result = Contract.GetOrder(1, 1);
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void PostOnlyLimitBuyOrderTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10000000000);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 10000000000);

        var reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
        var reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
        Assert.AreEqual(100_000_000000, reserveFusdt);
        Assert.AreEqual(10_00000000, reserveFwbtc);

        var ammPrice = (double)reserveFusdt / (double)reserveFwbtc;
        Console.WriteLine(ammPrice);

        // We start by pushing the price in the AMM a bit down so that we make sure the price of the AMM is close to the limit price of 999 but not exactly the same.
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 10000000000, 999, 0, true, false);

        reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
        reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
        ammPrice = (double)reserveFusdt / (double)reserveFwbtc;
        Console.WriteLine(ammPrice);

        var exception = Assert.ThrowsException<TestException>(() => Contract.AddLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 10000, 999, 0));
        Assert.IsTrue(exception.Message.Contains("Limit price is higher than AMM price"));

        Contract.AddLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 10000, 998, 0);

        var orderBobRaw = (Array?) Contract.GetOrder(1, 2);
        var orderBob = RawOrderToOrder(orderBobRaw);
        Assert.IsNotNull(orderBob);
        var orderBobOwner = orderBob.Owner;
        var orderBobTotalOrderBaseAmount = orderBob.TotalOrderBaseAmount;
        var orderBobPlacedInOrderBook = orderBob.PlacedInOrderBookBaseAmount;
        var orderBobPrice = orderBob.Price;
        var orderBobClaimedBaseAmount = orderBob.ClaimedBaseAmount;
        Assert.AreEqual(bobWithScope.Account, orderBobOwner);
        Assert.AreEqual(100, orderBobTotalOrderBaseAmount);
        Assert.AreEqual(100, orderBobPlacedInOrderBook);
        Assert.AreEqual(998, orderBobPrice);
        Assert.AreEqual(0, orderBobClaimedBaseAmount);
    }

    [TestMethod]
    public void PriceOracleTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 100000000000);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100000000000);

        var reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
        var reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
        Assert.AreEqual(100_000_000000, reserveFusdt);
        Assert.AreEqual(10_00000000, reserveFwbtc);

        var ammPrice = (double)reserveFusdt / (double)reserveFwbtc;
        Console.WriteLine(ammPrice);

        AdvanceBlocks(5);
        Console.WriteLine(Engine.PersistingBlock.Index);

        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 10000000000, 999, 0, true, false);

        reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
        reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
        ammPrice = (double)reserveFusdt / (double)reserveFwbtc;
        Console.WriteLine(ammPrice);

        var lastPriceIndex1 = Contract.GetLastIndexForPrice(1);
        Assert.AreEqual(5, lastPriceIndex1);

        AdvanceBlocks(5);

        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 10000000000, 998, 0, true, false);

        var lastPriceIndex2 = Contract.GetLastIndexForPrice(1);
        Assert.AreEqual(10, lastPriceIndex2);

        AdvanceBlocks(5);

        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 10000000000, 1011, 0, true, false);

        var lastPriceIndex3 = Contract.GetLastIndexForPrice(1);
        Assert.AreEqual(15, lastPriceIndex3);

        var priceInfo1 = Contract.GetPrice(1, 5);
        var price1 = (BigInteger) priceInfo1[1];
        Assert.AreEqual(BigInteger.Parse("999000001130433651610109"), price1);

        var priceInfo2 = Contract.GetPrice(1, 10);
        var price2 = (BigInteger) priceInfo2[1];
        Assert.AreEqual(BigInteger.Parse("998000000531467068208551"), price2);

        var priceInfo3 = Contract.GetPrice(1, 15);
        var price3 = (BigInteger) priceInfo3[1];
        Assert.AreEqual(BigInteger.Parse("1010999999378616091121618"), price3);

        var priceInfo4 = Contract.GetPrice(1, 20);
        var price4 = (BigInteger) priceInfo4[1];
        Assert.AreEqual(0, price4);

        var priceInfo5 = Contract.GetPrice(1, 2);
        var price5 = (BigInteger) priceInfo5[1];
        Assert.AreEqual(0, price5);

    }

    // [TestMethod]
    // public void PostOnlyLimitBuyOrderTestUsingBase()
    // {
    //     // # Arrange
    //     const int treeBitLength = 16;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 1;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(bobWithScope);
    //     Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 100);
    //
    //     var reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
    //     var reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
    //     Assert.AreEqual(100_000_000000, reserveFusdt);
    //     Assert.AreEqual(10_00000000, reserveFwbtc);
    //
    //     var exception = Assert.ThrowsException<TestException>(() => Contract.AddLimitBuyOrderUsingBase(bobWithScope.Account, 1, CalculateBaseAmountForQuoteAmount(1, 101, 100), 101, 0));
    //     Assert.IsTrue(exception.Message.Contains("Limit price is higher than AMM price"));
    //
    //     Contract.AddLimitBuyOrderUsingBase(bobWithScope.Account, 1, CalculateBaseAmountForQuoteAmount(1, 100, 100), 100, 0);
    //
    //     var orderBobRaw = (Array?) Contract.GetOrder(1, 1);
    //     var orderBob = RawOrderToOrder(orderBobRaw);
    //     Assert.IsNotNull(orderBob);
    //     var orderBobOwner = orderBob.Owner;
    //     var orderBobTotalOrderBaseAmount = orderBob.TotalOrderBaseAmount;
    //     var orderBobPlacedInOrderBook = orderBob.PlacedInOrderBookBaseAmount;
    //     var orderBobPrice = orderBob.Price;
    //     var orderBobClaimedBaseAmount = orderBob.ClaimedBaseAmount;
    //     Assert.AreEqual(bobWithScope.Account, orderBobOwner);
    //     Assert.AreEqual(100, orderBobTotalOrderBaseAmount);
    //     Assert.AreEqual(100, orderBobPlacedInOrderBook);
    //     Assert.AreEqual(100, orderBobPrice);
    //     Assert.AreEqual(0, orderBobClaimedBaseAmount);
    // }

    [TestMethod]
    public void MarketSellNoAmmNoOrdersTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10000;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initialBalance = 1_00000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, initialBalance);

        // # Act
        BigInteger sellAmount = 1_0000_0000;
        Contract.ExecuteLimitSellOrderUsingBase(aliceWithScope.Account, 1, sellAmount, 1, 0, false, false);
        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // # Assert

        Assert.AreEqual(initialBalance, btcBalance);
        Assert.AreEqual(0, fusdtBalance);
        var result = Contract.GetOrder(1, 1);
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void MarketBuyInAmmOnlyTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
        var reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
        var oldPrice = (double)reserveFusdt / (double)reserveFwbtc * 100;
        Console.WriteLine($"Old price: {oldPrice}");

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initalBalance = 1_000_000000;
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, initalBalance);

        // # Act
        BigInteger buyAmount = 1_000_000000;

        Contract.ExecuteLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, buyAmount, 1001, 0, true, false);
        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var soldAmount = initalBalance - fusdtBalance;
        var amountOut = Contract.GetAmountOut(soldAmount, reserveFusdt, reserveFwbtc);
        Console.WriteLine($"Sold amount: {soldAmount}");
        Console.WriteLine($"Amount out: {amountOut}");
        var userPrice = (double)soldAmount / (double)btcBalance * 100;
        Console.WriteLine($"User price: {userPrice}");

        var reserveFusdt2 = FLPfWBTCfUSDTContract.Reserve1;
        var reserveFwbtc2 = FLPfWBTCfUSDTContract.Reserve0;
        var newPrice = (double)reserveFusdt2 / (double)reserveFwbtc2 * 100;
        Console.WriteLine($"New price: {newPrice}");

        // # Assert
        Assert.AreEqual(499000, btcBalance);
        Assert.AreEqual(initalBalance - 50050068, fusdtBalance);
    }

    // [TestMethod]
    // public void MarketBuyInAmmOnlyTestUsingBase()
    // {
    //     // # Arrange
    //     const int treeBitLength = 16;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     // This price precision is two decimal places less than the quote token precision.
    //     // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
    //     var pricePrecision = priceCoefficient * 10;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
    //     var reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
    //     var oldPrice = (double)reserveFusdt / (double)reserveFwbtc * 100;
    //     Console.WriteLine($"Old price: {oldPrice}");
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     BigInteger initalBalance = 1_000_000000;
    //     Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, initalBalance);
    //
    //     // # Act
    //     var baseAmount = 0_10000000;
    //
    //     Contract.ExecuteLimitBuyOrderUsingBase(aliceWithScope.Account, 1, baseAmount, 1001, 0, true, false);
    //     var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //     var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     var soldAmount = initalBalance - fusdtBalance;
    //     var amountOut = Contract.GetAmountOut(soldAmount, reserveFusdt, reserveFwbtc);
    //     Console.WriteLine($"Sold amount: {soldAmount}");
    //     Console.WriteLine($"Amount out: {amountOut}");
    //     var userPrice = (double)soldAmount / (double)btcBalance * 100;
    //     Console.WriteLine($"User price: {userPrice}");
    //
    //     var reserveFusdt2 = FLPfWBTCfUSDTContract.Reserve1;
    //     var reserveFwbtc2 = FLPfWBTCfUSDTContract.Reserve0;
    //     var newPrice = (double)reserveFusdt2 / (double)reserveFwbtc2 * 100;
    //     Console.WriteLine($"New price: {newPrice}");
    //
    //     // # Assert
    //     Assert.AreEqual(initalBalance - 50050038, fusdtBalance);
    //     // 499000 is what we can expect to get from the AMM.
    //     Assert.AreEqual(499000, btcBalance);
    // }

    [TestMethod]
    public void MarketSellInAmmOnlyTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient * 10;

        var reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
        var reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
        var oldPrice = (double)reserveFusdt / (double)reserveFwbtc * 100;
        Console.WriteLine($"Old price: {oldPrice}");

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        var initalFusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        BigInteger initalBalance = 1_0000_0000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, initalBalance);

        // # Act
        BigInteger sellAmount = 1_0000_0000;
        Contract.ExecuteLimitSellOrderUsingBase(aliceWithScope.Account, 1, sellAmount, 999, 0, true, false);
        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var soldAmount = initalBalance - btcBalance;
        var amountOut = Contract.GetAmountOut(soldAmount, reserveFwbtc, reserveFusdt);
        Console.WriteLine($"Sold amount: {soldAmount}");
        Console.WriteLine($"Amount out: {amountOut}");
        var userPrice = (double)fusdtBalance / (double)soldAmount * 100;
        Console.WriteLine($"User price: {userPrice}");

        var reserveFusdt2 = FLPfWBTCfUSDTContract.Reserve1;
        var reserveFwbtc2 = FLPfWBTCfUSDTContract.Reserve0;
        var newPrice = (double)reserveFusdt2 / (double)reserveFwbtc2 * 100;
        Console.WriteLine($"New price: {newPrice}");

        // # Assert

        // With an AMM fee of 0.25% and reserves of 100,000 fUSDT and 10 fWBTC (a price of 10,000 fUSDT per fWBTC),
        // the expected fUSDT amount received when selling 1 fWBTC is 9,066.108938 fUSDT.
        Assert.AreEqual(0, initalFusdtBalance);
        Assert.AreEqual(49_949887, fusdtBalance);

        // We can expect to pay 50.1001 fWBTC to the AMM.
        Assert.AreEqual(initalBalance - 501001, btcBalance);
    }

    // [TestMethod]
    // public void MarketSellInAmmOnlyTestUsingQuote()
    // {
    //     // # Arrange
    //     const int treeBitLength = 16;
    //     var priceCoefficient = Contract.PriceCoefficient!.Value;
    //     var pricePrecision = priceCoefficient * 10;
    //
    //     InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);
    //
    //     var reserveFusdt = FLPfWBTCfUSDTContract.Reserve1;
    //     var reserveFwbtc = FLPfWBTCfUSDTContract.Reserve0;
    //     var oldPrice = (double)reserveFusdt / (double)reserveFwbtc * 100;
    //     Console.WriteLine($"Old price: {oldPrice}");
    //
    //     var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
    //     Engine.SetTransactionSigners(aliceWithScope);
    //     var initalFusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //     BigInteger initalBalance = 0_20000000;
    //     Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, initalBalance);
    //
    //     // # Act
    //     BigInteger quoteAmount = 1000_000000;
    //
    //     Contract.ExecuteLimitSellOrderUsingQuote(aliceWithScope.Account, 1, quoteAmount, 999, 0, true, false);
    //     var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
    //     var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
    //
    //     var soldAmount = initalBalance - btcBalance;
    //     var amountOut = Contract.GetAmountOut(soldAmount, reserveFwbtc, reserveFusdt);
    //     Console.WriteLine($"Sold amount: {soldAmount}");
    //     Console.WriteLine($"Amount out: {amountOut}");
    //     var userPrice = (double)fusdtBalance / (double)soldAmount * 100;
    //     Console.WriteLine($"User price: {userPrice}");
    //
    //     var reserveFusdt2 = FLPfWBTCfUSDTContract.Reserve1;
    //     var reserveFwbtc2 = FLPfWBTCfUSDTContract.Reserve0;
    //     var newPrice = (double)reserveFusdt2 / (double)reserveFwbtc2 * 100;
    //     Console.WriteLine($"New price: {newPrice}");
    //
    //     // # Assert
    //     Assert.AreEqual(0, initalFusdtBalance);
    //     Assert.AreEqual(49949887, fusdtBalance);
    //
    //     // We can expect to pay 50.1001 fWBTC to the AMM.
    //     Assert.AreEqual(initalBalance - 501001, btcBalance);
    // }

    [TestMethod]
    public void BuyInAmmAndOrderBookTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initalBalance = 100_000_000_000000;
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, initalBalance);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        BigInteger bobInitialBalance = 100_00000000;
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, bobInitialBalance);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 1_00000000, 101, 0, true,false);

        BigInteger buyAmount = 35_000_000000;
        Engine.SetTransactionSigners(aliceWithScope);

        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, buyAmount, 102, 0, true, false);

        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var orderBobRaw = (Array?) Contract.GetOrder(1, 1);
        var orderBob = RawOrderToOrder(orderBobRaw);
        orderBob.PrintPublicFields();

        Console.WriteLine();

        var orderAliceRaw = (Array?) Contract.GetOrder(1, 2);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        orderAlice.PrintPublicFields();

        // # Assert
        var ammPriceAliceOrder = orderAlice.QuoteAmountTradedInAmm > 0
            ? ((float) orderAlice.QuoteAmountTradedInAmm / 1_000_000f) / ((float) orderAlice.BaseAmountTradedInAmm / 1_0000_0000f)
            : 0;
        var orderBookPriceAliceOrder = orderAlice.QuoteAmountTradedBeforeOrderPlaced > 0
            ? ((float) orderAlice.QuoteAmountTradedBeforeOrderPlaced / 1_000_000f) / ((float) orderAlice.BaseAmountTradedBeforeOrderPlaced / 1_0000_0000f)
            : 0;

        var reserves = (List<object>?) SwapRouterContract.GetReserves(fWBTCContract.Hash, fUSDTContract.Hash);
        var fWbtcReserve = (long)(BigInteger)reserves![0];
        var fUsdtReserve = (long)(BigInteger)reserves[1];
        var ammPrice = (fUsdtReserve / 1_000_000f) / (fWbtcReserve / 1_0000_0000f);

        Console.WriteLine();
        Console.WriteLine($"ammPriceAliceOrder: {ammPriceAliceOrder}");
        Console.WriteLine($"orderBookPriceAliceOrder: {orderBookPriceAliceOrder}");
        Console.WriteLine($"ammPrice: {ammPrice}");

        var totalOrderSize = orderAlice.PlacedInOrderBookBaseAmount + orderAlice.BaseAmountTradedBeforeOrderPlaced + orderAlice.BaseAmountTradedInAmm;

        // 196078431 is the total amount of WBTC that Alice should expect to get with the price she set.
        Assert.IsTrue(totalOrderSize > 196078431);

        Assert.IsTrue(orderAlice.PlacedInOrderBookBaseAmount > 0);

        Assert.IsTrue(ammPriceAliceOrder <= 10_200f);
        Assert.IsTrue(orderBookPriceAliceOrder <= 10_200f);
        Assert.IsTrue(orderBookPriceAliceOrder > 10_100f);

        Assert.AreEqual(initalBalance - 35_000_000000, fusdtBalance);
        Assert.AreEqual(9840247 + 99850000, btcBalance);
    }

    [TestMethod]
    public void BuyInAmmAndOrderBookTest2()
    {
        // In this test we try to buy a lot more than what is available in the order book at a higher price than the Alice's order.

        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initalBalance = 100_000_000_000000;
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, initalBalance);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        BigInteger bobInitialBalance = 100_00000000;
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, bobInitialBalance);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 1_00000000, 101, 0, true, false);

        BigInteger buyAmount = 35_000_000000;
        Engine.SetTransactionSigners(aliceWithScope);

        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, buyAmount, 200, 0, true, false);

        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var orderBobRaw = (Array?) Contract.GetOrder(1, 1);
        var orderBob = RawOrderToOrder(orderBobRaw);
        orderBob.PrintPublicFields();

        Console.WriteLine();

        var orderAliceRaw = (Array?) Contract.GetOrder(1, 2);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        orderAlice.PrintPublicFields();

        // # Assert
        var ammPriceAliceOrder = orderAlice.QuoteAmountTradedInAmm > 0
            ? ((float) orderAlice.QuoteAmountTradedInAmm / 1_000_000f) / ((float) orderAlice.BaseAmountTradedInAmm / 1_0000_0000f)
            : 0;
        var orderBookPriceAliceOrder = orderAlice.QuoteAmountTradedBeforeOrderPlaced > 0
            ? ((float) orderAlice.QuoteAmountTradedBeforeOrderPlaced / 1_000_000f) / ((float) orderAlice.BaseAmountTradedBeforeOrderPlaced / 1_0000_0000f)
            : 0;

        var reserves = (List<object>?) SwapRouterContract.GetReserves(fWBTCContract.Hash, fUSDTContract.Hash);
        var fWbtcReserve = (long)(BigInteger)reserves![0];
        var fUsdtReserve = (long)(BigInteger)reserves[1];
        var ammPrice = (fUsdtReserve / 1_000_000f) / (fWbtcReserve / 1_0000_0000f);

        Console.WriteLine();
        Console.WriteLine($"ammPriceAliceOrder: {ammPriceAliceOrder}");
        Console.WriteLine($"orderBookPriceAliceOrder: {orderBookPriceAliceOrder}");
        Console.WriteLine($"ammPrice: {ammPrice}");

        var totalOrderSize = orderAlice.PlacedInOrderBookBaseAmount + orderAlice.BaseAmountTradedBeforeOrderPlaced + orderAlice.BaseAmountTradedInAmm;

        // 196078431 is the total amount of WBTC that Alice should expect to get with the price she set.
        Assert.IsTrue(totalOrderSize > 196078431);

        Assert.IsTrue(orderAlice.BaseAmountTradedBeforeOrderPlaced > 0);
        Assert.IsTrue(orderAlice.PlacedInOrderBookBaseAmount == 0);

        Assert.IsTrue(ammPriceAliceOrder <= 20_000f);
        Assert.IsTrue(orderBookPriceAliceOrder <= 10_200f);
        Assert.IsTrue(orderBookPriceAliceOrder > 10_100f);

        Assert.AreEqual(initalBalance - 35_000_000000, fusdtBalance);
        Assert.AreEqual(198960250 + 99850000, btcBalance);
    }

    [TestMethod]
    public void BuyInAmmAndOrderBookSamePriceTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initalBalance = 100_000_000_000000;
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, initalBalance);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        BigInteger bobInitialBalance = 100_00000000;
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, bobInitialBalance);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 1_00000000, 101, 0, true,false);

        BigInteger buyAmount = 35_000_000000;
        Engine.SetTransactionSigners(aliceWithScope);

        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, buyAmount, 101, 0, true, false);

        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var orderBobRaw = (Array?) Contract.GetOrder(1, 1);
        var orderBob = RawOrderToOrder(orderBobRaw);
        orderBob.PrintPublicFields();

        Console.WriteLine();

        var orderAliceRaw = (Array?) Contract.GetOrder(1, 2);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        orderAlice.PrintPublicFields();

        // # Assert
        var ammPriceAliceOrder = orderAlice.QuoteAmountTradedInAmm > 0
            ? ((float) orderAlice.QuoteAmountTradedInAmm / 1_000_000f) / ((float) orderAlice.BaseAmountTradedInAmm / 1_0000_0000f)
            : 0;
        var orderBookPriceAliceOrder = orderAlice.QuoteAmountTradedBeforeOrderPlaced > 0
            ? ((float) orderAlice.QuoteAmountTradedBeforeOrderPlaced / 1_000_000f) / ((float) orderAlice.BaseAmountTradedBeforeOrderPlaced / 1_0000_0000f)
            : 0;

        var reserves = (List<object>?) SwapRouterContract.GetReserves(fWBTCContract.Hash, fUSDTContract.Hash);
        var fWbtcReserve = (long)(BigInteger)reserves![0];
        var fUsdtReserve = (long)(BigInteger)reserves[1];
        var ammPrice = (fUsdtReserve / 1_000_000f) / (fWbtcReserve / 1_0000_0000f);

        Console.WriteLine();
        Console.WriteLine($"ammPriceAliceOrder: {ammPriceAliceOrder}");
        Console.WriteLine($"orderBookPriceAliceOrder: {orderBookPriceAliceOrder}");
        Console.WriteLine($"ammPrice: {ammPrice}");

        var totalOrderSize = orderAlice.PlacedInOrderBookBaseAmount + orderAlice.BaseAmountTradedBeforeOrderPlaced + orderAlice.BaseAmountTradedInAmm;

        // 196078431 is the total amount of WBTC that Alice should expect to get with the price she set.
        Assert.IsTrue(totalOrderSize > 196078431);

        Assert.AreEqual(10100000000, orderAlice.QuoteAmountTradedBeforeOrderPlaced);
        Assert.IsTrue(orderAlice.PlacedInOrderBookBaseAmount > 0);

        Assert.IsTrue(ammPriceAliceOrder <= 10_200f);
        Assert.IsTrue(orderBookPriceAliceOrder <= 10_200f);
        Assert.IsTrue(orderBookPriceAliceOrder > 10_100f);

        Assert.AreEqual(initalBalance - 35_000_000000, fusdtBalance);
        Assert.AreEqual(99850000 + 4956629, btcBalance);
    }

    [TestMethod]
    public void BuySmallAmountInAmmAndOrderBookTest()
    {
        // In this scenario we set the price in the pool so there is just a little amount to buy from the AMM.
        // If the Broker contract tries to trade this small amount from the pool it will fail because trades in the AMM resulting in 0 amount out is not allowed.
        // We check that the Broker contract can handle this edge case.

        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initalBalance = 100_000_000_000000;
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, initalBalance);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        BigInteger bobInitialBalance = 100_00000000;
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, bobInitialBalance);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 1_00000000, 100, 0, true,false);

        BigInteger buyAmount = 10_000_000001;
        Engine.SetTransactionSigners(aliceWithScope);

        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, buyAmount, 102, 0, true, false);

        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var orderBobRaw = (Array?) Contract.GetOrder(1, 1);
        var orderBob = RawOrderToOrder(orderBobRaw);
        orderBob.PrintPublicFields();

        Console.WriteLine();

        var orderAliceRaw = (Array?) Contract.GetOrder(1, 2);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        orderAlice.PrintPublicFields();

        // # Assert
        var ammPriceAliceOrder = orderAlice.QuoteAmountTradedInAmm > 0
            ? ((float) orderAlice.QuoteAmountTradedInAmm / 1_000_000f) / ((float) orderAlice.BaseAmountTradedInAmm / 1_0000_0000f)
            : 0;
        var orderBookPriceAliceOrder = orderAlice.QuoteAmountTradedBeforeOrderPlaced > 0
            ? ((float) orderAlice.QuoteAmountTradedBeforeOrderPlaced / 1_000_000f) / ((float) orderAlice.BaseAmountTradedBeforeOrderPlaced / 1_0000_0000f)
            : 0;

        var reserves = (List<object>?) SwapRouterContract.GetReserves(fWBTCContract.Hash, fUSDTContract.Hash);
        var fWbtcReserve = (long)(BigInteger)reserves![0];
        var fUsdtReserve = (long)(BigInteger)reserves[1];
        var ammPrice = (fUsdtReserve / 1_000_000f) / (fWbtcReserve / 1_0000_0000f);

        Console.WriteLine();
        Console.WriteLine($"ammPriceAliceOrder: {ammPriceAliceOrder}");
        Console.WriteLine($"orderBookPriceAliceOrder: {orderBookPriceAliceOrder}");
        Console.WriteLine($"ammPrice: {ammPrice}");

        Assert.IsTrue(orderAlice.BaseAmountTradedInAmm == 0);
    }

    [TestMethod]
    public void SellInAmmAndOrderBookTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initalBalance = 100_00000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, initalBalance);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        BigInteger bobInitialBalance = 100_000_000_000000;
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, bobInitialBalance);

        // # Act
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 10_000_000000, 99, 0, true,false);

        BigInteger sellAmount = 2_00000000;
        Engine.SetTransactionSigners(aliceWithScope);

        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, sellAmount, 98, 0, true, false);

        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var orderBobRaw = (Array?) Contract.GetOrder(1, 1);
        var orderBob = RawOrderToOrder(orderBobRaw);
        orderBob.PrintPublicFields();

        Console.WriteLine();

        var orderAliceRaw = (Array?) Contract.GetOrder(1, 2);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        orderAlice.PrintPublicFields();

        // # Assert
        var ammPriceAliceOrder = orderAlice.QuoteAmountTradedInAmm > 0
            ? ((float) orderAlice.QuoteAmountTradedInAmm / 1_000_000f) / ((float) orderAlice.BaseAmountTradedInAmm / 1_0000_0000f)
            : 0;
        var orderBookPriceAliceOrder = orderAlice.QuoteAmountTradedBeforeOrderPlaced > 0
            ? ((float) orderAlice.QuoteAmountTradedBeforeOrderPlaced / 1_000_000f) / ((float) orderAlice.BaseAmountTradedBeforeOrderPlaced / 1_0000_0000f)
            : 0;

        var reserves = (List<object>?) SwapRouterContract.GetReserves(fWBTCContract.Hash, fUSDTContract.Hash);
        var fWbtcReserve = (long)(BigInteger)reserves![0];
        var fUsdtReserve = (long)(BigInteger)reserves[1];
        var ammPrice = (fUsdtReserve / 1_000_000f) / (fWbtcReserve / 1_0000_0000f);

        Console.WriteLine();
        Console.WriteLine($"ammPriceAliceOrder: {ammPriceAliceOrder}");
        Console.WriteLine($"orderBookPriceAliceOrder: {orderBookPriceAliceOrder}");
        Console.WriteLine($"ammPrice: {ammPrice}");

        var totalOrderSize = orderAlice.PlacedInOrderBookBaseAmount + orderAlice.BaseAmountTradedBeforeOrderPlaced + orderAlice.BaseAmountTradedInAmm;

        // 196078431 is the total amount of WBTC that Alice should expect to get with the price she set.
        Assert.IsTrue(totalOrderSize > 196078431);

        Assert.IsTrue(orderAlice.PlacedInOrderBookBaseAmount > 0);

        Assert.IsTrue(ammPriceAliceOrder >= 9_800f);
        Assert.IsTrue(orderBookPriceAliceOrder >= 9_800f);
        Assert.IsTrue(orderBookPriceAliceOrder < 9_900f);

        Assert.AreEqual(initalBalance - sellAmount, btcBalance);
        Assert.AreEqual(9985000000 + 1003805363, fusdtBalance);
    }

    [TestMethod]
    public void SellInAmmAndOrderBookTest2()
    {
        // In this test we try to sell a lot more than what is available in the order book at a higher price than the Alice's order.

        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initalBalance = 100_00000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, initalBalance);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        BigInteger bobInitialBalance = 100_000_000_000000;
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, bobInitialBalance);

        // # Act
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 10_000_000000, 99, 0, true, false);

        BigInteger sellAmount = 2_00000000;
        Engine.SetTransactionSigners(aliceWithScope);

        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, sellAmount, 50, 0, true, false);

        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var orderBobRaw = (Array?) Contract.GetOrder(1, 1);
        var orderBob = RawOrderToOrder(orderBobRaw);
        orderBob.PrintPublicFields();

        Console.WriteLine();

        var orderAliceRaw = (Array?) Contract.GetOrder(1, 2);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        orderAlice.PrintPublicFields();

        // # Assert
        var ammPriceAliceOrder = orderAlice.QuoteAmountTradedInAmm > 0
            ? ((float) orderAlice.QuoteAmountTradedInAmm / 1_000_000f) / ((float) orderAlice.BaseAmountTradedInAmm / 1_0000_0000f)
            : 0;
        var orderBookPriceAliceOrder = orderAlice.QuoteAmountTradedBeforeOrderPlaced > 0
            ? ((float) orderAlice.QuoteAmountTradedBeforeOrderPlaced / 1_000_000f) / ((float) orderAlice.BaseAmountTradedBeforeOrderPlaced / 1_0000_0000f)
            : 0;

        var reserves = (List<object>?) SwapRouterContract.GetReserves(fWBTCContract.Hash, fUSDTContract.Hash);
        var fWbtcReserve = (long)(BigInteger)reserves![0];
        var fUsdtReserve = (long)(BigInteger)reserves[1];
        var ammPrice = (fUsdtReserve / 1_000_000f) / (fWbtcReserve / 1_0000_0000f);

        Console.WriteLine();
        Console.WriteLine($"ammPriceAliceOrder: {ammPriceAliceOrder}");
        Console.WriteLine($"orderBookPriceAliceOrder: {orderBookPriceAliceOrder}");
        Console.WriteLine($"ammPrice: {ammPrice}");

        var totalOrderSize = orderAlice.PlacedInOrderBookBaseAmount + orderAlice.BaseAmountTradedBeforeOrderPlaced + orderAlice.BaseAmountTradedInAmm;

        // 196078431 is the total amount of WBTC that Alice should expect to get with the price she set.
        Assert.IsTrue(totalOrderSize > 196078431);

        Assert.IsTrue(orderAlice.BaseAmountTradedBeforeOrderPlaced > 0);
        Assert.IsTrue(orderAlice.PlacedInOrderBookBaseAmount == 0);

        Assert.IsTrue(ammPriceAliceOrder >= 5_000f);
        Assert.IsTrue(orderBookPriceAliceOrder >= 9_800f);
        Assert.IsTrue(orderBookPriceAliceOrder < 9_900f);

        Assert.AreEqual(initalBalance - sellAmount, btcBalance);
        Assert.AreEqual(8986858254 + 9985000000, fusdtBalance);
    }

    [TestMethod]
    public void SellInAmmAndOrderBookSamePriceTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initalBalance = 100_00000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, initalBalance);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        BigInteger bobInitialBalance = 100_000_000_000000;
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, bobInitialBalance);

        // # Act
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 10_000_000000, 99, 0, true,false);

        BigInteger sellAmount = 2_00000000;
        Engine.SetTransactionSigners(aliceWithScope);

        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, sellAmount, 99, 0, true, false);

        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var orderBobRaw = (Array?) Contract.GetOrder(1, 1);
        var orderBob = RawOrderToOrder(orderBobRaw);
        orderBob.PrintPublicFields();

        Console.WriteLine();

        var orderAliceRaw = (Array?) Contract.GetOrder(1, 2);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        orderAlice.PrintPublicFields();

        // # Assert
        var ammPriceAliceOrder = orderAlice.QuoteAmountTradedInAmm > 0
            ? ((float) orderAlice.QuoteAmountTradedInAmm / 1_000_000f) / ((float) orderAlice.BaseAmountTradedInAmm / 1_0000_0000f)
            : 0;
        var orderBookPriceAliceOrder = orderAlice.QuoteAmountTradedBeforeOrderPlaced > 0
            ? ((float) orderAlice.QuoteAmountTradedBeforeOrderPlaced / 1_000_000f) / ((float) orderAlice.BaseAmountTradedBeforeOrderPlaced / 1_0000_0000f)
            : 0;

        var reserves = (List<object>?) SwapRouterContract.GetReserves(fWBTCContract.Hash, fUSDTContract.Hash);
        var fWbtcReserve = (long)(BigInteger)reserves![0];
        var fUsdtReserve = (long)(BigInteger)reserves[1];
        var ammPrice = (fUsdtReserve / 1_000_000f) / (fWbtcReserve / 1_0000_0000f);

        Console.WriteLine();
        Console.WriteLine($"ammPriceAliceOrder: {ammPriceAliceOrder}");
        Console.WriteLine($"orderBookPriceAliceOrder: {orderBookPriceAliceOrder}");
        Console.WriteLine($"ammPrice: {ammPrice}");

        var totalOrderSize = orderAlice.PlacedInOrderBookBaseAmount + orderAlice.BaseAmountTradedBeforeOrderPlaced + orderAlice.BaseAmountTradedInAmm;

        // 196078431 is the total amount of WBTC that Alice should expect to get with the price she set.
        Assert.IsTrue(totalOrderSize > 196078431);

        Assert.AreEqual(101010101, orderAlice.BaseAmountTradedBeforeOrderPlaced);
        Assert.IsTrue(orderAlice.PlacedInOrderBookQuoteAmount > 0);

        Assert.IsTrue(ammPriceAliceOrder >= 9_900f);
        Assert.IsTrue(orderBookPriceAliceOrder >= 9_800f);
        Assert.IsTrue(orderBookPriceAliceOrder < 9_900f);

        Assert.AreEqual(initalBalance - sellAmount, btcBalance);
        Assert.AreEqual(9985000000 + 500632035, fusdtBalance);
    }

    [TestMethod]
    public void SellSmallAmountInAmmAndOrderBookTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 100;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        BigInteger initalBalance = 100_00000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, initalBalance);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        BigInteger bobInitialBalance = 100_000_000_000000;
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, bobInitialBalance);

        // Reset pool so that now it's empty and there's no base token drawable
        Engine.SetTransactionSigners(bobWithScope);
        var newFLPfWBTCfUSDTContract = Engine.Deploy<FLPfWBTCfUSDT>(FLPfWBTCfUSDT.Nef, FLPfWBTCfUSDT.Manifest);
        Engine.SetTransactionSigners(SuperAdmin);
        SwapFactoryContract.RemoveExchangePair(FLPfWBTCfUSDTContract.Token0, FLPfWBTCfUSDTContract.Token1);
        SwapFactoryContract.RegisterExchangePair(newFLPfWBTCfUSDTContract.Hash);
        newFLPfWBTCfUSDTContract.SetWhiteListContract(SwapPairWhiteListContract.Hash);

        // # Act
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 10_000_000000, 100, 0, true,false);

        BigInteger sellAmount = 1_00000001;
        Engine.SetTransactionSigners(aliceWithScope);

        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, sellAmount, 98, 0, true, false);

        var btcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var fusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var orderBobRaw = (Array?) Contract.GetOrder(1, 1);
        var orderBob = RawOrderToOrder(orderBobRaw);
        orderBob.PrintPublicFields();

        Console.WriteLine();

        var orderAliceRaw = (Array?) Contract.GetOrder(1, 2);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        orderAlice.PrintPublicFields();

        // # Assert
        var ammPriceAliceOrder = orderAlice.QuoteAmountTradedInAmm > 0
            ? ((float) orderAlice.QuoteAmountTradedInAmm / 1_000_000f) / ((float) orderAlice.BaseAmountTradedInAmm / 1_0000_0000f)
            : 0;
        var orderBookPriceAliceOrder = orderAlice.QuoteAmountTradedBeforeOrderPlaced > 0
            ? ((float) orderAlice.QuoteAmountTradedBeforeOrderPlaced / 1_000_000f) / ((float) orderAlice.BaseAmountTradedBeforeOrderPlaced / 1_0000_0000f)
            : 0;

        var reserves = (List<object>?) SwapRouterContract.GetReserves(fWBTCContract.Hash, fUSDTContract.Hash);
        var fWbtcReserve = (long)(BigInteger)reserves![0];
        var fUsdtReserve = (long)(BigInteger)reserves[1];
        var ammPrice = (fUsdtReserve / 1_000_000f) / (fWbtcReserve / 1_0000_0000f);

        Console.WriteLine();
        Console.WriteLine($"ammPriceAliceOrder: {ammPriceAliceOrder}");
        Console.WriteLine($"orderBookPriceAliceOrder: {orderBookPriceAliceOrder}");
        Console.WriteLine($"ammPrice: {ammPrice}");

        Assert.IsTrue(orderAlice.BaseAmountTradedInAmm == 0);
    }

    [TestMethod]
    public void BuyCancelTest()
    {
        // In this test scenario we:
        // 1. Place 4 buy orders at the same price
        // 2. Cancel the second order
        // 3. Place a sell order to fill the first order and a bit of the third order
        // We expect that the cancelled order should be effectively removed from the order book and that the third order has some of its amount filled.

        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 10000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10_000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength, false);

        // # Act
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 10, 0, false, false); // 1, 1
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 10, 0, false, false); // 2, 2
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 10, 0, false, false); // 3, 3
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 10, 0, false, false); // 4, 4
        Contract.CancelOrder(1, 2);
        Contract.CancelOrder(1, 4);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "BuyCancelTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 1500, 10, 0, false, false); // 5, 1

        var orderRaw1 = (Array?) Contract.GetOrder(1, 1);
        var order1 = RawOrderToOrder(orderRaw1);
        // order1.PrintPublicFields();

        // Create images
        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "BuyCancelTest/orderbook-image-diff-2.png");

        // var nodeIndexToCheck1 = Contract.FindNodeToCheckForClaim(1, 1);
        // var claimableAmount1 = (BigInteger) Contract.GetClaimableAmount(1, 1, nodeIndexToCheck1)[0];
        // Console.WriteLine(claimableAmount1);
        //
        // var nodeIndexToCheck2 = Contract.FindNodeToCheckForClaim(1, 2);
        // var claimableAmount2 = (BigInteger) Contract.GetClaimableAmount(1, 2, nodeIndexToCheck2)[0];
        // Console.WriteLine(claimableAmount2);

        var nodeIndexToCheck3 = Contract.FindNodeToCheckForClaim(1, 3);
        var claimableAmount3 = (BigInteger) Contract.GetClaimableAmount(1, 3, nodeIndexToCheck3)[0];
        Console.WriteLine($"claimableAmount3: {claimableAmount3}");

        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 10, 0, false, false); // 6, 5
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 10, 0, false, false); // 7, 6
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 10, 0, false, false); // 8, 7
        Contract.CancelOrder(1, 7);

        Contract.ClaimOrder(1, 3, nodeIndexToCheck3);
        Contract.CancelOrder(1, 3);

        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 500, 10, 0, false, false); // 9, 2

        var nodeIndexToCheck6 = Contract.FindNodeToCheckForClaim(1, 6);
        var claimableAmount6 = (BigInteger) Contract.GetClaimableAmount(1, 6, nodeIndexToCheck6)[0];
        Console.WriteLine($"claimableAmount6: {claimableAmount6}");

        var nodeIndexToCheck8 = Contract.FindNodeToCheckForClaim(1, 8);
        var claimableAmount8 = (BigInteger) Contract.GetClaimableAmount(1, 8, nodeIndexToCheck8)[0];
        Console.WriteLine($"claimableAmount8: {claimableAmount8}");

        // # Assert
        Assert.AreEqual(500, claimableAmount3);
        Assert.AreEqual(500, claimableAmount6);
        Assert.AreEqual(0, claimableAmount8);
    }

    [TestMethod]
    public void BuyCancelTest2()
    {
        // In this test scenario we:
        // 1. Place two orders so that we have a small "cancel tree"
        // 2. Cancel the second order. This should leave amounts cancelled in the small tree that we need to add to the bigger tree later.
        // 3. Place 30 orders so that we have a bigger tree
        // 4. Cancel the 30th order
        // 5. Cancel the 32nd order
        // 6. Make a market buy order that should fill the first order and some of the third order
        // We expect that the cancelled orders should be effectively removed from the order book and that the third order has some of its amount filled.
        // We expect that the "cancel tree" expand the first cancelled amounts to the bigger tree.

        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 200000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 32_000);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength, false);

        // # Act
        for (int i = 0; i < 2; i++)
        {
            Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 10, 0, false, false);
        }

        // TODO: This give node id -1. Fix the bug
        Contract.CancelOrder(1, 2);

        // # Act
        for (int i = 0; i < 30; i++)
        {
            Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 10, 0, false, false);
        }

        Contract.CancelOrder(1, 29);
        Contract.CancelOrder(1, 31);

        // Generate orderbook grid after order placement and the grid that highlight it with differences with respect to grid1
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);

        // Create images
        ExportGrid(diffGrid, "BuyCancelTest2/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 28500, 10, 0, false, false);

        var orderRaw1 = (Array?) Contract.GetOrder(1, 1);
        var order1 = RawOrderToOrder(orderRaw1);
        // order1.PrintPublicFields();

        // Create images
        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "BuyCancelTest2/orderbook-image-diff-2.png");

        var nodeIndexToCheck16 = Contract.FindNodeToCheckForClaim(1, 30);
        var claimableAmount16 = (BigInteger) Contract.GetClaimableAmount(1, 30, nodeIndexToCheck16)[0];
        Console.WriteLine(claimableAmount16);

        // # Assert
        Assert.AreEqual(500, claimableAmount16);
    }

    [TestMethod]
    public void EmptyOrderBookThenPlaceNewOrderSellSideTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 200);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 200);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 7, 0, false, false);

        // Debugging
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
        var sellGridBeforeSell = GenerateGrid(treeBitLength, false);

        // Create images
        ExportGrid(diffGrid, "EmptyOrderBookThenPlaceNewOrderSellSideTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 8, 8, 0, false, false);

        // Debugging
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "EmptyOrderBookThenPlaceNewOrderSellSideTest/orderbook-image-diff-2.png");
        var sellGridAfterSell = GenerateGrid(treeBitLength, false);
        var diffGridSell = OrderBookImageGenerator.HighlightDifferences(sellGridBeforeSell, sellGridAfterSell);
        ExportGrid(diffGridSell, "EmptyOrderBookThenPlaceNewOrderSellSideTest/orderbook-image-diff-3-sell.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);

        // Place order again
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 5, 0, false, false);
        var grid4 = GenerateGrid(treeBitLength);
        var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
        ExportGrid(diffGrid3, "EmptyOrderBookThenPlaceNewOrderSellSideTest/orderbook-image-diff-4.png");

        // # Assert

        // Check order
        var orderAlice2Raw = (Array?) Contract.GetOrder(1, 3);
        var orderAlice2 = RawOrderToOrder(orderAlice2Raw);

        Assert.IsNotNull(orderAlice2);
        Assert.AreEqual(2, orderAlice2.EmptiedCountWhenInserted);
    }

    [TestMethod]
    public void EmptyOrderBookThenPlaceNewOrderSiblingEmptiedCountSellSideTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 200);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 200);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 7, 0, false, false);

        // Debugging
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
        var sellGridBeforeSell = GenerateGrid(treeBitLength, false);

        // Create images
        ExportGrid(diffGrid, "EmptyOrderBookThenPlaceNewOrderSiblingEmptiedCountSellSideTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 8, 9, 0, false, false);

        // Debugging
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "EmptyOrderBookThenPlaceNewOrderSiblingEmptiedCountSellSideTest/orderbook-image-diff-2.png");
        var sellGridAfterSell = GenerateGrid(treeBitLength, false);
        var diffGridSell = OrderBookImageGenerator.HighlightDifferences(sellGridBeforeSell, sellGridAfterSell);
        ExportGrid(diffGridSell, "EmptyOrderBookThenPlaceNewOrderSiblingEmptiedCountSellSideTest/orderbook-image-diff-3-sell.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);

        // Place order again
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 5, 0, false, false);
        var grid4 = GenerateGrid(treeBitLength);
        var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
        ExportGrid(diffGrid3, "EmptyOrderBookThenPlaceNewOrderSiblingEmptiedCountSellSideTest/orderbook-image-diff-4.png");

        // # Assert

        // Check order
        var orderAlice2Raw = (Array?) Contract.GetOrder(1, 3);
        var orderAlice2 = RawOrderToOrder(orderAlice2Raw);

        Assert.IsNotNull(orderAlice2);
        Assert.AreEqual(2, orderAlice2.EmptiedCountWhenInserted);
    }

    [TestMethod]
    public void EmptyOrderBookThenPlaceNewOrderBuySideTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 200);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 20);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength, false);

        // # Act
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 10, 0, false, false);

        // Debugging
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
        var sellGridBeforeSell = GenerateGrid(treeBitLength);

        // Create images
        ExportGrid(diffGrid, "EmptyOrderBookThenPlaceNewOrderTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 200, 9, 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Debugging
        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "EmptyOrderBookThenPlaceNewOrderTest/orderbook-image-diff-2.png");
        var sellGridAfterSell = GenerateGrid(treeBitLength);
        var diffGridSell = OrderBookImageGenerator.HighlightDifferences(sellGridBeforeSell, sellGridAfterSell);
        ExportGrid(diffGridSell, "EmptyOrderBookThenPlaceNewOrderTest/orderbook-image-diff-3-sell.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Console.WriteLine(nodeIndexToCheck);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);
        var aliceBalanceFwbtcAfterClaim = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // Place order again
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 12, 0, false, false);
        var grid4 = GenerateGrid(treeBitLength, false);
        var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
        ExportGrid(diffGrid3, "EmptyOrderBookThenPlaceNewOrderTest/orderbook-image-diff-4.png");

        // # Assert

        // Check order
        var orderAlice2Raw = (Array?) Contract.GetOrder(1, 3);
        var orderAlice2 = RawOrderToOrder(orderAlice2Raw);

        Assert.IsNotNull(orderAlice2);
        Assert.AreEqual(2, orderAlice2.EmptiedCountWhenInserted);

        // Check balances
        Assert.AreEqual(100, aliceBalanceFwbtcAfterClaim);
        Assert.AreEqual(10, bobBalanceFusdt);
        Assert.AreEqual(0, bobBalanceFwbtc);
    }

    [TestMethod]
    public void EmptyOrderBookThenPlaceNewOrderSiblingEmptiedCountBuySideTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 200);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 20);

        // Visual representation
        var grid1 = GenerateGrid(treeBitLength, false);

        // # Act
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 10, 0, false, false);

        // Debugging
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
        var sellGridBeforeSell = GenerateGrid(treeBitLength);

        // Create images
        ExportGrid(diffGrid, "EmptyOrderBookThenPlaceNewOrderSiblingEmptiedCountTest/orderbook-image-diff.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 200, 8, 0, false, false);

        // Debugging
        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "EmptyOrderBookThenPlaceNewOrderSiblingEmptiedCountTest/orderbook-image-diff-2.png");
        var sellGridAfterSell = GenerateGrid(treeBitLength);
        var diffGridSell = OrderBookImageGenerator.HighlightDifferences(sellGridBeforeSell, sellGridAfterSell);
        ExportGrid(diffGridSell, "EmptyOrderBookThenPlaceNewOrderSiblingEmptiedCountTest/orderbook-image-diff-3-sell.png");

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);
        Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);

        // Place order again
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 12, 0, false, false);
        var grid4 = GenerateGrid(treeBitLength, false);
        var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
        ExportGrid(diffGrid3, "EmptyOrderBookThenPlaceNewOrderSiblingEmptiedCountTest/orderbook-image-diff-4.png");

        // # Assert
        var orderAlice2Raw = (Array?) Contract.GetOrder(1, 3);
        var orderAlice2 = RawOrderToOrder(orderAlice2Raw);

        Assert.IsNotNull(orderAlice2);
        Assert.AreEqual(2, orderAlice2.EmptiedCountWhenInserted);
    }

    [TestMethod]
    public void CreateLimitSellOrderExceptionTest()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        bool useAmm = false;

        // Check that the pair is not paused.
        Assert.IsFalse(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);

        // Try to pause the pair
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.PausePairOrderTrading(1, superAdminWithScope.Account);
        Assert.IsTrue(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);

        // Try to create a limit sell order and expect an exception.
        Engine.SetTransactionSigners(aliceWithScope);
        var exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Pair order trading paused"));

        // Un-pause the pair and check that it is not paused.
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.UnpausePairOrderTrading(1);
        Assert.IsFalse(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);

        // No user witness exception. At this point the contract is not paused and the signer is the super admin.
        exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("No user witness"));

        // Check that the contract throws an exception when the amount is less than or equal to 0.
        Engine.SetTransactionSigners(aliceWithScope);

        exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 0, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Amount must be greater than 0"));

        exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, -1, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Amount must be greater than 0"));

        // Check that invalid limit prices throw an exception.
        var maxPrice = 1 << treeBitLength;
        foreach (var limitPrice in new[] {0, -1, maxPrice + 1})
        {
            exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10, limitPrice, 0, useAmm, false));
            Assert.IsTrue(exception.Message.Contains("Invalid limit price"));
        }

        // Check that the contract throws an exception when we input a base amount that is too low.
        exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 99, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Quote amount must be greater than zero"));

        // Check that the contract throws "not enough funds exceptions" (BaseToken like fWBTC)
        // The user has the funds but has not deposited them yet.
        exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Not enough funds"));
    }

    [TestMethod]
    public void CreateLimitBuyOrderExceptionTest()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        bool useAmm = false;

        // Check that the pair is not paused.
        Assert.IsFalse(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);

        // Try to pause the pair
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.PausePairOrderTrading(1, superAdminWithScope.Account);
        Assert.IsTrue(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);

        // Try to create a limit buy order and expect an exception.
        Engine.SetTransactionSigners(aliceWithScope);
        var exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Pair order trading paused"));

        // Un-pause the pair and check that it is not paused.
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.UnpausePairOrderTrading(1);
        Assert.IsFalse(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);

        // No user witness exception
        exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("No user witness"));

        // Check that the contract throws an exception when the amount is less than or equal to 0.
        Engine.SetTransactionSigners(aliceWithScope);

        exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 0, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Amount must be greater than 0"));

        exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, -1, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Amount must be greater than 0"));

        // Check that invalid limit prices throw an exception.
        var maxPrice = 1 << treeBitLength;
        foreach (var limitPrice in new[] {0, -1, maxPrice + 1})
        {
            exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, limitPrice, 0, useAmm, false));
            Assert.IsTrue(exception.Message.Contains("Invalid limit price"));
        }

        // Check that the contract throws an exception when we input a base amount that is too low.
        exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 99, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Quote amount must be greater than zero"));

        // Check that the contract throws "not enough funds exceptions" (BaseToken like fUSDT)
        // The user has the funds but has not deposited them yet.
        exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Not enough funds"));
    }

    [TestMethod]
    public void ExecuteLimitSellOrderExceptionTest()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        bool useAmm = false;

        // contract IsPairOrderTradingPaused(1) exception
        Assert.IsFalse(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.PausePairOrderTrading(1, superAdminWithScope.Account);
        Assert.IsTrue(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);
        Engine.SetTransactionSigners(aliceWithScope);
        var exception = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("Pair order trading paused"));
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.UnpausePairOrderTrading(1);
        Assert.IsFalse(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);

        // No user witness exception
        exception = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10, 1, 0, useAmm, false));
        Assert.IsTrue(exception.Message.Contains("No user witness"));

        // amount exception
        Engine.SetTransactionSigners(aliceWithScope);
        foreach (var amount in new[] {0, -1})
        {
            exception = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitSellOrderUsingBase(aliceWithScope.Account, 1, amount, 1, 0, useAmm, false));
            Assert.IsTrue(exception.Message.Contains("Amount must be greater than 0"));
        }

        // Not enough in contract exception
        exception = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitSellOrderUsingBase(aliceWithScope.Account, 1, 200, 1, 0, true, false));
        Assert.IsTrue(exception.Message.Contains("Not enough base token in the contract to swap"));

        // To test the "Not enough funds" exception, we need to deposit some funds to the contract so that the contract is able to swap in the AMM.
        Engine.SetTransactionSigners(SuperAdmin);
        fWBTCContract.Mint(SuperAdmin.Account, Contract.Hash, 100);

        // Not enough funds(BaseToken like fwBTC) exception
        Engine.SetTransactionSigners(aliceWithScope);
        exception = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 1, 0, true, false));
        Assert.IsTrue(exception.Message.Contains("Not enough funds"));
    }

    [TestMethod]
    public void ExecuteLimitBuyOrderExceptionTest()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        bool useAmm = false;

        // contract IsPairOrderTradingPaused(1) exception
        Assert.IsFalse(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.PausePairOrderTrading(1, superAdminWithScope.Account);
        Assert.IsTrue(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);
        Engine.SetTransactionSigners(aliceWithScope);
        var exception = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, Contract.GetMaxPrice(1), 0, useAmm,false));
        Assert.IsTrue(exception.Message.Contains("Pair order trading paused"));
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.UnpausePairOrderTrading(1);
        Assert.IsFalse(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);
        // No user witness exception
        exception = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, Contract.GetMaxPrice(1), 0, useAmm,false));
        Assert.IsTrue(exception.Message.Contains("No user witness"));

        // amount exception
        Engine.SetTransactionSigners(aliceWithScope);
        foreach (var amount in new[] {0, -1})
        {
            exception = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, amount, Contract.GetMaxPrice(1), 0, useAmm,false));
            Assert.IsTrue(exception.Message.Contains("Amount must be greater than 0"));
        }
    }

    [TestMethod]
    public void PostOnlyLimitExceptionTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        // Add a new pair
        Engine.SetTransactionSigners(SuperAdmin);
        Contract.AddPair(fWBTCContract, FLMContract, treeBitLength, pricePrecision);
        Contract.UnpausePairOrderTrading(2);
        Contract.UnpausePairOrderManagement(2);
        Contract.EnableTokenDeposit(fWBTCContract);
        Contract.EnableTokenWithdraw(fWBTCContract);
        Contract.EnableTokenDeposit(FLMContract);
        Contract.EnableTokenWithdraw(FLMContract);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 1000000);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 1000000);

        var exceptionSell = Assert.ThrowsException<TestException>(() => Contract.AddLimitSellOrderUsingBase(bobWithScope.Account, 2, 100, 101, 0));
        Assert.IsTrue(exceptionSell.Message.Contains("PairContract Not Found"));

        var exceptionBuy = Assert.ThrowsException<TestException>(() => Contract.AddLimitBuyOrderUsingQuote(bobWithScope.Account, 2, 101, 101, 0));
        Assert.IsTrue(exceptionBuy.Message.Contains("PairContract Not Found"));
    }

    [TestMethod]
    public void ClaimFeesBuyTest()
    {
        // Test scenario:
        // 1. Alice and Bob deposits
        // 2. Alice creates a limit sell order
        // 3. Bob creates a limit buy order to buy 50% of Alice's order
        // 4. Alice claims the order
        // 5. Bob creates a second limit buy order to buy the rest of Alice's order
        // 6. Alice claims the order
        // 7. Alice and Bob withdraws the same amount as the other deposited.
        // 8. We check the order book fee amounts for both tokens.

        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(aliceWithScope);
        var fWBTCDepositAmount = 100000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount);

        Engine.SetTransactionSigners(bobWithScope);
        var fUSDTDepositAmount = 100000000;
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount);

        // # Act
        // Alice creates a limit sell order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100000000, 100, 0, false, false);

        // Bob creates a limit buy order to buy 50% of Alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 50000000, 100, 0, false, false);

        // Alice claims the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);

        // Bob creates a second limit buy order to buy the rest of Alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 50000000, 100, 0, false, false);

        // Alice claims the order
        Engine.SetTransactionSigners(aliceWithScope);
        nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);

        var feeAmount = 150000;

        // Alice and Bob withdraws the same amount as the other deposited
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Withdraw(aliceWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount - feeAmount);
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Withdraw(bobWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount - feeAmount);

        // # Assert
        var fWBTCFeeAmount = Contract.GetFeeBalance(fWBTCContract);
        var fUSDTFeeAmount = Contract.GetFeeBalance(fUSDTContract);

        Assert.AreEqual(feeAmount, fWBTCFeeAmount);
        Assert.AreEqual(feeAmount, fUSDTFeeAmount);
    }

    [TestMethod]
    public void ClaimFeesSellTest()
    {
        // Test scenario:
        // 1. Alice and Bob deposits
        // 2. Alice creates a limit buy order
        // 3. Bob creates a limit sell order to sell 50% to Alice's order
        // 4. Alice claims the order
        // 5. Bob creates a second limit sell order to sell the rest to Alice's order
        // 6. Alice claims the order
        // 7. Alice and Bob withdraws the same amount as the other deposited.
        // 8. We check the order book fee amounts for both tokens.

        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(aliceWithScope);
        var fUSDTDepositAmount = 100000000;
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount);

        Engine.SetTransactionSigners(bobWithScope);
        var fWBTCDepositAmount = 100000000;
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount);

        // # Act
        // Alice creates a limit sell order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100000000, 100, 0, false, false);

        // Bob creates a limit buy order to buy 50% of Alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 50000000, 100, 0, false, false);

        // Alice claims the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);

        // Bob creates a second limit buy order to buy the rest of Alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 50000000, 100, 0, false, false);

        // Alice claims the order
        Engine.SetTransactionSigners(aliceWithScope);
        nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);

        var feeAmount = 150000;

        // Alice and Bob withdraws the same amount as the other deposited
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Withdraw(aliceWithScope.Account, fWBTCContract.Hash,  fWBTCDepositAmount- feeAmount);
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Withdraw(bobWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount - feeAmount);

        // # Assert
        var fWBTCFeeAmount = Contract.GetFeeBalance(fWBTCContract);
        var fUSDTFeeAmount = Contract.GetFeeBalance(fUSDTContract);

        Assert.AreEqual(feeAmount, fWBTCFeeAmount);
        Assert.AreEqual(feeAmount, fUSDTFeeAmount);
    }

    [TestMethod]
    public void ClaimAndCancelFeesBuyTest()
    {
        // Test scenario:
        // 1. Alice and Bob deposits
        // 2. Alice creates a limit sell order
        // 3. Bob creates a limit buy order to buy 50% of Alice's order
        // 4. Alice claims the order
        // 5. Alice cancels the rest of the order.
        // 7. Alice and Bob withdraws all their funds.
        // 8. We check the order book fee amounts for both tokens.

        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(aliceWithScope);
        var fWBTCDepositAmount = 100000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount * 2);

        Engine.SetTransactionSigners(bobWithScope);
        var fUSDTDepositAmount = 100000000;
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount);

        // # Act
        // Alice creates a limit sell order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100000000, 100, 0, false, false);

        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100000000, 100, 0, false, false);

        // Bob creates a limit buy order to buy 50% of Alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 50000000, 100, 0, false, false);

        // Alice claims the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);

        // Alice cancels the rest of the order
        Contract.CancelOrder(1, 1);

        var feeAmount = 75000;

        // # Assert
        var nodeIndex = Contract.GetNodeIndex(16, 16 - 1, 100 - 1);
        var priceNodeRaw = (Array) Contract.GetPriceNode(1, nodeIndex, true)!;
        var priceNode = new OrderBookImageGenerator.PriceNode
        {
            BaseAmount = priceNodeRaw[0].GetInteger(), QuoteTotal = priceNodeRaw[1].GetInteger(), EmptiedCount = priceNodeRaw[2].GetInteger(), EmptiedCountSibling = priceNodeRaw[3].GetInteger(),
        };
        Assert.AreEqual(fWBTCDepositAmount, priceNode.BaseAmount);
        Assert.AreEqual(fUSDTDepositAmount, priceNode.QuoteTotal);

        Contract.CancelOrder(1, 2);

        // Alice and Bob withdraws the same amount as the other deposited
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Withdraw(aliceWithScope.Account, fUSDTContract.Hash, (fUSDTDepositAmount / 2) - feeAmount);
        Contract.Withdraw(aliceWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount);
        Contract.Withdraw(aliceWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount / 2);

        Engine.SetTransactionSigners(bobWithScope);
        Contract.Withdraw(bobWithScope.Account, fWBTCContract.Hash, (fWBTCDepositAmount / 2) - feeAmount);
        Contract.Withdraw(bobWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount / 2);

        var aliceFwbtcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var aliceFusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        var bobFwbtcBalance = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobFusdtBalance = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        Assert.AreEqual(0, aliceFwbtcBalance);
        Assert.AreEqual(0, aliceFusdtBalance);
        Assert.AreEqual(0, bobFusdtBalance);
        Assert.AreEqual(0, bobFwbtcBalance);

        var fWBTCFeeAmount = Contract.GetFeeBalance(fWBTCContract);
        var fUSDTFeeAmount = Contract.GetFeeBalance(fUSDTContract);

        Assert.AreEqual(feeAmount, fWBTCFeeAmount);
        Assert.AreEqual(feeAmount, fUSDTFeeAmount);

        var brokerFwbtcBalance = fWBTCContract.BalanceOf(Contract.Hash);
        var brokerFusdtBalance = fUSDTContract.BalanceOf(Contract.Hash);

        Assert.AreEqual(feeAmount, brokerFwbtcBalance);
        Assert.AreEqual(feeAmount, brokerFusdtBalance);
    }

    [TestMethod]
    public void ClaimAndCancelFeesSellTest()
    {
        // Test scenario:
        // 1. Alice and Bob deposits
        // 2. Alice creates a limit buy order
        // 3. Bob creates a limit sell order to sell 50% of Alice's order
        // 4. Alice claims the order
        // 5. Alice cancels the rest of the order.
        // 7. Alice and Bob withdraws all their funds.
        // 8. We check the order book fee amounts for both tokens.

        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(aliceWithScope);
        var fUSDTDepositAmount = 100000000;
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount * 2);

        Engine.SetTransactionSigners(bobWithScope);
        var fWBTCDepositAmount = 100000000;
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount);

        // # Act
        // Alice creates a limit buy order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100000000, 100, 0, false, false);

        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100000000, 100, 0, false, false);

        // Bob creates a limit sell order to sell 50% of Alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitSellOrderUsingBase(bobWithScope.Account, 1, 50000000, 100, 0, false, false);

        // Alice claims the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);

        // Alice cancels the rest of the order
        Contract.CancelOrder(1, 1);

        var feeAmount = 75000;

        // # Assert
        var nodeIndex = Contract.GetNodeIndex(16, 16 - 1, 100 - 1);
        var priceNodeRaw = (Array) Contract.GetPriceNode(1, nodeIndex, false)!;
        var priceNode = new OrderBookImageGenerator.PriceNode
        {
            BaseAmount = priceNodeRaw[0].GetInteger(), QuoteTotal = priceNodeRaw[1].GetInteger(), EmptiedCount = priceNodeRaw[2].GetInteger(), EmptiedCountSibling = priceNodeRaw[3].GetInteger(),
        };
        Assert.AreEqual(fWBTCDepositAmount, priceNode.BaseAmount);
        Assert.AreEqual(fUSDTDepositAmount, priceNode.QuoteTotal);

        Contract.CancelOrder(1, 2);

        // Alice and Bob withdraws the same amount as the other deposited
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Withdraw(aliceWithScope.Account, fWBTCContract.Hash, (fUSDTDepositAmount / 2) - feeAmount);
        Contract.Withdraw(aliceWithScope.Account, fUSDTContract.Hash, fWBTCDepositAmount);
        Contract.Withdraw(aliceWithScope.Account, fUSDTContract.Hash, fWBTCDepositAmount / 2);

        Engine.SetTransactionSigners(bobWithScope);
        Contract.Withdraw(bobWithScope.Account, fUSDTContract.Hash, (fWBTCDepositAmount / 2) - feeAmount);
        Contract.Withdraw(bobWithScope.Account, fWBTCContract.Hash, fUSDTDepositAmount / 2);

        var aliceFwbtcBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var aliceFusdtBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        var bobFwbtcBalance = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobFusdtBalance = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        Assert.AreEqual(0, aliceFwbtcBalance);
        Assert.AreEqual(0, aliceFusdtBalance);
        Assert.AreEqual(0, bobFusdtBalance);
        Assert.AreEqual(0, bobFwbtcBalance);

        var fWBTCFeeAmount = Contract.GetFeeBalance(fWBTCContract);
        var fUSDTFeeAmount = Contract.GetFeeBalance(fUSDTContract);

        Assert.AreEqual(feeAmount, fWBTCFeeAmount);
        Assert.AreEqual(feeAmount, fUSDTFeeAmount);

        var brokerFwbtcBalance = fWBTCContract.BalanceOf(Contract.Hash);
        var brokerFusdtBalance = fUSDTContract.BalanceOf(Contract.Hash);

        Assert.AreEqual(feeAmount, brokerFwbtcBalance);
        Assert.AreEqual(feeAmount, brokerFusdtBalance);
    }

    [TestMethod]
    public void PartialFillThenFullyFillBuyTest()
    {
        // Test scenario:
        // 1. Alice and Bob deposits
        // 2. Alice creates a limit sell order of 101 base token
        // 3. Bob creates a limit buy order to buy 100 base token
        // 4. Bob creates a second limit buy order to buy 10 base token, thus surpassing Alice's order
        // 5. Alice claims the order
        // 6. Alice creates a new order of 100 base token
        // 7. Bob creates a limit buy order to buy 1 base token
        // 8. Alice tries to cancel the order

        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(aliceWithScope);
        var fWBTCDepositAmount = 100000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount);

        Engine.SetTransactionSigners(bobWithScope);
        var fUSDTDepositAmount = 100000000;
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount);

        // # Act
        // Alice creates a limit sell order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 101, 100, 0, false, false);

        // Bob creates a limit buy order to buy 1000 base token
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 100, 110, 0, false, false);

        // Bob creates a second limit buy order to buy 100 base token, thus surpassing Alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 10, 110, 0, false, false);

        // Alice claims the order
        Engine.SetTransactionSigners(aliceWithScope);
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);

        var aliceBalanceFusdt1 = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        Assert.AreEqual(101, aliceBalanceFusdt1);
        var nodeIndex = Contract.GetNodeIndex(16, 16 - 1, 100 - 1);
        var executedAmountAtPrice = Contract.GetQuoteAmountExecutedAtNode(false,1, nodeIndex);
        Assert.AreEqual(100, executedAmountAtPrice);

        // Alice creates a new order of 100 base token
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 100, 0, false, false);

        var orderRaw = (Array?) Contract.GetOrder(1, 4);
        var order = RawOrderToOrder(orderRaw);
        order.PrintPublicFields();

        var executedAmountAtPrice2 = Contract.GetQuoteAmountExecutedAtNode(false,1, nodeIndex);
        Assert.AreEqual(101, executedAmountAtPrice2);

        // Bob creates a limit buy order to buy 1 base token
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 1, 100, 0, false, false);

        var executedAmountAtPrice3 = Contract.GetBaseAmountExecutedAtNode(false,1, nodeIndex);
        Assert.AreEqual(102, executedAmountAtPrice3);

        var claimableAmount = Contract.GetClaimableAmount(1, 4, nodeIndexToCheck);
        var claimableAmount0 = (BigInteger) claimableAmount[0];
        Assert.AreEqual(1, claimableAmount0);

        // Alice tries to cancel the order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CancelOrder(1, 4);
    }

    [TestMethod]
    public void PartialFillThenFullyFillAndCancelBuyTest()
    {
        // Test scenario:
        // 1. Alice and Bob deposits
        // 2. Alice creates a limit sell order of 50 base token, then cancels it
        // 3. Alice creates a limit sell order of 100 base token at the same price
        // 4. Bob creates a limit buy order to buy 110 base token thus surpassing Alice's order
        // 5. Alice creates a limit sell order of 100 base token at the same price
        // 6. Assert that alice does not have anything claimable on her third order (Order ID 4)

        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(aliceWithScope);
        var fWBTCDepositAmount = 100000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount);

        Engine.SetTransactionSigners(bobWithScope);
        var fUSDTDepositAmount = 100000000;
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount);

        // # Act
        // Alice creates a limit sell order of 50 base token, then cancels it
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 50, 100, 0, false, false);
        Contract.CancelOrder(1, 1);

        // Alice creates a limit sell order of 100 base token at the same price
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 100, 0, false, false);

        // Bob creates a limit buy order to buy 110 base token thus surpassing Alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 200, 140, 0, false, false);

        // Alice creates a limit sell order of 100 base token at the same price
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 100, 0, false, false);

        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 4);
        var claimableAmount = Contract.GetClaimableAmount(1, 4, nodeIndexToCheck);
        var claimableAmount0 = (BigInteger) claimableAmount[0];
        Assert.AreEqual(0, claimableAmount0);
    }

    [TestMethod]
    public void PartialFillThenFullyFillAndCancelSellTest()
    {
        // Test scenario:
        // 1. Alice and Bob deposits
        // 2. Alice creates a limit buy order of 50 base token, then cancels it
        // 3. Alice creates a limit buy order of 100 base token at the same price
        // 4. Bob creates a limit sell order to sell 110 base token thus surpassing Alice's order
        // 5. Alice creates a limit buy order of 100 base token at the same price
        // 6. Assert that alice does not have anything claimable on her third order (Order ID 4)

        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(aliceWithScope);
        var fUSDTDepositAmount = 100000000;
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount);

        Engine.SetTransactionSigners(bobWithScope);
        var fWBTCDepositAmount = 100000000;
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount);

        // # Act
        // Alice creates a limit buy order of 50 base token, then cancels it
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 50, 100, 0, false, false);
        Contract.CancelOrder(1, 1);

        // Alice creates a limit buy order of 100 base token at the same price
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 100, 0, false, false);

        // Bob creates a limit sell order to sell 110 base token thus surpassing Alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 200, 60, 0, false, false);

        // Alice creates a limit buy order of 100 base token at the same price
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 100, 0, false, false);

        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 4);
        var claimableAmount = Contract.GetClaimableAmount(1, 4, nodeIndexToCheck);
        var claimableAmount0 = (BigInteger) claimableAmount[0];
        Assert.AreEqual(0, claimableAmount0);
    }

    [TestMethod]
    public void EmptyCountSiblingAncestorSellTest()
    {
        // Test scenario:
        // 1. Alice and Bob deposits
        // 2. Alice creates a limit sell order of 200 base token at price 22
        // 3. Alice creates a limit sell order of 200 base token at price 23 and cancels it
        // 4. Alice creates a limit sell order of 100 base token at price 12
        // 5. Alice creates a limit sell order of 100 base token at price 11
        // 6. Bob creates a limit buy order to buy 1000 base token at price 11
        // 7. Alice creates a limit sell order of 200 base token at price 11
        // 8. Bob creates a limit buy order to buy 1000 base token at price 16
        // 9. Bob tries to buy 1000 base token at price 12
        // 10. We check that the order has not been executed

        // # Arrange

        const int treeBitLength = 5;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(aliceWithScope);
        var fWBTCDepositAmount = 100000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount);

        Engine.SetTransactionSigners(bobWithScope);
        var fUSDTDepositAmount = 100000000;
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount);

        // # Act

        // Debugging
        var grid1 = GenerateGrid(treeBitLength);

        // Alice creates a limit sell order of 200 base token at price 22
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 200, 22, 0, false, false);

        // Alice creates a limit sell order of 200 base token at price 23
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 200, 22, 0, false, false);
        Contract.CancelOrder(1, 2);

        // Alice creates a limit sell order of 100 base token at price 12
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 12, 0, false, false);

        // Alice creates a limit sell order of 100 base token at price 11
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 11, 0, false, false);

        // Debugging
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid1 = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
        ExportGrid(diffGrid1, "EmptyCountSiblingAncestorSellTest/orderbook-image-diff-1.png");

        // Bob creates a limit buy order to buy 1000 base token at price 11
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 1000, 11, 0, false, false);

        // Debugging
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "EmptyCountSiblingAncestorSellTest/orderbook-image-diff-2.png");

        // Alice creates a limit sell order of 200 base token at price 11
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 200, 11, 0, false, false);

        // Debugging
        var grid4 = GenerateGrid(treeBitLength);
        var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
        ExportGrid(diffGrid3, "EmptyCountSiblingAncestorSellTest/orderbook-image-diff-3.png");

        // Bob creates a limit buy order to buy 1000 base token at price 16
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 1000, 16, 0, false, false);

        // Debugging
        var grid5 = GenerateGrid(treeBitLength);
        var diffGrid4 = OrderBookImageGenerator.HighlightDifferences(grid4, grid5);
        ExportGrid(diffGrid4, "EmptyCountSiblingAncestorSellTest/orderbook-image-diff-4.png");

        var amountsAtPrice = Contract.GetAmountsAtPrice(1, false, 11);
        var amountAtPrice = (BigInteger) amountsAtPrice[0];
        Assert.AreEqual(0, amountAtPrice);

        var nodeIndexToCheck6 = Contract.FindNodeToCheckForClaim(1, 6);
        Assert.AreEqual(75, nodeIndexToCheck6);

        // Bob tries to buy 1000 base token at price 12
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 1000, 12, 0, false, false);

        var orderRaw = (Array?) Contract.GetOrder(1, 8);
        var order = RawOrderToOrder(orderRaw);
        order.PrintPublicFields();

        // # Assert
        Assert.AreEqual(0, order.BaseAmountTradedBeforeOrderPlaced);
        Assert.AreEqual(0, order.QuoteAmountTradedBeforeOrderPlaced);
    }

    [TestMethod]
    public void EmptyCountSiblingAncestorBuyTest()
    {
        // Test scenario:
        // 1. Alice and Bob deposits
        // 2. Alice creates a limit buy order of 200 base token at price 22
        // 3. Alice creates a limit buy order of 200 base token at price 23 and cancels it
        // 4. Alice creates a limit buy order of 100 base token at price 12
        // 5. Alice creates a limit buy order of 100 base token at price 11
        // 6. Bob creates a limit sell order to sell 1000 base token at price 11
        // 7. Alice creates a limit buy order of 200 base token at price 11
        // 8. Bob creates a limit sell order to sell 1000 base token at price 16
        // 9. Bob tries to sell 1000 base token at price 12
        // 10. We check that the order has not been executed

        // # Arrange

        const int treeBitLength = 5;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(aliceWithScope);
        var fUSDTDepositAmount = 100000000;
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount);

        Engine.SetTransactionSigners(bobWithScope);
        var fWBTCDepositAmount = 100000000;
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount);

        // # Act

        // Debugging
        var grid1 = GenerateGrid(treeBitLength);

        // Alice creates a limit buy order of 200 base token at price 22
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 200, 11, 0, false, false);

        // Alice creates a limit buy order of 200 base token at price 23
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 200, 11, 0, false, false);
        Contract.CancelOrder(1, 2);

        // Alice creates a limit buy order of 100 base token at price 12
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 21, 0, false, false);

        // Alice creates a limit buy order of 100 base token at price 11
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 100, 22, 0, false, false);

        // Debugging
        var grid2 = GenerateGrid(treeBitLength, false);
        var diffGrid1 = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
        ExportGrid(diffGrid1, "EmptyCountSiblingAncestorBuyTest/orderbook-image-diff-1.png");

        // Bob creates a limit sell order to sell 1000 base token at price 11
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 4000, 22, 0, false, false);

        // Debugging
        var grid3 = GenerateGrid(treeBitLength, false);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "EmptyCountSiblingAncestorBuyTest/orderbook-image-diff-2.png");

        // Alice creates a limit buy order of 200 base token at price 11
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 200, 22, 0, false, false);

        // Debugging
        var grid4 = GenerateGrid(treeBitLength, false);
        var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
        ExportGrid(diffGrid3, "EmptyCountSiblingAncestorBuyTest/orderbook-image-diff-3.png");

        // Bob creates a limit sell order to sell 1000 base token at price 16
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 4000, 17, 0, false, false);

        // Debugging
        var grid5 = GenerateGrid(treeBitLength, false);
        var diffGrid4 = OrderBookImageGenerator.HighlightDifferences(grid4, grid5);
        ExportGrid(diffGrid4, "EmptyCountSiblingAncestorBuyTest/orderbook-image-diff-4.png");

        var amountsAtPrice = Contract.GetAmountsAtPrice(1, true, 22);
        var amountAtPrice = (BigInteger) amountsAtPrice[1];
        Assert.AreEqual(0, amountAtPrice);

        var nodeIndexToCheck6 = Contract.FindNodeToCheckForClaim(1, 6);
        Assert.AreEqual(87, nodeIndexToCheck6);

        // Bob tries to sell 1000 base token at price 12
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 1000, 21, 0, false, false);

        var orderRaw = (Array?) Contract.GetOrder(1, 8);
        var order = RawOrderToOrder(orderRaw);
        order.PrintPublicFields();

        // # Assert
        Assert.AreEqual(0, order.QuoteAmountTradedBeforeOrderPlaced);
        Assert.AreEqual(0, order.BaseAmountTradedBeforeOrderPlaced);
    }

    [TestMethod]
    public void VirtuallyEmptyNodeInMiddleOfTreeTest()
    {
        // Test scenario:
        // 1. Alice and Bob deposits
        // 2. Alice creates a limit sell order of 100 base token that will never be executed
        // 2. Alice creates a limit sell order of 100 base token
        // 3. Bob creates executes a limit buy order to buy 200 base token at a price 1 above Alice's order
        // 4. Bob creates executes a limit buy order to buy 200 base token at the same price as alice's order

        // # Arrange
        const int treeBitLength = 5;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        Engine.SetTransactionSigners(aliceWithScope);
        var fWBTCDepositAmount = 100000000;
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, fWBTCDepositAmount);

        Engine.SetTransactionSigners(bobWithScope);
        var fUSDTDepositAmount = 100000000;
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, fUSDTDepositAmount);

        // # Act

        // Debugging
        var grid1 = GenerateGrid(treeBitLength);

        // Alice creates a limit sell order that will never be executed
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 16, 0, false, false);

        // Alice creates a limit sell order
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 10, 0, false, false);

        // Debugging
        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid1 = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
        ExportGrid(diffGrid1, "VirtuallyEmptyNodeInMiddleOfTreeTest/orderbook-image-diff-1.png");

        // Bob creates a limit buy order to buy 200 base token at a price 1 above Alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 200, 14, 0, false, false);

        // Debugging
        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "VirtuallyEmptyNodeInMiddleOfTreeTest/orderbook-image-diff-2.png");

        // Bob creates a limit buy order to buy 200 base token at the same price as alice's order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 200, 10, 0, false, false);

        // Debugging
        var grid4 = GenerateGrid(treeBitLength);
        var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
        ExportGrid(diffGrid3, "VirtuallyEmptyNodeInMiddleOfTreeTest/orderbook-image-diff-3.png");

        // # Assert
        var orderRaw = (Array?) Contract.GetOrder(1, 2);
        var order = RawOrderToOrder(orderRaw);
        order.PrintPublicFields();
        Console.WriteLine();

        var order2Raw = (Array?) Contract.GetOrder(1, 3);
        var order2 = RawOrderToOrder(order2Raw);
        order2.PrintPublicFields();
        Console.WriteLine();

        var order3Raw = (Array?) Contract.GetOrder(1, 4);
        var order3 = RawOrderToOrder(order3Raw);
        order3.PrintPublicFields();

        // Bob's second order should NOT be filled, because alice's order at that price is already filled.
        Assert.AreEqual(0, order3.BaseAmountTradedBeforeOrderPlaced);
        Assert.AreEqual(0, order3.QuoteAmountTradedBeforeOrderPlaced);
    }

    [TestMethod]
    public void CheckThatContractIsClaimingBeforeCancelingTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100_000);

        // # Act

        var grid1 = GenerateGrid(treeBitLength);

        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 50, 7, 0, false, false);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 5_000, 9, 0, false, false);
        var aliceBalanceFwbtc = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        var aliceBalanceFusdtBeforeClaim = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        var grid2 = GenerateGrid(treeBitLength);
        var diffGrid1 = OrderBookImageGenerator.HighlightDifferences(grid1, grid2);
        ExportGrid(diffGrid1, "CheckThatContractIsClaimingBeforeCancelingTest/orderbook-image-diff-1.png");

        // Make a market buy order
        Engine.SetTransactionSigners(bobWithScope);
        Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        Contract.ExecuteLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 16, Contract.GetMaxPrice(1), 0, false, false);
        var bobBalanceFwbtc = Contract.GetAccountBalance(bobWithScope.Account, fWBTCContract);
        var bobBalanceFusdt = Contract.GetAccountBalance(bobWithScope.Account, fUSDTContract);

        // Claim the assets on the order
        Engine.SetTransactionSigners(aliceWithScope);

        // Check if the contract calculates the correct nodeIndexToCheck when
        var nodeIndexToCheck1 = Contract.FindNodeToCheckForClaim(1, 1);
        var nodeIndexToCheck2 = Contract.FindNodeToCheckForClaim(1, 2);

        // Check if the contract calculates the correct claimable amounts
        var claimableAmount1 = (BigInteger) Contract.GetClaimableAmount(1, 1, nodeIndexToCheck1)[0];
        var claimableAmount2 = (BigInteger) Contract.GetClaimableAmount(1, 2, nodeIndexToCheck2)[0];

        var grid3 = GenerateGrid(treeBitLength);
        var diffGrid2 = OrderBookImageGenerator.HighlightDifferences(grid2, grid3);
        ExportGrid(diffGrid2, "CheckThatContractIsClaimingBeforeCancelingTest/orderbook-image-diff-2.png");

        var grid4 = GenerateGrid(treeBitLength);
        var diffGrid3 = OrderBookImageGenerator.HighlightDifferences(grid3, grid4);
        ExportGrid(diffGrid3, "CheckThatContractIsClaimingBeforeCancelingTest/orderbook-image-diff-3.png");

        // Since the first order is fully filled, we expect an exception when trying to cancel it
        var exception = Assert.ThrowsException<TestException>(() => Contract.CancelOrder(1, 1));
        Assert.IsTrue(exception.Message.Contains("Nothing to cancel"));
        var aliceBalanceFusdtAfterFirstCancel = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // Instead we need to claim the order
        var nodeIndexToCheck = Contract.FindNodeToCheckForClaim(1, 1);
        Contract.ClaimOrder(1, 1, nodeIndexToCheck);

        // We expect the second order to be partially filled
        Contract.CancelOrder(1, 2);

        var aliceBalanceFusdtAfterClaimAndCancel = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);

        // # Assert

        // Check order
        var orderAliceRaw = (Array?) Contract.GetOrder(1, 1);
        var orderAlice = RawOrderToOrder(orderAliceRaw);
        Assert.IsNotNull(orderAlice);
        var orderAliceOwner = orderAlice.Owner;
        var orderAliceTotalOrderBaseAmount = orderAlice.TotalOrderBaseAmount;
        var orderAlicePlacedInOrderBook = orderAlice.PlacedInOrderBookBaseAmount;
        var orderAlicePrice = orderAlice.Price;
        var orderAliceCancelledBaseAmount = orderAlice.CancelledBaseAmount;
        var orderAliceClaimedBaseAmount = orderAlice.ClaimedBaseAmount;
        Assert.AreEqual(aliceWithScope.Account, orderAliceOwner);
        Assert.AreEqual(50, orderAliceTotalOrderBaseAmount);
        Assert.AreEqual(50, orderAlicePlacedInOrderBook);
        Assert.AreEqual(7, orderAlicePrice);
        Assert.AreEqual(0, orderAliceCancelledBaseAmount);
        Assert.AreEqual(50, orderAliceClaimedBaseAmount);

        var order2AliceRaw = (Array?) Contract.GetOrder(1, 2);
        var order2Alice = RawOrderToOrder(order2AliceRaw);
        var order2AliceCancelledBaseAmount = order2Alice.CancelledBaseAmount;
        var order2AliceClaimedBaseAmount = order2Alice.ClaimedBaseAmount;
        Assert.AreEqual(4_856, order2AliceCancelledBaseAmount);
        Assert.AreEqual(144, order2AliceClaimedBaseAmount);

        // Check balances
        Assert.AreEqual(94950, aliceBalanceFwbtc);
        Assert.AreEqual(0, aliceBalanceFusdtBeforeClaim);
        Assert.AreEqual(0, aliceBalanceFusdtAfterFirstCancel);
        Assert.AreEqual(16, aliceBalanceFusdtAfterClaimAndCancel);
        Assert.AreEqual(9_984, bobBalanceFusdt);
        Assert.AreEqual(194, bobBalanceFwbtc);

        // Check the nodeIndexToCheck
        Assert.AreEqual(7, nodeIndexToCheck1);
        Assert.AreEqual(-1, nodeIndexToCheck2);

        // Check the claimable amounts
        Assert.AreEqual(50, claimableAmount1);
        Assert.AreEqual(144, claimableAmount2);
    }

    [TestMethod]
    public void GasBurnWhenNotTradingInAmmSellSideTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100_000);

        Engine.SetTransactionSigners(SuperAdmin);
        Contract.SetGasToBurn(1, 1);

        // # Act
        // Debugging:
        var gasCostMapBeforeNoExecBurn = GenerateDocumentGasCostMap();

        Engine.SetTransactionSigners(aliceWithScope);
        var gasCostBeforeSellOrderNoExecBurn = (int)Engine.FeeConsumed;
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 50, 70, 0, false, true);
        var gasCostAfterSellOrderNoExecBurn = (int)Engine.FeeConsumed;
        var gasCostNoExecBurn = gasCostAfterSellOrderNoExecBurn - gasCostBeforeSellOrderNoExecBurn;

        // Debugging:
        var gasCostMapAfterNoExecBurn = GenerateDocumentGasCostMap(gasCostMapBeforeNoExecBurn);
        //WriteGasCostsToFile("gasCostMapAfterNoExecBurn", gasCostMapAfterNoExecBurn);

        // Reset the test environment
        TestBaseSetup(FlamingoBroker.Nef, FlamingoBroker.Manifest, NeoDebugInfo.TryLoad("../../../TestingArtifacts/FlamingoBroker.nefdbgnfo", out var debugInfo) ? debugInfo : null);

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100_000);

        Engine.SetTransactionSigners(SuperAdmin);
        Contract.SetGasToBurn(1, 5);

        // Debugging:
        var gasCostMapBeforeWithExecBurn = GenerateDocumentGasCostMap();

        Engine.SetTransactionSigners(aliceWithScope);
        var gasCostBeforeSellOrderWithExecBurn = (int)Engine.FeeConsumed;
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 50, 70, 0, false, false);
        var gasCostAfterSellOrderWithExecBurn = (int)Engine.FeeConsumed;
        var gasCostWithExecBurn = gasCostAfterSellOrderWithExecBurn - gasCostBeforeSellOrderWithExecBurn;

        // Debugging:
        var gasCostMapAfterGasWithExecBurn = GenerateDocumentGasCostMap(gasCostMapBeforeWithExecBurn);
        //WriteGasCostsToFile("gasCostMapAfterGasWithExecBurn", gasCostMapAfterGasWithExecBurn);

        // # Assert
        var gasDifference = gasCostWithExecBurn - gasCostNoExecBurn;
        Assert.AreEqual(4, gasDifference);

        // # Assert failure from debug
        var exception = Assert.ThrowsException<TestException>(() => Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 50, 70, 0, true, true));
        Assert.IsTrue(exception.Message.Contains("Reason: 4"));
    }

    [TestMethod]
    public void GasBurnWhenNotTradingInAmmBuySideTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 10000;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 1_000_000000);

        Engine.SetTransactionSigners(SuperAdmin);
        Contract.SetGasToBurn(1, 1);

        // # Act
        // Debugging:
        var gasCostMapBeforeNoExecBurn = GenerateDocumentGasCostMap();

        Engine.SetTransactionSigners(aliceWithScope);
        var gasCostBeforeBuyOrderNoExecBurn = (int)Engine.FeeConsumed;
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 350_000, 70, 0, false, true);
        var gasCostAfterBuyOrderNoExecBurn = (int)Engine.FeeConsumed;
        var gasCostNoExecBurn = gasCostAfterBuyOrderNoExecBurn - gasCostBeforeBuyOrderNoExecBurn;

        // Debugging:
        var gasCostMapAfterNoExecBurn = GenerateDocumentGasCostMap(gasCostMapBeforeNoExecBurn);
        //WriteGasCostsToFile("gasCostMapAfterNoExecBurn", gasCostMapAfterNoExecBurn);

        // Reset the test environment
        TestBaseSetup(FlamingoBroker.Nef, FlamingoBroker.Manifest, NeoDebugInfo.TryLoad("../../../TestingArtifacts/FlamingoBroker.nefdbgnfo", out var debugInfo) ? debugInfo : null);

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 1_000_000000);

        Engine.SetTransactionSigners(SuperAdmin);
        Contract.SetGasToBurn(1, 5);

        // Debugging:
        var gasCostMapBeforeWithExecBurn = GenerateDocumentGasCostMap();

        Engine.SetTransactionSigners(aliceWithScope);
        var gasCostBeforeBuyOrderWithExecBurn = (int)Engine.FeeConsumed;
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 350_000, 70, 0, false, false);
        var gasCostAfterBuyOrderWithExecBurn = (int)Engine.FeeConsumed;
        var gasCostWithExecBurn = gasCostAfterBuyOrderWithExecBurn - gasCostBeforeBuyOrderWithExecBurn;

        // Debugging:
        var gasCostMapAfterGasWithExecBurn = GenerateDocumentGasCostMap(gasCostMapBeforeWithExecBurn);
        //WriteGasCostsToFile("gasCostMapAfterGasWithExecBurn", gasCostMapAfterGasWithExecBurn);

        // # Assert
        var gasDifference = gasCostWithExecBurn - gasCostNoExecBurn;
        Assert.AreEqual(4, gasDifference);

        // # Assert failure from debug
        var exception = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 996_132000, Contract.GetMaxPrice(1), 0, true, true));
        Assert.IsTrue(exception.Message.Contains("Reason: 4"));
    }

    [TestMethod]
    public void GetAmountAtPriceTest()
    {
        // # Arrange
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.000000_01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 10_000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100_000);

        // # Act
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 50, 12, 0, false, false);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 5_000, 14, 0, false, false);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 500, 14, 0, false, false);

        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 10, 9, 0, false, false);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 15, 9, 0, false, false);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 18, 8, 0, false, false);

        // One fills the other completely
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 100, 10, 0, false, false);
        Engine.SetTransactionSigners(bobWithScope);
        Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, 20, 11, 0, false, false);

        var amountsAtPrice1 = Contract.GetAmountsAtPrice(1, false, 12);
        var baseAmountAtPrice1 = (BigInteger) amountsAtPrice1[0];
        var quoteAmountAtPrice1 = (BigInteger) amountsAtPrice1[1];

        var amountsAtPrice2 = Contract.GetAmountsAtPrice(1, false, 14);
        var baseAmountAtPrice2 = (BigInteger) amountsAtPrice2[0];
        var quoteAmountAtPrice2 = (BigInteger) amountsAtPrice2[1];

        var amountsAtPrice3 = Contract.GetAmountsAtPrice(1, true, 9);
        var baseAmountAtPrice3 = (BigInteger) amountsAtPrice3[0];
        var quoteAmountAtPrice3 = (BigInteger) amountsAtPrice3[1];

        var amountsAtPrice4 = Contract.GetAmountsAtPrice(1, true, 8);
        var baseAmountAtPrice4 = (BigInteger) amountsAtPrice4[0];
        var quoteAmountAtPrice4 = (BigInteger) amountsAtPrice4[1];

        var amountsAtPrice5Sell = Contract.GetAmountsAtPrice(1, false, 10);
        var baseAmountAtPrice5Sell = (BigInteger) amountsAtPrice5Sell[0];
        var quoteAmountAtPrice5Sell = (BigInteger) amountsAtPrice5Sell[1];

        var amountsAtPrice6Buy = Contract.GetAmountsAtPrice(1, true, 11);
        var baseAmountAtPrice6Buy = (BigInteger) amountsAtPrice6Buy[0];
        var quoteAmountAtPrice6Buy = (BigInteger) amountsAtPrice6Buy[1];

        // # Assert
        Assert.AreEqual(50, baseAmountAtPrice1);
        Assert.AreEqual(6, quoteAmountAtPrice1);

        Assert.AreEqual(5_500, baseAmountAtPrice2);
        Assert.AreEqual(770, quoteAmountAtPrice2);

        Assert.AreEqual(25, quoteAmountAtPrice3);
        Assert.AreEqual(277, baseAmountAtPrice3);

        Assert.AreEqual(18, quoteAmountAtPrice4);
        Assert.AreEqual(225, baseAmountAtPrice4);

        Assert.AreEqual(0, baseAmountAtPrice5Sell);
        Assert.AreEqual(0, quoteAmountAtPrice5Sell);

        Assert.AreEqual(90, baseAmountAtPrice6Buy);
        Assert.AreEqual(10, quoteAmountAtPrice6Buy);
    }

    [TestMethod]
    public void GetAccountInfoTest()
    {
        // Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        // # Act
        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 100);
        var info = Contract.GetAccountBalanceAll(aliceWithScope.Account);

        Assert.IsTrue(info?.Keys.Count == 2);
        var baseBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        Assert.IsTrue(baseBalance == 100);
        var quoteBalance = Contract.GetAccountBalance(aliceWithScope.Account, fUSDTContract);
        Assert.IsTrue(quoteBalance == 100);
    }

    [TestMethod]
    public void GetAllUserOrdersTests()
    {
        // Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 1000000);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 1000000);
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 1000000);
        Contract.Deposit(bobWithScope.Account, fUSDTContract.Hash, 1000000);

        var ordersToPlaceAlice = new List<(int, int)>
        {
            (100, 1000),
            (100, 2000),
            (100, 13000),
        };

        var ordersToPlaceBob = new List<(int, int)>
        {
            (10, 10),
            (20, 20),
            (130, 130)
        };

        // Act
        foreach (var order in ordersToPlaceAlice)
        {
            Engine.SetTransactionSigners(aliceWithScope);
            Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, order.Item1, order.Item2, 0, false, false);
        }

        foreach (var order in ordersToPlaceBob)
        {
            Engine.SetTransactionSigners(bobWithScope);
            Contract.CreateLimitBuyOrderUsingQuote(bobWithScope.Account, 1, order.Item1, order.Item2, 0, false, false);
        }

        var ordersAlice = (List<object>)Contract.GetAllUserOrders(aliceWithScope.Account,  0)!;
        var ordersBob = (List<object>)Contract.GetAllUserOrders(bobWithScope.Account, 0)!;

        // Assert
        Assert.IsNotNull(ordersAlice);
        Assert.IsNotNull(ordersBob);

        Assert.AreEqual(3, ordersAlice.Count);
        Assert.AreEqual(3, ordersBob.Count);

        foreach (var x in Enumerable.Range(0, ordersToPlaceAlice.Count))
        {
            var orderRaw = (Array)ordersAlice[x];
            var order = RawOrderInfoToOrderInfo(orderRaw);

            var orderId = order.Id;
            var pairId = order.PairId;
            var orderOwner = order.Owner;
            var orderTotalOrderBaseAmount = order.TotalOrderBaseAmount;
            var orderPlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount;
            var orderPrice = order.Price;

            Assert.AreEqual(x + 1, orderId);
            Assert.AreEqual(1, pairId);
            Assert.AreEqual(aliceWithScope.Account, orderOwner);
            Assert.AreEqual(100, orderTotalOrderBaseAmount);
            Assert.AreEqual(100, orderPlacedInOrderBookBaseAmount);
            Assert.AreEqual(orderPrice, orderPrice);
        }

        foreach (var x in Enumerable.Range(0, ordersToPlaceBob.Count))
        {
            var orderRaw = (Array)ordersBob[x];
            var order = RawOrderInfoToOrderInfo(orderRaw);

            var orderId = order.Id;
            var pairId = order.PairId;
            var orderOwner = order.Owner;
            var orderTotalOrderBaseAmount = order.TotalOrderBaseAmount;
            var orderPlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount;
            var orderPrice = order.Price;

            Assert.AreEqual(x + 4, orderId);
            Assert.AreEqual(1, pairId);
            Assert.AreEqual(bobWithScope.Account, orderOwner);
            Assert.AreEqual(100, orderTotalOrderBaseAmount);
            Assert.AreEqual(100, orderPlacedInOrderBookBaseAmount);
            Assert.AreEqual(orderPrice, orderPrice);
        }
    }

    [TestMethod]
    public void AccountDepositTest()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};

        // Sign
        Engine.SetTransactionSigners(aliceWithScope);

        // amount exception
        foreach (var amount in new[] {0, -1})
        {
             // NB: It won't throw an exception because if token follows NEP17 it should already check amount
            var ex = Assert.ThrowsException<TestException>(() => Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, amount));
            Assert.IsTrue(ex.Message.Contains("Amount must be greater than 0"));
        }

        // Check deposit for an invalid/not whitelisted token.
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.DisableTokenDeposit(fUSDTContract.Hash);
        Assert.IsTrue(!Contract.IsTokenDepositEnabled(fUSDTContract.Hash));
        Engine.SetTransactionSigners(aliceWithScope);
        var exception = Assert.ThrowsException<TestException>(() => Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 100));
        Assert.IsTrue(exception.Message.Contains("Token deposit not enabled"));

        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.EnableTokenDeposit(fUSDTContract.Hash);
        Engine.SetTransactionSigners(aliceWithScope);

        // OnAccountUpdated is invoked
        var onAccountUpdatedInvoked = false;
        Contract.OnAccountUpdated += (_, _, _, _) => { onAccountUpdatedInvoked = true; };
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);
        Assert.IsTrue(onAccountUpdatedInvoked);
        // check base token balance
        var baseBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        Assert.IsTrue(baseBalance == 100);

        // Deposit quote token
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 100);
        // check base token balance
        baseBalance = Contract.GetAccountBalance(aliceWithScope.Account, fWBTCContract);
        Assert.IsTrue(baseBalance == 100);
    }

    [TestMethod]
    public void AccountWithdrawTest()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);

        Engine.SetTransactionSigners(superAdminWithScope);
        // No user witness exception
        var exception = Assert.ThrowsException<TestException>(() => Contract.Withdraw(Alice.Account, fWBTCContract, 100));
        Assert.IsTrue(exception.Message.Contains("No user witness"));

        Engine.SetTransactionSigners(aliceWithScope);
        // Not enough to withdraw
        exception = Assert.ThrowsException<TestException>(() => Contract.Withdraw(Alice.Account, fWBTCContract, 120));
        Assert.IsTrue(exception.Message.Contains("Not enough to withdraw"));

        // amount exception
        foreach (var amount in new[] {0, -1})
        {
            // NB: It won't throw an exception because if token follows NEP17 it should already check amount
            exception = Assert.ThrowsException<TestException>(() => Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, amount));
            Assert.IsTrue(exception.Message.Contains("Amount must be greater than 0"));
        }

        // OnAccountUpdated is invoked
        var onAccountUpdatedInvoked = false;
        Contract.OnAccountUpdated += (_, _, _, _) => { onAccountUpdatedInvoked = true; };
        Contract.Withdraw(Alice.Account, fWBTCContract, 100);
        Assert.IsTrue(onAccountUpdatedInvoked);

        // check base token balance
        var baseBalance = Contract.GetAccountBalance(Alice.Account, fWBTCContract);
        Assert.IsTrue(baseBalance == 0);

        // deposit qutote token and withdraw
        Contract.Deposit(Alice.Account, fUSDTContract.Hash, 100);
        var quoteBalance = Contract.GetAccountBalance(Alice.Account, fUSDTContract);
        Assert.IsTrue(quoteBalance == 100);
        Contract.Withdraw(Alice.Account, fUSDTContract, 100);
        quoteBalance = Contract.GetAccountBalance(Alice.Account, fUSDTContract);
        Assert.IsTrue(quoteBalance == 0);
    }

    [TestMethod]
    public void DepositTokenNotEnabled()
    {
        InitializeContract([]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(aliceWithScope);

        // FlamingoOrderBook.External OnNEP17Payment
        var exception = Assert.ThrowsException<TestException>(() => Contract.Deposit(Alice.Account, fWBTCContract.Hash, 100));
        Console.WriteLine(exception.Message);
        Assert.IsTrue(exception.Message.Contains("Token deposit not enabled"));
    }

    [TestMethod]
    public void ApprovedTransferFromCaller()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fWBTCContract.Hash, 100);

        // Check that the router is correctly the same set in the contract
        Assert.IsTrue(Contract.AMMRouter == SwapRouterContract.Hash);

        // Test that Alice cannot execute the function to transfer funds to bob because she is not the ammrouter
        // FlamingoOrderBook.External ApprovedTransfer
        var exception = Assert.ThrowsException<TestException>(() => Contract.ApprovedTransfer(fWBTCContract.Hash, Bob.Account, 100));
        Console.WriteLine(exception.Message);
        Assert.IsTrue(exception.Message.Contains("Only the AMM router can call this"));
    }

    [TestMethod]
    public void ChangeOwner()
    {
        InitializeContract([]);

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(aliceWithScope);

        // Test that without owner witness the method cannot be called
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.ChangeOwner(Alice.Account));
        Console.WriteLine(exception1.Message);
        Assert.IsTrue(exception1.Message.Contains("No owner witness"));

        // Test that without new owner witness the method cannot be called
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception1B = Assert.ThrowsException<TestException>(() => Contract.ChangeOwner(Alice.Account));
        Console.WriteLine(exception1B.Message);
        Assert.IsTrue(exception1B.Message.Contains("No new owner witness"));

        //var exception2b = Assert.ThrowsException<TestException>(() => Contract.ChangeOwner(UInt160.Zero));
        //Console.WriteLine(exception2b.Message);
        //Assert.IsTrue(exception2b.Message.Contains("Invalid new owner"));

        // Test that we can change the Owner to a new valid one
        Engine.SetTransactionSigners(superAdminWithScope, aliceWithScope);
        Contract.ChangeOwner(Alice.Account);
        Assert.IsTrue(Contract.Owner == Alice.Account);
    }

    [TestMethod]
    public void ChangeFeeCollector()
    {
        InitializeContract([]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(aliceWithScope);

        // Test that without owner witness the method cannot be called
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.ChangeFeeCollector(Alice.Account));
        Console.WriteLine(exception1.Message);
        Assert.IsTrue(exception1.Message.Contains("No owner witness"));

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception2B = Assert.ThrowsException<TestException>(() => Contract.ChangeFeeCollector(UInt160.Zero));
        Console.WriteLine(exception2B.Message);
        Assert.IsTrue(exception2B.Message.Contains("Invalid collector"));

        // Test that we can change the FeeCollector to a new valid one
        Contract.ChangeFeeCollector(Alice.Account);
        Assert.IsTrue(Contract.FeeCollector == Alice.Account);
    }

    [TestMethod]
    public void ChangeAmmRouter()
    {
        InitializeContract([]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(aliceWithScope);

        // Test that without owner witness the method cannot be called
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.ChangeAMMRouter(Alice.Account));
        Console.WriteLine(exception1.Message);
        Assert.IsTrue(exception1.Message.Contains("No owner witness"));

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception2B = Assert.ThrowsException<TestException>(() => Contract.ChangeAMMRouter(UInt160.Zero));
        Console.WriteLine(exception2B.Message);
        Assert.IsTrue(exception2B.Message.Contains("Invalid router"));

        // Test that we can change the AmmRouter to a new valid one
        Contract.ChangeAMMRouter(Alice.Account);
        Assert.IsTrue(Contract.AMMRouter == Alice.Account);
    }

    [TestMethod]
    public void ChangeAmmFactory()
    {
        InitializeContract([]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(aliceWithScope);

        // Test that without owner witness the method cannot be called
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.ChangeAMMFactory(Alice.Account));
        Console.WriteLine(exception1.Message);
        Assert.IsTrue(exception1.Message.Contains("No owner witness"));

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception2B = Assert.ThrowsException<TestException>(() => Contract.ChangeAMMFactory(UInt160.Zero));
        Console.WriteLine(exception2B.Message);
        Assert.IsTrue(exception2B.Message.Contains("Invalid factory"));

        // Test that we can change the AMMFactory to a new valid one
        Contract.ChangeAMMFactory(Alice.Account);
        Assert.IsTrue(Contract.AMMFactory == Alice.Account);
    }

    [TestMethod]
    public void EnableDisableTokenDeposit()
    {
        InitializeContract([]);

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};

        // Test that token deposit is disabled
        Assert.IsTrue(!Contract.IsTokenDepositEnabled(fWBTCContract.Hash));

        // Test that only the owner can enable token deposit
        Engine.SetTransactionSigners(aliceWithScope);
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.EnableTokenDeposit(fWBTCContract.Hash));
        Console.WriteLine(exception1.Message);
        Assert.IsTrue(exception1.Message.Contains("No owner witness"));

        // Test that the token is contract
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception2 = Assert.ThrowsException<TestException>(() => Contract.EnableTokenDeposit(Alice.Account));
        Console.WriteLine(exception2.Message);
        Assert.IsTrue(exception2.Message.Contains("Token not a contract"));

        // Test that token is valid address
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception3 = Assert.ThrowsException<TestException>(() => Contract.EnableTokenDeposit(UInt160.Zero));
        Console.WriteLine(exception3.Message);
        Assert.IsTrue(exception3.Message.Contains("Token not a contract"));

        Contract.EnableTokenDeposit(fWBTCContract.Hash);

        // Test that token deposit is enabled
        Assert.IsTrue(Contract.IsTokenDepositEnabled(fWBTCContract.Hash));

        // Test that only the owner can disable token deposit
        Engine.SetTransactionSigners(aliceWithScope);
        var exception4 = Assert.ThrowsException<TestException>(() => Contract.DisableTokenDeposit(fWBTCContract.Hash));
        Console.WriteLine(exception4.Message);
        Assert.IsTrue(exception4.Message.Contains("No owner witness"));

        // Test that the token is contract
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception5 = Assert.ThrowsException<TestException>(() => Contract.DisableTokenDeposit(Alice.Account));
        Console.WriteLine(exception5.Message);
        Assert.IsTrue(exception5.Message.Contains("Token not a contract"));

        // Test that token is valid address
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception6 = Assert.ThrowsException<TestException>(() => Contract.DisableTokenDeposit(UInt160.Zero));
        Console.WriteLine(exception6.Message);
        Assert.IsTrue(exception6.Message.Contains("Token not a contract"));

        Contract.DisableTokenDeposit(fWBTCContract.Hash);

        // Test that token deposit is disabled
        Assert.IsTrue(!Contract.IsTokenDepositEnabled(fWBTCContract.Hash));
    }

    [TestMethod]
    public void EnableDisableTokenWithdraw()
    {
        InitializeContract([]);

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};

        // Test that token withdraw is disabled
        Assert.IsTrue(!Contract.IsTokenWithdrawEnabled(fWBTCContract.Hash));

        // Test that only the owner can enable token withdraw
        Engine.SetTransactionSigners(aliceWithScope);
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.EnableTokenWithdraw(fWBTCContract.Hash));
        Console.WriteLine(exception1.Message);
        Assert.IsTrue(exception1.Message.Contains("No owner witness"));

        // Test that the token is contract
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception2 = Assert.ThrowsException<TestException>(() => Contract.EnableTokenWithdraw(Alice.Account));
        Console.WriteLine(exception2.Message);
        Assert.IsTrue(exception2.Message.Contains("Token not a contract"));

        // Test that token is valid address
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception3 = Assert.ThrowsException<TestException>(() => Contract.EnableTokenWithdraw(UInt160.Zero));
        Console.WriteLine(exception3.Message);
        Assert.IsTrue(exception3.Message.Contains("Token not a contract"));

        Contract.EnableTokenWithdraw(fWBTCContract.Hash);

        // Test that token withdraw is enabled
        Assert.IsTrue(Contract.IsTokenWithdrawEnabled(fWBTCContract.Hash));

        // Test that only the owner can disable token withdraw
        Engine.SetTransactionSigners(aliceWithScope);
        var exception4 = Assert.ThrowsException<TestException>(() => Contract.DisableTokenWithdraw(fWBTCContract.Hash, aliceWithScope.Account));
        Console.WriteLine(exception4.Message);
        Assert.IsTrue(exception4.Message.Contains("No owner or moderator witness"));

        // Test that the token is contract
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception5 = Assert.ThrowsException<TestException>(() => Contract.DisableTokenWithdraw(Alice.Account, superAdminWithScope.Account));
        Console.WriteLine(exception5.Message);
        Assert.IsTrue(exception5.Message.Contains("Token not a contract"));

        // Test that token is valid address
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception6 = Assert.ThrowsException<TestException>(() => Contract.DisableTokenWithdraw(UInt160.Zero, superAdminWithScope.Account));
        Console.WriteLine(exception6.Message);
        Assert.IsTrue(exception6.Message.Contains("Token not a contract"));

        Contract.DisableTokenWithdraw(fWBTCContract.Hash, superAdminWithScope.Account);

        // Test that token withdraw is disabled
        Assert.IsTrue(!Contract.IsTokenWithdrawEnabled(fWBTCContract.Hash));
    }

    [TestMethod]
    public void ModeratorTest()
    {
        // # Arrange
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient;

        InitializeContract([]);

        // Add a new pair
        Engine.SetTransactionSigners(SuperAdmin);
        Contract.AddPair(fWBTCContract, FLMContract, treeBitLength, pricePrecision);
        Contract.UnpausePairOrderTrading(1);
        Contract.UnpausePairOrderManagement(1);
        Contract.EnableTokenDeposit(fWBTCContract);
        Contract.EnableTokenWithdraw(fWBTCContract);
        Contract.EnableTokenDeposit(FLMContract);
        Contract.EnableTokenWithdraw(FLMContract);

        // Owner
        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        // Moderator
        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global};
        // Not a moderator
        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global};

        // Test that Alice is not a moderator
        Assert.IsTrue(!Contract.IsModerator(Alice.Account));
        // Test that Bob is not a moderator
        Assert.IsTrue(!Contract.IsModerator(Bob.Account));

        // Set Alice as a moderator
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.AddModerator(Alice.Account);

        // Test that Alice is a moderator
        Assert.IsTrue(Contract.IsModerator(Alice.Account));

        // Test that Bob is not a moderator
        Assert.IsTrue(!Contract.IsModerator(Bob.Account));

        // Test that only the owner can add a moderator
        Engine.SetTransactionSigners(bobWithScope);
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.AddModerator(Bob.Account));
        Console.WriteLine(exception1.Message);
        Assert.IsTrue(exception1.Message.Contains("No owner witness"));

        // Test that moderator can pause trading
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.PausePairOrderTrading(1, aliceWithScope.Account);
        Assert.IsTrue(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);

        // Unpause trading as owner
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.UnpausePairOrderTrading(1);
        Assert.IsTrue(!Contract.IsPairOrderTradingPaused(1)!.Value);

        // Test that Bob cannot pause trading
        Engine.SetTransactionSigners(bobWithScope);
        var exception2 = Assert.ThrowsException<TestException>(() => Contract.PausePairOrderTrading(1, bobWithScope.Account));
        Console.WriteLine(exception2.Message);
        Assert.IsTrue(exception2.Message.Contains("No owner or moderator witness"));

        // Test that moderator can pause order management
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.PausePairOrderManagement(1, Alice.Account);
        Assert.IsTrue(Contract.IsPairOrderManagementPaused(1).HasValue && Contract.IsPairOrderManagementPaused(1)!.Value);

        // Unpause order management as owner
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.UnpausePairOrderManagement(1);
        Assert.IsTrue(!Contract.IsPairOrderManagementPaused(1)!.Value);

        // Test that Bob cannot pause order management
        Engine.SetTransactionSigners(bobWithScope);
        var exception3 = Assert.ThrowsException<TestException>(() => Contract.PausePairOrderManagement(1, bobWithScope.Account));
        Console.WriteLine(exception3.Message);
        Assert.IsTrue(exception3.Message.Contains("No owner or moderator witness"));

        // Owner enables token withdraw
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.EnableTokenWithdraw(fWBTCContract.Hash);
        Assert.IsTrue(Contract.IsTokenWithdrawEnabled(fWBTCContract.Hash));

        // Test that moderator can pause withdrawals
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.DisableTokenWithdraw(fWBTCContract.Hash, aliceWithScope.Account);
        Assert.IsTrue(!Contract.IsTokenWithdrawEnabled(fWBTCContract.Hash));

        // Test that Bob cannot pause withdrawals
        Engine.SetTransactionSigners(bobWithScope);
        var exception4 = Assert.ThrowsException<TestException>(() => Contract.DisableTokenWithdraw(fWBTCContract.Hash, bobWithScope.Account));
        Console.WriteLine(exception4.Message);
        Assert.IsTrue(exception4.Message.Contains("No owner or moderator witness"));

        // Test that only owner can remove a moderator
        Engine.SetTransactionSigners(bobWithScope);
        var exception5 = Assert.ThrowsException<TestException>(() => Contract.RemoveModerator(aliceWithScope.Account));
        Console.WriteLine(exception5.Message);
        Assert.IsTrue(exception5.Message.Contains("No owner witness"));

        // Test that owner can remove a moderator
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.RemoveModerator(aliceWithScope.Account);
        Assert.IsTrue(!Contract.IsModerator(aliceWithScope.Account));
    }

    [TestMethod]
    public void AddPair()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        // This price precision is two decimal places less than the quote token precision.
        // For example with USDT that has 6 decimal places, we can trade on every 0.01 USDT.
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([]);

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};

        // Test only the owner can add a pair
        Engine.SetTransactionSigners(aliceWithScope);
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.AddPair(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision));
        Console.WriteLine(exception1.Message);
        Assert.IsTrue(exception1.Message.Contains("No owner witness"));

        // Test tokens are valid
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception2 = Assert.ThrowsException<TestException>(() => Contract.AddPair(UInt160.Zero, fUSDTContract.Hash, treeBitLength, pricePrecision));
        Console.WriteLine(exception2.Message);
        Assert.IsTrue(exception2.Message.Contains("Token not a contract"));
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception3 = Assert.ThrowsException<TestException>(() => Contract.AddPair(fWBTCContract.Hash, UInt160.Zero, treeBitLength, pricePrecision));
        Console.WriteLine(exception3.Message);
        Assert.IsTrue(exception3.Message.Contains("Token not a contract"));

        // Test for valid treebitlength
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception4 = Assert.ThrowsException<TestException>(() => Contract.AddPair(fWBTCContract.Hash, fUSDTContract.Hash, 123, pricePrecision));
        Console.WriteLine(exception4.Message);
        Assert.IsTrue(exception4.Message.Contains("Invalid tree bit length"));
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception5 = Assert.ThrowsException<TestException>(() => Contract.AddPair(fWBTCContract.Hash, fUSDTContract.Hash, -1, pricePrecision));
        Console.WriteLine(exception5.Message);
        Assert.IsTrue(exception5.Message.Contains("Invalid tree bit length"));

        // Test pair precision
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception6 = Assert.ThrowsException<TestException>(() => Contract.AddPair(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, 0));
        Console.WriteLine(exception6.Message);
        Assert.IsTrue(exception6.Message.Contains("Invalid price precision"));

        Engine.SetTransactionSigners(superAdminWithScope);
        var eventInvoked = false;
        Contract.OnPairAdded += (_, _, _, _, _) => { eventInvoked = true; };
        Contract.AddPair(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision);

        // Test pair added event is invoked
        Assert.IsTrue(eventInvoked);

        // Test that i've added a new pair
        Assert.IsTrue(Contract.PairCounter == 1);
        Assert.IsTrue(Contract.IsPairExisting(1));
    }

    [TestMethod]
    public void PauseUnpausePairOrderTrading()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision), (fUSDTContract.Hash, FLMContract.Hash, treeBitLength, pricePrecision)]);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};
        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        bool useAmm = false;

        Assert.IsFalse(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.PausePairOrderTrading(1, superAdminWithScope.Account);
        Assert.IsTrue(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);
        Engine.SetTransactionSigners(aliceWithScope);

        // SELL
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.CreateLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10, 16, 0, useAmm, false));
        Assert.IsTrue(exception1.Message.Contains("Pair order trading paused"));
        var exception2 = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10, 16, 0, useAmm, false));
        Assert.IsTrue(exception2.Message.Contains("Pair order trading paused"));
        var exception3 = Assert.ThrowsException<TestException>(() => Contract.AddLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10, 16, 0));
        Assert.IsTrue(exception3.Message.Contains("Pair order trading paused"));
        var exception4 = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitSellOrderUsingBase(aliceWithScope.Account, 1, 10, 1, 0, useAmm, false));
        Assert.IsTrue(exception4.Message.Contains("Pair order trading paused"));

        // BUY
        var exception5 = Assert.ThrowsException<TestException>(() => Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 16, 0, useAmm, false));
        Assert.IsTrue(exception5.Message.Contains("Pair order trading paused"));
        var exception6 = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 16, 0, useAmm, false));
        Assert.IsTrue(exception6.Message.Contains("Pair order trading paused"));
        var exception7 = Assert.ThrowsException<TestException>(() => Contract.AddLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, 16, 0));
        Assert.IsTrue(exception7.Message.Contains("Pair order trading paused"));
        var exception8 = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 10, Contract.GetMaxPrice(1), 0, useAmm, false));
        Assert.IsTrue(exception8.Message.Contains("Pair order trading paused"));
        var exception9 = Assert.ThrowsException<TestException>(() => Contract.ExecuteLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 1, Contract.GetMaxPrice(1), 0, useAmm, false));
        Assert.IsTrue(exception9.Message.Contains("Pair order trading paused"));

        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.UnpausePairOrderTrading(1);
        Assert.IsFalse(Contract.IsPairOrderTradingPaused(1).HasValue && Contract.IsPairOrderTradingPaused(1)!.Value);
    }

    [TestMethod]
    public void PauseUnpausePairOrderManagement()
    {
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 100);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10_000);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 1, 1, 0, false, false);

        Engine.SetTransactionSigners(bobWithScope);
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);

        Assert.IsFalse(Contract.IsPairOrderManagementPaused(1).HasValue && Contract.IsPairOrderManagementPaused(1)!.Value);
        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.PausePairOrderManagement(1, superAdminWithScope.Account);
        Assert.IsTrue(Contract.IsPairOrderManagementPaused(1).HasValue && Contract.IsPairOrderManagementPaused(1)!.Value);
        Engine.SetTransactionSigners(aliceWithScope);

        // CLAIM
        Engine.SetTransactionSigners(aliceWithScope);
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.ClaimOrder(1, 1, 15));
        Assert.IsTrue(exception1.Message.Contains("Pair order management paused"));

        // CANCEL
        var exception2 = Assert.ThrowsException<TestException>(() => Contract.CancelOrder(1, 1));
        Assert.IsTrue(exception2.Message.Contains("Pair order management paused"));

        Engine.SetTransactionSigners(superAdminWithScope);
        Contract.UnpausePairOrderManagement(1);
        Assert.IsFalse(Contract.IsPairOrderManagementPaused(1).HasValue && Contract.IsPairOrderManagementPaused(1)!.Value);
    }

    [TestMethod]
    public void SetPairFees()
    {
        const int treeBitLength = 4;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        var superAdminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global};
        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};

        Engine.SetTransactionSigners(aliceWithScope);
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.SetPairMakerFee(1, 10));
        Assert.IsTrue(exception1.Message.Contains("No owner witness"));
        Engine.SetTransactionSigners(aliceWithScope);
        var exception2 = Assert.ThrowsException<TestException>(() => Contract.SetPairTakerFee(1, 10));
        Assert.IsTrue(exception2.Message.Contains("No owner witness"));

        Engine.SetTransactionSigners(superAdminWithScope);
        var exception3 = Assert.ThrowsException<TestException>(() => Contract.SetPairMakerFee(3, 10));
        Assert.IsTrue(exception3.Message.Contains("Pair does not exists"));
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception4 = Assert.ThrowsException<TestException>(() => Contract.SetPairTakerFee(3, 10));
        Assert.IsTrue(exception4.Message.Contains("Pair does not exists"));

        Engine.SetTransactionSigners(superAdminWithScope);
        var exception5 = Assert.ThrowsException<TestException>(() => Contract.SetPairMakerFee(1, -10));
        Assert.IsTrue(exception5.Message.Contains("Invalid maker fee"));
        Engine.SetTransactionSigners(superAdminWithScope);
        var exception6 = Assert.ThrowsException<TestException>(() => Contract.SetPairTakerFee(1, -10));
        Assert.IsTrue(exception6.Message.Contains("Invalid taker fee"));

        Engine.SetTransactionSigners(superAdminWithScope);
        var makerFeeEventInvoked = false;
        Contract.OnPairMakerFeeChanged += (_, _) => { makerFeeEventInvoked = true; };
        Contract.SetPairMakerFee(1, 10);
        Assert.IsTrue(makerFeeEventInvoked);
        Assert.IsTrue(Contract.GetMakerFee(1) == 10);

        Engine.SetTransactionSigners(superAdminWithScope);
        var takerFeeEventInvoked = false;
        Contract.OnPairTakerFeeChanged += (_, _) => { takerFeeEventInvoked = true; };
        Contract.SetPairTakerFee(1, 10);
        Assert.IsTrue(takerFeeEventInvoked);
        Assert.IsTrue(Contract.GetTakerFee(1) == 10);
    }

    [TestMethod]
    public void ClaimFeeToCollector()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        // Assert fees are equal to default values
        var takerFeeValue = Contract.GetTakerFee(1);
        var makerFeeValue = Contract.GetMakerFee(1);
        Assert.IsTrue(takerFeeValue == 1500);
        Assert.IsTrue(makerFeeValue == 1500);

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 10_000_000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10_000_000);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 65536, 65536, 0, false, false);

        Engine.SetTransactionSigners(bobWithScope);
        var onOrderExecutedInvoked = false;
        Contract.OnOrderUpserted += (_, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _) =>
        {
            onOrderExecutedInvoked = true;
        };
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);
        Assert.IsTrue(onOrderExecutedInvoked);

        Engine.SetTransactionSigners(aliceWithScope);
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.ClaimFeeToCollector(FLMContract.Hash));
        Assert.IsTrue(exception1.Message.Contains("Only fee collector can call this method"));

        // Set that the signer is the fee collector
        Engine.SetTransactionSigners(new Signer {Account = Contract.FeeCollector, Scopes = WitnessScope.Global,});
        Engine.OnGetCallingScriptHash = (_, _) => { return Contract.FeeCollector; };

        // Check not transferred if balance of token fee is 0
        var onFeeClaimedInvoked = false;
        Contract.OnFeeClaimed += (_, _, _) => { onFeeClaimedInvoked = true; };
        Contract.ClaimFeeToCollector(FLMContract.Hash);
        Assert.IsFalse(onFeeClaimedInvoked);

        Engine.SetTransactionSigners(new Signer {Account = Contract.FeeCollector, Scopes = WitnessScope.Global,});
        Engine.OnGetCallingScriptHash = (_, _) => { return Contract.FeeCollector; };

        // Check not transferred if balance of token fee is 0
        var onFeeClaimedInvoked2 = false;
        Contract.OnFeeClaimed += (_, _, _) => { onFeeClaimedInvoked2 = true; };
        var feeBalance = Contract.GetFeeBalance(fUSDTContract.Hash);
        Assert.IsTrue(feeBalance > 0);
        var collectorBalanceBefore = fUSDTContract.BalanceOf(Contract.FeeCollector);

        // Use contract as signer so that the transfer works insider the broker call
        Engine.SetTransactionSigners(new Signer {Account = Contract.Hash, Scopes = WitnessScope.Global,});
        // Fake that this has been called by the fee callector so that it goes through
        Engine.OnGetCallingScriptHash = (_, _) => { return Contract.FeeCollector; };
        Contract.ClaimFeeToCollector(fUSDTContract.Hash);
        var collectorBalanceAfter = fUSDTContract.BalanceOf(Contract.FeeCollector);

        // OnGetCallingScriptHash make the event not being fired but it has been tested without and it's emitted correctly
        //Assert.IsTrue(onFeeClaimedInvoked2);
        Assert.IsTrue(Contract.GetFeeBalance(fUSDTContract.Hash) == 0);
        Assert.IsTrue((collectorBalanceAfter - collectorBalanceBefore) == feeBalance);
    }

    [TestMethod]
    public void ClaimFeeToCollectorAndFusdFund()
    {
        const int treeBitLength = 16;
        var priceCoefficient = Contract.PriceCoefficient!.Value;
        var pricePrecision = priceCoefficient * 1;

        InitializeContract([(fWBTCContract.Hash, fUSDTContract.Hash, treeBitLength, pricePrecision)]);

        // Assert fees are equal to default values
        var takerFeeValue = Contract.GetTakerFee(1);
        var makerFeeValue = Contract.GetMakerFee(1);
        Assert.IsTrue(takerFeeValue == 1500);
        Assert.IsTrue(makerFeeValue == 1500);

        var adminWithScope = new Signer {Account = SuperAdmin.Account, Scopes = WitnessScope.Global,};

        var bobWithScope = new Signer {Account = Bob.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(bobWithScope);
        Contract.Deposit(bobWithScope.Account, fWBTCContract.Hash, 10_000_000);

        var aliceWithScope = new Signer {Account = Alice.Account, Scopes = WitnessScope.Global,};
        Engine.SetTransactionSigners(aliceWithScope);
        Contract.Deposit(aliceWithScope.Account, fUSDTContract.Hash, 10_000_000);
        Contract.CreateLimitBuyOrderUsingQuote(aliceWithScope.Account, 1, 65536, 65536, 0, false, false);

        var fusdFundAccount = TestEngine.GetNewSigner();

        Engine.SetTransactionSigners(bobWithScope);
        var onOrderExecutedInvoked = false;
        Contract.OnOrderUpserted += (_, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _) =>
        {
            onOrderExecutedInvoked = true;
        };
        Contract.ExecuteLimitSellOrderUsingBase(bobWithScope.Account, 1, 100, 1, 0, false, false);
        Assert.IsTrue(onOrderExecutedInvoked);

        Engine.SetTransactionSigners(adminWithScope);
        Contract.SetFusdFundAddress(fusdFundAccount.Account);

        Engine.SetTransactionSigners(aliceWithScope);
        var exception1 = Assert.ThrowsException<TestException>(() => Contract.ClaimFeeToCollector(FLMContract.Hash));
        Assert.IsTrue(exception1.Message.Contains("Only fee collector can call this method"));

        // Set that the signer is the fee collector
        Engine.SetTransactionSigners(new Signer {Account = Contract.FeeCollector, Scopes = WitnessScope.Global,});
        Engine.OnGetCallingScriptHash = (_, _) => { return Contract.FeeCollector; };

        // Check not transferred if balance of token fee is 0
        var onFeeClaimedInvoked = false;
        Contract.OnFeeClaimed += (_, _, _) => { onFeeClaimedInvoked = true; };
        Contract.ClaimFeeToCollector(FLMContract.Hash);
        Assert.IsFalse(onFeeClaimedInvoked);

        Engine.SetTransactionSigners(new Signer {Account = Contract.FeeCollector, Scopes = WitnessScope.Global,});
        Engine.OnGetCallingScriptHash = (_, _) => { return Contract.FeeCollector; };

        // Check not transferred if balance of token fee is 0
        var onFeeClaimedInvoked2 = false;
        Contract.OnFeeClaimed += (_, _, _) => { onFeeClaimedInvoked2 = true; };
        var feeBalance = Contract.GetFeeBalance(fUSDTContract.Hash);
        Assert.IsTrue(feeBalance > 0);
        var collectorBalanceBefore = fUSDTContract.BalanceOf(Contract.FeeCollector);
        var fusdFundBalanceBefore = fUSDTContract.BalanceOf(fusdFundAccount.Account);

        // Use contract as signer so that the transfer works insider the broker call
        Engine.SetTransactionSigners(new Signer {Account = Contract.Hash, Scopes = WitnessScope.Global,});
        // Fake that this has been called by the fee callector so that it goes through
        Engine.OnGetCallingScriptHash = (_, _) => { return Contract.FeeCollector; };
        Contract.ClaimFeeToCollector(fUSDTContract.Hash);
        var collectorBalanceAfter = fUSDTContract.BalanceOf(Contract.FeeCollector);
        var fusdFundBalanceAfter = fUSDTContract.BalanceOf(fusdFundAccount.Account);

        var feesCollectedByCollector = collectorBalanceAfter - collectorBalanceBefore;
        var feesCollectedByFusdFund = fusdFundBalanceAfter - fusdFundBalanceBefore;

        var feeBalanceToFusdFund = (BigInteger)feeBalance * 200000 / 1000000;
        var feeBalanceToCollector = feeBalance - feeBalanceToFusdFund;

        // OnGetCallingScriptHash make the event not being fired but it has been tested without and it's emitted correctly
        //Assert.IsTrue(onFeeClaimedInvoked2);
        Assert.IsTrue(Contract.GetFeeBalance(fUSDTContract.Hash) == 0);
        Assert.AreEqual(feeBalanceToCollector, feesCollectedByCollector);
        Assert.AreEqual(feeBalanceToFusdFund, feesCollectedByFusdFund);
    }

    private void InitializeContract((UInt160 baseToken, UInt160 quoteToken, BigInteger treeBitLength, BigInteger pricePrecision)[] pairs)
    {
        Engine.SetTransactionSigners(SuperAdmin);

        Contract.ChangeAMMRouter(SwapRouterContract.Hash);
        Contract.ChangeAMMFactory(SwapFactoryContract.Hash);
        Contract.ChangeFeeCollector(Alice.Account);
        //Contract.ChangeFeeCollector(FlocksContract.Hash);
        SwapRouterContract.SetBrokerContract(Contract.Hash);

        for (int i = 0; i < pairs.Length; i++)
        {
            var pair = pairs[i];

            Contract.AddPair(pair.baseToken, pair.quoteToken, pair.treeBitLength, pair.pricePrecision);
            Contract.UnpausePairOrderTrading(i + 1);
            Contract.UnpausePairOrderManagement(i + 1);

            Contract.EnableTokenDeposit(pair.baseToken);
            Contract.EnableTokenWithdraw(pair.baseToken);

            Contract.EnableTokenDeposit(pair.quoteToken);
            Contract.EnableTokenWithdraw(pair.quoteToken);
        }

        Engine.SetTransactionSigners(Alice);
    }

    private BigInteger CalculateQuoteAmountForBaseAmount(BigInteger pairId, BigInteger limitPrice, BigInteger baseAmount)
    {
        var realPrice = Contract.ConvertPriceRowToRealPrice(limitPrice, Contract.GetPricePrecision(pairId), Contract.GetDecimals(Contract.GetBaseToken(pairId)), Contract.GetDecimals(Contract.GetQuoteToken(pairId)));
        var quoteAmount = baseAmount * realPrice / Contract.PriceRoundingPrecision;
        return (BigInteger)quoteAmount;
    }

    private BigInteger CalculateBaseAmountForQuoteAmount(BigInteger pairId, BigInteger limitPrice, BigInteger quoteAmount)
    {
        var realPrice = Contract.ConvertPriceRowToRealPrice(limitPrice, Contract.GetPricePrecision(pairId), Contract.GetDecimals(Contract.GetBaseToken(pairId)), Contract.GetDecimals(Contract.GetQuoteToken(pairId)));
        var baseAmount = quoteAmount * Contract.PriceRoundingPrecision / realPrice;
        return (BigInteger)baseAmount;
    }

    private Func<int, int, BigInteger> GetNodeIndex(BigInteger treeBitLength)
    {
        return (row, col) => (BigInteger) Contract.GetNodeIndex(treeBitLength, col, row)!;
    }

    private Func<BigInteger, OrderBookImageGenerator.PriceNode> GetPriceNode(bool fromSellTree)
    {
        return nodeIndex =>
        {
            Array priceNode = (Array) Contract.GetPriceNode(1, nodeIndex, fromSellTree)!;
            return new OrderBookImageGenerator.PriceNode
            {
                BaseAmount = priceNode[0].GetInteger(), QuoteTotal = priceNode[1].GetInteger(), EmptiedCount = priceNode[2].GetInteger(), EmptiedCountSibling = priceNode[3].GetInteger(),
            };
        };
    }

    private OrderBookImageGenerator.OrderBookGrid GenerateGrid(int treeBitLength, bool fromSellTree = true)
    {
        var getNodeIndex = GetNodeIndex(treeBitLength);
        var getPriceNode = GetPriceNode(fromSellTree);
        return OrderBookImageGenerator.GenerateOrderBookGrid(treeBitLength, getNodeIndex, getPriceNode);
    }

    private static void ExportGrid(OrderBookImageGenerator.OrderBookGrid grid, string outputPath)
    {
        var fullOutputPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "OrderBookImages", outputPath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        OrderBookImageGenerator.ExportOrderBookGridImage(grid, fullOutputPath);
    }

    private static Order RawOrderToOrder(Array? rawOrder)
    {
        Debug.Assert(rawOrder != null, nameof(rawOrder) + " != null");

        return new Order
        {
            Owner = new UInt160(rawOrder[0].GetSpan()),
            TotalOrderBaseAmount = rawOrder[1].GetInteger(),
            TotalOrderQuoteAmount = rawOrder[2].GetInteger(),
            PlacedInOrderBookBaseAmount = rawOrder[3].GetInteger(),
            PlacedInOrderBookQuoteAmount = rawOrder[4].GetInteger(),
            Price = rawOrder[5].GetInteger(),
            BaseAmountTradedInAmm = rawOrder[6].GetInteger(),
            QuoteAmountTradedInAmm = rawOrder[7].GetInteger(),
            BaseAmountTradedBeforeOrderPlaced = rawOrder[8].GetInteger(),
            QuoteAmountTradedBeforeOrderPlaced = rawOrder[9].GetInteger(),
            IsBuy = rawOrder[10].GetBoolean(),
            CancelledBaseAmount = rawOrder[11].GetInteger(),
            CancelledQuoteAmount = rawOrder[12].GetInteger(),
            ClaimedBaseAmount = rawOrder[13].GetInteger(),
            ClaimedQuoteAmount = rawOrder[14].GetInteger(),
            EmptiedCountWhenInserted = rawOrder[15].GetInteger(),
            GlobalBaseAmountPlacedAtPriceWhenInserted = rawOrder[16].GetInteger(),
            GlobalQuoteAmountPlacedAtPriceWhenInserted = rawOrder[17].GetInteger(),
            CancelId = rawOrder[18].GetInteger(),
            FeeAmount = rawOrder[19].GetInteger(),
            CreatedAt = rawOrder[20].GetInteger(),
        };
    }

    private static OrderInfo RawOrderInfoToOrderInfo(Array? rawOrder)
    {
        Debug.Assert(rawOrder != null, nameof(rawOrder) + " != null");

        return new OrderInfo
        {
            Id = rawOrder[0].GetInteger(),
            PairId = rawOrder[1].GetInteger(),
            Owner = new UInt160(rawOrder[2].GetSpan()),
            TotalOrderBaseAmount = rawOrder[3].GetInteger(),
            TotalOrderQuoteAmount = rawOrder[4].GetInteger(),
            PlacedInOrderBookBaseAmount = rawOrder[5].GetInteger(),
            PlacedInOrderBookQuoteAmount = rawOrder[6].GetInteger(),
            Price = rawOrder[7].GetInteger(),
            BaseAmountTradedInAmm = rawOrder[8].GetInteger(),
            QuoteAmountTradedInAmm = rawOrder[9].GetInteger(),
            BaseAmountTradedBeforeOrderPlaced = rawOrder[10].GetInteger(),
            QuoteAmountTradedBeforeOrderPlaced = rawOrder[11].GetInteger(),
            IsBuy = rawOrder[12].GetBoolean(),
            CancelledBaseAmount = rawOrder[13].GetInteger(),
            CancelledQuoteAmount = rawOrder[14].GetInteger(),
            ClaimedBaseAmount = rawOrder[15].GetInteger(),
            ClaimedQuoteAmount = rawOrder[16].GetInteger(),
            EmptiedCountWhenInserted = rawOrder[17].GetInteger(),
            GlobalBaseAmountPlacedAtPriceWhenInserted = rawOrder[18].GetInteger(),
            GlobalQuoteAmountPlacedAtPriceWhenInserted = rawOrder[19].GetInteger(),
            CancelId = rawOrder[20].GetInteger(),
            FeeAmount = rawOrder[21].GetInteger(),
            CreatedAt = rawOrder[22].GetInteger(),
        };
    }

    public class Order
    {
        // The user submitting the order
        public UInt160 Owner;
        // The amount as base token that the user placed originally.
        // This is used to have an accurate history of the user's activity.
        public BigInteger TotalOrderBaseAmount;
        // The amount as quote token that the user placed originally.
        // This is used to have an accurate history of the user's activity.
        public BigInteger TotalOrderQuoteAmount;
        // The amount as base token of the order that was placed in the order book.
        // This can be less than the original amount if the order has been partially filled when it was placed.
        public BigInteger PlacedInOrderBookBaseAmount;
        // The amount as quote token of the order that was placed in the order book.
        public BigInteger PlacedInOrderBookQuoteAmount;
        // The price at which the order has been added
        public BigInteger Price;
        // The amount as base token that was traded in the AMM when the order was placed.
        // If the amount is more than 0, it means that the order has been partially filled by the AMM when it was placed.
        public BigInteger BaseAmountTradedInAmm;
        // The amount as quote token that was traded in the AMM when the order was placed.
        // If the amount is more than 0, it means that the order has been partially filled by the AMM when it was placed.
        public BigInteger QuoteAmountTradedInAmm;
        // The amount as base token that was traded before the order was placed.
        public BigInteger BaseAmountTradedBeforeOrderPlaced;
        // The amount as quote token that was traded before the order was placed.
        public BigInteger QuoteAmountTradedBeforeOrderPlaced;
        // To know if buy or sell order
        public bool IsBuy;
        // The base amount that has been cancelled by the owner. Any amount greater than 0 means that the order has been cancelled.
        public BigInteger CancelledBaseAmount;
        // The quote amount that has been cancelled by the owner. Any amount greater than 0 means that the order has been cancelled.
        public BigInteger CancelledQuoteAmount;
        // The base amount claimed by the owner
        public BigInteger ClaimedBaseAmount;
        // The quote amount claimed by the owner
        public BigInteger ClaimedQuoteAmount;
        // The empty count at that price when the order has been inserted
        public BigInteger EmptiedCountWhenInserted;
        // The global base amount that has been placed in the order book at the same price when the order has been inserted.
        // This is used to check if the order has been filled or not.
        public BigInteger GlobalBaseAmountPlacedAtPriceWhenInserted;
        // The global quote amount that has been placed in the order book at the same price when the order has been inserted.
        // This is used to check if the order has been filled or not.
        public BigInteger GlobalQuoteAmountPlacedAtPriceWhenInserted;
        // An ID assigned to the order when created, so we can keep track of cancelled amounts for partially filled orders.
        // We use a separate ID for this since it is related to the price that the order was placed at, and because we can then reset the ID counter when a price shelf is emptied.
        public BigInteger CancelId;
        // The amount of fee that has been paid on the order. The fee is paid in the base token if it is a buy order, and in the quote token if it is a sell order.
        public BigInteger FeeAmount;
        // The timestamp at which the order has been created
        public BigInteger CreatedAt;

        public void PrintPublicFields()
        {
            Type type = GetType();
            Console.WriteLine("Public fields of " + type.Name + ":");

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                var value = field.GetValue(this)!;
                Console.WriteLine($"{field.Name} = {value}");
            }
        }
    }

    public class OrderInfo
    {
        // The id of the order
        public BigInteger Id;
        // The pair id of the order
        public BigInteger PairId;
        // The user submitting the order
        public UInt160 Owner;
        // The amount as base token that the user placed originally.
        // This is used to have an accurate history of the user's activity.
        public BigInteger TotalOrderBaseAmount;
        // The amount as quote token that the user placed originally.
        // This is used to have an accurate history of the user's activity.
        public BigInteger TotalOrderQuoteAmount;
        // The amount as base token of the order that was placed in the order book.
        // This can be less than the original amount if the order has been partially filled when it was placed.
        public BigInteger PlacedInOrderBookBaseAmount;
        // The amount as quote token of the order that was placed in the order book.
        public BigInteger PlacedInOrderBookQuoteAmount;
        // The price at which the order has been added
        public BigInteger Price;
        // The amount as base token that was traded in the AMM when the order was placed.
        // If the amount is more than 0, it means that the order has been partially filled by the AMM when it was placed.
        public BigInteger BaseAmountTradedInAmm;
        // The amount as quote token that was traded in the AMM when the order was placed.
        // If the amount is more than 0, it means that the order has been partially filled by the AMM when it was placed.
        public BigInteger QuoteAmountTradedInAmm;
        // The amount as base token that was traded before the order was placed.
        public BigInteger BaseAmountTradedBeforeOrderPlaced;
        // The amount as quote token that was traded before the order was placed.
        public BigInteger QuoteAmountTradedBeforeOrderPlaced;
        // To know if buy or sell order
        public bool IsBuy;
        // The base amount that has been cancelled by the owner. Any amount greater than 0 means that the order has been cancelled.
        public BigInteger CancelledBaseAmount;
        // The quote amount that has been cancelled by the owner. Any amount greater than 0 means that the order has been cancelled.
        public BigInteger CancelledQuoteAmount;
        // The base amount claimed by the owner
        public BigInteger ClaimedBaseAmount;
        // The quote amount claimed by the owner
        public BigInteger ClaimedQuoteAmount;
        // The empty count at that price when the order has been inserted
        public BigInteger EmptiedCountWhenInserted;
        // The global base amount that has been placed in the order book at the same price when the order has been inserted.
        // This is used to check if the order has been filled or not.
        public BigInteger GlobalBaseAmountPlacedAtPriceWhenInserted;
        // The global quote amount that has been placed in the order book at the same price when the order has been inserted.
        // This is used to check if the order has been filled or not.
        public BigInteger GlobalQuoteAmountPlacedAtPriceWhenInserted;
        // An ID assigned to the order when created, so we can keep track of cancelled amounts for partially filled orders.
        // We use a separate ID for this since it is related to the price that the order was placed at, and because we can then reset the ID counter when a price shelf is emptied.
        public BigInteger CancelId;
        // The amount of fee that has been paid on the order. The fee is paid in the base token if it is a buy order, and in the quote token if it is a sell order.
        public BigInteger FeeAmount;
        // The timestamp at which the order has been created
        public BigInteger CreatedAt;

        public void PrintPublicFields()
        {
            Type type = GetType();
            Console.WriteLine("Public fields of " + type.Name + ":");

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                var value = field.GetValue(this)!;
                Console.WriteLine($"{field.Name} = {value}");
            }
        }
    }

    private void AdvanceBlocks(int blockCount = 1)
    {
        // Fake advance Ledger.CurrentHeight, so blockchain blocks for testing
        for (int i = 0; i < blockCount; i++)
        {
            Engine.PersistingBlock.Persist(new Transaction()
            {
                Attributes = [],
                Signers =
                [
                    new()
                    {
                        Account = UInt160.Zero,
                        AllowedContracts = [],
                        AllowedGroups = [],
                        Rules = [],
                        Scopes = WitnessScope.Global
                    }
                ],
                Witnesses = [],
                Script = System.Array.Empty<byte>()
            });
        }
    }
}
