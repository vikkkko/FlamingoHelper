using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Broker
{
    public partial class FlamingoBroker
    {
        /// <summary>
        /// Converts a price row used in the order book tree to an AMM price. What we mean by AMM price is the
        /// price you get when you divide the quote token reserve by the base token reserve.
        /// The real price takes into account the price precision and the price coefficient used to determine
        /// the price interval of each price row.
        ///
        /// The price is also adjusted to take into account the number of decimals of the base and quote tokens.
        /// E.g. if the base token has 8 decimals and the quote token has 6 decimals, and the price is 1000,
        /// the real price will be 10. This is because the real price represent the ratio between the smallest
        /// fraction of the base token and the smallest fraction of the quote token.
        /// </summary>
        /// <param name="priceRow">The price row to convert to a real price.</param>
        /// <param name="pairPricePrecision">The price precision of the pair.</param>
        /// <param name="baseTokenDecimals">The number of decimals of the base token.</param>
        /// <param name="quoteTokenDecimals">The number of decimals of the quote token.</param>
        /// <returns></returns>
        [Safe]
        public static BigInteger ConvertPriceRowToAmmPrice(
            BigInteger priceRow,
            BigInteger pairPricePrecision,
            BigInteger baseTokenDecimals,
            BigInteger quoteTokenDecimals
        )
        {
            var roundingPrecision = 100_000_000_000;
            var priceIntervalPerPriceRow = pairPricePrecision * roundingPrecision * PriceRoundingPrecision / PriceCoefficient;
            var realPriceWithoutDecimalOffset = priceIntervalPerPriceRow * priceRow / roundingPrecision;
            var quoteTokenDecimalMultiplier = BigInteger.Pow(10, (int) quoteTokenDecimals);
            var baseTokenDecimalMultiplier = BigInteger.Pow(10, (int) baseTokenDecimals);
            var realPrice = realPriceWithoutDecimalOffset * quoteTokenDecimalMultiplier / baseTokenDecimalMultiplier;
            return realPrice;
        }

        /// <summary>
        /// Converts a price row used in the order book tree to a real price. By "real price" we mean the price
        /// for example that 1 BTC is worth 10,000 USD. This is the price that is displayed to the user.
        /// The real price is calculated by using the price row, the price precision of the pair, and the price coefficient.
        ///
        /// Example: If a trading pair is BTC/USDT and the pair has a price precision that is 100th of the price coefficient,
        /// each price row will represent a price interval 0.01 USDT. If the price row is 1000, the real price will be 10 USD.
        /// Equally, if the price precision is 100 times the price coefficient, each price row will represent a price interval
        /// of 100 USDT. If the price row is 1000, the real price will be 100,000 USD.
        /// </summary>
        /// <param name="priceRow">The price row to convert to a real price.</param>
        /// <param name="pairPricePrecision">The price precision of the pair.</param>
        /// <returns></returns>
        [Safe]
        public static BigInteger ConvertPriceRowToRealPrice(
            BigInteger priceRow,
            BigInteger pairPricePrecision,
            BigInteger baseTokenDecimals,
            BigInteger quoteTokenDecimals
        )
        {
            var roundingPrecision = 100_000_000_000;
            var priceIntervalPerPriceRow = pairPricePrecision * roundingPrecision * PriceRoundingPrecision / PriceCoefficient;
            var realPrice = priceIntervalPerPriceRow * priceRow / roundingPrecision;
            var quoteTokenDecimalMultiplier = BigInteger.Pow(10, (int) quoteTokenDecimals);
            var baseTokenDecimalMultiplier = BigInteger.Pow(10, (int) baseTokenDecimals);
            return realPrice * quoteTokenDecimalMultiplier / baseTokenDecimalMultiplier;
        }

        /// <summary>
        /// Converts an AMM price to a limit price based on the pair's price precision and the number of decimals of the base and quote tokens.
        /// This is used when comparing the price of a place-only order with the price of an AMM order to make sure the place-only must be placed
        /// below or above the current AMM price.
        /// </summary>
        /// <param name="baseTokenReserve">The current base token reserve of the pool.</param>
        /// <param name="quoteTokenReserve">The current quote token reserve of the pool.</param>
        /// <param name="pairPricePrecision">The price precision of the pair.</param>
        /// <param name="baseTokenDecimals">The number of decimals of the base token.</param>
        /// <param name="quoteTokenDecimals">The number of decimals of the quote token.</param>
        /// <param name="roundUp">Whether to round up the price or not.</param>
        [Safe]
        public static BigInteger ConvertAmmPriceToLimitPrice(
            BigInteger baseTokenReserve,
            BigInteger quoteTokenReserve,
            BigInteger pairPricePrecision,
            BigInteger baseTokenDecimals,
            BigInteger quoteTokenDecimals,
            bool roundUp = false
        )
        {
            var roundingPrecision = 100_000_000_000;
            var priceIntervalPerPriceRow = pairPricePrecision * roundingPrecision / PriceCoefficient;
            var quoteTokenDecimalMultiplier = BigInteger.Pow(10, (int)quoteTokenDecimals);
            var baseTokenDecimalMultiplier = BigInteger.Pow(10, (int)baseTokenDecimals);

            var numerator = quoteTokenReserve * roundingPrecision * baseTokenDecimalMultiplier;
            var denominator = baseTokenReserve * quoteTokenDecimalMultiplier * priceIntervalPerPriceRow;

            // If roundUp is true, apply ceil division; otherwise, use standard floor division
            return roundUp ? (numerator + denominator - 1) / denominator : numerator / denominator;
        }

        /// <summary>
        /// Calculate the amount of base token to sell to reach the target price.
        /// If the target price is not reachable, it will return 0.
        /// </summary>
        /// <param name="baseTokenReserve">The current base token reserve of the pool.</param>
        /// <param name="quoteTokenReserve">The current quote token reserve of the pool.</param>
        /// <param name="targetPrice">The target price to reach. Should be multiplied by RoundingPrecision before passing.</param>
        /// <returns>The amount of base token to sell to reach the target price.</returns>
        [Safe]
        public static BigInteger CalculateBaseTokenInUntilPrice(BigInteger baseTokenReserve, BigInteger quoteTokenReserve, BigInteger targetPrice)
        {
            // Let's say v = baseTokenReserve, w = quoteTokenReserve
            // Using the constant product formula: v * w = k
            // we can say that: (v + amountIn) * (w - amountOut) = v * w = k
            // Using the constant product formula we calculate amountOut as:
            // amountOut = (amountIn * (1 - fee) * w) / (v + amountIn * (1 - fee))
            // Substituting amountOut in the constant product formula we get:
            // (v + amountIn) * (w - ((amountIn * (1 - fee) * w) / (v + amountIn * (1 - fee)))) = v * w

            // Next we need to figure out what the quote token reserve will be after the transaction.
            // Let's call the new quote token reserve wNew and the new base token reserve vNew.
            // We can say that wNew = w - ((amountIn * (1 - fee) * w) / (v + amountIn * (1 - fee)))
            // And we can simplify this to: wNew = v * w / (v + amountIn * (1 - fee))
            // And vNew = v + amountIn

            // Next we can define targetPrice = wNew / vNew
            // Expanding that we get targetPrice = (v * w / (v + amountIn * (1 - fee))) / (v + amountIn)
            // Simplifying the equation we get:
            // targetPrice = (v * w) / ((v + amountIn * (1 - fee)) * (v + amountIn))

            // Next let's rearrange the equation to be in the form of a quadratic equation
            // Let x = amountIn from now on
            // Step 1: Eliminate the Denominator
            //   v * w = targetPrice * (v + x * (1 - fee)) * (v + x)
            // Step 2: Expand the expression on the right-hand side
            //   (v + x * (1 - fee)) * (v + x) = v^2 + vx(2 - fee) + x^2(1 - fee)
            // Step 3: Substitute the expanded expression back into the equation
            //   v * w = targetPrice * (v^2 + vx(2 - fee) + x^2(1 - fee))
            // Step 4: Rearrange the equation to be in the form of a quadratic equation
            //   (1 - fee)x^2 + v(2 - fee)x + v^2 - (v * w / targetPrice) = 0
            // Step 5: Collect coefficients of the quadratic equation
            //   [(1 - fee)]x^2 + [v(2 - fee)]x + [v^2 - (v * w / targetPrice)] = 0
            //   a = (1 - fee)
            //   b = v(2 - fee)
            //   c = v^2 - (v * w / targetPrice)

            // We create the following variables based on the coefficients of the quadratic equation
            // Note: We need to use FeeRoundingPrecision and PriceRoundingPrecision to avoid floating point errors.
            BigInteger a = FeeRoundingPrecision - AMMRouterSwapFee;
            BigInteger b = baseTokenReserve * (2 * FeeRoundingPrecision - AMMRouterSwapFee);
            BigInteger c = FeeRoundingPrecision * (baseTokenReserve * baseTokenReserve - (baseTokenReserve * quoteTokenReserve * PriceRoundingPrecision / targetPrice));

            // We can now solve for amountInWithFee using the quadratic formula:
            // x = (-b ± √(b^2 - 4ac)) / 2a
            // But first, we calculate the discriminant of the quadratic equation to check if there are real solutions.
            // The discriminant is: D = b^2 - 4a (the part under the square root in the quadratic formula)
            BigInteger D = b * b - 4 * a * c;

            ExecutionEngine.Assert(D >= 0, "Negative discriminant, no solution");

            // We can now calculate the positive root of the quadratic equation, which will be the amountIn
            BigInteger positiveRoot = (-b + D.Sqrt()) / (2 * a);

            if (positiveRoot < 0) return 0;

            return positiveRoot;
        }

        /// <summary>
        /// Calculate the amount of base token to sell to reach the target price.
        /// If the target price is not reachable, it will return 0.
        /// </summary>
        /// <param name="baseTokenReserve">The current base token reserve of the pool.</param>
        /// <param name="quoteTokenReserve">The current quote token reserve of the pool.</param>
        /// <param name="targetPrice">The target price to reach. Should be multiplied by RoundingPrecision before passing.</param>
        /// <returns>The amount of base token to sell to reach the target price.</returns>
        [Safe]
        public static BigInteger CalculateQuoteTokenOutUntilPrice(BigInteger baseTokenReserve, BigInteger quoteTokenReserve, BigInteger targetPrice)
        {
            // Let's say v = baseTokenReserve, w = quoteTokenReserve
            // amountOut = (amountIn * (1 - fee) * w) / (v + amountIn * (1 - fee))
            BigInteger baseTokenIn = CalculateBaseTokenInUntilPrice(baseTokenReserve, quoteTokenReserve, targetPrice);
            BigInteger quoteTokenOut = GetAmountOut(baseTokenIn, baseTokenReserve, quoteTokenReserve);
            return quoteTokenOut;
        }

        /// <summary>
        /// Calculate the amount of quote token to sell to reach the target price.
        /// If the target price is not reachable, it will return 0.
        /// </summary>
        /// <param name="baseTokenReserve">The current base token reserve of the pool.</param>
        /// <param name="quoteTokenReserve">The current quote token reserve of the pool.</param>
        /// <param name="targetPrice">The target price to reach. Should be multiplied by RoundingPrecision before passing.</param>
        /// <returns>The amount of quote token to sell to reach the target price.</returns>
        [Safe]
        public static BigInteger CalculateQuoteTokenInUntilPrice(BigInteger baseTokenReserve, BigInteger quoteTokenReserve, BigInteger targetPrice)
        {
            // Let's say v = quoteTokenReserve, w = baseTokenReserve
            // Using the constant product formula: v * w = k
            // we can say that: (v + amountIn) * (w - amountOut) = v * w = k
            // Using the constant product formula we calculate amountOut as:
            // amountOut = (amountIn * (1 - fee) * w) / (v + amountIn * (1 - fee))
            // Substituting amountOut in the constant product formula we get:
            // (v + amountIn) * (w - ((amountIn * (1 - fee) * w) / (v + amountIn * (1 - fee)))) = v * w

            // Next we need to figure out what the quote token reserve will be after the transaction.
            // Let's call the new quote token reserve wNew and the new base token reserve vNew.
            // We can say that wNew = w - ((amountIn * (1 - fee) * w) / (v + amountIn * (1 - fee)))
            // And we can simplify this to: wNew = v * w / (v + amountIn * (1 - fee))
            // And vNew = v + amountIn

            // Next we can define targetPrice = vNew / wNew
            // Expanding that we get targetPrice = (v + amountIn) / (v * w / (v + amountIn * (1 - fee)))
            // Simplifying the equation we get:
            // targetPrice = ((v + amountIn * (1 - fee)) * (v + amountIn)) / (v * w)

            // Next let's rearrange the equation to be in the form of a quadratic equation
            // Let x = amountIn from now on
            // Step 1: Eliminate the Denominator
            //   v * w = (v + x * (1 - fee)) * (v + x) / targetPrice
            // Step 2: Expand the expression on the right-hand side
            //   (v + x * (1 - fee)) * (v + x) = v^2 + vx(2 - fee) + x^2(1 - fee)
            // Step 3: Substitute the expanded expression back into the equation
            //   v * w = (v^2 + vx(2 - fee) + x^2(1 - fee)) / targetPrice
            // Step 4: Rearrange the equation to be in the form of a quadratic equation
            //   (1 - fee)x^2 + v(2 - fee)x + v^2 - (v * w * targetPrice) = 0
            // Step 5: Collect coefficients of the quadratic equation
            //   [(1 - fee)]x^2 + [v(2 - fee)]x + [v^2 - (v * w * targetPrice)] = 0
            //   a = (1 - fee)
            //   b = v(2 - fee)
            //   c = v^2 - (v * w * targetPrice)

            // We create the following variables based on the coefficients of the quadratic equation
            // Note: We need to use FeeRoundingPrecision and PriceRoundingPrecision to avoid floating point errors.
            BigInteger a = FeeRoundingPrecision - AMMRouterSwapFee;
            BigInteger b = quoteTokenReserve * (2 * FeeRoundingPrecision - AMMRouterSwapFee);
            BigInteger c = FeeRoundingPrecision * (quoteTokenReserve * quoteTokenReserve - (quoteTokenReserve * baseTokenReserve * targetPrice / PriceRoundingPrecision));

            // We can now solve for amountInWithFee using the quadratic formula:
            // x = (-b ± √(b^2 - 4ac)) / 2a
            // But first, we calculate the discriminant of the quadratic equation to check if there are real solutions.
            // The discriminant is: D = b^2 - 4ac
            BigInteger D = b * b - 4 * a * c;

            ExecutionEngine.Assert(D >= 0, "Negative discriminant, no solution");

            // We can now calculate the positive root of the quadratic equation, which will be the amountIn
            BigInteger positiveRoot = (-b + D.Sqrt()) / (2 * a);

            if (positiveRoot < 0) return 0;

            return positiveRoot;
        }

        /// <summary>
        /// Calculate the amount of base token to buy to reach the target price.
        /// </summary>
        /// <param name="baseTokenReserve"></param>
        /// <param name="quoteTokenReserve"></param>
        /// <param name="targetPrice"></param>
        /// <returns>The amount of base token to buy to reach the target price.</returns>
        [Safe]
        public static BigInteger CalculateBaseTokenOutUntilPrice(BigInteger baseTokenReserve, BigInteger quoteTokenReserve, BigInteger targetPrice)
        {
            // Let's say v = quoteTokenReserve, w = baseTokenReserve
            // amountOut = (amountIn * (1 - fee) * w) / (v + amountIn * (1 - fee))

            //step1, calc amountIn
            BigInteger quoteTokenIn = CalculateQuoteTokenInUntilPrice(baseTokenReserve, quoteTokenReserve, targetPrice);
            BigInteger baseTokenOut = GetAmountOut(quoteTokenIn, quoteTokenReserve, baseTokenReserve);
            return baseTokenOut;
        }

        [Safe]
        public static BigInteger GetAmountOut(BigInteger amountIn, BigInteger reserveIn, BigInteger reserveOut)
        {
            var amountInWithFee = amountIn * (FeeRoundingPrecision - AMMRouterSwapFee);
            var numerator = amountInWithFee * reserveOut;
            var denominator = reserveIn * FeeRoundingPrecision + amountInWithFee;
            var amountOut = numerator / denominator;
            return amountOut;
        }

        [Safe]
        public static BigInteger GetAmountIn(BigInteger amountOut, BigInteger reserveIn, BigInteger reserveOut)
        {
            var numerator = reserveIn * amountOut * FeeRoundingPrecision;
            var denominator = (reserveOut - amountOut) * (FeeRoundingPrecision - AMMRouterSwapFee);
            var amountIn = (numerator / denominator) + 1;
            return amountIn;
        }

        public static class NEP17Helper
        {
            internal static void SafeTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount, object data = null)
            {
                try
                {
                    var result = (bool) Contract.Call(token, "transfer", CallFlags.All, new object[] {from, to, amount, data});
                    ExecutionEngine.Assert(result, "Transfer fail");
                }
                catch
                {
                    ExecutionEngine.Assert(false, "NEP17 Transfer error");
                }
            }

            internal static BigInteger SafeBalanceOf(UInt160 asset, UInt160 user)
            {
                try
                {
                    var result = (BigInteger) Contract.Call(asset, "balanceOf", CallFlags.ReadOnly, new object[] {user});
                    return result;
                }
                catch
                {
                    ExecutionEngine.Assert(false, "Catched BalanceOf error");
                    throw;
                }
            }

            public static BigInteger GetDecimals(UInt160 asset)
            {
                return (BigInteger) Contract.Call(asset, "decimals", CallFlags.ReadOnly, new object[] { });
            }
        }

        public static class AMMHelper
        {
            internal static void SafeSwapTokenInForTokenOut(BigInteger amountIn, BigInteger amountOutMin, UInt160[] paths)
            {
                try
                {
                    var result = (bool) Contract.Call(GetAMMRouter(), "swapTokenInForTokenOut", CallFlags.All, new object[] {amountIn, amountOutMin, paths, Runtime.Time + 1});
                    ExecutionEngine.Assert(result, "SwapTokenInForTokenOut Error");
                }
                catch
                {
                    ExecutionEngine.Assert(false, "Catch Transfer Error");
                    throw;
                }
            }

            internal static void SafeSwapTokenOutForTokenIn(BigInteger amountOut, BigInteger amountInMax, UInt160[] paths)
            {
                try
                {
                    var result = (bool) Contract.Call(GetAMMRouter(), "swapTokenOutForTokenIn", CallFlags.All, new object[] {amountOut, amountInMax, paths, Runtime.Time + 1});
                    ExecutionEngine.Assert(result, "SwapTokenOutForTokenIn Error");
                }
                catch
                {
                    ExecutionEngine.Assert(false, "Catch Transfer Error");
                    throw;
                }
            }

            internal static UInt160 GetExchangePairFromFactory(UInt160 token0, UInt160 token1)
            {
                ExecutionEngine.Assert(token0.IsValid && token1.IsValid, "Invalid A or B Address");
                // NB: From factory!
                var pairContract = (byte[]) Contract.Call(GetAMMFactory(), "getExchangePair", CallFlags.ReadOnly, new object[] {token0, token1});
                ExecutionEngine.Assert(pairContract != null && pairContract.Length == 20, "PairContract Not Found");
                return (UInt160) pairContract;
            }

            internal static UInt160 GetToken0FromPair(UInt160 pair)
            {
                return (UInt160) Contract.Call(pair, "getToken0", CallFlags.ReadOnly, new object[] { });
            }

            internal static UInt160 GetToken1FromPair(UInt160 pair)
            {
                return (UInt160) Contract.Call(pair, "getToken1", CallFlags.ReadOnly, new object[] { });
            }

            internal static BigInteger GetReserve0FromPair(UInt160 pair)
            {
                return (BigInteger) Contract.Call(pair, "getReserve0", CallFlags.ReadOnly, new object[] { });
            }

            internal static BigInteger GetReserve1FromPair(UInt160 pair)
            {
                return (BigInteger) Contract.Call(pair, "getReserve1", CallFlags.ReadOnly, new object[] { });
            }

            internal static (BigInteger, BigInteger) GetReserves(UInt160 baseToken, UInt160 quoteToken)
            {
                UInt160 pair = GetExchangePairFromFactory(baseToken, quoteToken);
                UInt160 token0 = GetToken0FromPair(pair);
                UInt160 token1 = GetToken1FromPair(pair);
                BigInteger reserve0 = GetReserve0FromPair(pair);
                BigInteger reserve1 = GetReserve1FromPair(pair);
                BigInteger baseTokenReserve = token0 == baseToken ? reserve0 : reserve1;
                BigInteger quoteTokenReserve = token1 == baseToken ? reserve0 : reserve1;
                return (baseTokenReserve, quoteTokenReserve);
            }
        }
    }
}
