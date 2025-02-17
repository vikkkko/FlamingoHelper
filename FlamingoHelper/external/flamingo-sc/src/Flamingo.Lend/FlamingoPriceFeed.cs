using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Lend
{
    [DisplayName("FlamingoPriceFeed")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "Flamingo Price Feed")]
    [ContractPermission("*", "*")]
    public class FlamingoPriceFeed : SmartContract
    {
        private static readonly StorageContext ctx = Storage.CurrentContext;
        private static readonly StorageContext rtx = Storage.CurrentReadOnlyContext;

        private static readonly UInt160 INITIAL_OWNER = "14131211100f0e0d0c0b0a090807060504030201";

        // Keys
        private static readonly byte[] OWNER_KEY = new byte[] {0x00};

        private static readonly StorageMap PATH_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x01});

        // The Flamingo Swap Factory gives us the hashes of the Swap Pairs
        private static readonly byte[] SWAP_FACTORY_HASH_KEY = new byte[] {0x02};

        // FLUND needs final separate treatment - FLUND -> FLM -> <other token>
        private static readonly byte[] FLUND_HASH_KEY = new byte[] {0x03};
        private static readonly byte[] FLM_HASH_KEY = new byte[] {0x04};

        // Events
        [DisplayName("SetPath")] public static event Action<UInt160, UInt160, List<UInt160>> OnSetPath;

        /**
         * This event is intended to be fired before aborting the VM. The first argument should be a message and the
         * second argument should be the method name within which it has been fired.
         */
        [DisplayName("Error")]
        public static event Action<string, string> OnError;

        public static void Update(ByteString script, string manifest)
        {
            ValidateOwner("update");
            ContractManagement.Update(script, manifest);
        }

        public static void Destroy()
        {
            ValidateOwner("destroy");
            ContractManagement.Destroy();
        }

        [Safe]
        public static bool Verify()
        {
            return Runtime.CheckWitness(GetOwner());
        }

        // Parameter Methods
        public static void SetOwner(UInt160 owner)
        {
            ValidateOwner("setOwner");
            ValidateHash160(owner, "owner");

            Storage.Put(ctx, OWNER_KEY, owner);
        }

        [Safe]
        public static UInt160 GetOwner()
        {
            var owner = Storage.Get(rtx, OWNER_KEY);
            return owner is null ? INITIAL_OWNER : (UInt160) owner;
        }

        public static void SetSwapFactoryHash(UInt160 swapFactoryHash)
        {
            ValidateOwner("setSwapFactoryHash");
            ValidateHash160(swapFactoryHash, "swapFactoryHash");

            Storage.Put(ctx, SWAP_FACTORY_HASH_KEY, swapFactoryHash);
        }

        [Safe]
        public static UInt160 GetSwapFactoryHash()
        {
            return (UInt160) Storage.Get(ctx, SWAP_FACTORY_HASH_KEY);
        }

        public static void SetFLUNDHash(UInt160 flundHash)
        {
            ValidateOwner("setFLUNDHash");
            ValidateHash160(flundHash, "flundHash");

            Storage.Put(ctx, FLUND_HASH_KEY, flundHash);
        }

        [Safe]
        public static UInt160 GetFLUNDHash()
        {
            return (UInt160) Storage.Get(ctx, FLUND_HASH_KEY);
        }

        public static void SetFLMHash(UInt160 flmHash)
        {
            ValidateOwner("setFLMHash");
            ValidateHash160(flmHash, "flmHash");

            Storage.Put(ctx, FLM_HASH_KEY, flmHash);
        }

        [Safe]
        public static UInt160 GetFLMHash()
        {
            return (UInt160) Storage.Get(ctx, FLM_HASH_KEY);
        }

        [Safe]
        public static List<UInt160> GetPath(UInt160 fromToken, UInt160 toToken)
        {
            ValidateHash160(fromToken, "fromToken");
            ValidateHash160(toToken, "toToken");

            var tokenPairBytes = Helper.Concat(fromToken, toToken);
            ByteString pathByteString = PATH_MAP.Get(tokenPairBytes);
            List<UInt160> path = (List<UInt160>) StdLib.Deserialize(pathByteString);
            return path;
        }

        public static void SetPath(UInt160 fromToken, UInt160 toToken, List<UInt160> path)
        {
            ValidateOwner("setPath");
            ValidateHash160(fromToken, "fromToken");
            ValidateHash160(toToken, "toToken");

            if (path.Count < 2)
                throw new Exception("path must have length >= 2 (i.e. [fromToken, toToken])");
            if (fromToken.Equals(toToken))
                throw new Exception("Cannot set path if fromToken == toToken");
            if (!fromToken.Equals(path[0]))
                throw new Exception("path[0] must be fromToken");
            if (!toToken.Equals(path[path.Count - 1]))
                throw new Exception("path[-1] must be toToken");

            for (int i = 0; i < path.Count; i++)
            {
                UInt160 stop = path[i];
                ValidateHash160(stop, $"path[{i}]");
                if (i > 0 && i < path.Count - 1)
                {
                    if (fromToken.Equals(stop) || toToken.Equals(stop))
                    {
                        throw new Exception($"path[{i}] cannot be fromToken or toToken");
                    }
                }
            }

            var tokenPairBytes = Helper.Concat(fromToken, toToken);
            PATH_MAP.Put(tokenPairBytes, StdLib.Serialize(path));
            OnSetPath(fromToken, toToken, path);
        }

        private static UInt160 GetSwapPair(UInt160 tokenA, UInt160 tokenB)
        {
            var pairBytes = (ByteString) Contract.Call(GetSwapFactoryHash(), "getExchangePair", CallFlags.All, new object[] {tokenA, tokenB});
            UInt160 pairHash = (UInt160) pairBytes;
            ValidateHash160(pairHash, "pairHash");
            return pairHash;
        }

        private static bool GetReversed(UInt160 fromToken, UInt160 toToken, UInt160 pair)
        {
            var token1 = (UInt160) Contract.Call(pair, "getToken1", CallFlags.All);
            return fromToken == token1;
        }

        [Safe]
        public static BigInteger GetPrice(UInt160 baseToken, UInt160 quoteToken, int decimals)
        {
            ValidateHash160(baseToken, "baseToken");
            ValidateHash160(quoteToken, "quoteToken");
            ValidatePositiveNumber(decimals, "decimals");

            if (baseToken.Equals(quoteToken))
                throw new Exception("baseToken and quoteToken cannot be the same token");

            UInt160 flundHash = GetFLUNDHash();
            UInt160 flmHash = GetFLMHash();

            if (flundHash.Equals(baseToken))
            {
                List<BigInteger> flundFlmRatio = GetFlundFlmRatio();
                return GetDirectPrice(flmHash, quoteToken, decimals) * flundFlmRatio[0] / flundFlmRatio[1];
            }
            else if (flundHash.Equals(quoteToken))
            {
                List<BigInteger> flundFlmRatio = GetFlundFlmRatio();
                return GetDirectPrice(baseToken, flmHash, decimals) * flundFlmRatio[1] / flundFlmRatio[0];
            }
            else
            {
                return GetDirectPrice(baseToken, quoteToken, decimals);
            }
        }

        private static BigInteger GetDirectPrice(UInt160 baseToken, UInt160 quoteToken, int decimals)
        {
            List<UInt160> path = GetPath(baseToken, quoteToken);
            if (path.Count < 2)
            {
                throw new Exception($"The path [{baseToken}, {quoteToken}] cannot be found");
            }

            BigInteger price = BigInteger.Pow(10, decimals);
            UInt160 flundHash = GetFLUNDHash();
            if (path[0].Equals(flundHash))
            {
                List<BigInteger> flundFlmRatio = GetFlundFlmRatio();
                price = (price * flundFlmRatio[0]) / flundFlmRatio[1];
            }

            for (int i = 1; i < path.Count; i++)
            {
                UInt160 fromToken = path[i - 1];
                UInt160 toToken = path[i];
                List<BigInteger> ratio = GetRatio(fromToken, toToken);
                price = (price * ratio[0]) / ratio[1];
            }

            return price;
        }

        [Safe]
        public static List<BigInteger> GetRatio(UInt160 fromToken, UInt160 toToken)
        {
            ValidateHash160(fromToken, "fromToken");
            ValidateHash160(toToken, "toToken");

            if (fromToken.Equals(toToken))
                throw new Exception("Cannot fetch ratio if fromToken == toToken");

            UInt160 pairHash = GetSwapPair(fromToken, toToken);
            // FlamingoSwapPairContract pair = new FlamingoSwapPairContract(pairHash);

            bool reversed = GetReversed(fromToken, toToken, pairHash);
            UInt160 token0Hash = reversed ? toToken : fromToken;
            UInt160 token1Hash = reversed ? fromToken : toToken;
            int decimals0 = (int) Contract.Call(token0Hash, "decimals", CallFlags.ReadOnly);
            int decimals1 = (int) Contract.Call(token1Hash, "decimals", CallFlags.ReadOnly);
            BigInteger reserve0 = (BigInteger) Contract.Call(pairHash, "getReserve0", CallFlags.ReadOnly);
            BigInteger reserve1 = (BigInteger) Contract.Call(pairHash, "getReserve1", CallFlags.ReadOnly);

            bool numeratorDecimals = decimals0 > decimals1;
            bool denominatorDecimals = decimals1 > decimals0;
            BigInteger numerator = numeratorDecimals ? reserve1 * BigInteger.Pow(10, decimals0 - decimals1) : reserve1;
            BigInteger denominator = denominatorDecimals ? reserve0 * BigInteger.Pow(10, decimals1 - decimals0) : reserve0;

            List<BigInteger> ratio = new List<BigInteger>();
            if (reversed)
            {
                ratio.Add(denominator);
                ratio.Add(numerator);
            }
            else
            {
                ratio.Add(numerator);
                ratio.Add(denominator);
            }

            return ratio;
        }

        [Safe]
        public static List<BigInteger> GetFlundFlmRatio()
        {
            UInt160 flundHash = GetFLUNDHash();
            var flmInFlund = (BigInteger) Contract.Call(GetFLMHash(), "balanceOf", CallFlags.ReadOnly, new object[] {flundHash});
            var flundCirculating = (BigInteger) Contract.Call(flundHash, "totalSupply", CallFlags.ReadOnly);

            List<BigInteger> ratio = new List<BigInteger>();
            ratio.Add(flmInFlund);
            ratio.Add(flundCirculating);

            return ratio;
        }

        // Helper Methods
        private static void FireErrorAndAbort(string msg, string method)
        {
            OnError(msg, method);
            ExecutionEngine.Abort();
        }

        private static void ValidateHash160(UInt160 hash, string hashName)
        {
            if (hash == null || hash == UInt160.Zero)
                throw new Exception($"The parameter '{hashName}' must be a 20-byte address");
        }

        private static void ValidatePositiveNumber(BigInteger number, string numberName)
        {
            if (number <= 0)
                throw new Exception($"The parameter '{numberName}' must be positive");
        }

        private static void ValidateOwner(string method)
        {
            if (!Runtime.CheckWitness(GetOwner()))
            {
                FireErrorAndAbort("Not authorized", method);
            }
        }
    }
}
