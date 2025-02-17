using System.Numerics;
using Neo.SmartContract.Framework;

namespace Flamingo.Broker.Models
{
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
        // A field the user can use to store an ID for the order. Using the same ID twice is possible here, but it is not recommended.
        public BigInteger UserDefinedId;
    }
}
