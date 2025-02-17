using System.Numerics;
using Flamingo.Broker.Models;
using Flamingo.Broker.Utils;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Broker
{
    public partial class FlamingoBroker
    {
/// <summary>
        /// Create a limit buy order, processing the amount and placing what's left in the orderbook.
        /// </summary>
        /// <param name="user">The user related to the operation.</param>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="quoteAmount">The amount of quote currency.</param>
        /// <param name="limitPrice">The limit price for the order.</param>
        /// <param name="useAMM">Whether to use the AMM when matching the order through the orderbook.</param>
        /// <param name="debug">Wether to output debug information.</param>
        [NoReentrant]
        public static void CreateLimitBuyOrderUsingQuote(UInt160 user, BigInteger pairId, BigInteger quoteAmount, BigInteger limitPrice, BigInteger userDefinedId, bool useAMM, bool debug)
        {
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(user), "Invalid address");
            ExecutionEngine.Assert(Runtime.CheckWitness(user) || user.Equals(Runtime.CallingScriptHash), "No user witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");
            ExecutionEngine.Assert(!IsPairOrderTradingPaused(pairId), "Pair order trading paused");
            ExecutionEngine.Assert(quoteAmount > 0, "Amount must be greater than 0");

            // Create an object with pair data to be reusable and efficient from storage read perspective
            var baseToken = GetBaseToken(pairId);
            var quoteToken = GetQuoteToken(pairId);
            Pair pair = new Pair()
            {
                Id = pairId,
                BaseToken = baseToken,
                QuoteToken = quoteToken,
                TreeWidth = (int) GetTreeBitLength(pairId),
                PricePrecision = GetPricePrecision(pairId),
                QuoteTokenDecimals = GetDecimals(quoteToken),
                BaseTokenDecimals = GetDecimals(baseToken),
            };
            ExecutionEngine.Assert(limitPrice > 0 && limitPrice < (1 << pair.TreeWidth) + 1, "Invalid limit price");

            var realPrice = ConvertPriceRowToRealPrice(limitPrice, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);
            var baseAmount = quoteAmount * PriceRoundingPrecision / realPrice;
            ExecutionEngine.Assert(baseAmount > 0, "Base amount must be greater than zero [E_CLBO_QA]");

            // Process the order with the limit price provided by the user
            (BigInteger totalBaseAmountReceived, BigInteger totalQuoteAmountSpent, BigInteger baseAmountSwapped, BigInteger quoteAmountSwapped, BigInteger endingPrice, BigInteger feeAmount) = ProcessBuyOrderUsingQuote(pair, quoteAmount, limitPrice, useAMM, debug);

            var quoteAmountLeft = quoteAmount - totalQuoteAmountSpent;

            var baseAmountTradedBeforeOrderPlaced = totalBaseAmountReceived - baseAmountSwapped;
            var quoteAmountTradedBeforeOrderPlaced = totalQuoteAmountSpent - quoteAmountSwapped;

            var baseAmountToPlace = quoteAmountLeft * PriceRoundingPrecision / realPrice;
            var quoteAmountPlaced = PlaceBuyOrder(
                user,
                pair,
                quoteAmountLeft,
                baseAmountToPlace,
                limitPrice,
                baseAmount,
                quoteAmount,
                baseAmountSwapped,
                quoteAmountSwapped,
                baseAmountTradedBeforeOrderPlaced,
                quoteAmountTradedBeforeOrderPlaced,
                endingPrice,
                feeAmount,
                userDefinedId
            );

            totalQuoteAmountSpent += quoteAmountPlaced;

            // Quote balance check and update
            ExecutionEngine.Assert(GetAccountBalance(user, pair.QuoteToken) >= totalQuoteAmountSpent, "Not enough funds");
            DecreaseUserBalance(user, pair.QuoteToken, totalQuoteAmountSpent);
            // Base balance update
            IncreaseUserBalance(user, pair.BaseToken, totalBaseAmountReceived);

            DebugMethodCalled(user, "CreateLimitBuyOrderUsingQuote", new List<object> { user, pairId, quoteAmount, limitPrice, userDefinedId });
        }

        /// <summary>
        /// Execute a limit buy order, processing the amount and spending only what has been processed in the orderbook.
        /// </summary>
        /// <param name="user">The user related to the operation.</param>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="quoteAmount">The amount of quote currency.</param>
        /// <param name="limitPrice">The limit price for the order.</param>
        /// <param name="useAMM">Whether to use the AMM when matching the order through the orderbook.</param>
        /// <param name="debug">Wether to output debug information.</param>
        [NoReentrant]
        public static void ExecuteLimitBuyOrderUsingQuote(UInt160 user, BigInteger pairId, BigInteger quoteAmount, BigInteger limitPrice, BigInteger userDefinedId, bool useAMM, bool debug)
        {
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(user), "Invalid address");
            ExecutionEngine.Assert(Runtime.CheckWitness(user) || user.Equals(Runtime.CallingScriptHash), "No user witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");
            ExecutionEngine.Assert(!IsPairOrderTradingPaused(pairId), "Pair order trading paused");
            ExecutionEngine.Assert(quoteAmount > 0, "Amount must be greater than 0");

            // Create an object with pair data to be reusable and efficient from storage read perspective
            var baseToken = GetBaseToken(pairId);
            var quoteToken = GetQuoteToken(pairId);
            Pair pair = new Pair()
            {
                Id = pairId,
                BaseToken = baseToken,
                QuoteToken = quoteToken,
                TreeWidth = (int) GetTreeBitLength(pairId),
                PricePrecision = GetPricePrecision(pairId),
                QuoteTokenDecimals = GetDecimals(quoteToken),
                BaseTokenDecimals = GetDecimals(baseToken),
            };
            ExecutionEngine.Assert(limitPrice > 0 && limitPrice < (1 << pair.TreeWidth) + 1, "Invalid limit price");

            var realPrice = ConvertPriceRowToRealPrice(limitPrice, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);
            var baseAmount = quoteAmount * PriceRoundingPrecision / realPrice;
            ExecutionEngine.Assert(baseAmount > 0, "Base amount must be greater than zero [E_ELBO_QA]");

            // Process the order with the limit price provided by the user
            (BigInteger totalBaseAmountReceived, BigInteger totalQuoteAmountSpent, BigInteger baseAmountSwapped, BigInteger quoteAmountSwapped, BigInteger endingPrice, BigInteger feeAmount) =
                ProcessBuyOrderUsingQuote(pair, quoteAmount, limitPrice, useAMM, debug);

            var baseAmountTradedBeforeOrderPlaced = totalBaseAmountReceived - baseAmountSwapped;
            var quoteAmountTradedBeforeOrderPlaced = totalQuoteAmountSpent - quoteAmountSwapped;

            PlaceBuyOrder(
                user,
                pair,
                0,
                0,
                limitPrice,
                baseAmount,
                quoteAmount,
                baseAmountSwapped,
                quoteAmountSwapped,
                baseAmountTradedBeforeOrderPlaced,
                quoteAmountTradedBeforeOrderPlaced,
                endingPrice,
                feeAmount,
                userDefinedId
            );

            // Quote balance check and update
            ExecutionEngine.Assert(GetAccountBalance(user, pair.QuoteToken) >= totalQuoteAmountSpent, "Not enough funds");
            DecreaseUserBalance(user, pair.QuoteToken, totalQuoteAmountSpent);
            // Base balance update
            IncreaseUserBalance(user, pair.BaseToken, totalBaseAmountReceived);

            DebugMethodCalled(user, "ExecuteLimitBuyOrderUsingQuote", new List<object>
            {
                user,
                pairId,
                quoteAmount,
                limitPrice,
                userDefinedId
            });
        }

        /// <summary>
        /// Add a limit buy order into the orderbook.
        /// </summary>
        /// <param name="user">The user related to the operation.</param>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="quoteAmount">The amount of quote currency.</param>
        /// <param name="limitPrice">The limit price for the order.</param>
        [NoReentrant]
        public static void AddLimitBuyOrderUsingQuote(UInt160 user, BigInteger pairId, BigInteger quoteAmount, BigInteger limitPrice, BigInteger userDefinedId)
        {
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(user), "Invalid address");
            ExecutionEngine.Assert(Runtime.CheckWitness(user) || user.Equals(Runtime.CallingScriptHash), "No user witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");
            ExecutionEngine.Assert(!IsPairOrderTradingPaused(pairId), "Pair order trading paused");
            ExecutionEngine.Assert(quoteAmount > 0, "Amount must be greater than 0");
            // Create an object with pair data to be reusable and efficient from storage read perspective
            var baseToken = GetBaseToken(pairId);
            var quoteToken = GetQuoteToken(pairId);
            Pair pair = new Pair()
            {
                Id = pairId,
                BaseToken = baseToken,
                QuoteToken = quoteToken,
                TreeWidth = (int) GetTreeBitLength(pairId),
                PricePrecision = GetPricePrecision(pairId),
                QuoteTokenDecimals = GetDecimals(quoteToken),
                BaseTokenDecimals = GetDecimals(baseToken),
            };
            ExecutionEngine.Assert(limitPrice > 0 && limitPrice < (1 << pair.TreeWidth) + 1, "Invalid limit price");

            var realPrice = ConvertPriceRowToRealPrice(limitPrice, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);
            var baseAmount = quoteAmount * PriceRoundingPrecision / realPrice;
            ExecutionEngine.Assert(baseAmount > 0, "Base amount must be greater than zero [E_ALBO_QA]");

            var (baseTokenReserve, quoteTokenReserve) = AMMHelper.GetReserves(pair.BaseToken, pair.QuoteToken);
            var ammAsPriceLimitPrice = ConvertAmmPriceToLimitPrice(baseTokenReserve, quoteTokenReserve, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);

            ExecutionEngine.Assert(limitPrice < ammAsPriceLimitPrice, "Limit price is higher than AMM price");

            // Place the amount in the orderbook
            var baseAmountToPlace = quoteAmount * PriceRoundingPrecision / realPrice;

            var quoteAmountPlaced = PlaceBuyOrder(
                user,
                pair,
                quoteAmount,
                baseAmountToPlace,
                limitPrice,
                baseAmount,
                quoteAmount,
                0,
                0,
                0,
                0,
                0,
                0,
                userDefinedId
            );
            ExecutionEngine.Assert(quoteAmountPlaced >= 0, "Invalid amount placed");

            // Quote balance check and update
            ExecutionEngine.Assert(GetAccountBalance(user, pair.QuoteToken) >= quoteAmountPlaced, "Not enough funds");
            DecreaseUserBalance(user, pair.QuoteToken, quoteAmountPlaced);

            DebugMethodCalled(user, "AddLimitBuyOrderUsingQuote", new List<object>
            {
                user,
                pairId,
                quoteAmount,
                limitPrice,
                userDefinedId
            });
        }

        private static (BigInteger totalBaseAmountReceived, BigInteger totalQuoteAmountSpent, BigInteger baseAmountSwapped, BigInteger quoteAmountSwapped, BigInteger endingPrice, BigInteger feeAmount) ProcessBuyOrderUsingQuote(
            Pair pair,
            BigInteger quoteAmount,
            BigInteger limitPrice,
            bool useAMM,
            bool debug = false
        )
        {
            // Get token reserve for amm usage
            var (baseTokenReserve, quoteTokenReserve) = useAMM ? AMMHelper.GetReserves(pair.BaseToken, pair.QuoteToken) : (0, 0);
            // Orderbook + AMM matching
            var (quoteAmountLeft, quoteAmountFromAMM, baseAmountReceived, endingPrice) = MatchBuyOrderUsingQuote(quoteAmount, pair, limitPrice, useAMM, baseTokenReserve, quoteTokenReserve);

            // Fee calculation
            BigInteger baseFee = GetTakerFee(pair.Id) * (baseAmountReceived) / FeeRoundingPrecision;
            IncreaseFeeBalance(pair.BaseToken, baseFee);
            baseAmountReceived -= baseFee;

            // AMM swap
            BigInteger totalBaseAmountReceived = baseAmountReceived;
            BigInteger totalQuoteAmountSpent = quoteAmount - quoteAmountLeft;
            BigInteger baseAmountSwapped = 0;
            BigInteger quoteAmountSwapped = 0;

            long gasUsed = 0;

            if (quoteAmountFromAMM > 0)
            {
                if (debug) gasUsed = Runtime.GasLeft;

                BigInteger balanceOfBaseTokenBefore = NEP17Helper.SafeBalanceOf(pair.BaseToken, Runtime.ExecutingScriptHash);
                BigInteger balanceOfQuoteTokenBefore = NEP17Helper.SafeBalanceOf(pair.QuoteToken, Runtime.ExecutingScriptHash);

                ExecutionEngine.Assert(balanceOfQuoteTokenBefore >= quoteAmountFromAMM, "Not enough quote token in the contract to swap");

                AMMHelper.SafeSwapTokenInForTokenOut(quoteAmountFromAMM, 0, new UInt160[] {pair.QuoteToken, pair.BaseToken});

                BigInteger balanceOfBaseTokenAfter = NEP17Helper.SafeBalanceOf(pair.BaseToken, Runtime.ExecutingScriptHash);
                BigInteger balanceOfQuoteTokenAfter = NEP17Helper.SafeBalanceOf(pair.QuoteToken, Runtime.ExecutingScriptHash);

                var (newBaseTokenReserve, newQuoteTokenReserve) = AMMHelper.GetReserves(pair.BaseToken, pair.QuoteToken);
                TakePriceSnapshot(pair.Id, newBaseTokenReserve, newQuoteTokenReserve, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);

                // NB: Here we are using quote tokens to buy the base tokens so before and after are inverted
                baseAmountSwapped = balanceOfBaseTokenAfter - balanceOfBaseTokenBefore;
                quoteAmountSwapped = balanceOfQuoteTokenBefore - balanceOfQuoteTokenAfter;

                totalBaseAmountReceived += baseAmountSwapped;
                totalQuoteAmountSpent += quoteAmountSwapped;

                ExecutionEngine.Assert(quoteAmountSwapped == quoteAmountFromAMM, "Invalid swap");

                if (debug)
                {
                    var gasUsedTotal = gasUsed - Runtime.GasLeft;
                    ExecutionEngine.Assert(false, gasUsedTotal.ToString());
                }
            }
            else
            {
                var gasToBurn = GasToBurnStorage.Get(pair.Id);
                if (gasToBurn > 0) Runtime.BurnGas((long) gasToBurn);
            }

            ExecutionEngine.Assert(totalBaseAmountReceived >= 0, "Invalid amount processed [E_PBOUQ1]");
            ExecutionEngine.Assert(totalQuoteAmountSpent >= 0, "Invalid amount processed [E_PBOUQ2]");

            // Return remaining base amount
            return (totalBaseAmountReceived, totalQuoteAmountSpent, baseAmountSwapped, quoteAmountSwapped, endingPrice, baseFee);
        }

        private static (BigInteger quoteAmountLeft, BigInteger quoteAmountFromAMM, BigInteger baseAmountReceived, BigInteger endingPrice) MatchBuyOrderUsingQuote(
            BigInteger quoteAmount,
            Pair pair,
            BigInteger limitPrice,
            bool useAMM,
            BigInteger baseTokenReserve,
            BigInteger quoteTokenReserve
        )
        {
            var treeWidth = pair.TreeWidth;
            BigInteger rightMostNumberOfNodes = 1 << treeWidth;

            // The array with nodes to be updated as (nodeIndex, priceNode, baseConsumedAtColumn, quoteConsumedAtColumn) 1 node per column
            (BigInteger, PriceNode, BigInteger, BigInteger)[] nodesToBeUpdated = new (BigInteger, PriceNode, BigInteger, BigInteger)[treeWidth];

            var quoteAmountLeft = quoteAmount;
            BigInteger baseAmountReceived = 0;

            BigInteger quoteAmountFromAMM = 0;

            var lowerNodeLowerPrice = (BigInteger) 1;
            var upperNodeUpperPrice = rightMostNumberOfNodes;
            var lowerNodeUpperPrice = rightMostNumberOfNodes / 2;
            var upperNodeLowerPrice = lowerNodeUpperPrice + 1;

            var lowerNodeIndex = GetNodeIndex(treeWidth, 0, lowerNodeUpperPrice - 1);
            var upperNodeIndex = GetNodeIndex(treeWidth, 0, upperNodeUpperPrice - 1);

            // To correctly adjust values on last backpropagation save-to-storage loop
            BigInteger baseAmountConsumedAtLastNode = 0;
            BigInteger quoteAmountConsumedAtLastNode = 0;
            BigInteger highestEmptiedCount = HighestSellRoundStorage.Get(pair.Id);
            BigInteger nextHighestEmptiedCount = highestEmptiedCount;
            BigInteger parentNodeEmptiedCount = 0;
            BigInteger endingPrice = 0;
            var ancestorHasEmptied = false;

            for (int i = 0; i < treeWidth; i++)
            {
                var isLastColumn = i == treeWidth - 1;

                var upperNode = SellTreeStorage.Get(pair.Id, upperNodeIndex);
                var lowerNode = SellTreeStorage.Get(pair.Id, lowerNodeIndex);

                // To correctly adjust values on last backpropagation save-to-storage loop
                BigInteger baseAmountReceivedAtColumn = 0;
                BigInteger quoteAmountReceivedAtColumn = 0;
                BigInteger baseAmountExtraAtColumn = 0;
                BigInteger quoteAmountExtraAtColumn = 0;

                // Lower node

                BigInteger quoteAmountAtLowerNodeInAMM = 0;

                if (useAMM)
                {
                    var ammPrice = ConvertPriceRowToAmmPrice(lowerNodeUpperPrice, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);
                    quoteAmountAtLowerNodeInAMM = MathUtils.Max(CalculateQuoteTokenInUntilPrice(baseTokenReserve, quoteTokenReserve, ammPrice), 0);
                    quoteAmountAtLowerNodeInAMM = MathUtils.Min(quoteAmountLeft, quoteAmountAtLowerNodeInAMM);

                    // We check that we will actually swap something. If not we should set the amount to 0.
                    var amountOut = quoteAmountAtLowerNodeInAMM > 0 ? GetAmountOut(quoteAmountAtLowerNodeInAMM, quoteTokenReserve, baseTokenReserve) : 0;
                    quoteAmountAtLowerNodeInAMM = amountOut > 0 ? quoteAmountAtLowerNodeInAMM : 0;

                    quoteAmountFromAMM = quoteAmountAtLowerNodeInAMM;
                }

                var ancestorHasFlipped = false;
                var lowerNodeIsAlreadyConsumed = upperNode.EmptiedCountSibling > lowerNode.EmptiedCount;
                var lowerNodeIsAlreadyEmpty = ancestorHasEmptied || parentNodeEmptiedCount > lowerNode.EmptiedCount;
                if (lowerNodeIsAlreadyConsumed)
                {
                    ancestorHasFlipped = ancestorHasEmptied == false;
                    ancestorHasEmptied = true;
                }

                var quoteAmountAtLowerNode = lowerNodeIsAlreadyConsumed || lowerNodeIsAlreadyEmpty
                    ? 0
                    : lowerNode.QuoteTotal;

                var limitPriceIsWithinUpperNodePriceRange = limitPrice >= upperNodeLowerPrice;
                var shouldMoveToUpperNode = quoteAmountAtLowerNode + quoteAmountAtLowerNodeInAMM < quoteAmountLeft && limitPriceIsWithinUpperNodePriceRange;

                BigInteger quoteAmountConsumedAtLowerNode = 0;

                if (shouldMoveToUpperNode)
                {
                    // When we move to the upper node we need to reset the ancestorHasEmptied flag
                    // because we are on a different branch and cannot longer assume all children are empty.
                    if (ancestorHasFlipped)
                    {
                        ancestorHasEmptied = false;
                    }

                    // If we move to the upper node we have to consume the amount left. Even if we are at the last column.
                    quoteAmountConsumedAtLowerNode = MathUtils.Min(quoteAmountAtLowerNode, quoteAmountLeft);
                    quoteAmountConsumedAtLowerNode = MathUtils.Max(quoteAmountConsumedAtLowerNode, 0);
                }
                else if (isLastColumn)
                {
                    // If we are at the last column and not moving to the upper node we have to consume the amount left.
                    // This time we need to find out how much we can consume from the lower node after the AMM trade.
                    var amountLeftAfterAmmTrade = quoteAmountLeft - quoteAmountAtLowerNodeInAMM;
                    quoteAmountConsumedAtLowerNode = MathUtils.Min(quoteAmountAtLowerNode, amountLeftAfterAmmTrade);
                    quoteAmountConsumedAtLowerNode = MathUtils.Max(quoteAmountConsumedAtLowerNode, 0);
                }

                var shouldCalculateLowerBaseAmount = lowerNode.QuoteTotal > 0 && quoteAmountConsumedAtLowerNode > 0;
                var baseAmountConsumedAtLowerNode = shouldCalculateLowerBaseAmount
                    ? (lowerNode.BaseAmount * (quoteAmountConsumedAtLowerNode * RoundingPrecision / lowerNode.QuoteTotal)) / RoundingPrecision
                    : 0;

                if (shouldCalculateLowerBaseAmount)
                {
                    ExecutionEngine.Assert(baseAmountConsumedAtLowerNode > 0, "Invalid base amount consumed at lower node");
                }

                // Update totals
                quoteAmountLeft -= quoteAmountConsumedAtLowerNode;
                ExecutionEngine.Assert(quoteAmountLeft >= 0, "Invalid quote amount left [lower node]");
                baseAmountReceived += baseAmountConsumedAtLowerNode;
                quoteAmountReceivedAtColumn += quoteAmountConsumedAtLowerNode;
                baseAmountReceivedAtColumn += baseAmountConsumedAtLowerNode;

                if (isLastColumn)
                {
                    // Upper node
                    if (shouldMoveToUpperNode)
                    {
                        BigInteger quoteAmountAtUpperNodeInAMM = 0;

                        if (useAMM)
                        {
                            var ammPrice = ConvertPriceRowToAmmPrice(upperNodeUpperPrice, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);
                            quoteAmountAtUpperNodeInAMM = MathUtils.Max(CalculateQuoteTokenInUntilPrice(baseTokenReserve, quoteTokenReserve, ammPrice), 0);
                            quoteAmountAtUpperNodeInAMM = MathUtils.Min(quoteAmountLeft, quoteAmountAtUpperNodeInAMM);

                            // We check that we will actually swap something. If not we should set the amount to 0.
                            var amountOut = quoteAmountAtUpperNodeInAMM > 0 ? GetAmountOut(quoteAmountAtUpperNodeInAMM, quoteTokenReserve, baseTokenReserve) : 0;
                            quoteAmountAtUpperNodeInAMM = amountOut > 0 ? quoteAmountAtUpperNodeInAMM : 0;

                            quoteAmountFromAMM = quoteAmountAtUpperNodeInAMM;
                        }

                        // If we are at the last column, and we are moving to the upper node we have to consume the amount left.
                        // This time we need to find out how much we can consume from the upper node after the AMM trade.
                        var amountLeftAfterAmmTrade = quoteAmountLeft - quoteAmountAtUpperNodeInAMM;
                        var quoteAmountAtUpperNode = parentNodeEmptiedCount > upperNode.EmptiedCount
                            ? 0
                            : upperNode.QuoteTotal;
                        var quoteAmountConsumedAtUpperNode = MathUtils.Min(quoteAmountAtUpperNode, amountLeftAfterAmmTrade);

                        var shouldCalculateUpperBaseAmount = upperNode.QuoteTotal > 0 && quoteAmountConsumedAtUpperNode > 0;
                        var baseAmountConsumedAtUpperNode = shouldCalculateUpperBaseAmount
                            ? (upperNode.BaseAmount * (quoteAmountConsumedAtUpperNode * RoundingPrecision / upperNode.QuoteTotal)) / RoundingPrecision
                            : 0;

                        if (shouldCalculateUpperBaseAmount)
                        {
                            ExecutionEngine.Assert(baseAmountConsumedAtUpperNode > 0, "Invalid base amount consumed at upper node");
                        }

                        // Update totals
                        quoteAmountLeft -= quoteAmountConsumedAtUpperNode;
                        ExecutionEngine.Assert(quoteAmountLeft >= 0, "Invalid quote amount left [upper node]");
                        baseAmountReceived += baseAmountConsumedAtUpperNode;
                        quoteAmountReceivedAtColumn += quoteAmountConsumedAtUpperNode;
                        baseAmountReceivedAtColumn += baseAmountConsumedAtUpperNode;

                        // To know the amount consumed here for the last backpropagating loop
                        quoteAmountConsumedAtLastNode = quoteAmountConsumedAtUpperNode;
                        baseAmountConsumedAtLastNode = baseAmountConsumedAtUpperNode;
                        endingPrice = upperNodeUpperPrice;

                        SellBaseAmountExecutedStorage.Increase(pair.Id, upperNodeIndex, baseAmountConsumedAtUpperNode);
                        SellQuoteAmountExecutedStorage.Increase(pair.Id, upperNodeIndex, quoteAmountConsumedAtUpperNode);
                    }
                    else
                    {
                        quoteAmountConsumedAtLastNode = quoteAmountConsumedAtLowerNode;
                        baseAmountConsumedAtLastNode = baseAmountConsumedAtLowerNode;
                        endingPrice = lowerNodeUpperPrice;

                        SellBaseAmountExecutedStorage.Increase(pair.Id, lowerNodeIndex, baseAmountConsumedAtLowerNode);
                        SellQuoteAmountExecutedStorage.Increase(pair.Id, lowerNodeIndex, quoteAmountConsumedAtLowerNode);
                    }

                    // We need to update the amount executed at the node index, so we can keep track of partially filled orders
                    ExecutionEngine.Assert(baseAmountReceivedAtColumn >= 0, "Invalid base amount received");
                    ExecutionEngine.Assert(quoteAmountReceivedAtColumn >= 0, "Invalid quote amount received");
                }

                // The extra amounts are the amounts consumed at the lower node before moving to the upper node
                baseAmountExtraAtColumn += shouldMoveToUpperNode ? baseAmountConsumedAtLowerNode : 0;
                quoteAmountExtraAtColumn += shouldMoveToUpperNode ? quoteAmountConsumedAtLowerNode : 0;

                // Nodes update plus orderbook movement handling
                if (shouldMoveToUpperNode)
                {
                    // Increment the counter in the upper node to indicate that the lower node is consumed to avoid more than one update per column
                    if (lowerNode.QuoteTotal > 0 && upperNode.EmptiedCountSibling <= lowerNode.EmptiedCount)
                    {
                        upperNode.EmptiedCountSibling = highestEmptiedCount + 1;
                        nextHighestEmptiedCount = highestEmptiedCount + 1;
                    }

                    nodesToBeUpdated[i] = (upperNodeIndex, upperNode, baseAmountExtraAtColumn, quoteAmountExtraAtColumn);

                    // Move to the upper right
                    lowerNodeLowerPrice = upperNodeLowerPrice;
                }
                else
                {
                    nodesToBeUpdated[i] = (lowerNodeIndex, lowerNode, baseAmountExtraAtColumn, quoteAmountExtraAtColumn);

                    // Move to the lower right
                    upperNodeUpperPrice = lowerNodeUpperPrice;
                }

                // Next iteration updates or last ones
                if (!isLastColumn)
                {
                    lowerNodeUpperPrice = (lowerNodeLowerPrice + upperNodeUpperPrice - 1) / 2;
                    upperNodeLowerPrice = lowerNodeUpperPrice + 1;

                    // Calculate node indexes for next column so i + 1
                    lowerNodeIndex = GetNodeIndex(treeWidth, i + 1, lowerNodeUpperPrice - 1);
                    upperNodeIndex = GetNodeIndex(treeWidth, i + 1, upperNodeUpperPrice - 1);
                }

                // If we are moving to the upper, the upper node emptied count is used.
                // If not, the highest of the lower node and the sibling of the upper node is used.
                var newParentNodeEmptiedCount = shouldMoveToUpperNode
                    ? upperNode.EmptiedCount
                    : MathUtils.Max(upperNode.EmptiedCountSibling, lowerNode.EmptiedCount);
                // var newParentNodeEmptiedCount = shouldMoveToUpperNode ? upperNode.EmptiedCount : lowerNode.EmptiedCount;
                parentNodeEmptiedCount = MathUtils.Max(parentNodeEmptiedCount, newParentNodeEmptiedCount);
            }

            // To keep a reference of the total amount consumed in the iteration before
            BigInteger baseAmountConsumedInChildren = 0;
            BigInteger quoteAmountConsumedInChildren = 0;

            for (int i = nodesToBeUpdated.Length - 1; i >= 0; i--)
            {
                var (nodeIndex, priceNode, baseAmountExtraAtColumn, quoteAmountExtraAtColumn) = nodesToBeUpdated[i];

                var baseAmountConsumedAtColumn = baseAmountExtraAtColumn;
                var quoteAmountConsumedAtColumn = quoteAmountExtraAtColumn;

                // If last node we have to consider the amounts consumed there for the update
                if (i == nodesToBeUpdated.Length - 1)
                {
                    // Emptied only if not already empty
                    if (priceNode.BaseAmount > 0 && baseAmountConsumedAtLastNode >= priceNode.BaseAmount)
                    {
                        priceNode.BaseAmount = 0;
                        priceNode.QuoteTotal = 0;
                        priceNode.EmptiedCount = highestEmptiedCount + 1;
                        nextHighestEmptiedCount = highestEmptiedCount + 1;
                    }
                    else
                    {
                        priceNode.BaseAmount = MathUtils.Max(priceNode.BaseAmount - baseAmountConsumedAtLastNode, 0);
                        priceNode.QuoteTotal = MathUtils.Max(priceNode.QuoteTotal - quoteAmountConsumedAtLastNode, 0);
                    }

                    baseAmountConsumedAtColumn += baseAmountConsumedAtLastNode;
                    quoteAmountConsumedAtColumn += quoteAmountConsumedAtLastNode;
                }
                else
                {
                    // Emptied only if not already empty
                    if (priceNode.BaseAmount > 0 && baseAmountConsumedInChildren >= priceNode.BaseAmount)
                    {
                        priceNode.BaseAmount = 0;
                        priceNode.QuoteTotal = 0;
                        priceNode.EmptiedCount = highestEmptiedCount + 1;
                        nextHighestEmptiedCount = highestEmptiedCount + 1;
                    }
                    else
                    {
                        priceNode.BaseAmount = MathUtils.Max(priceNode.BaseAmount - baseAmountConsumedInChildren, 0);
                        priceNode.QuoteTotal = MathUtils.Max(priceNode.QuoteTotal - quoteAmountConsumedInChildren, 0);
                    }

                    baseAmountConsumedAtColumn += baseAmountConsumedInChildren;
                    quoteAmountConsumedAtColumn += quoteAmountConsumedInChildren;
                }

                SellTreeStorage.Put(pair.Id, nodeIndex, priceNode);

                baseAmountConsumedInChildren = baseAmountConsumedAtColumn;
                quoteAmountConsumedInChildren = quoteAmountConsumedAtColumn;
            }

            HighestSellRoundStorage.Put(pair.Id, nextHighestEmptiedCount);

            return (quoteAmountLeft, quoteAmountFromAMM, baseAmountReceived, endingPrice);
        }

        /// <summary>
        /// Create a limit buy order.
        /// </summary>
        /// <param name="user">The user related to the operation.</param>
        /// <param name="pair">The pair of tokens</param>
        /// <param name="baseAmount">The amount of base currency.</param>
        /// <returns>The amount of quote currency placed.</returns>
        private static BigInteger PlaceBuyOrder(
            UInt160 user,
            Pair pair,
            BigInteger quoteAmount,
            BigInteger baseAmountToPlace,
            BigInteger limitPrice,
            BigInteger originalAmount,
            BigInteger originalQuoteAmount,
            BigInteger baseAmountTradedInAmm,
            BigInteger quoteAmountTradedInAmm,
            BigInteger baseAmountTradedBeforeOrderPlaced,
            BigInteger quoteAmountTradedBeforeOrderPlaced,
            BigInteger endingPrice,
            BigInteger feeAmount,
            BigInteger userDefinedId
        )
        {
            var treeWidth = pair.TreeWidth;
            BigInteger rightMostNumberOfNodes = 1 << treeWidth;

            var lowerNodeLowerPrice = (BigInteger) 1;
            var upperNodeUpperPrice = rightMostNumberOfNodes;
            var lowerNodeUpperPrice = rightMostNumberOfNodes / 2;
            var upperNodeLowerPrice = lowerNodeUpperPrice + 1;

            var lowerNodeIndex = GetNodeIndex(treeWidth, 0, lowerNodeUpperPrice - 1);
            var upperNodeIndex = GetNodeIndex(treeWidth, 0, upperNodeUpperPrice - 1);

            if (baseAmountToPlace == 0)
            {
                quoteAmount = 0;
            }

            // Used to check if parent node has been emptied to consider all children must be considered empty
            BigInteger parentNodeEmptiedCount = 0;
            BigInteger highestEmptiedCount = HighestBuyRoundStorage.Get(pair.Id);
            BigInteger nextHighestEmptiedCount = highestEmptiedCount;
            var ancestorHasEmptied = false;

            for (int i = 0; i < treeWidth; i++)
            {
                var isLastColumn = i == treeWidth - 1;

                var upperNode = BuyTreeStorage.Get(pair.Id, upperNodeIndex);
                var lowerNode = BuyTreeStorage.Get(pair.Id, lowerNodeIndex);

                var limitPriceIsWithinLowerNodeRange = limitPrice >= lowerNodeLowerPrice && limitPrice <= lowerNodeUpperPrice;

                var shouldBeEmptied = limitPriceIsWithinLowerNodeRange
                    ? ancestorHasEmptied || parentNodeEmptiedCount > lowerNode.EmptiedCount
                    : ancestorHasEmptied || parentNodeEmptiedCount > upperNode.EmptiedCount || lowerNode.EmptiedCountSibling > upperNode.EmptiedCount;
                if (shouldBeEmptied) ancestorHasEmptied = true;

                if (limitPriceIsWithinLowerNodeRange)
                {
                    // If it was consumed we have to "reset" the node and increase the emptied count for next order
                    if (shouldBeEmptied)
                    {
                        lowerNode.BaseAmount = 0;
                        lowerNode.QuoteTotal = 0;

                        // Sync the emptied count with the parent node
                        lowerNode.EmptiedCount = highestEmptiedCount + 1;
                        nextHighestEmptiedCount = highestEmptiedCount + 1;
                    }

                    lowerNode.BaseAmount += baseAmountToPlace;
                    lowerNode.QuoteTotal += quoteAmount;
                    BuyTreeStorage.Put(pair.Id, lowerNodeIndex, lowerNode);

                    parentNodeEmptiedCount = lowerNode.EmptiedCount;

                    // Move to the lower right
                    upperNodeUpperPrice = lowerNodeUpperPrice;
                }
                else
                {
                    // If it was consumed we have to "reset" the node and increase the emptied count for next order
                    if (shouldBeEmptied)
                    {
                        upperNode.BaseAmount = 0;
                        upperNode.QuoteTotal = 0;

                        // Sync the emptied count with the parent node or sibling node
                        upperNode.EmptiedCount = highestEmptiedCount + 1;
                        nextHighestEmptiedCount = highestEmptiedCount + 1;
                    }

                    upperNode.BaseAmount += baseAmountToPlace;
                    upperNode.QuoteTotal += quoteAmount;
                    BuyTreeStorage.Put(pair.Id, upperNodeIndex, upperNode);

                    parentNodeEmptiedCount = upperNode.EmptiedCount;

                    // Move to the upper right
                    lowerNodeLowerPrice = upperNodeLowerPrice;
                }

                if (!isLastColumn)
                {
                    lowerNodeUpperPrice = (lowerNodeLowerPrice + upperNodeUpperPrice - 1) / 2;
                    upperNodeLowerPrice = lowerNodeUpperPrice + 1;

                    // Calculate node indexes for next column so i + 1
                    lowerNodeIndex = GetNodeIndex(treeWidth, i + 1, lowerNodeUpperPrice - 1);
                    upperNodeIndex = GetNodeIndex(treeWidth, i + 1, upperNodeUpperPrice - 1);
                }
                else
                {
                    // We are on the last column
                    var endingNodeIndex = limitPriceIsWithinLowerNodeRange ? lowerNodeIndex : upperNodeIndex;
                    var buyBaseAmountPlaced = BuyBaseAmountPlacedStorage.Get(pair.Id, endingNodeIndex);
                    var buyQuoteAmountPlaced = BuyQuoteAmountPlacedStorage.Get(pair.Id, endingNodeIndex);
                    var endingNode = limitPriceIsWithinLowerNodeRange ? lowerNode : upperNode;

                    var treeKey = GetCanceledTreeKey(pair.Id, limitPrice - 1, true);
                    var cancelId = CancelledOrdersCountAtPriceStorage.Increment(treeKey);

                    if (shouldBeEmptied)
                    {
                        var (canceledBaseAmountAtNode, canceledQuoteAmountAtNode) = FindAmountCanceledFromOrdersWithLowerId(pair.Id, true, cancelId, limitPrice - 1);
                        var newExecutedBaseAmount = buyBaseAmountPlaced - canceledBaseAmountAtNode;
                        var newExecutedQuoteAmount = buyQuoteAmountPlaced - canceledQuoteAmountAtNode;

                        // Set the executed amounts to the amounts placed, so that we reset the executed amounts to track partially filled orders.
                        BuyBaseAmountExecutedStorage.Put(pair.Id, endingNodeIndex, newExecutedBaseAmount);
                        BuyQuoteAmountExecutedStorage.Put(pair.Id, endingNodeIndex, newExecutedQuoteAmount);
                    }

                    // Place the order in storage
                    var order = new Order
                    {
                        Owner = user,
                        TotalOrderBaseAmount = originalAmount,
                        TotalOrderQuoteAmount = originalQuoteAmount,
                        PlacedInOrderBookBaseAmount = baseAmountToPlace,
                        PlacedInOrderBookQuoteAmount = quoteAmount,
                        Price = limitPrice,
                        BaseAmountTradedInAmm = baseAmountTradedInAmm,
                        QuoteAmountTradedInAmm = quoteAmountTradedInAmm,
                        BaseAmountTradedBeforeOrderPlaced = baseAmountTradedBeforeOrderPlaced,
                        QuoteAmountTradedBeforeOrderPlaced = quoteAmountTradedBeforeOrderPlaced,
                        IsBuy = true,
                        CancelledBaseAmount = 0,
                        CancelledQuoteAmount = 0,
                        ClaimedBaseAmount = 0,
                        ClaimedQuoteAmount = 0,
                        EmptiedCountWhenInserted = endingNode.EmptiedCount,
                        GlobalBaseAmountPlacedAtPriceWhenInserted = buyBaseAmountPlaced,
                        GlobalQuoteAmountPlacedAtPriceWhenInserted = buyQuoteAmountPlaced,
                        CancelId = cancelId,
                        FeeAmount = feeAmount,
                        CreatedAt = Runtime.Time,
                        UserDefinedId = userDefinedId,
                    };

                    var orderId = OrderIdStorage.Increase(pair.Id);
                    var userOrderCount = UserOrderCountStorage.Increment(user);
                    OrderDataStorage.Put(pair.Id, orderId, order);
                    var page = (userOrderCount - 1) / OrdersPerPage;
                    UserOrderStorage.Put(user, pair.Id, orderId, page);

                    OrderUpserted(
                        orderId,
                        pair.Id,
                        user,
                        originalAmount,
                        originalQuoteAmount,
                        baseAmountToPlace,
                        quoteAmount,
                        limitPrice,
                        baseAmountTradedInAmm,
                        quoteAmountTradedInAmm,
                        baseAmountTradedBeforeOrderPlaced,
                        quoteAmountTradedBeforeOrderPlaced,
                        true,
                        0,
                        0,
                        0,
                        0,
                        endingNode.EmptiedCount,
                        buyBaseAmountPlaced,
                        buyQuoteAmountPlaced,
                        cancelId,
                        endingPrice,
                        feeAmount
                    );

                    // We need to update the amount placed at the node index.
                    // This is so we can keep track of partially filled orders.
                    BuyBaseAmountPlacedStorage.Increase(pair.Id, endingNodeIndex, baseAmountToPlace);
                    BuyQuoteAmountPlacedStorage.Increase(pair.Id, endingNodeIndex, quoteAmount);
                }
            }

            HighestBuyRoundStorage.Put(pair.Id, nextHighestEmptiedCount);

            return quoteAmount;
        }
    }
}
