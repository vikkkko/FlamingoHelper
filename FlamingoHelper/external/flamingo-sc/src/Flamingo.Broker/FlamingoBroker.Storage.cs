using System.Numerics;
using Flamingo.Broker.Models;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Broker
{
    public partial class FlamingoBroker
    {
        public static class OwnerStorage
        {
            private static readonly ByteString ownerPrefix = "\x01";

            internal static UInt160 Get()
            {
                var owner = Storage.Get(Storage.CurrentReadOnlyContext, ownerPrefix);
                return owner != null ? (UInt160) owner : InitialOwner;
            }

            internal static void Put(UInt160 address)
            {
                Storage.Put(Storage.CurrentContext, ownerPrefix, address);
            }
        }

        public static class FeeCollectorStorage
        {
            private static readonly ByteString feeCollectorPrefix = "\x02";

            internal static UInt160 Get()
            {
                var feeCollector = Storage.Get(Storage.CurrentReadOnlyContext, feeCollectorPrefix);
                return feeCollector != null ? ((UInt160) feeCollector) : InitialFeeCollector;
            }

            internal static void Put(UInt160 address)
            {
                Storage.Put(Storage.CurrentContext, feeCollectorPrefix, address);
            }
        }

        public static class GasCollectorStorage
        {
            private static readonly ByteString gasCollectorPrefix = "\x03";

            internal static UInt160 Get()
            {
                var feeCollector = Storage.Get(Storage.CurrentReadOnlyContext, gasCollectorPrefix);
                return feeCollector != null ? ((UInt160) feeCollector) : InitialFeeCollector;
            }

            internal static void Put(UInt160 address)
            {
                Storage.Put(Storage.CurrentContext, gasCollectorPrefix, address);
            }
        }

        public static class AMMRouterStorage
        {
            private static readonly ByteString ammRouterPrefix = "\x04";

            internal static UInt160 Get()
            {
                var ammRouter = Storage.Get(Storage.CurrentReadOnlyContext, ammRouterPrefix);
                return ammRouter != null ? ((UInt160) ammRouter) : InitialAMMRouter;
            }

            internal static void Put(UInt160 address)
            {
                Storage.Put(Storage.CurrentContext, ammRouterPrefix, address);
            }
        }

        public static class AMMFactoryStorage
        {
            private static readonly ByteString ammFactoryPrefix = "\x05";

            internal static UInt160 Get()
            {
                var ammFactory = Storage.Get(Storage.CurrentReadOnlyContext, ammFactoryPrefix);
                return ammFactory != null ? ((UInt160) ammFactory) : InitialAMMFactory;
            }

            internal static void Put(UInt160 address)
            {
                Storage.Put(Storage.CurrentContext, ammFactoryPrefix, address);
            }
        }

        public static class AccountStorage
        {
            private static readonly ByteString accountPrefix = "\x06";

            internal static BigInteger Get(UInt160 user, UInt160 token)
            {
                StorageMap accountToTokenAmountMap = new StorageMap(Storage.CurrentReadOnlyContext, accountPrefix);
                return (BigInteger) accountToTokenAmountMap[user + token];
            }

            internal static void Put(UInt160 user, UInt160 token, BigInteger amount)
            {
                StorageMap accountToTokenAmountMap = new StorageMap(Storage.CurrentContext, accountPrefix);
                accountToTokenAmountMap.Put(user + token, amount);
            }

            internal static BigInteger Increase(UInt160 user, UInt160 token, BigInteger amount)
            {
                BigInteger currentAmount = Get(user, token);
                BigInteger newAmount = currentAmount + amount;
                Put(user, token, newAmount);
                return newAmount;
            }

            internal static BigInteger Decrease(UInt160 user, UInt160 token, BigInteger amount)
            {
                BigInteger currentAmount = Get(user, token);
                BigInteger newAmount = currentAmount - amount;
                Put(user, token, newAmount);
                return newAmount;
            }

            internal static Map<UInt160, BigInteger> GetAll(UInt160 user)
            {
                var result = new Map<UInt160, BigInteger>();

                StorageMap accountToTokenAmountMap = new StorageMap(Storage.CurrentReadOnlyContext, accountPrefix);
                var iterator = accountToTokenAmountMap.Find(user, FindOptions.RemovePrefix);
                while (iterator.Next())
                {
                    var content = (object[]) iterator.Value;
                    result[(UInt160) content[0]] = (BigInteger) content[1];
                }

                return result;
            }
        }

        public static class TokenDepositEnabledStorage
        {
            private static readonly ByteString tokenDepositEnabledPrefix = "\x07";

            internal static bool Get(UInt160 token)
            {
                StorageMap tokenToDepositEnabledMap = new StorageMap(Storage.CurrentReadOnlyContext, tokenDepositEnabledPrefix);
                var value = tokenToDepositEnabledMap[token];
                return value != null && (BigInteger) value == 1;
            }

            internal static void Put(UInt160 token, bool enabled)
            {
                StorageMap tokenToDepositEnabledMap = new StorageMap(Storage.CurrentContext, tokenDepositEnabledPrefix);
                tokenToDepositEnabledMap.Put(token, enabled ? 1 : 0);
            }
        }

        public static class TokenWithdrawEnabledStorage
        {
            private static readonly ByteString tokenWithdrawEnabledPrefix = "\x08";

            internal static bool Get(UInt160 token)
            {
                StorageMap tokenToWithdrawEnabledMap = new StorageMap(Storage.CurrentReadOnlyContext, tokenWithdrawEnabledPrefix);
                var value = tokenToWithdrawEnabledMap[token];
                return value != null && (BigInteger) value == 1;
            }

            internal static void Put(UInt160 token, bool enabled)
            {
                StorageMap tokenToWithdrawEnabledMap = new StorageMap(Storage.CurrentContext, tokenWithdrawEnabledPrefix);
                tokenToWithdrawEnabledMap.Put(token, enabled ? 1 : 0);
            }
        }

        public static class FeeBalanceStorage
        {
            private static readonly ByteString feeBalancePrefix = "\x09";

            internal static BigInteger Get(UInt160 token)
            {
                StorageMap tokenToFeeBalanceMap = new StorageMap(Storage.CurrentReadOnlyContext, feeBalancePrefix);
                var value = tokenToFeeBalanceMap[token];
                return value != null ? (BigInteger) value : 0;
            }

            internal static void Put(UInt160 token, BigInteger amount)
            {
                StorageMap tokenToFeeBalanceMap = new StorageMap(Storage.CurrentContext, feeBalancePrefix);
                tokenToFeeBalanceMap.Put(token, amount);
            }

            internal static BigInteger Increase(UInt160 token, BigInteger amount)
            {
                BigInteger currentAmount = Get(token);
                BigInteger newAmount = currentAmount + amount;
                Put(token, newAmount);
                return newAmount;
            }

            internal static BigInteger Decrease(UInt160 token, BigInteger amount)
            {
                BigInteger currentAmount = Get(token);
                BigInteger newAmount = currentAmount - amount;
                Put(token, newAmount);
                return newAmount;
            }
        }

        public static class PairCounterStorage
        {
            private static readonly ByteString pairCounterPrefix = "\x10";

            internal static BigInteger Get()
            {
                var value = Storage.Get(Storage.CurrentReadOnlyContext, pairCounterPrefix);
                return value != null ? ((BigInteger) value) : 0;
            }

            internal static void Put(BigInteger value)
            {
                Storage.Put(Storage.CurrentContext, pairCounterPrefix, value);
            }

            internal static BigInteger Increase()
            {
                BigInteger currentValue = Get();
                BigInteger newValue = currentValue + 1;
                Put(newValue);
                return newValue;
            }
        }

        public static class OrderTradingPausedStorage
        {
            private static readonly ByteString orderTradingPausedPrefix = "\x11";

            internal static bool Get(BigInteger pairId)
            {
                StorageMap pairIdToPausedMap = new StorageMap(Storage.CurrentReadOnlyContext, orderTradingPausedPrefix);
                var value = pairIdToPausedMap[(ByteString) pairId];
                return value == null || (BigInteger) value == 1;
            }

            internal static void Put(BigInteger pairId, bool paused)
            {
                StorageMap pairIdToPausedMap = new StorageMap(Storage.CurrentContext, orderTradingPausedPrefix);
                pairIdToPausedMap.Put((ByteString) pairId, paused ? 1 : 0);
            }
        }

        public static class OrderManagementPausedStorage
        {
            private static readonly ByteString orderManagementPausedPrefix = "\x12";

            internal static bool Get(BigInteger pairId)
            {
                StorageMap pairIdToPausedMap = new StorageMap(Storage.CurrentReadOnlyContext, orderManagementPausedPrefix);
                var value = pairIdToPausedMap[(ByteString) pairId];
                return value == null || (BigInteger) value == 1;
            }

            internal static void Put(BigInteger pairId, bool paused)
            {
                StorageMap pairIdToPausedMap = new StorageMap(Storage.CurrentContext, orderManagementPausedPrefix);
                pairIdToPausedMap.Put((ByteString) pairId, paused ? 1 : 0);
            }
        }

        public static class TreeBitLengthStorage
        {
            private static readonly ByteString treeBitLengthPrefix = "\x13";

            internal static BigInteger Get(BigInteger pairId)
            {
                StorageMap pairIdToTreeBitLengthMap = new StorageMap(Storage.CurrentReadOnlyContext, treeBitLengthPrefix);
                var value = pairIdToTreeBitLengthMap[(ByteString) pairId];
                return (BigInteger) value;
            }

            internal static void Put(BigInteger pairId, BigInteger treeBitLength)
            {
                StorageMap pairIdToTreeBitLengthMap = new StorageMap(Storage.CurrentContext, treeBitLengthPrefix);
                pairIdToTreeBitLengthMap.Put((ByteString) pairId, treeBitLength);
            }
        }

        public static class PricePrecisionStorage
        {
            private static readonly ByteString pricePrecisionPrefix = "\x14";

            internal static BigInteger Get(BigInteger pairId)
            {
                StorageMap pairIdToPricePrecisionMap = new StorageMap(Storage.CurrentReadOnlyContext, pricePrecisionPrefix);
                var value = pairIdToPricePrecisionMap[(ByteString) pairId];
                return (BigInteger) value;
            }

            internal static void Put(BigInteger pairId, BigInteger pricePrecision)
            {
                StorageMap pairIdToPricePrecisionMap = new StorageMap(Storage.CurrentContext, pricePrecisionPrefix);
                pairIdToPricePrecisionMap.Put((ByteString) pairId, pricePrecision);
            }
        }

        public static class BaseTokenStorage
        {
            private static readonly ByteString baseTokenPrefix = "\x15";

            internal static UInt160 Get(BigInteger pairId)
            {
                StorageMap pairIdToBaseTokenMap = new StorageMap(Storage.CurrentReadOnlyContext, baseTokenPrefix);
                var value = pairIdToBaseTokenMap[(ByteString) pairId];
                return (UInt160) value;
            }

            internal static void Put(BigInteger pairId, UInt160 token)
            {
                StorageMap pairIdToBaseTokenMap = new StorageMap(Storage.CurrentContext, baseTokenPrefix);
                pairIdToBaseTokenMap.Put((ByteString) pairId, token);
            }
        }

        public static class QuoteTokenStorage
        {
            private static readonly ByteString quoteTokenPrefix = "\x16";

            internal static UInt160 Get(BigInteger pairId)
            {
                StorageMap pairIdToQuoteTokenMap = new StorageMap(Storage.CurrentReadOnlyContext, quoteTokenPrefix);
                var value = pairIdToQuoteTokenMap[(ByteString) pairId];
                return (UInt160) value;
            }

            internal static void Put(BigInteger pairId, UInt160 token)
            {
                StorageMap pairIdToQuoteTokenMap = new StorageMap(Storage.CurrentContext, quoteTokenPrefix);
                pairIdToQuoteTokenMap.Put((ByteString) pairId, token);
            }
        }

        public static class MakerFeeStorage
        {
            private static readonly ByteString makerFeePrefix = "\x17";

            internal static BigInteger Get(BigInteger pairId)
            {
                StorageMap pairIdToMakerFeeMap = new StorageMap(Storage.CurrentReadOnlyContext, makerFeePrefix);
                var value = pairIdToMakerFeeMap[(ByteString) pairId];
                return value != null ? (BigInteger) value : DefaultMakerFee;
            }

            internal static void Put(BigInteger pairId, BigInteger fee)
            {
                StorageMap pairIdToMakerFeeMap = new StorageMap(Storage.CurrentContext, makerFeePrefix);
                pairIdToMakerFeeMap.Put((ByteString) pairId, fee);
            }
        }

        public static class TakerFeeStorage
        {
            private static readonly ByteString takerFeePrefix = "\x18";

            internal static BigInteger Get(BigInteger pairId)
            {
                StorageMap pairIdToTakerFeeMap = new StorageMap(Storage.CurrentReadOnlyContext, takerFeePrefix);
                var value = pairIdToTakerFeeMap[(ByteString) pairId];
                return value != null ? (BigInteger) value : DefaultTakerFee;
            }

            internal static void Put(BigInteger pairId, BigInteger fee)
            {
                StorageMap pairIdToTakerFeeMap = new StorageMap(Storage.CurrentContext, takerFeePrefix);
                pairIdToTakerFeeMap.Put((ByteString) pairId, fee);
            }
        }

        public static class SellTreeStorage
        {
            private static readonly ByteString sellTreePrefix = "\x19";

            internal static PriceNode Get(BigInteger pairId, BigInteger nodeIndex)
            {
                StorageMap nodeIndexToPriceNodeMap = new StorageMap(Storage.CurrentReadOnlyContext, sellTreePrefix);
                var key = (ByteString) (nodeIndex + (pairId << 32));
                var data = nodeIndexToPriceNodeMap.Get(key);
                return data != null ? (PriceNode) StdLib.Deserialize(data) : new PriceNode();
            }

            internal static void Put(BigInteger pairId, BigInteger nodeIndex, PriceNode priceNode)
            {
                StorageMap nodeIndexToPriceNodeMap = new StorageMap(Storage.CurrentContext, sellTreePrefix);
                var key = (ByteString) (nodeIndex + (pairId << 32));
                nodeIndexToPriceNodeMap.Put(key, StdLib.Serialize(priceNode));
            }
        }

        public static class BuyTreeStorage
        {
            private static readonly ByteString buyTreePrefix = "\x20";

            internal static PriceNode Get(BigInteger pairId, BigInteger nodeIndex)
            {
                StorageMap nodeIndexToPriceNodeMap = new StorageMap(Storage.CurrentReadOnlyContext, buyTreePrefix);
                var key = (ByteString) (nodeIndex + (pairId << 32));
                var data = nodeIndexToPriceNodeMap.Get(key);
                return data != null ? (PriceNode) StdLib.Deserialize(data) : new PriceNode();
            }

            internal static void Put(BigInteger pairId, BigInteger nodeIndex, PriceNode priceNode)
            {
                StorageMap nodeIndexToPriceNodeMap = new StorageMap(Storage.CurrentContext, buyTreePrefix);
                var key = (ByteString) (nodeIndex + (pairId << 32));
                nodeIndexToPriceNodeMap.Put(key, StdLib.Serialize(priceNode));
            }
        }

        public static class OrderIdStorage
        {
            private static readonly ByteString orderIdPrefix = "\x21";

            internal static BigInteger Get(BigInteger pairId)
            {
                StorageMap pairToOrderIdMap = new StorageMap(Storage.CurrentReadOnlyContext, orderIdPrefix);
                var value = pairToOrderIdMap[(ByteString) pairId];
                return value != null ? (BigInteger) value : 0;
            }

            internal static BigInteger Current(BigInteger pairId)
            {
                return Get(pairId);
            }

            internal static BigInteger Increase(BigInteger pairId)
            {
                var orderId = Get(pairId) + 1;

                StorageMap pairToOrderIdMap = new StorageMap(Storage.CurrentContext, orderIdPrefix);
                pairToOrderIdMap.Put((ByteString) pairId, orderId);

                return orderId;
            }
        }

        public static class OrderDataStorage
        {
            private static readonly ByteString orderDataPrefix = "\x22";

            private static ByteString Key(BigInteger pairId, BigInteger orderId) => StdLib.Serialize((pairId, orderId));

            internal static Order Get(BigInteger pairId, BigInteger orderId)
            {
                StorageMap orderIdToOrderDataMap = new StorageMap(Storage.CurrentReadOnlyContext, orderDataPrefix);
                var data = orderIdToOrderDataMap[Key(pairId, orderId)];
                return data != null ? (Order) StdLib.Deserialize(data) : null;
            }

            internal static void Put(BigInteger pairId, BigInteger orderId, Order order)
            {
                StorageMap orderIdToOrderDataMap = new StorageMap(Storage.CurrentContext, orderDataPrefix);
                orderIdToOrderDataMap.Put(Key(pairId, orderId), StdLib.Serialize(order));
            }
        }

        public static class UserOrderStorage
        {
            private static readonly ByteString userOrderPrefix = "\x23";
            public static readonly int PairIdKeyLength = 3;
            public static readonly int PageKeyLength = 4;

            private static ByteString Key(BigInteger page, BigInteger pairId, BigInteger orderId) => ToFixedLengthBytes(page, PageKeyLength) + ToFixedLengthBytes(pairId, PairIdKeyLength) + (ByteString) orderId.ToByteArray();

            internal static BigInteger GetPairIdFromKey(ByteString key)
            {
                var keyBytes = (byte[]) key;
                var bytesReversed = keyBytes.Range(0, PairIdKeyLength).Reverse();
                return new BigInteger(bytesReversed);
            }

            internal static void Put(UInt160 user, BigInteger pairId, BigInteger orderId, BigInteger page)
            {
                StorageMap userOrderIdToOrderIdMap = new StorageMap(Storage.CurrentContext, userOrderPrefix);
                userOrderIdToOrderIdMap.Put(user + Key(page, pairId, orderId), orderId);
            }

            internal static Iterator FindAll(UInt160 user)
            {
                StorageMap userOrderIdToOrderIdMap = new StorageMap(Storage.CurrentReadOnlyContext, userOrderPrefix);
                return userOrderIdToOrderIdMap.Find(user, FindOptions.RemovePrefix);
            }

            internal static ByteString ToFixedLengthBytes(BigInteger value, int length)
            {
                // Convert the BigInteger to a byte array
                byte[] bytes = value.ToByteArray();

                // Ensure the byte array is exactly the desired length
                ExecutionEngine.Assert(bytes.Length <= length, "Value is too large to fit in the desired length");

                var result = new byte[length];

                int start = length - bytes.Length;
                for (int i = 0; i < bytes.Length; i++)
                {
                    result[start + i] = bytes[i];
                }

                return (ByteString) result;
            }
        }

        public static class BuyBaseAmountPlacedStorage
        {
            private static readonly ByteString buyBaseAmountPlacedPrefix = "\x24";

            private static ByteString Key(BigInteger pairId, BigInteger nodeIndex) => StdLib.Serialize((pairId, nodeIndex));

            internal static BigInteger Get(BigInteger pairId, BigInteger nodeIndex)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentReadOnlyContext, buyBaseAmountPlacedPrefix);
                var data = nodeIndexToAmountMap[Key(pairId, nodeIndex)];
                return data != null ? (BigInteger) data : 0;
            }

            internal static void Increase(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                BigInteger currentAmount = Get(pairId, nodeIndex);

                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, buyBaseAmountPlacedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), currentAmount + amount);
            }
        }

        public static class BuyQuoteAmountPlacedStorage
        {
            private static readonly ByteString buyQuoteAmountPlacedPrefix = "\x25";

            private static ByteString Key(BigInteger pairId, BigInteger nodeIndex) => StdLib.Serialize((pairId, nodeIndex));

            internal static BigInteger Get(BigInteger pairId, BigInteger nodeIndex)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentReadOnlyContext, buyQuoteAmountPlacedPrefix);
                var data = nodeIndexToAmountMap[Key(pairId, nodeIndex)];
                return data != null ? (BigInteger) data : 0;
            }

            internal static void Increase(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                BigInteger currentAmount = Get(pairId, nodeIndex);

                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, buyQuoteAmountPlacedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), currentAmount + amount);
            }
        }

        public static class BuyBaseAmountExecutedStorage
        {
            private static readonly ByteString buyBaseAmountExecutedPrefix = "\x26";

            private static ByteString Key(BigInteger pairId, BigInteger nodeIndex) => StdLib.Serialize((pairId, nodeIndex));

            internal static BigInteger Get(BigInteger pairId, BigInteger nodeIndex)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentReadOnlyContext, buyBaseAmountExecutedPrefix);
                var data = nodeIndexToAmountMap[Key(pairId, nodeIndex)];
                return data != null ? (BigInteger) data : 0;
            }

            internal static void Put(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, buyBaseAmountExecutedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), amount);
            }

            internal static void Increase(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                BigInteger currentAmount = Get(pairId, nodeIndex);

                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, buyBaseAmountExecutedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), currentAmount + amount);
            }
        }

        public static class BuyQuoteAmountExecutedStorage
        {
            private static readonly ByteString buyQuoteAmountExecutedPrefix = "\x27";

            private static ByteString Key(BigInteger pairId, BigInteger nodeIndex) => StdLib.Serialize((pairId, nodeIndex));

            internal static BigInteger Get(BigInteger pairId, BigInteger nodeIndex)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentReadOnlyContext, buyQuoteAmountExecutedPrefix);
                var data = nodeIndexToAmountMap[Key(pairId, nodeIndex)];
                return data != null ? (BigInteger) data : 0;
            }

            internal static void Put(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, buyQuoteAmountExecutedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), amount);
            }

            internal static void Increase(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                BigInteger currentAmount = Get(pairId, nodeIndex);

                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, buyQuoteAmountExecutedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), currentAmount + amount);
            }
        }

        public static class SellBaseAmountPlacedStorage
        {
            private static readonly ByteString sellBaseAmountPlacedPrefix = "\x28";

            private static ByteString Key(BigInteger pairId, BigInteger nodeIndex) => StdLib.Serialize((pairId, nodeIndex));

            internal static BigInteger Get(BigInteger pairId, BigInteger nodeIndex)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentReadOnlyContext, sellBaseAmountPlacedPrefix);
                var data = nodeIndexToAmountMap[Key(pairId, nodeIndex)];
                return data != null ? (BigInteger) data : 0;
            }

            internal static void Increase(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                BigInteger currentAmount = Get(pairId, nodeIndex);

                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, sellBaseAmountPlacedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), currentAmount + amount);
            }
        }

        public static class SellQuoteAmountPlacedStorage
        {
            private static readonly ByteString sellQuoteAmountPlacedPrefix = "\x29";

            private static ByteString Key(BigInteger pairId, BigInteger nodeIndex) => StdLib.Serialize((pairId, nodeIndex));

            internal static BigInteger Get(BigInteger pairId, BigInteger nodeIndex)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentReadOnlyContext, sellQuoteAmountPlacedPrefix);
                var data = nodeIndexToAmountMap[Key(pairId, nodeIndex)];
                return data != null ? (BigInteger) data : 0;
            }

            internal static void Increase(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                BigInteger currentAmount = Get(pairId, nodeIndex);

                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, sellQuoteAmountPlacedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), currentAmount + amount);
            }
        }

        public static class SellBaseAmountExecutedStorage
        {
            private static readonly ByteString sellBaseAmountExecutedPrefix = "\x30";

            private static ByteString Key(BigInteger pairId, BigInteger nodeIndex) => StdLib.Serialize((pairId, nodeIndex));

            internal static BigInteger Get(BigInteger pairId, BigInteger nodeIndex)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentReadOnlyContext, sellBaseAmountExecutedPrefix);
                var data = nodeIndexToAmountMap[Key(pairId, nodeIndex)];
                return data != null ? (BigInteger) data : 0;
            }

            internal static void Put(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, sellBaseAmountExecutedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), amount);
            }

            internal static void Increase(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                BigInteger currentAmount = Get(pairId, nodeIndex);

                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, sellBaseAmountExecutedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), currentAmount + amount);
            }
        }

        public static class SellQuoteAmountExecutedStorage
        {
            private static readonly ByteString sellQuoteAmountExecutedPrefix = "\x31";

            private static ByteString Key(BigInteger pairId, BigInteger nodeIndex) => StdLib.Serialize((pairId, nodeIndex));

            internal static BigInteger Get(BigInteger pairId, BigInteger nodeIndex)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentReadOnlyContext, sellQuoteAmountExecutedPrefix);
                var data = nodeIndexToAmountMap[Key(pairId, nodeIndex)];
                return data != null ? (BigInteger) data : 0;
            }

            internal static void Put(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, sellQuoteAmountExecutedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), amount);
            }

            internal static void Increase(BigInteger pairId, BigInteger nodeIndex, BigInteger amount)
            {
                BigInteger currentAmount = Get(pairId, nodeIndex);

                StorageMap nodeIndexToAmountMap = new StorageMap(Storage.CurrentContext, sellQuoteAmountExecutedPrefix);
                nodeIndexToAmountMap.Put(Key(pairId, nodeIndex), currentAmount + amount);
            }
        }

        public static class TokenDecimalPlacesStorage
        {
            private static readonly ByteString tokenDecimalsPrefix = "\x32";

            internal static BigInteger Get(UInt160 token)
            {
                StorageMap tokenToDecimalPlacesMap = new StorageMap(Storage.CurrentReadOnlyContext, tokenDecimalsPrefix);
                var value = tokenToDecimalPlacesMap[token];
                return value != null ? (BigInteger) value : 0;
            }

            internal static void Put(UInt160 token, BigInteger decimalPlaces)
            {
                StorageMap tokenToDecimalPlacesMap = new StorageMap(Storage.CurrentContext, tokenDecimalsPrefix);
                var previousDecimalPlaces = tokenToDecimalPlacesMap[token];
                if (previousDecimalPlaces != null)
                {
                    ExecutionEngine.Assert((BigInteger) previousDecimalPlaces == decimalPlaces, "Token decimal places cannot be changed");
                }

                tokenToDecimalPlacesMap.Put(token, decimalPlaces);
            }
        }

        public static class GasToBurnStorage
        {
            private static readonly ByteString gasToBurnPrefix = "\x33";

            internal static BigInteger Get(BigInteger pairId)
            {
                StorageMap pairIdToGasToBurnMap = new StorageMap(Storage.CurrentReadOnlyContext, gasToBurnPrefix);
                var value = pairIdToGasToBurnMap[(ByteString) pairId];
                return value != null ? (int) (BigInteger) value : 0;
            }

            internal static void Put(BigInteger pairId, BigInteger gasToBurnAmount)
            {
                StorageMap pairIdToGasToBurnMap = new StorageMap(Storage.CurrentContext, gasToBurnPrefix);
                pairIdToGasToBurnMap.Put((ByteString) pairId, gasToBurnAmount);
            }
        }

        public static class HighestBuyRoundStorage
        {
            private static readonly ByteString highestBuyRoundPrefix = "\x34";

            internal static BigInteger Get(BigInteger pairId)
            {
                StorageMap pairIdToHighestBuyRoundMap = new StorageMap(Storage.CurrentReadOnlyContext, highestBuyRoundPrefix);
                var value = pairIdToHighestBuyRoundMap[(ByteString) pairId];
                return value != null ? (int) (BigInteger) value : 0;
            }

            internal static void Put(BigInteger pairId, BigInteger round)
            {
                StorageMap pairIdToHighestBuyRoundMap = new StorageMap(Storage.CurrentContext, highestBuyRoundPrefix);
                pairIdToHighestBuyRoundMap.Put((ByteString) pairId, round);
            }
        }

        public static class HighestSellRoundStorage
        {
            private static readonly ByteString highestSellRoundPrefix = "\x35";

            internal static BigInteger Get(BigInteger pairId)
            {
                StorageMap pairIdToHighestSellRoundMap = new StorageMap(Storage.CurrentReadOnlyContext, highestSellRoundPrefix);
                var value = pairIdToHighestSellRoundMap[(ByteString) pairId];
                return value != null ? (int) (BigInteger) value : 0;
            }

            internal static void Put(BigInteger pairId, BigInteger round)
            {
                StorageMap pairIdToHighestSellRoundMap = new StorageMap(Storage.CurrentContext, highestSellRoundPrefix);
                pairIdToHighestSellRoundMap.Put((ByteString) pairId, round);
            }
        }

        public static class CancelledOrdersCountAtPriceStorage
        {
            private static readonly ByteString cancelledOrdersCountAtPricePrefix = "\x36";

            internal static BigInteger Get(ByteString treeKey)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentReadOnlyContext, cancelledOrdersCountAtPricePrefix);
                var value = storageMap[treeKey];
                return value != null ? (int) (BigInteger) value : 0;
            }

            internal static BigInteger Increment(ByteString treeKey)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentContext, cancelledOrdersCountAtPricePrefix);
                var currentCount = Get(treeKey);
                var newCount = currentCount + 1;
                storageMap.Put(treeKey, newCount);
                return newCount;
            }
        }

        public static class CancelledAmountAtPriceStorage
        {
            private static readonly ByteString cancelledBaseAmountAtPricePrefix = "\x37";
            private static readonly ByteString cancelledQuoteAmountAtPricePrefix = "\x38";

            private static ByteString Key(ByteString treeKey, BigInteger nodeIndex) => StdLib.Serialize((treeKey, nodeIndex));

            internal static (BigInteger, BigInteger) Get(ByteString treeKey, BigInteger nodeIndex)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentReadOnlyContext, cancelledBaseAmountAtPricePrefix);
                var value = storageMap[Key(treeKey, nodeIndex)];
                var baseAmount = value != null ? (BigInteger) value : 0;

                StorageMap storageMap2 = new StorageMap(Storage.CurrentReadOnlyContext, cancelledQuoteAmountAtPricePrefix);
                var value2 = storageMap2[Key(treeKey, nodeIndex)];
                var quoteAmount = value2 != null ? (BigInteger) value2 : 0;

                return (baseAmount, quoteAmount);
            }

            internal static void Put(ByteString treeKey, BigInteger nodeIndex, BigInteger baseAmount, BigInteger quoteAmount)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentContext, cancelledBaseAmountAtPricePrefix);
                storageMap.Put(Key(treeKey, nodeIndex), baseAmount);

                StorageMap storageMap2 = new StorageMap(Storage.CurrentContext, cancelledQuoteAmountAtPricePrefix);
                storageMap2.Put(Key(treeKey, nodeIndex), quoteAmount);
            }

            internal static void Increase(ByteString treeKey, BigInteger nodeIndex, BigInteger baseAmount, BigInteger quoteAmount)
            {
                var (currentBaseAmount, currentQuoteAmount) = Get(treeKey, nodeIndex);
                Put(treeKey, nodeIndex, currentBaseAmount + baseAmount, currentQuoteAmount + quoteAmount);
            }
        }

        public static class CancelledTreeColumnCountStorage
        {
            private static readonly ByteString cancelledTreeColumnCountPrefix = "\x39";

            internal static BigInteger Get(ByteString treeKey)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentReadOnlyContext, cancelledTreeColumnCountPrefix);
                var value = storageMap[treeKey];
                return value != null ? (int) (BigInteger) value : 1;
            }

            internal static void Put(ByteString treeKey, BigInteger count)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentContext, cancelledTreeColumnCountPrefix);
                storageMap.Put(treeKey, count);
            }
        }

        public static class UserOrderCountStorage
        {
            private static readonly ByteString userOrderCountPrefix = "\x40";

            internal static BigInteger Get(UInt160 user)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentReadOnlyContext, userOrderCountPrefix);
                var value = storageMap[user];
                return value != null ? (int) (BigInteger) value : 0;
            }

            internal static BigInteger Increment(UInt160 user)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentContext, userOrderCountPrefix);
                var currentCount = Get(user);
                var newCount = currentCount + 1;
                storageMap.Put(user, newCount);
                return newCount;
            }
        }

        public static class TokenBalanceStorage
        {
            private static readonly ByteString tokenBalancePrefix = "\x41";

            internal static BigInteger Get(UInt160 token)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentReadOnlyContext, tokenBalancePrefix);
                var value = storageMap[token];
                return value != null ? (BigInteger) value : 0;
            }

            internal static void Put(UInt160 token, BigInteger amount)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentContext, tokenBalancePrefix);
                storageMap.Put(token, amount);
            }

            internal static BigInteger Increase(UInt160 token, BigInteger amount)
            {
                BigInteger currentAmount = Get(token);
                BigInteger newAmount = currentAmount + amount;
                Put(token, newAmount);
                return newAmount;
            }

            internal static BigInteger Decrease(UInt160 token, BigInteger amount)
            {
                BigInteger currentAmount = Get(token);
                BigInteger newAmount = currentAmount - amount;
                Put(token, newAmount);
                return newAmount;
            }
        }

        public static class ModeratorsStorage
        {
            private static readonly ByteString moderatorsPrefix = "\x42";

            internal static bool Get(UInt160 address)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentReadOnlyContext, moderatorsPrefix);
                var value = storageMap[address];
                return value != null && (BigInteger) value == 1;
            }

            internal static void Put(UInt160 address, bool isModerator)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentContext, moderatorsPrefix);
                storageMap.Put(address, isModerator ? 1 : 0);
            }
        }

        public static class FusdFundAddressStorage
        {
            private static readonly ByteString fusdFundAddressPrefix = "\x43";

            internal static UInt160 Get()
            {
                var value = Storage.Get(Storage.CurrentReadOnlyContext, fusdFundAddressPrefix);
                return value != null ? (UInt160) value : UInt160.Zero;
            }

            internal static void Put(UInt160 address)
            {
                Storage.Put(Storage.CurrentContext, fusdFundAddressPrefix, address);
            }

            internal static bool IsSet()
            {
                return Get() != UInt160.Zero;
            }
        }

        public static class FusdFundFeePercentageStorage
        {
            private static readonly ByteString fusdFundFeePercentagePrefix = "\x44";

            internal static BigInteger Get()
            {
                var value = Storage.Get(Storage.CurrentReadOnlyContext, fusdFundFeePercentagePrefix);
                return value != null ? (BigInteger) value : DefaultFusdFundFeePercentage;
            }

            internal static void Put(BigInteger feePercentage)
            {
                Storage.Put(Storage.CurrentContext, fusdFundFeePercentagePrefix, feePercentage);
            }
        }

        public static class PriceSnapshotStorage
        {
            private static readonly ByteString priceSnapshotPrefix = "\x45";

            private static ByteString Key(BigInteger pairId, BigInteger currentBlockIndex) => StdLib.Serialize((pairId, currentBlockIndex));

            internal static (BigInteger previousSnapshotBlockIndex, BigInteger price) Get(BigInteger pairId, BigInteger currentBlockIndex)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentReadOnlyContext, priceSnapshotPrefix);
                var value = storageMap[Key(pairId, currentBlockIndex)];
                return value != null ? ((BigInteger, BigInteger)) StdLib.Deserialize(value) : (0, 0);
            }

            internal static void Put(BigInteger pairId, BigInteger currentBlockIndex, BigInteger previousSnapshotBlockIndex, BigInteger price)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentContext, priceSnapshotPrefix);
                storageMap.Put(Key(pairId, currentBlockIndex), StdLib.Serialize((previousSnapshotBlockIndex, price)));
            }
        }

        public static class LastPriceBlockIndexStorage
        {
            private static readonly ByteString lastPriceBlockIndexPrefix = "\x46";

            internal static BigInteger Get(BigInteger pairId)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentReadOnlyContext, lastPriceBlockIndexPrefix);
                var value = storageMap[(ByteString) pairId];
                return value != null ? (BigInteger) value : 0;
            }

            internal static void Put(BigInteger pairId, BigInteger blockIndex)
            {
                StorageMap storageMap = new StorageMap(Storage.CurrentContext, lastPriceBlockIndexPrefix);
                storageMap.Put((ByteString) pairId, blockIndex);
            }
        }
    }
}
