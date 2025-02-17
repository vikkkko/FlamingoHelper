using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System;
using System.ComponentModel;
using System.Numerics;
namespace Flamingo.FLUND
{
    public partial class FLUND : SmartContract
    {
        public struct ReservesData
        {
            public BigInteger Reserve0;
            public BigInteger Reserve1;
            public BigInteger BlockTimestampLast;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        /// <summary>
        /// 断言
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        private static void Assert(bool condition, string message, params object[] data)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        /// <summary>
        /// 安全查询交易对，查不到立即中断合约执行
        /// </summary>
        /// <param name="tokenA"></param>
        /// <param name="tokenB"></param>
        /// <returns></returns>
        public static UInt160 GetExchangePairWithAssert(UInt160 tokenA, UInt160 tokenB)
        {
            Assert(tokenA.IsValid && tokenB.IsValid, "Invalid A or B Address");
            var pairContract = (byte[])Contract.Call(Factory, "getExchangePair", CallFlags.ReadOnly, new object[] { tokenA, tokenB });
            Assert(pairContract != null && pairContract.Length == 20, "PairContract Not Found", tokenA, tokenB);
            return (UInt160)pairContract;
        }


        /// <summary>
        /// 安全转账，失败则中断退出
        /// </summary>
        /// <param name="token"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        private static void SafeTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount)
        {
            try
            {
                var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { from, to, amount, null });
                Assert(result, "Transfer Fail in Router", token);
            }
            catch (Exception)
            {
                Assert(false, "Transfer Error in Router", token);
            }
        }


        public static BigInteger[] GetReserves(UInt160 tokenA, UInt160 tokenB)
        {
            var reserveData = (ReservesData)Contract.Call(GetExchangePairWithAssert(tokenA, tokenB), "getReserves", CallFlags.All, new object[] { });
            return tokenA.ToUInteger() < tokenB.ToUInteger() ? new BigInteger[] { reserveData.Reserve0, reserveData.Reserve1 } : new BigInteger[] { reserveData.Reserve1, reserveData.Reserve0 };
        }

        /// <summary>
        /// 根据输入A获取兑换B的量（扣除千分之三手续费）
        /// </summary>
        /// <param name="amountIn"></param>
        /// <param name="reserveIn"></param>
        /// <param name="reserveOut"></param>
        /// <returns></returns>
        public static BigInteger GetAmountOut(BigInteger amountIn, BigInteger reserveIn, BigInteger reserveOut)
        {
            //    Assert(amountIn > 0, "amountIn should be positive number");
            //    Assert(reserveIn > 0 && reserveOut > 0, "reserve should be positive number");
            Assert(amountIn > 0 && reserveIn > 0 && reserveOut > 0, "AmountIn Must > 0");

            var amountInWithFee = amountIn * 997;
            var numerator = amountInWithFee * reserveOut;
            var denominator = reserveIn * 1000 + amountInWithFee;
            var amountOut = numerator / denominator;
            return amountOut;
        }

        /// <summary>
        /// 获取链式交易报价
        /// </summary>
        /// <param name="amountIn">第一种token输入量</param>
        /// <param name="paths">兑换链Token列表(正向：tokenIn,token1,token2...,tokenOut）</param>
        /// <returns></returns>
        public static BigInteger[] GetAmountsOut(BigInteger amountIn, UInt160[] paths)
        {
            Assert(paths.Length >= 2, "INVALID_PATH");
            var amounts = new BigInteger[paths.Length];
            amounts[0] = amountIn;
            var max = paths.Length - 1;
            for (var i = 0; i < max; i++)
            {
                var nextIndex = i + 1;
                var data = GetReserves(paths[i], paths[nextIndex]);
                amounts[nextIndex] = GetAmountOut(amounts[i], data[0], data[1]);
            }
            return amounts;
        }

        private static bool SwapTokenInForTokenOut(BigInteger amountIn, BigInteger amountOutMin, UInt160[] paths, BigInteger deadLine)
        {
            //看看有没有超过最后期限
            Assert((BigInteger)Runtime.Time <= deadLine, "Exceeded the deadline");

            var amounts = GetAmountsOut(amountIn, paths);
            Assert(amounts[amounts.Length - 1] >= amountOutMin, "Insufficient AmountOut");

            var pairContract = GetExchangePairWithAssert(paths[0], paths[1]);
            //先将用户的token转入第一个交易对合约
            SafeTransfer(paths[0], Runtime.ExecutingScriptHash, pairContract, amounts[0]);
            Swap(amounts, paths, Runtime.ExecutingScriptHash);
            return true;
        }

        private static void Swap(BigInteger[] amounts, UInt160[] paths, UInt160 toAddress)
        {
            var max = paths.Length - 1;
            for (int i = 0; i < max; i++)
            {
                var input = paths[i];
                var output = paths[i + 1];
                var amountOut = amounts[i + 1];//本轮兑换，合约需要转出的token量

                BigInteger amount0Out = 0;
                BigInteger amount1Out = 0;
                //判定要转出的是token0还是token1
                if (input.ToUInteger() < output.ToUInteger())
                {
                    //input是token0，所以要转出的output是token1
                    amount1Out = amountOut;
                }
                else
                {
                    amount0Out = amountOut;
                }

                var to = toAddress;//最后一轮swap的接收地址
                if (i < paths.Length - 2)
                {
                    //兑换链中每轮的接收地址都是下一对token的pair合约
                    to = GetExchangePairWithAssert(output, paths[i + 2]);
                }

                var pairContract = GetExchangePairWithAssert(input, output);
                //从pair[n,n+1]中转出amount[n+1]到pair[n+1,n+2]
                Contract.Call(pairContract, "swap", CallFlags.All, new object[] { amount0Out, amount1Out, to, null });

            }
        }
    }

    public static class Extension
    {
        /// <summary>
        /// uint160 转为正整数,用于合约地址排序，其它场景勿用
        /// </summary>
        /// <param name="val">合约地址</param>
        /// <returns></returns>
        [OpCode(OpCode.PUSHDATA1, "0100")]
        [OpCode(OpCode.CAT)]
        [OpCode(OpCode.CONVERT, "21")]
        public static extern BigInteger ToUInteger(this UInt160 val);

        /// <summary>
        /// 传入参数转为BigInteger
        /// </summary>
        /// <param name="val">合约地址</param>
        /// <returns></returns>
        [OpCode(OpCode.PUSH0)]
        [OpCode(OpCode.ADD)]
        public static extern BigInteger ToBigInt(this object val);
    }

}
