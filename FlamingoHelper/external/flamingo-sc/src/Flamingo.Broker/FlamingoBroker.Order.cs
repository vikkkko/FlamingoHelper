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
        [Safe]
        public static (BigInteger, BigInteger) FindAmountCanceledFromOrdersWithLowerId(BigInteger pairId, bool isBuy, BigInteger orderCancelId, BigInteger priceRow)
        {
            var treeKey = GetCanceledTreeKey(pairId, priceRow, isBuy);
            var orderCountAtKey = CancelledOrdersCountAtPriceStorage.Get(treeKey);

            if (orderCountAtKey == 0) return (0, 0);

            var columnCount = BigInteger.Log2(orderCountAtKey - 1);

            BigInteger rangeAtColumn = 1 << (int)columnCount;
            var lowerNodeId = rangeAtColumn;
            // var upperNodeId = rangeAtColumn * 2;
            var lowerNodeIndex = (columnCount << 32) | rangeAtColumn;
            var upperNodeIndex = lowerNodeIndex + rangeAtColumn;

            BigInteger canceledBaseAmount = 0;
            BigInteger canceledQuoteAmount = 0;

            // var debug = $"\n\norderCancelId: {orderCancelId}, columnCount: {columnCount}, orderCountAtKey: {orderCountAtKey}\n\n";

            for (BigInteger i = 0; i <= columnCount; i++)
            {
                var column = columnCount - i;

                var shouldMoveUp = lowerNodeId < orderCancelId;
                var isLastColumn = i == columnCount;

                // debug += $"column: {column}, lowerNodeId: {lowerNodeId}, upperNodeId: {lowerNodeId + rangeAtColumn}, lowerNodeIndex: {lowerNodeIndex}, upperNodeIndex: {upperNodeIndex}, shouldMoveUp: {shouldMoveUp}, isLastColumn: {isLastColumn}\n";

                if (shouldMoveUp || isLastColumn)
                {
                    var (canceledBaseAmountAtLowerNode, canceledQuoteAmountAtLowerNode) = CancelledAmountAtPriceStorage.Get(treeKey, lowerNodeIndex);
                    canceledBaseAmount += canceledBaseAmountAtLowerNode;
                    canceledQuoteAmount += canceledQuoteAmountAtLowerNode;
                    // debug += $"  canceledBaseAmount lower: {canceledBaseAmount}\n";
                }

                if (isLastColumn && shouldMoveUp)
                {
                    var (canceledBaseAmountAtUpperNode, canceledQuoteAmountAtUpperNode) = CancelledAmountAtPriceStorage.Get(treeKey, upperNodeIndex);
                    canceledBaseAmount += canceledBaseAmountAtUpperNode;
                    canceledQuoteAmount += canceledQuoteAmountAtUpperNode;
                    // debug += $"  canceledBaseAmount upper: {canceledBaseAmount}\n";
                    break;
                }

                rangeAtColumn >>= 1;
                lowerNodeId = shouldMoveUp ? lowerNodeId + rangeAtColumn : lowerNodeId - rangeAtColumn;
                // upperNodeId = lowerNodeId + rangeAtColumn;
                lowerNodeIndex = (column - 1 << 32) | lowerNodeId;
                upperNodeIndex = lowerNodeIndex + rangeAtColumn;
            }

            // if (orderCancelId == 5)
            // {
            //     debug += $"\n\norderCancelId: {orderCancelId}, canceledBaseAmount: {canceledBaseAmount}, canceledQuoteAmount: {canceledQuoteAmount}\n";
            //     ExecutionEngine.Assert(false, debug);
            // }

            return (canceledBaseAmount, canceledQuoteAmount);
        }

        private static void IncreaseCanceledAmountAtPrice(BigInteger pairId, bool isBuy, BigInteger orderCancelId, BigInteger priceRow, BigInteger baseAmountToCancel, BigInteger quoteAmountToCancel)
        {
            var treeKey = GetCanceledTreeKey(pairId, priceRow, isBuy);
            var orderCountAtKey = CancelledOrdersCountAtPriceStorage.Get(treeKey);

            if (orderCountAtKey == 0) return;

            var columnCount = BigInteger.Log2(orderCountAtKey - 1) + 1;

            var oldColumnCount = CancelledTreeColumnCountStorage.Get(treeKey);
            CancelledTreeColumnCountStorage.Put(treeKey, columnCount);
            var shouldFillLowerPartOfTree = columnCount > oldColumnCount;
            BigInteger baseAmountFromOldTree = 0;
            BigInteger quoteAmountFromOldTree = 0;

            var rangeAtColumn = 1;
            var isUpperNode = orderCancelId % 2 == 0;
            var upperNodeId = isUpperNode ? orderCancelId : orderCancelId + 1;
            var lowerNodeId = upperNodeId - 1;
            var lowerNodeIndex = lowerNodeId;
            var upperNodeIndex = lowerNodeIndex + 1;

            // var debug = $"\n\norderCancelId: {orderCancelId}, shouldFillLowerPartOfTree: {shouldFillLowerPartOfTree}, columnCount: {columnCount}, oldColumnCount: {oldColumnCount}\n\n";

            // We now move from the rightmost column to the leftmost column and increment the amount canceled at each node
            for (BigInteger i = 1; i <= columnCount; i++)
            {
                var currentColumn = i - 1;
                // debug += $"column: {currentColumn}, lowerNodeId: {lowerNodeId}, upperNodeId: {upperNodeId}, lowerNodeIndex: {lowerNodeIndex}, upperNodeIndex: {upperNodeIndex}\n";

                if (i == oldColumnCount + 1 && shouldFillLowerPartOfTree)
                {
                    var lowerTreeId = rangeAtColumn;
                    var lowerTreeIndex = (currentColumn << 32) | lowerTreeId;
                    (baseAmountFromOldTree, quoteAmountFromOldTree) = CancelledAmountAtPriceStorage.Get(treeKey, lowerTreeIndex);
                    // debug += $"    OLD AMOUNT, lowerTreeId: {lowerTreeId}, lowerTreeIndex: {lowerTreeIndex}, baseAmountFromOldTree: {baseAmountFromOldTree}\n";
                }

                if (i > oldColumnCount + 1 && shouldFillLowerPartOfTree)
                {
                    var lowerTreeId = rangeAtColumn;
                    var lowerTreeIndex = (currentColumn << 32) | lowerTreeId;
                    // debug += $"    FILL LOW, lowerTreeId: {lowerTreeId}, lowerTreeIndex: {lowerTreeIndex}\n";
                }

                if (lowerNodeId >= orderCancelId)
                {
                    CancelledAmountAtPriceStorage.Increase(treeKey, lowerNodeIndex, baseAmountToCancel, quoteAmountToCancel);
                    // debug += $"    LOWER NODE, baseAmountToCancel: {baseAmountToCancel}\n";
                } else
                {
                    CancelledAmountAtPriceStorage.Increase(treeKey, upperNodeIndex, baseAmountToCancel, quoteAmountToCancel);
                    // debug += $"    UPPER NODE, baseAmountToCancel: {baseAmountToCancel}\n";
                }

                rangeAtColumn <<= 1;
                var nextNodeIsUpperNode = upperNodeId % (rangeAtColumn * 2) == 0;
                upperNodeId = nextNodeIsUpperNode ? upperNodeId : upperNodeId + rangeAtColumn;
                lowerNodeId = upperNodeId - rangeAtColumn;
                upperNodeIndex = (i << 32) | upperNodeId;
                lowerNodeIndex = upperNodeIndex - rangeAtColumn;
            }

            var column = columnCount;
            // debug += $"column: {column}, lowerNodeId: {lowerNodeId}, lowerNodeIndex: {lowerNodeIndex}\n";
            // TODO: We should persist to one extra column so that when the tree grows we can aggregate the amount canceled at the last column
            // We persist the lower node only

            var baseAmountAtLastNode = shouldFillLowerPartOfTree ? baseAmountToCancel + baseAmountFromOldTree : baseAmountToCancel;
            var quoteAmountAtLastNode = shouldFillLowerPartOfTree ? quoteAmountToCancel + quoteAmountFromOldTree : quoteAmountToCancel;
            CancelledAmountAtPriceStorage.Increase(treeKey, lowerNodeIndex, baseAmountAtLastNode, quoteAmountAtLastNode);
            // debug += $"    LOWER NODE, baseAmountAtLastNode: {baseAmountAtLastNode}, quoteAmountAtLastNode: {quoteAmountAtLastNode}\n";

            // if (orderCancelId == 3)
            // {
            //     ExecutionEngine.Assert(false, debug);
            // }
        }

        /// <summary>
        /// Claims the filled amount of an order.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="orderId">The id of the order.</param>
        /// <param name="nodeIndexToCheck">
        /// The node index to check for a higher EmptiedCount. This is used so that we can claim amount from a node that has been virtually emptied by notation on a "greater" node
        /// You can pass -1 if you don't want to check for a higher EmptiedCount.
        /// </param>
        ///
        [NoReentrant]
        public static void ClaimOrder(BigInteger pairId, BigInteger orderId, BigInteger nodeIndexToCheck)
        {
            ExecutionEngine.Assert(!IsPairOrderManagementPaused(pairId), "Pair order management paused");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");

            var order = GetOrder(pairId, orderId);
            ExecutionEngine.Assert(order != null, "Order does not exists");
            ExecutionEngine.Assert(Runtime.CheckWitness(order.Owner), "No user witness");

            if (order.CancelledBaseAmount != 0) return;

            var treeWidth = (int)GetTreeBitLength(pairId);
            BigInteger endingNodeIndexAtOrderPrice = GetNodeIndex(treeWidth, treeWidth - 1, order.Price - 1);

            var baseAmountExecutedAtNode = order.IsBuy ? BuyBaseAmountExecutedStorage.Get(pairId, endingNodeIndexAtOrderPrice) : SellBaseAmountExecutedStorage.Get(pairId, endingNodeIndexAtOrderPrice);
            var (canceledBaseAmount, cancelledQuoteAmount) = FindAmountCanceledFromOrdersWithLowerId(pairId, order.IsBuy, order.CancelId, order.Price - 1);
            BigInteger filledBaseAmount = MathUtils.Max(baseAmountExecutedAtNode + canceledBaseAmount - order.GlobalBaseAmountPlacedAtPriceWhenInserted, 0);
            filledBaseAmount = MathUtils.Min(filledBaseAmount, order.PlacedInOrderBookBaseAmount);

            // The virtually filled amount is the amount that has been filled but is not noted on the price row of the order.
            // The amount is determined by the difference in EmptiedCount between the node to check and the ending node.
            var isVirtuallyFilled = false;

            if (nodeIndexToCheck >= 0)
            {
                isVirtuallyFilled = CheckIfVirtuallyFilled(order, pairId, treeWidth, nodeIndexToCheck);
            }

            var virtuallyFilledBaseAmount = isVirtuallyFilled ? order.PlacedInOrderBookBaseAmount : 0;

            filledBaseAmount = MathUtils.Max(filledBaseAmount, virtuallyFilledBaseAmount);

            if (filledBaseAmount <= 0) return;

            BigInteger amountToClaim = filledBaseAmount - order.ClaimedBaseAmount;

            if (amountToClaim <= 0) return;

            BigInteger quoteToClaim;

            UInt160 baseToken = GetBaseToken(pairId);
            UInt160 quoteToken = GetQuoteToken(pairId);

            if (isVirtuallyFilled)
            {
                var virtuallyFilledQuoteAmount = order.PlacedInOrderBookQuoteAmount;
                quoteToClaim = MathUtils.Max(virtuallyFilledQuoteAmount - order.ClaimedQuoteAmount, 0);
            }
            else
            {
                var quoteAmountExecutedAtNode = order.IsBuy ? BuyQuoteAmountExecutedStorage.Get(pairId, endingNodeIndexAtOrderPrice) : SellQuoteAmountExecutedStorage.Get(pairId, endingNodeIndexAtOrderPrice);
                var filledQuoteAmount = MathUtils.Max(quoteAmountExecutedAtNode + cancelledQuoteAmount - order.GlobalQuoteAmountPlacedAtPriceWhenInserted, 0);
                filledQuoteAmount = MathUtils.Min(filledQuoteAmount, order.PlacedInOrderBookQuoteAmount);
                quoteToClaim = filledQuoteAmount - order.ClaimedQuoteAmount;
            }

            if (quoteToClaim <= 0) return;

            // Give to the user the amount that can be claimed from the order
            if (order.IsBuy)
            {
                BigInteger baseFee = GetMakerFee(pairId) * amountToClaim / FeeRoundingPrecision;
                IncreaseFeeBalance(baseToken, baseFee);

                var amountToClaimWithFee = amountToClaim - baseFee;
                IncreaseUserBalance(order.Owner, baseToken, amountToClaimWithFee);

                order.FeeAmount += baseFee;
            }
            else
            {
                BigInteger quoteFee = GetMakerFee(pairId) * quoteToClaim / FeeRoundingPrecision;
                IncreaseFeeBalance(quoteToken, quoteFee);

                var quoteToClaimWithFee = quoteToClaim - quoteFee;
                IncreaseUserBalance(order.Owner, quoteToken, quoteToClaimWithFee);

                order.FeeAmount += quoteFee;
            }

            // Update order data
            order.ClaimedBaseAmount += amountToClaim;
            order.ClaimedQuoteAmount += quoteToClaim;
            OrderDataStorage.Put(pairId, orderId, order);

            OrderUpserted(
                orderId,
                pairId,
                order.Owner,
                order.TotalOrderBaseAmount,
                order.TotalOrderQuoteAmount,
                order.PlacedInOrderBookBaseAmount,
                order.PlacedInOrderBookQuoteAmount,
                order.Price,
                order.BaseAmountTradedInAmm,
                order.QuoteAmountTradedInAmm,
                order.BaseAmountTradedBeforeOrderPlaced,
                order.QuoteAmountTradedBeforeOrderPlaced,
                order.IsBuy,
                order.CancelledBaseAmount,
                order.CancelledQuoteAmount,
                order.ClaimedBaseAmount,
                order.ClaimedQuoteAmount,
                order.EmptiedCountWhenInserted,
                order.GlobalBaseAmountPlacedAtPriceWhenInserted,
                order.GlobalQuoteAmountPlacedAtPriceWhenInserted,
                order.CancelId,
                0,
                order.FeeAmount
            );
        }

        /// <summary>
        /// Get the amount that can be claimed from an order. This excludes the amount that has already been claimed.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="orderId">The id of the order</param>
        /// <param name="nodeIndexToCheck">
        /// The node index to check for a higher EmptiedCount. This is used so that we can claim amount from a node that has been virtually emptied by notation on a "greater" node.
        /// You can pass -1 if you don't want to check for a higher EmptiedCount.
        /// </param>
        /// <returns>The amount that can be claimed from the order for both base and quote tokens.</returns>
        [Safe]
        public static (BigInteger, BigInteger) GetClaimableAmount(BigInteger pairId, BigInteger orderId, BigInteger nodeIndexToCheck)
        {
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");

            var order = GetOrder(pairId, orderId);
            ExecutionEngine.Assert(order != null, "Order does not exists");

            if (order.CancelledBaseAmount != 0) return (0, 0);

            var treeWidth = (int)GetTreeBitLength(pairId);
            BigInteger endingNodeIndexAtOrderPrice = GetNodeIndex(treeWidth, treeWidth - 1, order.Price - 1);

            var baseAmountExecutedAtNode = order.IsBuy ? BuyBaseAmountExecutedStorage.Get(pairId, endingNodeIndexAtOrderPrice) : SellBaseAmountExecutedStorage.Get(pairId, endingNodeIndexAtOrderPrice);
            var (canceledBaseAmount, cancelledQuoteAmount) = FindAmountCanceledFromOrdersWithLowerId(pairId, order.IsBuy, order.CancelId, order.Price - 1);
            BigInteger filledBaseAmount = MathUtils.Max(baseAmountExecutedAtNode + canceledBaseAmount - order.GlobalBaseAmountPlacedAtPriceWhenInserted, 0);
            filledBaseAmount = MathUtils.Min(filledBaseAmount, order.PlacedInOrderBookBaseAmount);

            // filledBaseAmount: 0
            // baseAmountExecutedAtNode: 9113929381908
            // canceledBaseAmount: 236036025210083
            // order.GlobalBaseAmountPlacedAtPriceWhenInserted: 252841747899157
            // order.PlacedInOrderBookBaseAmount: 68734522689075

            // ExecutionEngine.Assert(false, $"filledBaseAmount: {filledBaseAmount}, baseAmountExecutedAtNode: {baseAmountExecutedAtNode}, canceledBaseAmount: {canceledBaseAmount}, order.GlobalBaseAmountPlacedAtPriceWhenInserted: {order.GlobalBaseAmountPlacedAtPriceWhenInserted}, order.PlacedInOrderBookBaseAmount: {order.PlacedInOrderBookBaseAmount}");

            // The virtually filled amount is the amount that has been filled but is not noted on the price row of the order.
            // The amount is determined by the difference in EmptiedCount between the node to check and the ending node.
            var isVirtuallyFilled = false;

            if (nodeIndexToCheck >= 0)
            {
                isVirtuallyFilled = CheckIfVirtuallyFilled(order, pairId, treeWidth, nodeIndexToCheck);
            }

            var virtuallyFilledBaseAmount = isVirtuallyFilled ? order.PlacedInOrderBookBaseAmount : 0;

            filledBaseAmount = MathUtils.Max(filledBaseAmount, virtuallyFilledBaseAmount);

            if (filledBaseAmount <= 0) return (0, 0);

            BigInteger amountToClaim = filledBaseAmount - order.ClaimedBaseAmount;

            if (amountToClaim <= 0) return (0, 0);

            BigInteger quoteToClaim;

            if (isVirtuallyFilled)
            {
                var virtuallyFilledQuoteAmount = order.PlacedInOrderBookQuoteAmount;
                quoteToClaim = MathUtils.Max(virtuallyFilledQuoteAmount - order.ClaimedQuoteAmount, 0);
            }
            else
            {
                var quoteAmountExecutedAtNode = order.IsBuy ? BuyQuoteAmountExecutedStorage.Get(pairId, endingNodeIndexAtOrderPrice) : SellQuoteAmountExecutedStorage.Get(pairId, endingNodeIndexAtOrderPrice);
                var filledQuoteAmount = MathUtils.Max(quoteAmountExecutedAtNode + cancelledQuoteAmount - order.GlobalQuoteAmountPlacedAtPriceWhenInserted, 0);
                filledQuoteAmount = MathUtils.Min(filledQuoteAmount, order.PlacedInOrderBookQuoteAmount);
                quoteToClaim = filledQuoteAmount - order.ClaimedQuoteAmount;
            }

            if (quoteToClaim <= 0) return (0, 0);

            return (amountToClaim, quoteToClaim);
        }

        [Safe]
        public static BigInteger FindNodeToCheckForClaim(BigInteger pairId, BigInteger orderId)
        {
            var order = GetOrder(pairId, orderId);
            ExecutionEngine.Assert(order != null, "Order does not exists");

            var treeWidth = (int)GetTreeBitLength(pairId);
            var orderPriceRow = order.Price - 1;
            var orderEmptiedCountWhenInserted = order.EmptiedCountWhenInserted;

            var upperNodeUpperPrice = order.Price % 2 == 0 ? orderPriceRow : orderPriceRow + 1;
            var upperNodeLowerPrice = upperNodeUpperPrice;
            var lowerNodeUpperPrice = upperNodeUpperPrice - 1;
            var lowerNodeLowerPrice = lowerNodeUpperPrice;

            BigInteger nodeIndexToCheck = -1;

            for (int i = treeWidth - 1; i >= 0; i--)
            {
                var upperIsAncestor = upperNodeLowerPrice <= orderPriceRow  && upperNodeUpperPrice >= orderPriceRow;
                var lowerIsAncestor = lowerNodeLowerPrice <= orderPriceRow && lowerNodeUpperPrice >= orderPriceRow;

                var upperNodeIndex = GetNodeIndex(treeWidth, i, upperNodeUpperPrice);
                var lowerNodeIndex = GetNodeIndex(treeWidth, i, lowerNodeUpperPrice);

                var upperNode = order.IsBuy ? BuyTreeStorage.Get(pairId, upperNodeIndex) : SellTreeStorage.Get(pairId, upperNodeIndex);
                var lowerNode = order.IsBuy ? BuyTreeStorage.Get(pairId, lowerNodeIndex) : SellTreeStorage.Get(pairId, lowerNodeIndex);

                if (upperIsAncestor)
                {
                    var emptiedCount = MathUtils.Max(upperNode.EmptiedCount, lowerNode.EmptiedCountSibling);

                    if (emptiedCount > orderEmptiedCountWhenInserted)
                    {
                        nodeIndexToCheck = upperNodeIndex;
                        break;
                    }
                }

                if (lowerIsAncestor)
                {
                    var emptiedCount = MathUtils.Max(lowerNode.EmptiedCount, upperNode.EmptiedCountSibling);

                    if (emptiedCount > orderEmptiedCountWhenInserted)
                    {
                        nodeIndexToCheck = lowerNodeIndex;
                        break;
                    }
                }

                var isLastNode = i == 0;

                if (isLastNode) break;

                var priceRangePerNode = 1 << (treeWidth - i - 1);
                var nextNodeIsUpperNode = (upperNodeUpperPrice + 1) % (priceRangePerNode * 4) == 0;

                if (nextNodeIsUpperNode)
                {
                    lowerNodeUpperPrice = upperNodeUpperPrice - (priceRangePerNode * 2);
                }
                else
                {
                    lowerNodeUpperPrice = upperNodeUpperPrice;
                    upperNodeUpperPrice = lowerNodeUpperPrice + (priceRangePerNode * 2);
                }

                upperNodeLowerPrice = upperNodeUpperPrice - ((priceRangePerNode * 2) - 1);
                lowerNodeLowerPrice = lowerNodeUpperPrice - ((priceRangePerNode * 2) - 1);
            }

            return nodeIndexToCheck;
        }

        [Safe]
        public static (BigInteger, BigInteger) GetAmountsAtPrice(BigInteger pairId, bool isBuy, BigInteger limitPrice)
        {
            var treeWidth = (int)GetTreeBitLength(pairId);
            var priceRow = limitPrice - 1;
            var rightMostNodeIndex = GetNodeIndex(treeWidth, treeWidth - 1, priceRow);

            var rightmostNode = isBuy ? BuyTreeStorage.Get(pairId, rightMostNodeIndex) : SellTreeStorage.Get(pairId, rightMostNodeIndex);
            BigInteger baseAmount = rightmostNode.BaseAmount;
            BigInteger quoteAmount = rightmostNode.QuoteTotal;

            var rightmostNodeEmptiedCount = rightmostNode.EmptiedCount;

            var upperNodeUpperPrice = limitPrice % 2 == 0 ? priceRow : priceRow + 1;
            var upperNodeLowerPrice = upperNodeUpperPrice;
            var lowerNodeUpperPrice = upperNodeUpperPrice - 1;
            var lowerNodeLowerPrice = lowerNodeUpperPrice;

            for (int i = treeWidth - 1; i >= 0; i--)
            {
                var upperNodeIsIsAncestor = upperNodeLowerPrice <= priceRow && upperNodeUpperPrice >= priceRow;
                var lowerNodeIsIsAncestor = lowerNodeLowerPrice <= priceRow && lowerNodeUpperPrice >= priceRow;

                var upperNodeIndex = GetNodeIndex(treeWidth, i, upperNodeUpperPrice);
                var lowerNodeIndex = GetNodeIndex(treeWidth, i, lowerNodeUpperPrice);

                var upperNode = isBuy ? BuyTreeStorage.Get(pairId, upperNodeIndex) : SellTreeStorage.Get(pairId, upperNodeIndex);
                var lowerNode = isBuy ? BuyTreeStorage.Get(pairId, lowerNodeIndex) : SellTreeStorage.Get(pairId, lowerNodeIndex);

                if (upperNodeIsIsAncestor)
                {
                    var isEmptied = lowerNode.EmptiedCountSibling > upperNode.EmptiedCount;
                    var emptiedCount = MathUtils.Max(upperNode.EmptiedCount, lowerNode.EmptiedCountSibling);

                    if (isEmptied || emptiedCount > rightmostNodeEmptiedCount)
                    {
                        baseAmount = 0;
                        quoteAmount = 0;
                        break;
                    }
                }

                if (lowerNodeIsIsAncestor)
                {
                    var isEmptied = upperNode.EmptiedCountSibling > lowerNode.EmptiedCount;
                    var emptiedCount = MathUtils.Max(lowerNode.EmptiedCount, upperNode.EmptiedCountSibling);

                    if (isEmptied || emptiedCount > rightmostNodeEmptiedCount)
                    {
                        baseAmount = 0;
                        quoteAmount = 0;
                        break;
                    }
                }

                var isLastNode = i == 0;

                if (isLastNode) break;

                var priceRangePerNode = 1 << (treeWidth - i - 1);
                var nextNodeIsUpperNode = (upperNodeUpperPrice + 1) % (priceRangePerNode * 4) == 0;

                if (nextNodeIsUpperNode)
                {
                    lowerNodeUpperPrice = upperNodeUpperPrice - (priceRangePerNode * 2);
                }
                else
                {
                    lowerNodeUpperPrice = upperNodeUpperPrice;
                    upperNodeUpperPrice = lowerNodeUpperPrice + (priceRangePerNode * 2);
                }

                upperNodeLowerPrice = upperNodeUpperPrice - ((priceRangePerNode * 2) - 1);
                lowerNodeLowerPrice = lowerNodeUpperPrice - ((priceRangePerNode * 2) - 1);
            }

            return (baseAmount, quoteAmount);
        }

        /// <summary>
        /// Get the virtually filled amount of an order. By virtually we mean that the amount has been filled but is not noted on the price row of the order.
        /// We need to look up an ancestor (or it's sibling) node to check if the amount has been filled.
        /// </summary>
        /// <param name="order">The order to check</param>
        /// <param name="pairId">The id of the trading pair</param>
        /// <param name="treeWidth">The width of the tree</param>
        /// <param name="nodeIndexToCheck">The node index of the ancestor node to check</param>
        [Safe]
        public static bool CheckIfVirtuallyFilled(Order order, BigInteger pairId, int treeWidth, BigInteger nodeIndexToCheck)
        {
            if (nodeIndexToCheck < 0) return false;

            BigInteger endingNodeIndexAtOrderPrice = GetNodeIndex(treeWidth, treeWidth - 1, order.Price - 1);

            // var endingNode = GetPriceNode(pairId, endingNodeIndexAtOrderPrice, !order.IsBuy);
            var endingNodePriceRow = GetPriceRowFromNodeIndex(endingNodeIndexAtOrderPrice, treeWidth);
            // var endingNodeColumn = GetColumnFromNodeIndex(endingNodeIndexAtOrderPrice, treeWidth);

            var nodeToCheck = !order.IsBuy ? SellTreeStorage.Get(pairId, nodeIndexToCheck) : BuyTreeStorage.Get(pairId, nodeIndexToCheck);
            var nodeToCheckColumn = GetColumnFromNodeIndex(nodeIndexToCheck, treeWidth);
            var nodeToCheckUpperPriceRow = GetPriceRowFromNodeIndex(nodeIndexToCheck, treeWidth);

            // ExecutionEngine.Assert(nodeToCheckColumn < endingNodeColumn, $"Node to check is not a ancestor node: {nodeIndexToCheck} ({nodeToCheckColumn}, {nodeToCheckUpperPriceRow})");

            BigInteger numberOfNodesInColumn = 1 << ((int)nodeToCheckColumn + 1);
            BigInteger rightMostNumberOfNodes = 1 << treeWidth;
            var priceRangePerNode = rightMostNumberOfNodes / numberOfNodesInColumn;
            var nodeToCheckLowerPriceRow = nodeToCheckUpperPriceRow - (priceRangePerNode - 1);

            var isAncestor = nodeToCheckLowerPriceRow <= endingNodePriceRow && nodeToCheckUpperPriceRow >= endingNodePriceRow;

            if (!isAncestor) return false;

            if (nodeToCheck.EmptiedCount > order.EmptiedCountWhenInserted)
            {
                return true;
            }

            var isUpperNode = (nodeToCheckUpperPriceRow + 1) % (priceRangePerNode * 2) == 0;

            var siblingNodeIndex = isUpperNode
                ? nodeIndexToCheck - priceRangePerNode
                : nodeIndexToCheck + priceRangePerNode;

            var siblingNode = order.IsBuy ? BuyTreeStorage.Get(pairId, siblingNodeIndex) : SellTreeStorage.Get(pairId, siblingNodeIndex);

            var bigSibling = isUpperNode == order.IsBuy ? siblingNode : nodeToCheck;
            var littleSibling = isUpperNode == order.IsBuy ? nodeToCheck : siblingNode;

            return bigSibling.EmptiedCountSibling > littleSibling.EmptiedCount;
        }

        /// <summary>
        /// Cancel an order updating related buy/sell tree with canceled amount.
        /// </summary>
        /// <param name="pairId">The id of the pair of tokens</param>
        /// <param name="orderId">The id of the order.</param>
        [NoReentrant]
        public static void CancelOrder(BigInteger pairId, BigInteger orderId)
        {
            ExecutionEngine.Assert(!IsPairOrderManagementPaused(pairId), "Pair order management paused");
            ExecutionEngine.Assert(IsPairExisting(pairId), "Pair does not exists");

            var nodeIdToCheck = FindNodeToCheckForClaim(pairId, orderId);
            ClaimOrder(pairId, orderId, nodeIdToCheck);

            var order = GetOrder(pairId, orderId);
            ExecutionEngine.Assert(order != null, "Order does not exists");
            ExecutionEngine.Assert(Runtime.CheckWitness(order.Owner), "No user witness");
            ExecutionEngine.Assert(order.CancelledBaseAmount == 0, "Order cancelled");

            var treeWidth = (int)GetTreeBitLength(pairId);

            BigInteger amountToCancel = order.PlacedInOrderBookBaseAmount - order.ClaimedBaseAmount;
            ExecutionEngine.Assert(amountToCancel > 0, "Nothing to cancel");
            BigInteger quoteToCancel = order.PlacedInOrderBookQuoteAmount - order.ClaimedQuoteAmount;

            UInt160 baseToken = GetBaseToken(pairId);
            UInt160 quoteToken = GetQuoteToken(pairId);

            // Give back to the user the amount that will be canceled
            if (order.IsBuy)
            {
                // Cancel quote from buy tree
                IncreaseUserBalance(order.Owner, quoteToken, quoteToCancel);
            }
            else
            {
                // Cancel base from sell tree
                IncreaseUserBalance(order.Owner, baseToken, amountToCancel);
            }

            BigInteger rightMostNumberOfNodes = 1 << treeWidth;

            // Remove amountToCancel from buy/sell tree and update canceled amount at rightmost node
            for (int column = treeWidth - 1; column >= 0; column--)
            {
                BigInteger numberOfNodesInColumn = 1 << (column + 1);
                var priceRangePerNode = rightMostNumberOfNodes / numberOfNodesInColumn;
                // Integer ceil division
                var nodeNumber = (order.Price - 1 + priceRangePerNode) / priceRangePerNode;
                var roundedPriceIndex = (nodeNumber * priceRangePerNode) - 1;

                BigInteger nodeIndex = GetNodeIndex(treeWidth, column, roundedPriceIndex);
                PriceNode priceNode = order.IsBuy ? BuyTreeStorage.Get(pairId, nodeIndex) : SellTreeStorage.Get(pairId, nodeIndex);

                ExecutionEngine.Assert(priceNode.BaseAmount - amountToCancel >= 0, "Not enough base amount to cancel");
                ExecutionEngine.Assert(priceNode.QuoteTotal - quoteToCancel >= 0, "Not enough quote amount to cancel");

                priceNode.BaseAmount -= amountToCancel;
                priceNode.QuoteTotal -= quoteToCancel;

                if (order.IsBuy)
                {
                    BuyTreeStorage.Put(pairId, nodeIndex, priceNode);
                }
                else
                {
                    SellTreeStorage.Put(pairId, nodeIndex, priceNode);
                }
            }

            IncreaseCanceledAmountAtPrice(pairId, order.IsBuy, order.CancelId, order.Price - 1, amountToCancel, quoteToCancel);

            // Update order data
            order.CancelledBaseAmount = amountToCancel;
            order.CancelledQuoteAmount = quoteToCancel;
            OrderDataStorage.Put(pairId, orderId, order);

            OrderUpserted(
                orderId,
                pairId,
                order.Owner,
                order.TotalOrderBaseAmount,
                order.TotalOrderQuoteAmount,
                order.PlacedInOrderBookBaseAmount,
                order.PlacedInOrderBookQuoteAmount,
                order.Price,
                order.BaseAmountTradedInAmm,
                order.QuoteAmountTradedInAmm,
                order.BaseAmountTradedBeforeOrderPlaced,
                order.QuoteAmountTradedBeforeOrderPlaced,
                order.IsBuy,
                amountToCancel,
                quoteToCancel,
                order.ClaimedBaseAmount,
                order.ClaimedQuoteAmount,
                order.EmptiedCountWhenInserted,
                order.GlobalBaseAmountPlacedAtPriceWhenInserted,
                order.GlobalQuoteAmountPlacedAtPriceWhenInserted,
                order.CancelId,
                0,
                order.FeeAmount
            );
        }

        [Safe]
        public static Order GetOrder(BigInteger pairId, BigInteger orderId)
        {
            return OrderDataStorage.Get(pairId, orderId);
        }

        [Safe]
        public static List<OrderInfo> GetAllUserOrders(UInt160 user, int page)
        {
            var results = new List<OrderInfo>();

            var prefix = user+ UserOrderStorage.ToFixedLengthBytes(page, UserOrderStorage.PageKeyLength);
            var iterator = UserOrderStorage.FindAll(prefix);

            while (iterator.Next())
            {
                var content = (object[])iterator.Value;
                var pairId = UserOrderStorage.GetPairIdFromKey((ByteString) content[0]);
                var orderId = (BigInteger) (ByteString) content[1];
                var order = GetOrder(pairId, orderId);
                ExecutionEngine.Assert(order != null, $"Order does not exists, orderId '{orderId}', pairId {pairId}");
                var orderWithId = new OrderInfo
                {
                    Id = orderId,
                    PairId = pairId,
                    Owner = order.Owner,
                    TotalOrderBaseAmount = order.TotalOrderBaseAmount,
                    TotalOrderQuoteAmount = order.TotalOrderQuoteAmount,
                    PlacedInOrderBookBaseAmount = order.PlacedInOrderBookBaseAmount,
                    PlacedInOrderBookQuoteAmount = order.PlacedInOrderBookQuoteAmount,
                    Price = order.Price,
                    BaseAmountTradedInAmm = order.BaseAmountTradedInAmm,
                    QuoteAmountTradedInAmm = order.QuoteAmountTradedInAmm,
                    BaseAmountTradedBeforeOrderPlaced = order.BaseAmountTradedBeforeOrderPlaced,
                    QuoteAmountTradedBeforeOrderPlaced = order.QuoteAmountTradedBeforeOrderPlaced,
                    IsBuy = order.IsBuy,
                    CancelledBaseAmount = order.CancelledBaseAmount,
                    CancelledQuoteAmount = order.CancelledQuoteAmount,
                    ClaimedBaseAmount = order.ClaimedBaseAmount,
                    ClaimedQuoteAmount = order.ClaimedQuoteAmount,
                    EmptiedCountWhenInserted = order.EmptiedCountWhenInserted,
                    GlobalBaseAmountPlacedAtPriceWhenInserted = order.GlobalBaseAmountPlacedAtPriceWhenInserted,
                    GlobalQuoteAmountPlacedAtPriceWhenInserted = order.GlobalQuoteAmountPlacedAtPriceWhenInserted,
                    CancelId = order.CancelId,
                    FeeAmount = order.FeeAmount,
                    CreatedAt = order.CreatedAt,
                    UserDefinedId = order.UserDefinedId
                };
                results.Add(orderWithId);
            }

            return results;
        }
    }
}
