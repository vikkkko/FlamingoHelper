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
        /// Create a limit sell order, processing the amount and placing what's left in the orderbook.
        /// </summary>
        /// <param name="user">The user related to the operation.</param>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="baseAmount">The amount of base currency.</param>
        /// <param name="limitPrice">The limit price for the order.</param>
        /// <param name="useAMM">Wether to use the AMM when matching the order through the orderbook.</param>
        /// <param name="debug">Wether to output debug information.</param>
        [NoReentrant]
        public static void CreateLimitSellOrderUsingBase(UInt160 user, BigInteger pairId, BigInteger baseAmount, BigInteger limitPrice, BigInteger userDefinedId, bool useAMM, bool debug)
        {
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(user), "Invalid address");
            ExecutionEngine.Assert(Runtime.CheckWitness(user) || user.Equals(Runtime.CallingScriptHash), "No user witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");
            ExecutionEngine.Assert(!IsPairOrderTradingPaused(pairId), "Pair order trading paused");
            ExecutionEngine.Assert(baseAmount > 0, "Amount must be greater than 0");

            // Create an object with pair data to be reusable and efficient from storage read perspective
            var baseToken = GetBaseToken(pairId);
            var quoteToken = GetQuoteToken(pairId);
            Pair pair = new Pair()
            {
                Id = pairId,
                BaseToken = baseToken,
                QuoteToken = quoteToken,
                TreeWidth = (int)GetTreeBitLength(pairId),
                PricePrecision = GetPricePrecision(pairId),
                QuoteTokenDecimals = GetDecimals(quoteToken),
                BaseTokenDecimals = GetDecimals(baseToken),
            };
            ExecutionEngine.Assert(limitPrice > 0 && limitPrice < (1 << pair.TreeWidth) + 1, "Invalid limit price");

            var realPrice = ConvertPriceRowToRealPrice(limitPrice, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);
            var quoteAmount = baseAmount * realPrice / PriceRoundingPrecision;
            ExecutionEngine.Assert(quoteAmount > 0, "Quote amount must be greater than zero");

            // Process the order with the limit price provided by the user
            (BigInteger totalBaseAmountSpent, BigInteger totalQuoteAmountReceived, BigInteger baseAmountSwapped, BigInteger quoteAmountSwapped, BigInteger endingPrice, BigInteger feeAmount) = ProcessSellOrderUsingBase(pair, baseAmount, limitPrice, useAMM, debug);
            // Place the remaining amount in the orderbook if there's something left
            BigInteger baseAmountLeft = baseAmount - totalBaseAmountSpent;

            var baseAmountTradedBeforeOrderPlaced = totalBaseAmountSpent - baseAmountSwapped;
            var quoteAmountTradedBeforeOrderPlaced = totalQuoteAmountReceived - quoteAmountSwapped;

            var quoteAmountPlace = baseAmountLeft * realPrice / PriceRoundingPrecision;

            var baseAmountPlaced = PlaceSellOrder(
                user,
                pair,
                baseAmountLeft,
                quoteAmountPlace,
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
            totalBaseAmountSpent += baseAmountPlaced;

            // Base balance check and update
            ExecutionEngine.Assert(GetAccountBalance(user, pair.BaseToken) >= totalBaseAmountSpent, "Not enough funds");
            DecreaseUserBalance(user, pair.BaseToken, totalBaseAmountSpent);
            // Quote balance update
            IncreaseUserBalance(user, pair.QuoteToken, totalQuoteAmountReceived);

            DebugMethodCalled(user, "CreateLimitSellOrderUsingBase", new List<object> { user, pairId, baseAmount, limitPrice, userDefinedId });
        }

        /// <summary>
        /// Execute a limit sell order, processing the amount and spending only what has been processed in the orderbook.
        /// </summary>
        /// <param name="user">The user related to the operation.</param>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="baseAmount">The amount of base currency.</param>
        /// <param name="limitPrice">The limit price for the order.</param>
        /// <param name="useAMM">Wether to use the AMM when matching the order through the orderbook.</param>
        /// <param name="debug">Wether to output debug information.</param>
        [NoReentrant]
        public static void ExecuteLimitSellOrderUsingBase(UInt160 user, BigInteger pairId, BigInteger baseAmount, BigInteger limitPrice, BigInteger userDefinedId, bool useAMM, bool debug)
        {
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(user), "Invalid address");
            ExecutionEngine.Assert(Runtime.CheckWitness(user) || user.Equals(Runtime.CallingScriptHash), "No user witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");
            ExecutionEngine.Assert(!IsPairOrderTradingPaused(pairId), "Pair order trading paused");
            ExecutionEngine.Assert(baseAmount > 0, "Amount must be greater than 0");

            // Create an object with pair data to be reusable and efficient from storage read perspective
            var baseToken = GetBaseToken(pairId);
            var quoteToken = GetQuoteToken(pairId);
            Pair pair = new Pair()
            {
                Id = pairId,
                BaseToken = baseToken,
                QuoteToken = quoteToken,
                TreeWidth = (int)GetTreeBitLength(pairId),
                PricePrecision = GetPricePrecision(pairId),
                QuoteTokenDecimals = GetDecimals(quoteToken),
                BaseTokenDecimals = GetDecimals(baseToken),
            };
            ExecutionEngine.Assert(limitPrice > 0 && limitPrice < (1 << pair.TreeWidth) + 1, "Invalid limit price");

            var realPrice = ConvertPriceRowToRealPrice(limitPrice, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);
            var quoteAmount = baseAmount * realPrice / PriceRoundingPrecision;
            ExecutionEngine.Assert(quoteAmount > 0, "Quote amount must be greater than zero");

            // Process the order with the limit price provided by the user
            (BigInteger totalBaseAmountSpent, BigInteger totalQuoteAmountReceived, BigInteger baseAmountSwapped, BigInteger quoteAmountSwapped, BigInteger endingPrice, BigInteger feeAmount) = ProcessSellOrderUsingBase(pair, baseAmount, limitPrice, useAMM, debug);

            var baseAmountTradedBeforeOrderPlaced = totalBaseAmountSpent - baseAmountSwapped;
            var quoteAmountTradedBeforeOrderPlaced = totalQuoteAmountReceived - quoteAmountSwapped;

            // Place an empty order for the user to keep track of the order in the orderbook
            PlaceSellOrder(
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

            // Base balance check and update
            ExecutionEngine.Assert(GetAccountBalance(user, pair.BaseToken) >= totalBaseAmountSpent, "Not enough funds");
            DecreaseUserBalance(user, pair.BaseToken, totalBaseAmountSpent);
            // Quote balance update
            IncreaseUserBalance(user, pair.QuoteToken, totalQuoteAmountReceived);

            DebugMethodCalled(user, "ExecuteLimitSellOrderUsingBase", new List<object> { user, pairId, baseAmount, limitPrice, userDefinedId });
        }

        /// <summary>
        /// Add a limit sell order into the orderbook.
        /// </summary>
        /// <param name="user">The user related to the operation.</param>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="baseAmount">The amount of base currency.</param>
        /// <param name="limitPrice">The limit price for the order.</param>
        [NoReentrant]
        public static void AddLimitSellOrderUsingBase(UInt160 user, BigInteger pairId, BigInteger baseAmount, BigInteger limitPrice, BigInteger userDefinedId)
        {
            ExecutionEngine.Assert(ContractUtils.IsValidAddress(user), "Invalid address");
            ExecutionEngine.Assert(Runtime.CheckWitness(user) || user.Equals(Runtime.CallingScriptHash), "No user witness");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");
            ExecutionEngine.Assert(!IsPairOrderTradingPaused(pairId), "Pair order trading paused");
            ExecutionEngine.Assert(baseAmount > 0, "Amount must be greater than 0");
            // Create an object with pair data to be reusable and efficient from storage read perspective
            var baseToken = GetBaseToken(pairId);
            var quoteToken = GetQuoteToken(pairId);
            Pair pair = new Pair()
            {
                Id = pairId,
                BaseToken = baseToken,
                QuoteToken = quoteToken,
                TreeWidth = (int)GetTreeBitLength(pairId),
                PricePrecision = GetPricePrecision(pairId),
                QuoteTokenDecimals = GetDecimals(quoteToken),
                BaseTokenDecimals = GetDecimals(baseToken),
            };
            ExecutionEngine.Assert(limitPrice > 0 && limitPrice < (1 << pair.TreeWidth) + 1, "Invalid limit price");

            var realPrice = ConvertPriceRowToRealPrice(limitPrice, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);
            var quoteAmount = baseAmount * realPrice / PriceRoundingPrecision;
            ExecutionEngine.Assert(quoteAmount > 0, "Quote amount must be greater than zero");

            var (baseTokenReserve, quoteTokenReserve) = AMMHelper.GetReserves(pair.BaseToken, pair.QuoteToken);
            var ammAsPriceLimitPrice = ConvertAmmPriceToLimitPrice(baseTokenReserve, quoteTokenReserve, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals, true);

            ExecutionEngine.Assert(limitPrice > ammAsPriceLimitPrice, "Limit price is lower than AMM price");

            // Place the amount in the orderbook
            PlaceSellOrder(
                user,
                pair,
                baseAmount,
                quoteAmount,
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

            // Base balance check and update
            ExecutionEngine.Assert(GetAccountBalance(user, pair.BaseToken) >= baseAmount, "Not enough funds");
            DecreaseUserBalance(user, pair.BaseToken, baseAmount);

            DebugMethodCalled(user, "AddLimitSellOrderUsingBase", new List<object> { user, pairId, baseAmount, limitPrice, userDefinedId });
        }

        private static (BigInteger totalBaseAmountSpent, BigInteger totalQuoteAmountReceived, BigInteger baseAmountSwapped, BigInteger quoteAmountSwapped, BigInteger endingPrice, BigInteger feeAmount) ProcessSellOrderUsingBase(
            Pair pair,
            BigInteger baseAmount,
            BigInteger limitPrice,
            bool useAMM,
            bool debug = false
        )
        {
            // Get token reserve for amm usage
            var (baseTokenReserve, quoteTokenReserve) = useAMM ? AMMHelper.GetReserves(pair.BaseToken, pair.QuoteToken) : (0, 0);
            // Orderbook + AMM matching
            var (baseAmountLeft, baseAmountFromAMM, quoteAmountReceived, endingPrice) = MatchSellOrderUsingBase(baseAmount, pair, limitPrice, useAMM, baseTokenReserve, quoteTokenReserve);

            // Fee calculation
            BigInteger quoteFee = GetTakerFee(pair.Id) * quoteAmountReceived / FeeRoundingPrecision;
            IncreaseFeeBalance(pair.QuoteToken, quoteFee);
            // Reduce amount received based on the fee to be paid for the ordersbook matching
            quoteAmountReceived -= quoteFee;

            // AMM swap
            BigInteger totalBaseAmountSpent = baseAmount - baseAmountLeft;
            BigInteger totalQuoteAmountReceived = quoteAmountReceived;
            BigInteger baseAmountSwapped = 0;
            BigInteger quoteAmountSwapped = 0;

            long gasUsed = 0;

            if (baseAmountFromAMM > 0)
            {
                if (debug) gasUsed = Runtime.GasLeft;

                BigInteger balanceOfBaseTokenBefore = NEP17Helper.SafeBalanceOf(pair.BaseToken, Runtime.ExecutingScriptHash);
                BigInteger balanceOfQuoteTokenBefore = NEP17Helper.SafeBalanceOf(pair.QuoteToken, Runtime.ExecutingScriptHash);

                ExecutionEngine.Assert(balanceOfBaseTokenBefore >= baseAmountFromAMM, "Not enough base token in the contract to swap");

                // Execute the swap through the AMM, NB: TokenInForTokenOut
                AMMHelper.SafeSwapTokenInForTokenOut(baseAmountFromAMM, 0, new UInt160[] { pair.BaseToken, pair.QuoteToken });

                BigInteger balanceOfBaseTokenAfter = NEP17Helper.SafeBalanceOf(pair.BaseToken, Runtime.ExecutingScriptHash);
                BigInteger balanceOfQuoteTokenAfter = NEP17Helper.SafeBalanceOf(pair.QuoteToken, Runtime.ExecutingScriptHash);

                var (newBaseTokenReserve, newQuoteTokenReserve) = AMMHelper.GetReserves(pair.BaseToken, pair.QuoteToken);
                TakePriceSnapshot(pair.Id, newBaseTokenReserve, newQuoteTokenReserve, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);

                // NB: Here we are using quote tokens to buy the base tokens so before and after are inverted
                baseAmountSwapped = balanceOfBaseTokenBefore - balanceOfBaseTokenAfter;
                quoteAmountSwapped = balanceOfQuoteTokenAfter - balanceOfQuoteTokenBefore;

                totalBaseAmountSpent += baseAmountSwapped;
                totalQuoteAmountReceived += quoteAmountSwapped;

                ExecutionEngine.Assert(baseAmountSwapped == baseAmountFromAMM, "Invalid swap");

                if (debug)
                {
                    var gasUsedTotal = gasUsed - Runtime.GasLeft;
                    ExecutionEngine.Assert(false, gasUsedTotal.ToString());
                }
            }
            else
            {
                var gasToBurn = GasToBurnStorage.Get(pair.Id);
                if (gasToBurn > 0) Runtime.BurnGas((long)gasToBurn);
            }

            ExecutionEngine.Assert(totalBaseAmountSpent >= 0, "Invalid amount processed [E_PSOUB1]");
            ExecutionEngine.Assert(totalQuoteAmountReceived >= 0, "Invalid amount processed [E_PSOUB2]");

            return (totalBaseAmountSpent, totalQuoteAmountReceived, baseAmountSwapped, quoteAmountSwapped, endingPrice, quoteFee);
        }

        private static (BigInteger baseAmountLeft, BigInteger baseAmountFromAMM, BigInteger quoteAmountReceived, BigInteger endingPrice) MatchSellOrderUsingBase(
            BigInteger baseAmount,
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

            BigInteger baseAmountLeft = baseAmount;
            BigInteger quoteAmountReceived = 0;

            BigInteger baseAmountFromAMM = 0;

            var lowerNodeLowerPrice = (BigInteger) 1;
            var upperNodeUpperPrice = rightMostNumberOfNodes;
            var lowerNodeUpperPrice = rightMostNumberOfNodes / 2;
            var upperNodeLowerPrice = lowerNodeUpperPrice + 1;

            var lowerNodeIndex = GetNodeIndex(treeWidth, 0, lowerNodeUpperPrice - 1);
            var upperNodeIndex = GetNodeIndex(treeWidth, 0, upperNodeUpperPrice - 1);

            // To correctly adjust values on last backpropagation save-to-storage loop
            BigInteger baseAmountConsumedAtLastNode = 0;
            BigInteger quoteAmountConsumedAtLastNode = 0;
            BigInteger highestEmptiedCount = HighestBuyRoundStorage.Get(pair.Id);
            BigInteger nextHighestEmptiedCount = highestEmptiedCount;
            BigInteger parentNodeEmptiedCount = 0;
            BigInteger endingPrice = 0;
            var ancestorHasEmptied = false;

            for (int i = 0; i < treeWidth; i++)
            {
                var isLastColumn = i == treeWidth - 1;

                var upperNode = BuyTreeStorage.Get(pair.Id, upperNodeIndex);
                var lowerNode = BuyTreeStorage.Get(pair.Id, lowerNodeIndex);

                // To correctly adjust values on last backpropagation save-to-storage loop
                BigInteger baseAmountReceivedAtColumn = 0;
                BigInteger quoteAmountReceivedAtColumn = 0;
                BigInteger baseAmountExtraAtColumn = 0;
                BigInteger quoteAmountExtraAtColumn = 0;

                // Upper node

                BigInteger baseAmountAtUpperNodeInAMM = 0;

                if (useAMM)
                {
                    var ammPrice = ConvertPriceRowToAmmPrice(upperNodeLowerPrice, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);
                    baseAmountAtUpperNodeInAMM = MathUtils.Max(CalculateBaseTokenInUntilPrice(baseTokenReserve, quoteTokenReserve, ammPrice), 0);
                    baseAmountAtUpperNodeInAMM = MathUtils.Min(baseAmountLeft, baseAmountAtUpperNodeInAMM);

                    // We check that we will actually swap something. If not we should set the amount to 0.
                    var amountOut = baseAmountAtUpperNodeInAMM > 0 ? GetAmountOut(baseAmountAtUpperNodeInAMM, baseTokenReserve, quoteTokenReserve) : 0;
                    baseAmountAtUpperNodeInAMM = amountOut > 0 ? baseAmountAtUpperNodeInAMM : 0;

                    baseAmountFromAMM = baseAmountAtUpperNodeInAMM;
                }

                var ancestorHasFlipped = false;
                var upperNodeIsAlreadyConsumed = lowerNode.EmptiedCountSibling > upperNode.EmptiedCount;
                var upperNodeIsAlreadyEmpty = ancestorHasEmptied || parentNodeEmptiedCount > upperNode.EmptiedCount;
                if (upperNodeIsAlreadyConsumed)
                {
                    ancestorHasFlipped = ancestorHasEmptied == false;
                    ancestorHasEmptied = true;
                }

                var baseAmountAtUpperNode = upperNodeIsAlreadyConsumed || upperNodeIsAlreadyEmpty
                    ? 0
                    : upperNode.BaseAmount;

                var limitPriceIsWithinLowerNodePriceRange = limitPrice <= lowerNodeUpperPrice;
                var shouldMoveToLowerNode = baseAmountAtUpperNode + baseAmountAtUpperNodeInAMM < baseAmountLeft && limitPriceIsWithinLowerNodePriceRange;

                BigInteger baseAmountConsumedAtUpperNode = 0;

                if (shouldMoveToLowerNode)
                {
                    // When we move to the lower node we need to reset the ancestorHasEmptied flag
                    // because we are on a different branch and cannot longer assume all children are empty.
                    if (ancestorHasFlipped)
                    {
                        ancestorHasEmptied = false;
                    }

                    // If we move to the lower node we have to consume the amount left. Even if we are at the last column.
                    baseAmountConsumedAtUpperNode = MathUtils.Min(baseAmountAtUpperNode, baseAmountLeft);
                    baseAmountConsumedAtUpperNode = MathUtils.Max(baseAmountConsumedAtUpperNode, 0);
                }
                else if (isLastColumn)
                {
                    // If we are at the last column and not moving to the lower node we have to consume the amount left.
                    // This time we need to find out how much we can consume from the upper node after the AMM trade.
                    var amountLeftAfterAmmTrade = baseAmountLeft - baseAmountAtUpperNodeInAMM;
                    baseAmountConsumedAtUpperNode = MathUtils.Min(baseAmountAtUpperNode, amountLeftAfterAmmTrade);
                    baseAmountConsumedAtUpperNode = MathUtils.Max(baseAmountConsumedAtUpperNode, 0);
                }

                var shouldCalculateUpperQuoteAmount = upperNode.BaseAmount > 0 && baseAmountConsumedAtUpperNode > 0;
                var quoteAmountConsumedAtUpperNode = shouldCalculateUpperQuoteAmount
                    ? (upperNode.QuoteTotal * (baseAmountConsumedAtUpperNode * RoundingPrecision / upperNode.BaseAmount)) / RoundingPrecision
                    : 0;

                if (shouldCalculateUpperQuoteAmount)
                {
                    ExecutionEngine.Assert(quoteAmountConsumedAtUpperNode > 0, "Invalid quote amount consumed at upper node.");
                }

                // Update totals
                baseAmountLeft -= baseAmountConsumedAtUpperNode;
                ExecutionEngine.Assert(baseAmountLeft >= 0, "Invalid base amount left [upper node]");
                quoteAmountReceived += quoteAmountConsumedAtUpperNode;
                quoteAmountReceivedAtColumn += quoteAmountConsumedAtUpperNode;
                baseAmountReceivedAtColumn += baseAmountConsumedAtUpperNode;

                if (isLastColumn)
                {
                    // Lower node
                    if (shouldMoveToLowerNode)
                    {
                        BigInteger baseAmountAtLowerNodeInAMM = 0;

                        if (useAMM)
                        {
                            var ammPrice = ConvertPriceRowToAmmPrice(lowerNodeLowerPrice, pair.PricePrecision, pair.BaseTokenDecimals, pair.QuoteTokenDecimals);
                            baseAmountAtLowerNodeInAMM = MathUtils.Max(CalculateBaseTokenInUntilPrice(baseTokenReserve, quoteTokenReserve, ammPrice), 0);
                            baseAmountAtLowerNodeInAMM = MathUtils.Min(baseAmountLeft, baseAmountAtLowerNodeInAMM);

                            // We check that we will actually swap something. If not we should set the amount to 0.
                            var amountOut = baseAmountAtLowerNodeInAMM > 0 ? GetAmountOut(baseAmountAtLowerNodeInAMM, baseTokenReserve, quoteTokenReserve) : 0;
                            baseAmountAtLowerNodeInAMM = amountOut > 0 ? baseAmountAtLowerNodeInAMM : 0;

                            baseAmountFromAMM = baseAmountAtLowerNodeInAMM;
                        }

                        // If we are at the last column, and we are moving to the lower node we have to consume the amount left.
                        // This time we need to find out how much we can consume from the lower node after the AMM trade.
                        var amountLeftAfterAmmTrade = baseAmountLeft - baseAmountAtLowerNodeInAMM;
                        var baseAmountAtLowerNode = parentNodeEmptiedCount > lowerNode.EmptiedCount
                            ? 0
                            : lowerNode.BaseAmount;
                        var baseAmountConsumedAtLowerNode = MathUtils.Min(baseAmountAtLowerNode, amountLeftAfterAmmTrade);

                        var shouldCalculateLowerQuoteAmount = lowerNode.BaseAmount > 0 && baseAmountConsumedAtLowerNode > 0;
                        var quoteAmountConsumedAtLowerNode = shouldCalculateLowerQuoteAmount
                            ? (lowerNode.QuoteTotal * (baseAmountConsumedAtLowerNode * RoundingPrecision / lowerNode.BaseAmount)) / RoundingPrecision
                            : 0;

                        if (shouldCalculateLowerQuoteAmount)
                        {
                            ExecutionEngine.Assert(quoteAmountConsumedAtLowerNode > 0, "Invalid quote amount consumed at lower node.");
                        }

                        // Update totals
                        baseAmountLeft -= baseAmountConsumedAtLowerNode;
                        ExecutionEngine.Assert(baseAmountLeft >= 0, "Invalid base amount left [lower node]");
                        quoteAmountReceived += quoteAmountConsumedAtLowerNode;
                        quoteAmountReceivedAtColumn += quoteAmountConsumedAtLowerNode;
                        baseAmountReceivedAtColumn += baseAmountConsumedAtLowerNode;

                        // To know the amount consumed here for the last backpropagating loop
                        baseAmountConsumedAtLastNode = baseAmountConsumedAtLowerNode;
                        quoteAmountConsumedAtLastNode = quoteAmountConsumedAtLowerNode;
                        endingPrice = lowerNodeLowerPrice;

                        BuyBaseAmountExecutedStorage.Increase(pair.Id, lowerNodeIndex, baseAmountConsumedAtLowerNode);
                        BuyQuoteAmountExecutedStorage.Increase(pair.Id, lowerNodeIndex, quoteAmountConsumedAtLowerNode);
                    }
                    else
                    {
                        baseAmountConsumedAtLastNode = baseAmountConsumedAtUpperNode;
                        quoteAmountConsumedAtLastNode = quoteAmountConsumedAtUpperNode;
                        endingPrice = upperNodeUpperPrice;

                        BuyBaseAmountExecutedStorage.Increase(pair.Id, upperNodeIndex, baseAmountConsumedAtUpperNode);
                        BuyQuoteAmountExecutedStorage.Increase(pair.Id, upperNodeIndex, quoteAmountConsumedAtUpperNode);
                    }

                    // We need to update the amount executed at the node index, so we can keep track of partially filled orders
                    ExecutionEngine.Assert(baseAmountReceivedAtColumn >= 0, "Invalid base amount received");
                    ExecutionEngine.Assert(quoteAmountReceivedAtColumn >= 0, "Invalid quote amount received");
                }

                // The extra amounts are the amounts consumed at the upper node before moving to the lower node
                baseAmountExtraAtColumn += shouldMoveToLowerNode ? baseAmountConsumedAtUpperNode : 0;
                quoteAmountExtraAtColumn += shouldMoveToLowerNode ? quoteAmountConsumedAtUpperNode : 0;

                // Nodes update plus orderbook movement handling
                if (shouldMoveToLowerNode)
                {
                    // Increment the counter in the lower node to indicate that the upper node is consumed to avoid more than one update per column
                    if (upperNode.BaseAmount > 0 && lowerNode.EmptiedCountSibling <= upperNode.EmptiedCount)
                    {
                        lowerNode.EmptiedCountSibling = highestEmptiedCount + 1;
                        nextHighestEmptiedCount = highestEmptiedCount + 1;
                    }

                    nodesToBeUpdated[i] = (lowerNodeIndex, lowerNode, baseAmountExtraAtColumn, quoteAmountExtraAtColumn);

                    // Move to the lower right
                    upperNodeUpperPrice = lowerNodeUpperPrice;
                }
                else
                {
                    nodesToBeUpdated[i] = (upperNodeIndex, upperNode, baseAmountExtraAtColumn, quoteAmountExtraAtColumn);

                    // Move to the upper right
                    lowerNodeLowerPrice = upperNodeLowerPrice;
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

                // If we are moving to the lower, the lower node emptied count is used. If not, the highest of the upper node and the sibling of the lower node is used.
                var newParentEmptiedCount = shouldMoveToLowerNode
                    ? lowerNode.EmptiedCount
                    : MathUtils.Max(lowerNode.EmptiedCountSibling, upperNode.EmptiedCount);
                // var newParentEmptiedCount = shouldMoveToLowerNode ? lowerNode.EmptiedCount : upperNode.EmptiedCount;
                parentNodeEmptiedCount = MathUtils.Max(parentNodeEmptiedCount, newParentEmptiedCount);
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
                BuyTreeStorage.Put(pair.Id, nodeIndex, priceNode);

                baseAmountConsumedInChildren = baseAmountConsumedAtColumn;
                quoteAmountConsumedInChildren = quoteAmountConsumedAtColumn;
            }

            HighestBuyRoundStorage.Put(pair.Id, nextHighestEmptiedCount);

            return (baseAmountLeft, baseAmountFromAMM, quoteAmountReceived, endingPrice);
        }

        /// <summary>
        /// Create a limit buy order.
        /// </summary>
        /// <param name="user">The user related to the operation.</param>
        /// <param name="pair">The pair of tokens</param>
        /// <param name="baseAmount">The amount of base currency.</param>
        /// <returns>The amount of base currency placed.</returns>
        private static BigInteger PlaceSellOrder(
            UInt160 user,
            Pair pair,
            BigInteger baseAmount,
            BigInteger quoteAmountToPlace,
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

            var lowerNodeLowerPrice = (BigInteger)1;
            var upperNodeUpperPrice = rightMostNumberOfNodes;
            var lowerNodeUpperPrice = rightMostNumberOfNodes / 2;
            var upperNodeLowerPrice = lowerNodeUpperPrice + 1;

            var lowerNodeIndex = GetNodeIndex(treeWidth, 0, lowerNodeUpperPrice - 1);
            var upperNodeIndex = GetNodeIndex(treeWidth, 0, upperNodeUpperPrice - 1);

            // Check if the quote amount is greater than 0 if the base amount is greater than 0.
            // ExecutionEngine.Assert(baseAmount == 0 || quoteAmountToPlace >= 0, "Invalid amount to place");

            if (quoteAmountToPlace == 0)
            {
                baseAmount = 0;
            }

            // Used to check if parent node has been emptied to consider all children must be considered empty
            BigInteger parentNodeEmptiedCount = 0;
            BigInteger highestEmptiedCount = HighestSellRoundStorage.Get(pair.Id);
            BigInteger nextHighestEmptiedCount = highestEmptiedCount;
            var ancestorHasEmptied = false;

            for (int i = 0; i < treeWidth; i++)
            {
                var isLastColumn = i == treeWidth - 1;

                var upperNode = SellTreeStorage.Get(pair.Id, upperNodeIndex);
                var lowerNode = SellTreeStorage.Get(pair.Id, lowerNodeIndex);

                var limitPriceIsWithinUpperNodeRange = limitPrice >= upperNodeLowerPrice && limitPrice <= upperNodeUpperPrice;

                var shouldBeEmptied = limitPriceIsWithinUpperNodeRange
                    ? ancestorHasEmptied || parentNodeEmptiedCount > upperNode.EmptiedCount
                    : ancestorHasEmptied || parentNodeEmptiedCount > lowerNode.EmptiedCount || upperNode.EmptiedCountSibling > lowerNode.EmptiedCount;
                if (shouldBeEmptied) ancestorHasEmptied = true;

                if (limitPriceIsWithinUpperNodeRange)
                {
                    // If it was consumed we have to "reset" the node and increase the emptied count for next order
                    if (shouldBeEmptied)
                    {
                        upperNode.BaseAmount = 0;
                        upperNode.QuoteTotal = 0;

                        // Sync the emptied count with the parent node
                        upperNode.EmptiedCount = highestEmptiedCount + 1;
                        nextHighestEmptiedCount = highestEmptiedCount + 1;
                    }
                    upperNode.BaseAmount += baseAmount;
                    upperNode.QuoteTotal += quoteAmountToPlace;
                    SellTreeStorage.Put(pair.Id, upperNodeIndex, upperNode);

                    parentNodeEmptiedCount = upperNode.EmptiedCount;

                    // Move to the upper right
                    lowerNodeLowerPrice = upperNodeLowerPrice;
                }
                else
                {
                    // If it was consumed we have to "reset" the node and increase the emptied count for next order
                    if (shouldBeEmptied)
                    {
                        lowerNode.BaseAmount = 0;
                        lowerNode.QuoteTotal = 0;

                        // Sync the emptied count with the parent node or sibling node
                        lowerNode.EmptiedCount = highestEmptiedCount + 1;
                        nextHighestEmptiedCount = highestEmptiedCount + 1;
                    }

                    lowerNode.BaseAmount += baseAmount;
                    lowerNode.QuoteTotal += quoteAmountToPlace;
                    SellTreeStorage.Put(pair.Id, lowerNodeIndex, lowerNode);

                    parentNodeEmptiedCount = MathUtils.Max(lowerNode.EmptiedCount, upperNode.EmptiedCountSibling);

                    // Move to the lower right
                    upperNodeUpperPrice = lowerNodeUpperPrice;
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
                    var endingNodeIndex = limitPriceIsWithinUpperNodeRange ? upperNodeIndex : lowerNodeIndex;
                    var sellBaseAmountPlaced = SellBaseAmountPlacedStorage.Get(pair.Id, endingNodeIndex);
                    var sellQuoteAmountPlaced = SellQuoteAmountPlacedStorage.Get(pair.Id, endingNodeIndex);
                    var endingNode = limitPriceIsWithinUpperNodeRange ? upperNode : lowerNode;

                    var treeKey = GetCanceledTreeKey(pair.Id, limitPrice - 1, false);
                    var cancelId = CancelledOrdersCountAtPriceStorage.Increment(treeKey);

                    if (shouldBeEmptied)
                    {
                        var (canceledBaseAmountAtNode, canceledQuoteAmountAtNode) = FindAmountCanceledFromOrdersWithLowerId(pair.Id, false, cancelId, limitPrice - 1);
                        var newExecutedBaseAmount = sellBaseAmountPlaced - canceledBaseAmountAtNode;
                        var newExecutedQuoteAmount = sellQuoteAmountPlaced - canceledQuoteAmountAtNode;

                        // Set the executed amounts to the amounts placed, so that we reset the executed amounts to track partially filled orders.
                        SellBaseAmountExecutedStorage.Put(pair.Id, endingNodeIndex, newExecutedBaseAmount);
                        SellQuoteAmountExecutedStorage.Put(pair.Id, endingNodeIndex, newExecutedQuoteAmount);
                    }

                    // Place the order in storage
                    var order = new Order
                    {
                        Owner = user,
                        TotalOrderBaseAmount = originalAmount,
                        TotalOrderQuoteAmount = originalQuoteAmount,
                        PlacedInOrderBookBaseAmount = baseAmount,
                        PlacedInOrderBookQuoteAmount = quoteAmountToPlace,
                        Price = limitPrice,
                        BaseAmountTradedInAmm = baseAmountTradedInAmm,
                        QuoteAmountTradedInAmm = quoteAmountTradedInAmm,
                        BaseAmountTradedBeforeOrderPlaced = baseAmountTradedBeforeOrderPlaced,
                        QuoteAmountTradedBeforeOrderPlaced = quoteAmountTradedBeforeOrderPlaced,
                        IsBuy = false,
                        CancelledBaseAmount = 0,
                        CancelledQuoteAmount = 0,
                        ClaimedBaseAmount = 0,
                        ClaimedQuoteAmount = 0,
                        EmptiedCountWhenInserted = endingNode.EmptiedCount,
                        GlobalBaseAmountPlacedAtPriceWhenInserted = sellBaseAmountPlaced,
                        GlobalQuoteAmountPlacedAtPriceWhenInserted = sellQuoteAmountPlaced,
                        CancelId = cancelId,
                        FeeAmount = feeAmount,
                        CreatedAt = Runtime.Time,
                        UserDefinedId = userDefinedId
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
                        baseAmount,
                        quoteAmountToPlace,
                        limitPrice,
                        baseAmountTradedInAmm,
                        quoteAmountTradedInAmm,
                        baseAmountTradedBeforeOrderPlaced,
                        quoteAmountTradedBeforeOrderPlaced,
                        false,
                        0,
                        0,
                        0,
                        0,
                        endingNode.EmptiedCount,
                        sellBaseAmountPlaced,
                        sellQuoteAmountPlaced,
                        cancelId,
                        endingPrice,
                        feeAmount
                    );

                    // We need to update the amount placed at the node index.
                    // This is so we can keep track of partially filled orders.
                    SellBaseAmountPlacedStorage.Increase(pair.Id, endingNodeIndex, baseAmount);
                    SellQuoteAmountPlacedStorage.Increase(pair.Id, endingNodeIndex, quoteAmountToPlace);
                }
            }

            HighestSellRoundStorage.Put(pair.Id, nextHighestEmptiedCount);

            return baseAmount;
        }
    }
}
