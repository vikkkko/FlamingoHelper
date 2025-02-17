using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Flamingo.Lend
{
    [DisplayName("FTokenVault")]
    [ManifestExtra("Author", "Flamingo Finance")]
    [ManifestExtra("Email", "developer@flamingo.finance")]
    [ManifestExtra("Description", "Flamingo Stablecoin Vault")]
    [ContractPermission("*", "*")]
    public class FTokenVault : SmartContract
    {
        public class Nep17Payload
        {
            public string Action;
            public UInt160 TokenHash;
        }

        public class LiquidateOCPPayload
        {
            public string Action;
            public UInt160 CollateralHash;
            public UInt160 Liquidatee;
        }

        public class LiquidatePayload
        {
            public string Action;
            public UInt160 CollateralHash;
            public UInt160 Liquidatee;
            public string Json64;
            public string Signature64;
        }

        private static readonly StorageContext ctx = Storage.CurrentContext;
        private static readonly StorageContext rtx = Storage.CurrentReadOnlyContext;

        private static readonly UInt160 INITIAL_OWNER = "14131211100f0e0d0c0b0a090807060504030201";

        private const string ACTION_DEPOSIT = "DEPOSIT";
        private const string ACTION_REPAY = "REPAY";
        private const string ACTION_LIQUIDATE = "LIQUIDATE";
        private const string ACTION_LIQUIDATE_OCP = "LIQUIDATE_OCP";
        private const string ACTION_MINT = "MINT";
        private const string FLM = "FLM";

        private static readonly BigInteger FLOAT_MULTIPLIER = 1000000000000000000;
        private static readonly BigInteger MILLIS_IN_YEAR = 31536000000;
        private static readonly BigInteger MILLIS_IN_DAY = 24 * 60 * 60 * 1000;
        private const int PERCENT = 100;
        private const int BASIS_POINTS = 10_000;

        // LTV default values
        private const int INITIAL_MAX_LOAN_TO_VALUE = 40;
        private const int INITIAL_MAX_INIT_LOAN_TO_VALUE = 35;

        // Liquidation default values
        private const int INITIAL_LIQUIDATION_LIMIT = 50;
        private const int INITIAL_LIQUIDATION_PENALTY = 15;
        private const int INITIAL_LIQUIDATION_BONUS = 10;
        private const int INITIAL_LIQUIDATION_FLUND_ALLOCATION = 40;
        private const int INITIAL_LIQUIDATION_LRB_ALLOCATION = 20;

        // Interest rate default values
        private const int INITIAL_ANNUAL_INTEREST = 6;
        private const int INITIAL_INTEREST_FLUND_ALLOCATION = 47;
        private const int INITIAL_INTEREST_LRB_ALLOCATION = 33;

        // Expressed in percent
        private const int INITIAL_MAX_PRICE_DIFF = 3;

        private const int MAX_PAGE_SIZE = 128;
        private const int MAX_BALANCES_SIZE = 128;
        private const int MAX_SIGNERS_SIZE = 16;
        private static readonly BigInteger INITIAL_MINT_LIMIT_PER_BLOCK = 1000000000000;
        private static readonly BigInteger INITIAL_MINT_LIMIT_PER_DAY = 1000000000000;
        private static readonly BigInteger INITIAL_LIQUIDATE_LIMIT_PER_BLOCK = 1000000000000;

        // The decimal places to use when querying on-chain prices
        private const int ON_CHAIN_DECIMALS = 20;

        // Keys
        private static readonly byte[] OWNER_KEY = {0x00};

        private static readonly StorageMap SUPPORTED_COLLATERAL_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x01});
        private static readonly StorageMap SUPPORTED_FTOKEN_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x02});
        private static readonly StorageMap MAX_LOAN_TO_VALUE_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x03});
        private static readonly StorageMap MAX_INIT_LOAN_TO_VALUE_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x04});
        private static readonly StorageMap SECURITY_FUND_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x05});
        private static readonly StorageMap LRB_FUND_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x06});
        private static readonly StorageMap LIQUIDATION_LIMIT_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x07});
        private static readonly StorageMap LIQUIDATION_PENALTY_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x08});
        private static readonly StorageMap LIQUIDATION_BONUS_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x09});
        private static readonly StorageMap LIQUIDATION_FLUND_ALLOCATION_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x0a});
        private static readonly StorageMap LIQUIDATION_LRB_ALLOCATION_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x0b});
        private static readonly StorageMap ANNUAL_INTEREST_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x0c});
        private static readonly StorageMap INTEREST_FLUND_ALLOCATION_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x0d});
        private static readonly StorageMap INTEREST_LRB_ALLOCATION_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x0e});
        private static readonly StorageMap INTEREST_MULTIPLIER_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x0f});
        private static readonly StorageMap INTEREST_LAST_TIMESTAMP_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x10});

        // Collateral parameters
        // NOTE: This order is important so liquidators can scan for collateralHash + fTokenHash
        // collateralHash + fTokenHash + account -> collateral quantity
        private static readonly byte[] COLLATERAL_PREFIX = new byte[] {0x11};

        // collateralHash + fTokenHash + account -> fToken quantity
        private static readonly byte[] FTOKEN_PREFIX = new byte[] {0x12};

        // collateralHash + fTokenHash + account -> principal quantity
        private static readonly byte[] PRINCIPAL_PREFIX = new byte[] {0x13};

        // collateralHash + fTokenHash + account -> cumulative principal quantity
        private static readonly byte[] CUMULATIVE_PRINCIPAL_PREFIX = new byte[] {0x14};

        // collateralHash + fTokenHash + account -> cumulative repayment quantity
        private static readonly byte[] CUMULATIVE_REPAYMENT_PREFIX = new byte[] {0x15};

        // Other parameters
        // The signer(s) of the price feed
        private static readonly StorageMap SIGNER_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x16});

        // The GAS owner
        private static readonly byte[] GAS_ADMIN_KEY = new byte[] {0x17};

        // The LRB Fund owner
        private static readonly byte[] LRB_FUND_ADMIN_KEY = new byte[] {0x18};

        // The security fund owner
        private static readonly byte[] SECURITY_FUND_ADMIN_KEY = new byte[] {0x19};

        // The bNEO script hash
        private static readonly byte[] BNEO_HASH_KEY = new byte[] {0x1a};

        // The FLUND script hash
        private static readonly byte[] FLUND_HASH_KEY = new byte[] {0x1b};

        // The LRB Fund script hash
        private static readonly byte[] LRB_FUND_HASH_KEY = new byte[] {0x1c};

        // collateralHash + fTokenHash + account -> last used expiry time
        private static readonly byte[] PRICE_EXPIRY_PREFIX = new byte[] {0x1d};

        // Emergency pause
        private static readonly byte[] PAUSED_KEY = new byte[] {0x1e};

        // The on-chain price feed hash
        private static readonly byte[] PRICE_FEED_HASH_KEY = new byte[] {0x1f};

        // The reference USD token hash
        private static readonly byte[] QUOTE_TOKEN_HASH_KEY = new byte[] {0x20};

        // The maximum allowed relative difference between on-chain and off-chain prices
        private static readonly StorageMap MAX_PRICE_DIFF_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x21});

        // A whitelist of liquidators who are allowed to only use on-chain collateral prices
        private static readonly StorageMap LIQUIDATOR_WHITELIST_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x22});

        private static readonly StorageMap MINT_LIMIT_PER_BLOCK_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x23});

        // The last block height at which an FToken mint happened
        private static readonly StorageMap LAST_MINTED_BLOCK_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x24});

        // The quantity of FTokens that were minted at the latest minted block
        private static readonly StorageMap LAST_MINTED_QUANTITY_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x25});

        private static readonly StorageMap LIQUIDATE_LIMIT_PER_BLOCK_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x26});

        // The last block height at which an FToken mint happened
        private static readonly StorageMap LAST_LIQUIDATED_BLOCK_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x27});

        // The quantity of FTokens that were minted at the latest minted block
        private static readonly StorageMap LAST_LIQUIDATED_QUANTITY_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x28});

        private static readonly StorageMap RETIRED_COLLATERAL_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x29});
        private static readonly StorageMap RETIRED_FTOKEN_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x30});

        private static readonly StorageMap MINT_LIMIT_PER_DAY_MAP = new StorageMap(Storage.CurrentContext, new byte[] {0x31});

        private static readonly byte[] LAST_MINTED_TIME_PREFIX = new byte[] {0x32};

        private static readonly byte[] STAKING_CONTRACT_KEY = new byte[] {0x33};

        // Events
        [DisplayName("DepositCollateral")] public static event DepositCollateralDelegate OnDepositCollateral;

        public delegate void DepositCollateralDelegate(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, BigInteger depositQuantity);

        [DisplayName("WithdrawCollateral")] public static event WithdrawCollateralDelegate OnWithdrawCollateral;

        public delegate void WithdrawCollateralDelegate(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, BigInteger withdrawQuantity);

        [DisplayName("MintFToken")] public static event MintFTokenDelegate OnMintFToken;

        public delegate void MintFTokenDelegate(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, BigInteger mintQuantity);

        [DisplayName("RepayFToken")] public static event RepayFTokenDelegate OnRepayFToken;

        public delegate void RepayFTokenDelegate(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, BigInteger clippedRepayQuantity, BigInteger principalRepayQuantity);

        [DisplayName("LiquidateCollateral")] public static event LiquidateCollateralDelegate OnLiquidate;

        public delegate void LiquidateCollateralDelegate(UInt160 collateralHash, UInt160 fTokenHash, UInt160 liquidator, UInt160 liquidatee, BigInteger clippedLiquidateFunds, BigInteger totalLiquidateQuantity,
            BigInteger appliedLiquidateFunds);

        // This event is intended to be fired before aborting the VM. The first argument should be a
        // message and the second argument should be the method name within which it has been fired.
        [DisplayName("Error")] public static event ErrorDelegate OnError;

        public delegate void ErrorDelegate(string msg, string method);

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

        public static bool Verify()
        {
            return Runtime.CheckWitness(GetOwner());
        }

        [Safe]
        public static BigInteger GetInterestMultiplier(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            BigInteger unscaledInterestMultiplier = (BigInteger) INTEREST_MULTIPLIER_MAP.Get(collateralHash);
            BigInteger lastTimestamp = GetLastTimestamp(collateralHash);
            BigInteger newTimestamp = Runtime.Time;
            BigInteger diffTimestamp = newTimestamp - lastTimestamp;
            BigInteger annualInterest = GetAnnualInterest(collateralHash);
            BigInteger interestAccrued = (FLOAT_MULTIPLIER * diffTimestamp * annualInterest) / (MILLIS_IN_YEAR * PERCENT);

            return ((FLOAT_MULTIPLIER + interestAccrued) * unscaledInterestMultiplier) / FLOAT_MULTIPLIER;
        }

        [Safe]
        public static BigInteger GetLastTimestamp(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            return (BigInteger) INTEREST_LAST_TIMESTAMP_MAP.Get(collateralHash);
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            // Do not take further action on GAS claims
            UInt160 callingHash = Runtime.CallingScriptHash;
            if (callingHash == GAS.Hash)
            {
                return;
            }

            Nep17Payload callParams = (Nep17Payload) data;
            string action = callParams.Action;

            // Deposit collateral
            if (action == ACTION_DEPOSIT)
            {
                UInt160 collateralHash = callingHash;
                UInt160 fTokenHash = callParams.TokenHash;
                DepositCollateral(collateralHash, fTokenHash, from, amount);
            }
            // Repay FTokens
            else if (action == ACTION_REPAY)
            {
                UInt160 collateralHash = callParams.TokenHash;
                UInt160 fTokenHash = callingHash;
                RepayFToken(collateralHash, fTokenHash, from, amount);
            }
            // Liquidate collateral
            else if (action == ACTION_LIQUIDATE)
            {
                LiquidatePayload liquidateParams = (LiquidatePayload) data;
                LiquidateCollateral(
                    liquidateParams.CollateralHash, callingHash, from,
                    liquidateParams.Liquidatee, amount, liquidateParams.Json64, liquidateParams.Signature64);
            }
            else if (action == ACTION_LIQUIDATE_OCP)
            {
                LiquidateOCPPayload liquidateParams = (LiquidateOCPPayload) data;
                LiquidateCollateralOCP(
                    liquidateParams.CollateralHash, callingHash, from,
                    liquidateParams.Liquidatee, amount);
            }
            else if (action == ACTION_MINT)
            {
                // The Vault only accepts mints of FTokens
                if (!IsFTokenSupported(callingHash))
                {
                    FireErrorAndAbort(
                        $"The fToken '{callingHash}' is not supported",
                        "onNEP17Payment");
                }
            }
            else
            {
                FireErrorAndAbort("Mandatory arguments were missing", "onNEP17Payment");
            }
        }

        private static void DepositCollateral(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, BigInteger depositQuantity)
        {
            ValidateNotPaused("depositCollateral");
            ValidateCollateralCurrent(collateralHash);
            ValidateFTokenCurrent(fTokenHash);
            ValidateHash160(account, "account");
            ValidatePositiveNumber(depositQuantity, "depositQuantity");

            AccrueInterest(collateralHash);

            StorageMap collateralBalanceMap = GetCollateralBalanceMap(collateralHash, fTokenHash);
            AddToBalance(collateralBalanceMap, account, depositQuantity);

            OnDepositCollateral(collateralHash, fTokenHash, account, depositQuantity);
        }

        public static void WithdrawCollateral(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, BigInteger withdrawQuantity, string json64, string signature64)
        {
            ValidateNotPaused("withdrawCollateral");
            // Only the vault owner can withdraw collateral
            ValidateAccount(account, "withdrawCollateral");
            ValidateCollateral(collateralHash);
            ValidateFToken(fTokenHash);
            ValidatePositiveNumber(withdrawQuantity, "withdrawQuantity");

            var dataMap = GetDataMap(json64);
            BigInteger fTokenPrice = GetFTokenPrice(dataMap, fTokenHash, account);
            BigInteger fTokenMultiplier = GetTokenMultiplier(fTokenHash);
            BigInteger collateralMultiplier = GetTokenMultiplier(collateralHash);
            BigInteger priceExpiry = GetPriceExpiry(dataMap);

            ValidatePositiveNumber(fTokenPrice, "fTokenPrice");
            ValidatePositiveNumber(priceExpiry, "priceExpiry");

            AccrueInterest(collateralHash);

            VerifySignature(json64, signature64, "withdrawCollateral");
            VerifyPriceExpiry(priceExpiry, collateralHash, fTokenHash, account, "withdrawCollateral");

            StorageMap collateralBalanceMap = GetCollateralBalanceMap(collateralHash, fTokenHash);
            BigInteger collateralBalance = GetBalance(collateralBalanceMap, account);
            BigInteger fTokenBalance = GetFTokenBalance(collateralHash, fTokenHash, account);
            BigInteger newCollateralBalance = collateralBalance - withdrawQuantity;

            ValidateNonNegativeNumber(newCollateralBalance, "newCollateralBalance");

            if (newCollateralBalance == 0)
            {
                if (fTokenBalance > 0)
                {
                    string message = "The new collateral=0 but fTokenBalance=" + fTokenBalance;
                    FireErrorAndAbort(message, "withdrawCollateral");
                }
            }
            else if (fTokenBalance > 0)
            {
                BigInteger collateralPrice = GetCollateralPrice(dataMap, collateralHash, account);
                ValidatePositiveNumber(collateralPrice, "collateralPrice");

                // Handle LTV in basis points
                BigInteger newLoanToValue = (BASIS_POINTS * fTokenBalance * fTokenPrice * collateralMultiplier) /
                                            (newCollateralBalance * collateralPrice * fTokenMultiplier);
                BigInteger maxInitLoanToValue = PERCENT * GetMaxInitLoanToValue(collateralHash);

                // Ensure that the LTV falls in range
                if (newLoanToValue > maxInitLoanToValue)
                {
                    string message = "The new LTV=" + newLoanToValue + " must be smaller than max LTV=" + maxInitLoanToValue;
                    FireErrorAndAbort(message, "withdrawCollateral");
                }
            }

            DeductFromBalance(collateralBalanceMap, account, withdrawQuantity);

            OnWithdrawCollateral(collateralHash, fTokenHash, account, withdrawQuantity);

            UInt160 vault = Runtime.ExecutingScriptHash;
            var transferSuccess = (bool) Contract.Call(collateralHash, "transfer", CallFlags.All, new object[] {vault, account, withdrawQuantity, null});
            if (!transferSuccess)
            {
                FireErrorAndAbort("Failed to transfer collateral to account", "withdrawCollateral");
            }
        }

        public static void MintFToken(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, BigInteger mintQuantity, string json64, string signature64)
        {
            ValidateNotPaused("MintFToken");
            // Only the vault owner can mint
            ValidateAccount(account, "MintFToken");
            ValidateCollateralCurrent(collateralHash);
            ValidateFTokenCurrent(fTokenHash);
            ValidatePositiveNumber(mintQuantity, "mintQuantity");
            ValidateMintLimit(fTokenHash, mintQuantity);

            var dataMap = GetDataMap(json64);
            var fTokenPrice = GetFTokenPrice(dataMap, fTokenHash, account);
            var collateralPrice = GetCollateralPrice(dataMap, collateralHash, account);
            var fTokenMultiplier = GetTokenMultiplier(fTokenHash);
            var collateralMultiplier = GetTokenMultiplier(collateralHash);
            var priceExpiry = GetPriceExpiry(dataMap);

            ValidatePositiveNumber(collateralPrice, "collateralPrice");
            ValidatePositiveNumber(fTokenPrice, "fTokenPrice");
            ValidatePositiveNumber(priceExpiry, "priceExpiry");

            AccrueInterest(collateralHash);

            VerifySignature(json64, signature64, "MintFToken");
            VerifyPriceExpiry(priceExpiry, collateralHash, fTokenHash, account, "MintFToken");

            var collateralBalanceMap = GetCollateralBalanceMap(collateralHash, fTokenHash);
            var principalBalanceMap = GetPrincipalBalanceMap(collateralHash, fTokenHash);
            var cumulativePrincipalBalanceMap = GetCumulativePrincipalBalanceMap(collateralHash, fTokenHash);
            var collateralBalance = GetBalance(collateralBalanceMap, account);
            var fTokenBalance = GetFTokenBalance(collateralHash, fTokenHash, account);
            var newFTokenBalance = fTokenBalance + mintQuantity;

            // Handle LTV in basis points
            var newLoanToValue = (BASIS_POINTS * newFTokenBalance * fTokenPrice * collateralMultiplier) /
                                 (collateralBalance * collateralPrice * fTokenMultiplier);
            var maxInitLoanToValue = PERCENT * GetMaxInitLoanToValue(collateralHash);

            // Ensure that the LTV falls in range
            if (newLoanToValue > maxInitLoanToValue)
            {
                var message = $"The new LTV={newLoanToValue} must be smaller than max LTV={maxInitLoanToValue}";
                FireErrorAndAbort(message, "MintFToken");
            }

            AddToFTokenBalance(collateralHash, fTokenHash, account, mintQuantity);
            AddToBalance(principalBalanceMap, account, mintQuantity);
            AddToBalance(cumulativePrincipalBalanceMap, account, mintQuantity);
            AddMintedThisTime(fTokenHash, mintQuantity);

            var stakingContract = GetStakingContract();
            if (stakingContract != null)
            {
                try
                {
                    Contract.Call(stakingContract, "batchClaim", CallFlags.All, new object[] {account});
                }
                catch (Exception)
                {
                    FireErrorAndAbort("Failed to batchClaim to account", "MintFToken");
                }
            }

            OnMintFToken(collateralHash, fTokenHash, account, mintQuantity);

            // Mint and transfer the FTokens to the account
            var vault = Runtime.ExecutingScriptHash;
            Contract.Call(fTokenHash, "mint", CallFlags.All, new object[] {vault, mintQuantity});
            var transferSuccess = (bool) Contract.Call(fTokenHash, "transfer", CallFlags.All, new object[] {vault, account, mintQuantity, null});
            if (!transferSuccess)
            {
                FireErrorAndAbort("Failed to transfer FTokens to account", "MintFToken");
            }
        }

        private static void RepayFToken(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, BigInteger repayQuantity)
        {
            ValidateNotPaused("repayFToken");
            ValidateCollateral(collateralHash);
            ValidateFToken(fTokenHash);
            ValidateHash160(account, "account");
            ValidatePositiveNumber(repayQuantity, "repayQuantity");

            AccrueInterest(collateralHash);

            StorageMap principalBalanceMap = GetPrincipalBalanceMap(collateralHash, fTokenHash);
            StorageMap cumulativeRepaymentBalanceMap = GetCumulativeRepaymentBalanceMap(collateralHash, fTokenHash);
            BigInteger principalBalance = GetBalance(principalBalanceMap, account);
            BigInteger fTokenBalance = GetFTokenBalance(collateralHash, fTokenHash, account);
            BigInteger interestBalance = fTokenBalance - principalBalance;
            BigInteger clippedRepayQuantity = BigInteger.Min(fTokenBalance, repayQuantity);

            // fTokenBalance can be smaller than principalBalance right after minting before
            // enough interest has accrued
            if (principalBalance > fTokenBalance)
            {
                string message = "The principalBalance=" + principalBalance.ToString() +
                                 " must be smaller than fTokenBalance=" + fTokenBalance.ToString();
                FireErrorAndAbort(message, "repayFToken");
            }

            BigInteger interestRepayQuantity = BigInteger.Min(clippedRepayQuantity, interestBalance);
            BigInteger principalRepayQuantity = clippedRepayQuantity - interestRepayQuantity;
            BigInteger interestFlundQuantity = GetInterestFlundQuantity(interestRepayQuantity, collateralHash);
            BigInteger interestLrbQuantity = GetInterestLrbQuantity(interestRepayQuantity, collateralHash);
            BigInteger interestSecurityFundQuantity = interestRepayQuantity - interestFlundQuantity - interestLrbQuantity;

            // Add FLUND portion of fToken to FLUND
            AddToFlund(interestFlundQuantity, fTokenHash, "repayFToken");
            // Retain LRB Fund portion
            AddToLrbFund(interestLrbQuantity, fTokenHash);
            // Retain security fund portion
            AddToSecurityFund(interestSecurityFundQuantity, fTokenHash);

            DeductFromFTokenBalance(collateralHash, fTokenHash, account, clippedRepayQuantity);
            DeductFromBalance(principalBalanceMap, account, principalRepayQuantity);
            AddToBalance(cumulativeRepaymentBalanceMap, account, clippedRepayQuantity);
            DeductMintedThisTime(fTokenHash, clippedRepayQuantity);
            UInt160 stakingContract = GetStakingContract();
            if (stakingContract != null)
            {
                try
                {
                    Contract.Call(stakingContract, "batchClaim", CallFlags.All, new object[] {account});
                }
                catch (Exception)
                {
                    FireErrorAndAbort("Failed to batchClaim to account", "repayFToken");
                }
            }

            OnRepayFToken(collateralHash, fTokenHash, account, clippedRepayQuantity, principalRepayQuantity);

            UInt160 vault = Runtime.ExecutingScriptHash;
            Contract.Call(fTokenHash, "burn", CallFlags.All, new object[] {vault, principalRepayQuantity});

            // If repayment brings the fTokenBalance to 0, clear out the Vault
            if (GetFTokenBalance(collateralHash, fTokenHash, account) == 0)
            {
                ClearVault(collateralHash, fTokenHash, account);
            }

            // If repayQuantity exceeds fTokenBalance, we refund the rest
            if (clippedRepayQuantity < repayQuantity)
            {
                BigInteger refundQuantity = repayQuantity - clippedRepayQuantity;
                var transferSuccess = (bool) Contract.Call(fTokenHash, "transfer", CallFlags.All, new object[] {vault, account, refundQuantity, null});
                if (!transferSuccess)
                {
                    FireErrorAndAbort("Failed to transfer FTokens to account", "repayFToken");
                }
            }
        }

        /// <summary>
        /// Clear out principal, cumulative principal, and repayment quantity when a Vault's FToken balance is set to 0.
        /// At this point, the FToken balance is already 0. When the FToken balance is 0, the principal balance should also be 0 less int division loss of precision.
        /// </summary>
        private static void ClearVault(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account)
        {
            StorageMap principalBalanceMap = GetPrincipalBalanceMap(collateralHash, fTokenHash);
            StorageMap cumulativePrincipalBalanceMap = GetCumulativePrincipalBalanceMap(collateralHash, fTokenHash);
            StorageMap cumulativeRepaymentBalanceMap = GetCumulativeRepaymentBalanceMap(collateralHash, fTokenHash);

            principalBalanceMap.Delete(account);
            cumulativePrincipalBalanceMap.Delete(account);
            cumulativeRepaymentBalanceMap.Delete(account);
        }

        /// <summary>
        /// Liquidate collateral using an average of on-chain and off-chain prices.
        /// </summary>
        private static void LiquidateCollateral(UInt160 collateralHash, UInt160 fTokenHash, UInt160 liquidator, UInt160 liquidatee, BigInteger liquidateFunds, string json64, string signature64)
        {
            ValidateNotPausedWithBypass(liquidator, "LiquidateCollateral");

            Map<string, object> dataMap = GetDataMap(json64);
            BigInteger decimals = GetPriceDecimals(dataMap);
            if (decimals != ON_CHAIN_DECIMALS)
            {
                FireErrorAndAbort($"offChainDecimals={decimals} and onChainDecimals={ON_CHAIN_DECIMALS} must be the same", "GetCombinedPrice");
            }

            // The price used to compute LTV is 1
            BigInteger fTokenPrice = GetFTokenPrice(dataMap, fTokenHash, liquidator);
            // The price used to compute payout is the fair value of FUSD
            BigInteger fTokenLiquidationPrice = GetTrueOnChainPrice(fTokenHash, ON_CHAIN_DECIMALS);
            BigInteger collateralPrice = GetCollateralPrice(dataMap, collateralHash, liquidator);
            BigInteger priceExpiry = GetPriceExpiry(dataMap);

            ValidatePositiveNumber(priceExpiry, "priceExpiry");

            // In an emergency, we do not verify the signature of price expiry
            // During this time, no action other than liquidation may be performed
            // Only the contract owner will be able to liquidate since ValidateNotPaused fails otherwise
            if (!IsPaused())
            {
                VerifySignature(json64, signature64, "LiquidateCollateral");
                VerifyPriceExpiry(priceExpiry, collateralHash, fTokenHash, liquidatee, "LiquidateCollateral");
                ValidateLiquidateLimit(fTokenHash, liquidateFunds);
            }

            LiquidateCollateralInternal(collateralHash, fTokenHash, liquidator, liquidatee, liquidateFunds, collateralPrice, fTokenPrice, fTokenLiquidationPrice, true);
        }

        /// <summary>
        /// Liquidate collateral using only on-chain prices. This is reserved for whitelisted addresses.
        /// </summary>
        private static void LiquidateCollateralOCP(UInt160 collateralHash, UInt160 fTokenHash, UInt160 liquidator, UInt160 liquidatee, BigInteger liquidateFunds)
        {
            ValidateNotPaused("liquidateCollateralOCP");

            if (!IsLiquidatorWhitelisted(liquidator))
            {
                FireErrorAndAbort("liquidator is not whitelisted for OCP liquidation", "liquidateCollateralOCP");
            }

            // The price used to compute LTV is 1
            BigInteger fTokenPrice = GetOnChainPrice(fTokenHash, ON_CHAIN_DECIMALS);
            // The price used to compute payout is the fair value of FUSD
            BigInteger fTokenLiquidationPrice = GetTrueOnChainPrice(fTokenHash, ON_CHAIN_DECIMALS);
            BigInteger collateralPrice = GetTrueOnChainPrice(collateralHash, ON_CHAIN_DECIMALS);

            LiquidateCollateralInternal(collateralHash, fTokenHash, liquidator, liquidatee, liquidateFunds, collateralPrice, fTokenPrice, fTokenLiquidationPrice, false);
        }

        /// <summary>
        /// Common method for liquidations.
        /// if shouldAddLiquidated is true, then clippedLiquidateFunds
        /// counts toward the per-block liquidation limit.
        /// </summary>
        private static void LiquidateCollateralInternal(UInt160 collateralHash, UInt160 fTokenHash, UInt160 liquidator, UInt160 liquidatee, BigInteger liquidateFunds, BigInteger collateralPrice, BigInteger fTokenPrice,
            BigInteger fTokenLiquidationPrice, bool shouldAddLiquidated)
        {
            ValidateCollateral(collateralHash);
            ValidateFToken(fTokenHash);
            ValidateHash160(liquidator, "liquidator");
            ValidateHash160(liquidatee, "liquidatee");
            ValidatePositiveNumber(liquidateFunds, "liquidateFunds");

            ValidatePositiveNumber(collateralPrice, "collateralPrice");
            ValidatePositiveNumber(fTokenPrice, "fTokenPrice");
            ValidatePositiveNumber(fTokenLiquidationPrice, "fTokenLiquidationPrice");

            AccrueInterest(collateralHash);

            var collateralBalanceMap = GetCollateralBalanceMap(collateralHash, fTokenHash);
            var principalBalanceMap = GetPrincipalBalanceMap(collateralHash, fTokenHash);
            var cumulativeRepaymentBalanceMap = GetCumulativeRepaymentBalanceMap(collateralHash, fTokenHash);
            BigInteger principalBalance = GetBalance(principalBalanceMap, liquidatee);
            BigInteger fTokenBalance = GetFTokenBalance(collateralHash, fTokenHash, liquidatee);
            BigInteger interestBalance = fTokenBalance - principalBalance;
            BigInteger collateralBalance = GetBalance(collateralBalanceMap, liquidatee);
            BigInteger fTokenMultiplier = GetTokenMultiplier(fTokenHash);
            BigInteger collateralMultiplier = GetTokenMultiplier(collateralHash);

            // First, we check if the position is eligible for liquidation
            BigInteger loanToValue = (BASIS_POINTS * fTokenBalance * fTokenPrice * collateralMultiplier)
                                     / (collateralBalance * collateralPrice * fTokenMultiplier);
            BigInteger maxLoanToValue = PERCENT * GetMaxLoanToValue(collateralHash);

            // Ensure that the LTV falls outside of the range
            if (loanToValue < maxLoanToValue)
            {
                string message = "The LTV=" + loanToValue.ToString() + " must be smaller than max LTV="
                                 + maxLoanToValue.ToString() + " for liquidation";
                FireErrorAndAbort(message, "LiquidateCollateral");
            }

            // Next, we compute the max liquidation quantity
            BigInteger liquidateQuantity = (liquidateFunds * fTokenLiquidationPrice * collateralMultiplier)
                                           / (collateralPrice * fTokenMultiplier);
            BigInteger liquidationLimit = GetLiquidationLimit(collateralHash);
            BigInteger liquidationPenalty = GetLiquidationPenalty(collateralHash);

            // If the contract is paused and only handling emergency liquidations,
            // no liquidation bonus is paid out to the liquidator (contract owner)
            BigInteger liquidationBonus = IsPaused() ? 0 : GetLiquidationBonus(collateralHash);

            // Handle the FToken balance
            BigInteger maxLiquidateFunds = (liquidationLimit * fTokenBalance) / PERCENT;
            BigInteger clippedLiquidateFunds = BigInteger.Min(liquidateFunds, maxLiquidateFunds);
            BigInteger appliedLiquidateFunds = (clippedLiquidateFunds * (PERCENT - liquidationPenalty)) / PERCENT;
            BigInteger penaltyFunds = clippedLiquidateFunds - appliedLiquidateFunds;
            DeductFromFTokenBalance(collateralHash, fTokenHash, liquidatee, appliedLiquidateFunds);
            AddToBalance(cumulativeRepaymentBalanceMap, liquidatee, appliedLiquidateFunds);
            UInt160 stakingContract = GetStakingContract();
            if (stakingContract != null)
            {
                try
                {
                    Contract.Call(stakingContract, "batchClaim", CallFlags.All, new object[] {liquidatee});
                }
                catch (Exception)
                {
                    FireErrorAndAbort("Failed to batchClaim to account", "LiquidateCollateral");
                }
            }

            // Handle the collateral balance
            BigInteger clippedLiquidateQuantity = BigInteger.Min(liquidateQuantity,
                (clippedLiquidateFunds * fTokenLiquidationPrice * collateralMultiplier)
                / (collateralPrice * fTokenMultiplier));
            // This quantity includes the liquidation bonus
            BigInteger totalLiquidateQuantity = (clippedLiquidateQuantity * (PERCENT + liquidationBonus)) / PERCENT;
            // In an extreme case if the Vault becomes undercollateralized,
            // do not allow liquidation to bring the collateral balance below 0
            BigInteger newCollateralBalance = collateralBalance - totalLiquidateQuantity;
            ValidateNonNegativeNumber(newCollateralBalance, "newCollateralBalance");
            DeductFromBalance(collateralBalanceMap, liquidatee, totalLiquidateQuantity);

            // Handle the principal balance
            // fTokenBalance can be smaller than principalBalance right after minting before
            // enough interest has accrued
            if (principalBalance > fTokenBalance)
            {
                string message = "The principalBalance=" + principalBalance.ToString()
                                                         + " must be smaller than fTokenBalance=" + fTokenBalance.ToString();
                FireErrorAndAbort(message, "LiquidateCollateral");
            }

            BigInteger interestLiquidateQuantity = BigInteger.Min(appliedLiquidateFunds, interestBalance);
            BigInteger principalLiquidateQuantity = appliedLiquidateFunds - interestLiquidateQuantity;
            DeductFromBalance(principalBalanceMap, liquidatee, principalLiquidateQuantity);

            OnLiquidate(collateralHash, fTokenHash, liquidator, liquidatee, clippedLiquidateFunds, totalLiquidateQuantity, appliedLiquidateFunds);

            // Collect funds
            BigInteger interestFlundQuantity = GetInterestFlundQuantity(interestLiquidateQuantity, collateralHash);
            BigInteger interestLrbQuantity = GetInterestLrbQuantity(interestLiquidateQuantity, collateralHash);
            BigInteger interestSecurityFundQuantity = interestLiquidateQuantity - interestFlundQuantity - interestLrbQuantity;

            BigInteger liquidationFlundQuantity = GetLiquidationFlundQuantity(penaltyFunds, collateralHash);
            BigInteger liquidationLrbQuantity = GetLiquidationLrbQuantity(penaltyFunds, collateralHash);
            BigInteger liquidationSecurityFundQuantity = penaltyFunds - liquidationFlundQuantity - liquidationLrbQuantity;

            BigInteger totalFlundQuantity = interestFlundQuantity + liquidationFlundQuantity;
            BigInteger totalLrbQuantity = interestLrbQuantity + liquidationLrbQuantity;
            BigInteger totalSecurityFundQuantity = interestSecurityFundQuantity + liquidationSecurityFundQuantity;

            // Add FLUND portion of fToken to FLUND
            AddToFlund(totalFlundQuantity, fTokenHash, "LiquidateCollateral");
            // Retain LRB Fund portion
            AddToLrbFund(totalLrbQuantity, fTokenHash);
            // Retain security fund portion
            AddToSecurityFund(totalSecurityFundQuantity, fTokenHash);

            UInt160 vault = Runtime.ExecutingScriptHash;
            Contract.Call(fTokenHash, "burn", CallFlags.All, new object[] {vault, principalLiquidateQuantity});

            // Only count regular liquidations to the per-block liquidation limit
            // On-chain price liquidations do not count toward this limit
            if (shouldAddLiquidated)
            {
                AddLiquidatedThisBlock(fTokenHash, clippedLiquidateFunds);
            }

            bool transferSuccess = (bool) Contract.Call(collateralHash, "transfer", CallFlags.All, new object[] {vault, liquidator, totalLiquidateQuantity, null});
            if (!transferSuccess)
            {
                FireErrorAndAbort("Failed to transfer collateral to liquidator", "LiquidateCollateral");
            }

            // If liquidateFunds exceeds clippedLiquidateFunds, we refund the rest
            if (clippedLiquidateFunds < liquidateFunds)
            {
                BigInteger refundQuantity = liquidateFunds - clippedLiquidateFunds;

                Contract.Call(fTokenHash, "transfer", CallFlags.All, new object[] {vault, liquidator, refundQuantity, null});
                if (!transferSuccess)
                {
                    FireErrorAndAbort("Failed to transfer FTokens to liquidator", "LiquidateCollateral");
                }
            }
        }

        // Parameter Methods
        public static void SetOwner(UInt160 owner)
        {
            ValidateOwner("SetOwner");
            ValidateHash160(owner, "owner");

            Storage.Put(ctx, OWNER_KEY, owner);
        }

        [Safe]
        public static UInt160 GetOwner()
        {
            var owner = Storage.Get(rtx, OWNER_KEY);
            return owner is null ? INITIAL_OWNER : (UInt160) owner;
        }

        public static void SetStakingContract(UInt160 stakingContract)
        {
            ValidateOwner("SetStakingContract");
            ValidateHash160(stakingContract, "SetStakingContract");

            Storage.Put(ctx, STAKING_CONTRACT_KEY, stakingContract);
        }

        [Safe]
        public static UInt160 GetStakingContract()
        {
            return (UInt160) Storage.Get(rtx, STAKING_CONTRACT_KEY);
        }

        public static void SetSigner(byte[] signer)
        {
            ValidateOwner("SetSigner");
            ValidateECPoint(signer, "SetSigner");

            SIGNER_MAP.Put(signer, 1);
        }

        public static void UnsetSigner(byte[] signer)
        {
            ValidateOwner("UnsetSigner");
            ValidateECPoint(signer, "UnsetSigner");

            SIGNER_MAP.Delete(signer);
        }

        [Safe]
        public static List<ByteString> GetSigners()
        {
            var signerIterator = SIGNER_MAP.Find(FindOptions.KeysOnly | FindOptions.RemovePrefix);
            var signers = new List<ByteString>();
            foreach (var signer in signerIterator)
            {
                signers.Add((ByteString) signer);
                if (signers.Count >= MAX_SIGNERS_SIZE)
                {
                    return signers;
                }
            }

            return signers;
        }

        public static void SetGasAdmin(UInt160 gasAdmin)
        {
            ValidateOwner("SetGasAdmin");
            ValidateHash160(gasAdmin, "GasAdmin");

            Storage.Put(ctx, GAS_ADMIN_KEY, gasAdmin);
        }

        [Safe]
        public static UInt160 GetGasAdmin()
        {
            return (UInt160) Storage.Get(rtx, GAS_ADMIN_KEY);
        }

        public static void SetLRBFundAdmin(UInt160 lrbFundAdmin)
        {
            ValidateOwner("SetLRBFundAdmin");
            ValidateHash160(lrbFundAdmin, "LRBFundAdmin");

            Storage.Put(ctx, LRB_FUND_ADMIN_KEY, lrbFundAdmin);
        }

        [Safe]
        public static UInt160 GetLRBFundAdmin()
        {
            return (UInt160) Storage.Get(rtx, LRB_FUND_ADMIN_KEY);
        }

        public static void SetSecurityFundAdmin(UInt160 securityFundAdmin)
        {
            ValidateOwner("SetSecurityFundAdmin");
            ValidateHash160(securityFundAdmin, "SecurityFundAdmin");

            Storage.Put(ctx, SECURITY_FUND_ADMIN_KEY, securityFundAdmin);
        }

        [Safe]
        public static UInt160 GetSecurityFundAdmin()
        {
            return (UInt160) Storage.Get(rtx, SECURITY_FUND_ADMIN_KEY);
        }

        public static void SetbNEOHash(UInt160 bNEOHash)
        {
            ValidateOwner("SetbNEOHash");
            ValidateContract(bNEOHash, "bNEOHash");

            Storage.Put(ctx, BNEO_HASH_KEY, bNEOHash);
        }

        [Safe]
        public static UInt160 GetbNEOHash()
        {
            return (UInt160) Storage.Get(rtx, BNEO_HASH_KEY);
        }

        public static void SetFLUNDHash(UInt160 flundHash)
        {
            ValidateOwner("SetFLUNDHash");
            ValidateContract(flundHash, "FLUNDHash");

            Storage.Put(ctx, FLUND_HASH_KEY, flundHash);
        }

        [Safe]
        public static UInt160 GetFLUNDHash()
        {
            return (UInt160) Storage.Get(rtx, FLUND_HASH_KEY);
        }

        public static void SetLRBFundHash(UInt160 lrbHash)
        {
            ValidateOwner("SetLRBFundHash");
            ValidateContract(lrbHash, "LRBFundHash");

            Storage.Put(ctx, LRB_FUND_HASH_KEY, lrbHash);
        }

        [Safe]
        public static UInt160 GetLRBFundHash()
        {
            return (UInt160) Storage.Get(rtx, LRB_FUND_HASH_KEY);
        }

        public static void SetPriceFeedHash(UInt160 priceFeedHash)
        {
            ValidateOwner("SetPriceFeedHash");
            ValidateContract(priceFeedHash, "PriceFeedHash");

            Storage.Put(ctx, PRICE_FEED_HASH_KEY, priceFeedHash);
        }

        [Safe]
        public static UInt160 GetPriceFeedHash()
        {
            return (UInt160) Storage.Get(rtx, PRICE_FEED_HASH_KEY);
        }

        public static void SetQuoteTokenHash(UInt160 quoteTokenHash)
        {
            ValidateOwner("setQuoteTokenHash");
            ValidateContract(quoteTokenHash, "quoteTokenHash");

            Storage.Put(ctx, QUOTE_TOKEN_HASH_KEY, quoteTokenHash);
        }

        [Safe]
        public static UInt160 GetQuoteTokenHash()
        {
            return (UInt160) Storage.Get(rtx, QUOTE_TOKEN_HASH_KEY);
        }

        [Safe]
        public static BigInteger GetLRBFundBalance(UInt160 fTokenHash)
        {
            ValidateFToken(fTokenHash);
            return LRB_FUND_MAP.GetIntegerOrZero(fTokenHash);
        }

        [Safe]
        public static BigInteger GetSecurityFundBalance(UInt160 fTokenHash)
        {
            ValidateFToken(fTokenHash);
            return SECURITY_FUND_MAP.GetIntegerOrZero(fTokenHash);
        }

        [Safe]
        public static List<BigInteger> GetVaultBalance(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account)
        {
            ValidateCollateral(collateralHash);
            ValidateFToken(fTokenHash);
            ValidateHash160(account, "account");

            StorageMap collateralBalanceMap = GetCollateralBalanceMap(collateralHash, fTokenHash);
            StorageMap cumulativePrincipalBalanceMap = GetCumulativePrincipalBalanceMap(collateralHash, fTokenHash);
            StorageMap cumulativeRepaymentBalanceMap = GetCumulativeRepaymentBalanceMap(collateralHash, fTokenHash);
            StorageMap currentPrincipalBalanceMap = GetPrincipalBalanceMap(collateralHash, fTokenHash);

            BigInteger collateralBalance = collateralBalanceMap.GetIntegerOrZero(account);
            BigInteger fTokenBalance = GetFTokenBalance(collateralHash, fTokenHash, account);
            BigInteger cumulativePrincipalBalance = cumulativePrincipalBalanceMap.GetIntegerOrZero(account);
            BigInteger cumulativeRepaymentBalance = cumulativeRepaymentBalanceMap.GetIntegerOrZero(account);
            BigInteger currentPrincipalBalance = currentPrincipalBalanceMap.GetIntegerOrZero(account);

            List<BigInteger> balances = new List<BigInteger>();
            balances.Add(collateralBalance);
            balances.Add(collateralBalance);
            balances.Add(fTokenBalance);
            balances.Add(cumulativePrincipalBalance);
            balances.Add(cumulativeRepaymentBalance);
            balances.Add(currentPrincipalBalance);

            return balances;
        }

        /// <summary>
        /// Find all Vault balances associated with an account.
        /// The number of returned balances is limited to MAX_BALANCES_SIZE.
        /// However, we do not expect to reach anywhere near this number in practice
        /// due to the limited number of FTokens and Collaterals.
        /// </summary>
        /// <param name="account">The account that owns the Vaults.</param>
        /// <returns>A list of (collateralHash, fTokenHash, collateralBalance, fTokenBalance).</returns>
        [Safe]
        public static List<object[]> GetVaultBalances(UInt160 account)
        {
            ValidateHash160(account, "account");

            StorageMap supportedCollateralMap = SUPPORTED_COLLATERAL_MAP;
            StorageMap supportedFTokenMap = SUPPORTED_FTOKEN_MAP;

            var collaterals = new List<UInt160>();
            var collateralIterator = supportedCollateralMap.Find(FindOptions.KeysOnly | FindOptions.RemovePrefix);
            while (collateralIterator.Next())
            {
                collaterals.Add((UInt160) collateralIterator.Value);
            }

            var fTokens = new List<UInt160>();
            var fTokenIterator = supportedFTokenMap.Find(FindOptions.KeysOnly | FindOptions.RemovePrefix);
            while (fTokenIterator.Next())
            {
                fTokens.Add((UInt160) fTokenIterator.Value);
            }

            var balances = new List<object[]>();
            foreach (var collateralHash in collaterals)
            {
                foreach (var fTokenHash in fTokens)
                {
                    var collateralBalanceMap = GetCollateralBalanceMap(collateralHash, fTokenHash);
                    var cumulativePrincipalBalanceMap = GetCumulativePrincipalBalanceMap(collateralHash, fTokenHash);
                    var cumulativeRepaymentBalanceMap = GetCumulativeRepaymentBalanceMap(collateralHash, fTokenHash);
                    var currentPrincipalBalanceMap = GetPrincipalBalanceMap(collateralHash, fTokenHash);

                    BigInteger collateralBalance = collateralBalanceMap.GetIntegerOrZero(account);
                    BigInteger cumulativePrincipalBalance = cumulativePrincipalBalanceMap.GetIntegerOrZero(account);
                    BigInteger cumulativeRepaymentBalance = cumulativeRepaymentBalanceMap.GetIntegerOrZero(account);
                    BigInteger currentPrincipalBalance = currentPrincipalBalanceMap.GetIntegerOrZero(account);
                    BigInteger fTokenBalance = GetFTokenBalance(collateralHash, fTokenHash, account);

                    var balance = new object[] {collateralHash, fTokenHash, collateralBalance, fTokenBalance, cumulativePrincipalBalance, cumulativeRepaymentBalance, currentPrincipalBalance};

                    balances.Add(balance);
                    if (balances.Count >= MAX_BALANCES_SIZE)
                    {
                        return balances;
                    }
                }
            }

            return balances;
        }

        /// <summary>
        /// Find all Vaults associated with a collateral and fToken.
        /// </summary>
        /// <param name="collateralHash">The collateral token associated with the Vaults.</param>
        /// <param name="fTokenHash">The fToken associated with the Vaults.</param>
        /// <param name="pageSize">The number of entries to be retrieved.</param>
        /// <param name="pageNum">The page of entries to be retrieved.</param>
        /// <returns>A list of (account, collateralBalance, fTokenBalance).</returns>
        [Safe]
        public static List<object[]> GetAllVaults(UInt160 collateralHash, UInt160 fTokenHash, int pageSize, int pageNum)
        {
            ValidateCollateral(collateralHash);
            ValidateFToken(fTokenHash);
            ValidatePositiveNumberLessThan(pageSize, MAX_PAGE_SIZE, "pageSize");
            ValidateNonNegativeNumber(pageNum, "pageNum");

            StorageMap collateralBalanceMap = GetCollateralBalanceMap(collateralHash, fTokenHash);
            var collateralBalanceIterator = collateralBalanceMap.Find(FindOptions.RemovePrefix);
            var vaults = new List<object[]>();

            int pagesToSkip = pageSize * pageNum;
            int curPage = 0;

            while (collateralBalanceIterator.Next())
            {
                curPage += 1;
                if (curPage <= pagesToSkip)
                {
                    continue;
                }

                var currentValue = (object[]) collateralBalanceIterator.Value;
                var account = (UInt160) currentValue[0];
                BigInteger collateralBalance = (BigInteger) currentValue[1];
                BigInteger fTokenBalance = GetFTokenBalance(collateralHash, fTokenHash, account);

                var vault = new object[] {account, collateralBalance, fTokenBalance};

                vaults.Add(vault);

                if (vaults.Count >= pageSize)
                {
                    break;
                }
            }

            return vaults;
        }

        public static void SupportCollateral(UInt160 collateralHash)
        {
            ValidateOwner("supportCollateral");
            ValidateContract(collateralHash, "collateralHash");

            // GAS cannot be used as collateral due to its conflicts with bNEO GAS claims
            if (collateralHash == GAS.Hash)
            {
                throw new Exception("GAS cannot be supported as a collateral token");
            }

            BigInteger newTimestamp = Runtime.Time;

            // We have to be careful here - if the collateral is already "known",
            // we cannot overwrite the interest multiplier or timestamp
            if (!IsCollateralSupportedOrRetired(collateralHash))
            {
                SetInterestMultiplier(collateralHash, FLOAT_MULTIPLIER);
                SetLastTimestamp(collateralHash, newTimestamp);
            }

            SUPPORTED_COLLATERAL_MAP.Put(collateralHash, 1);
            RETIRED_COLLATERAL_MAP.Delete(collateralHash);
        }

        public static void UnsupportCollateral(UInt160 collateralHash)
        {
            ValidateOwner("unsupportCollateral");
            ValidateContract(collateralHash, "collateralHash");

            SUPPORTED_COLLATERAL_MAP.Delete(collateralHash);
            RETIRED_COLLATERAL_MAP.Put(collateralHash, 1);
        }

        [Safe]
        public static bool IsCollateralSupported(UInt160 collateralHash)
        {
            ValidateHash160(collateralHash, "collateralHash");

            var storageVal = SUPPORTED_COLLATERAL_MAP.Get(collateralHash);
            return storageVal != null && (BigInteger) storageVal == 1;
        }

        [Safe]
        public static bool IsCollateralRetired(UInt160 collateralHash)
        {
            ValidateHash160(collateralHash, "collateralHash");

            var storageVal = RETIRED_COLLATERAL_MAP.Get(collateralHash);
            return storageVal != null && (BigInteger) storageVal == 1;
        }

        [Safe]
        public static bool IsCollateralSupportedOrRetired(UInt160 collateralHash)
        {
            return IsCollateralSupported(collateralHash) || IsCollateralRetired(collateralHash);
        }

        public static void SupportFToken(UInt160 fTokenHash)
        {
            ValidateOwner("supportFToken");
            ValidateContract(fTokenHash, "fTokenHash");

            SUPPORTED_FTOKEN_MAP.Put(fTokenHash, 1);
            RETIRED_FTOKEN_MAP.Delete(fTokenHash);
        }

        public static void UnsupportFToken(UInt160 fTokenHash)
        {
            ValidateOwner("unsupportFToken");
            ValidateContract(fTokenHash, "fTokenHash");

            SUPPORTED_FTOKEN_MAP.Delete(fTokenHash);
            RETIRED_FTOKEN_MAP.Put(fTokenHash, 1);
        }

        [Safe]
        public static bool IsFTokenSupported(UInt160 fTokenHash)
        {
            ValidateHash160(fTokenHash, "fTokenHash");

            var storageVal = SUPPORTED_FTOKEN_MAP.Get(fTokenHash);
            return storageVal != null && (BigInteger) storageVal == 1;
        }

        [Safe]
        public static bool IsFTokenRetired(UInt160 fTokenHash)
        {
            ValidateHash160(fTokenHash, "fTokenHash");

            var storageVal = RETIRED_FTOKEN_MAP.Get(fTokenHash);
            return storageVal != null && (BigInteger) storageVal == 1;
        }

        [Safe]
        public static bool IsFTokenSupportedOrRetired(UInt160 fTokenHash)
        {
            return IsFTokenSupported(fTokenHash) || IsFTokenRetired(fTokenHash);
        }

        public static void SetMaxLoanToValue(UInt160 collateralHash, BigInteger maxLoanToValue)
        {
            ValidateOwner("setMaxLoanToValue");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(maxLoanToValue, "maxLoanToValue");

            MAX_LOAN_TO_VALUE_MAP.Put(collateralHash, maxLoanToValue);
        }

        /**
         * maxLoanToValue is expressed in %, i.e. 40 => (fToken value) / (collateral
         * value) < 40
         *
         * @return the maximum proportion of collateral that can be liquidated
         */
        [Safe]
        public static BigInteger GetMaxLoanToValue(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            var storageVal = MAX_LOAN_TO_VALUE_MAP.Get(collateralHash);
            return storageVal is null ? INITIAL_MAX_LOAN_TO_VALUE : (BigInteger) storageVal;
        }

        public static void SetMaxInitLoanToValue(UInt160 collateralHash, BigInteger maxInitLoanToValue)
        {
            ValidateOwner("SetMaxInitLoanToValue");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(maxInitLoanToValue, "maxInitLoanToValue");

            MAX_INIT_LOAN_TO_VALUE_MAP.Put(collateralHash, maxInitLoanToValue);
        }

        /**
         * liquidationLimit is expressed in %, i.e. 50 => at most 50% of collateral can
         * be liquidated
         *
         * @return the maxInitLoanToValue over which collateral can be liquidated
         */
        [Safe]
        public static BigInteger GetMaxInitLoanToValue(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            var storageVal = MAX_INIT_LOAN_TO_VALUE_MAP.Get(collateralHash);
            return storageVal is null ? INITIAL_MAX_INIT_LOAN_TO_VALUE : (BigInteger) storageVal;
        }

        public static void SetLiquidationLimit(UInt160 collateralHash, int liquidationLimit)
        {
            ValidateOwner("SetLiquidationLimit");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(liquidationLimit, "liquidationLimit");

            LIQUIDATION_LIMIT_MAP.Put(collateralHash, liquidationLimit);
        }

        /**
         * liquidationLimit is expressed in %, i.e. 35 => max liquidation percentage < 35%
         *
         * @return the liquidationLimit over which collateral can be liquidated
         */
        [Safe]
        public static BigInteger GetLiquidationLimit(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            var storageVal = LIQUIDATION_LIMIT_MAP.Get(collateralHash);
            return storageVal is null ? INITIAL_LIQUIDATION_LIMIT : (BigInteger) storageVal;
        }

        public static void SetLiquidationPenalty(UInt160 collateralHash, int liquidationPenalty)
        {
            ValidateOwner("SetLiquidationPenalty");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(liquidationPenalty, "liquidationPenalty");

            LIQUIDATION_PENALTY_MAP.Put(collateralHash, liquidationPenalty);
        }

        /**
         * liquidationPenalty is expressed in %, i.e. 5 => 5% of incoming FTokens
         * diverted
         *
         * @return the liquidationLimit over which collateral can be liquidated
         */
        [Safe]
        public static BigInteger GetLiquidationPenalty(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            var storageVal = LIQUIDATION_PENALTY_MAP.Get(collateralHash);
            return storageVal is null ? INITIAL_LIQUIDATION_PENALTY : (BigInteger) storageVal;
        }

        public static void SetLiquidationBonus(UInt160 collateralHash, int liquidationBonus)
        {
            ValidateOwner("SetLiquidationBonus");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(liquidationBonus, "liquidationBonus");

            LIQUIDATION_BONUS_MAP.Put(collateralHash, liquidationBonus);
        }

        /**
         * liquidationBonus is expressed in %, i.e. 10 => 10% additional collateral
         * purchased
         *
         * @return the liquidationLimit over which collateral can be liquidated
         */
        [Safe]
        public static BigInteger GetLiquidationBonus(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            var storageVal = LIQUIDATION_BONUS_MAP.Get(collateralHash);
            return storageVal is null ? INITIAL_LIQUIDATION_BONUS : (BigInteger) storageVal;
        }

        public static void SetLiquidationFlundAllocation(UInt160 collateralHash, int liquidationFlundAllocation)
        {
            ValidateOwner("SetLiquidationFlundAllocation");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(liquidationFlundAllocation, "liquidationFlundAllocation");

            var liquidationLrbAllocation = GetLiquidationLrbAllocation(collateralHash);
            if (liquidationFlundAllocation + liquidationLrbAllocation > 100)
            {
                throw new Exception("'liquidationFlundAllocation' + 'liquidationLrbAllocation' must be <= 100");
            }

            LIQUIDATION_FLUND_ALLOCATION_MAP.Put(collateralHash, liquidationFlundAllocation);
        }

        /// <summary>
        /// interestFlundAllocation is the percentage of interest accrued that is diverted to the FLUND
        /// </summary>
        /// <param name="collateralHash">The collateral hash.</param>
        /// <returns>The percentage of interest accrued to be diverted to the FLUND.</returns>
        [Safe]
        public static BigInteger GetLiquidationFlundAllocation(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            var storageVal = LIQUIDATION_FLUND_ALLOCATION_MAP.Get(collateralHash);
            return storageVal is null ? INITIAL_LIQUIDATION_FLUND_ALLOCATION : (BigInteger) storageVal;
        }

        public static void SetLiquidationLrbAllocation(UInt160 collateralHash, int liquidationLrbAllocation)
        {
            ValidateOwner("setLiquidationLrbAllocation");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(liquidationLrbAllocation, "liquidationLrbAllocation");

            BigInteger liquidationFlundAllocation = GetLiquidationFlundAllocation(collateralHash);
            if (liquidationFlundAllocation + liquidationLrbAllocation > 100)
                throw new Exception("'liquidationFlundAllocation' + 'liquidationLrbAllocation' must be <= 100");

            LIQUIDATION_LRB_ALLOCATION_MAP.Put(collateralHash, liquidationLrbAllocation);
        }

        /// <summary>
        /// interestLrbAllocation is the percentage of interest accrued that is diverted to the LRB Fund.
        /// </summary>
        /// <param name="collateralHash">The collateral hash.</param>
        /// <returns>The percentage of interest accrued to be diverted to the LRB Fund.</returns>
        [Safe]
        public static BigInteger GetLiquidationLrbAllocation(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            var storageVal = LIQUIDATION_LRB_ALLOCATION_MAP.Get(collateralHash);
            return storageVal is null ? INITIAL_LIQUIDATION_LRB_ALLOCATION : (BigInteger) storageVal;
        }

        public static void SetAnnualInterest(UInt160 collateralHash, int annualInterest)
        {
            ValidateOwner("setAnnualInterest");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(annualInterest, "annualInterest");

            // We need to accrue interest before changing the interest rate to ensure that the period before the change is compounded at the correct rate
            AccrueInterest(collateralHash);
            ANNUAL_INTEREST_MAP.Put(collateralHash, annualInterest);
        }

        /// <summary>
        /// annualInterest is expressed in %, i.e. 10 => 10% interest accrued on minted FToken balance.
        /// </summary>
        /// <param name="collateralHash">The collateral hash.</param>
        /// <returns>The liquidation limit over which collateral can be liquidated.</returns>
        [Safe]
        public static BigInteger GetAnnualInterest(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            var storageVal = ANNUAL_INTEREST_MAP.Get(collateralHash);
            return storageVal is null ? INITIAL_ANNUAL_INTEREST : (BigInteger) storageVal;
        }

        public static void SetInterestFlundAllocation(UInt160 collateralHash, int interestFlundAllocation)
        {
            ValidateOwner("setInterestFlundAllocation");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(interestFlundAllocation, "interestFlundAllocation");

            var interestLrbAllocation = GetInterestLrbAllocation(collateralHash);
            if (interestFlundAllocation + interestLrbAllocation > 100)
                throw new Exception("'interestFlundAllocation' + 'interestLrbAllocation' must be <= 100");

            INTEREST_FLUND_ALLOCATION_MAP.Put(collateralHash, interestFlundAllocation);
        }

        /// <summary>
        /// interestFlundAllocation is the percentage of interest accrued that is diverted to the FLUND.
        /// </summary>
        /// <param name="collateralHash">The collateral hash.</param>
        /// <returns>The percentage of interest accrued to be diverted to the FLUND.</returns>
        [Safe]
        public static BigInteger GetInterestFlundAllocation(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            var storageVal = INTEREST_FLUND_ALLOCATION_MAP.Get(collateralHash);
            return storageVal is null ? INITIAL_INTEREST_FLUND_ALLOCATION : (BigInteger) storageVal;
        }

        public static void SetInterestLrbAllocation(UInt160 collateralHash, int interestLrbAllocation)
        {
            ValidateOwner("setInterestLrbAllocation");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(interestLrbAllocation, "interestLrbAllocation");

            var interestFlundAllocation = GetInterestFlundAllocation(collateralHash);
            if (interestFlundAllocation + interestLrbAllocation > 100)
                throw new Exception("'interestFlundAllocation' + 'interestLrbAllocation' must be <= 100");

            INTEREST_LRB_ALLOCATION_MAP.Put(collateralHash, interestLrbAllocation);
        }

        /// <summary>
        /// interestLrbAllocation is the percentage of interest accrued that is diverted to the LRB Fund.
        /// </summary>
        /// <param name="collateralHash">The collateral hash.</param>
        /// <returns>The percentage of interest accrued to be diverted to the LRB Fund.</returns>
        [Safe]
        public static BigInteger GetInterestLrbAllocation(UInt160 collateralHash)
        {
            ValidateCollateral(collateralHash);

            var storageVal = INTEREST_LRB_ALLOCATION_MAP.Get(collateralHash);
            return storageVal is null ? INITIAL_INTEREST_LRB_ALLOCATION : (BigInteger) storageVal;
        }

        public static void SetMaxPriceDiff(UInt160 collateralHash, int maxPriceDiff)
        {
            ValidateOwner("setMaxPriceDiff");
            ValidateCollateral(collateralHash);
            ValidatePositiveNumber(maxPriceDiff, "maxPriceDiff");

            MAX_PRICE_DIFF_MAP.Put(collateralHash, maxPriceDiff);
        }

        /// <summary>
        /// maxPriceDiff is expressed in %, i.e. 5 => (onChainPrice / offChainPrice) < 5%.
        /// </summary>
        /// <param name="tokenHash">The token hash.</param>
        /// <returns>The maxPriceDiff that is allowed for an incoming price feed.</returns>
        [Safe]
        public static BigInteger GetMaxPriceDiff(UInt160 tokenHash)
        {
            ValidateHash160(tokenHash, "getMaxPriceDiff");

            var storageVal = MAX_PRICE_DIFF_MAP.Get(tokenHash);
            return storageVal is null ? INITIAL_MAX_PRICE_DIFF : (BigInteger) storageVal;
        }

        public static void SetMintLimitPerBlock(UInt160 fTokenHash, BigInteger mintLimitPerBlock)
        {
            ValidateOwner("setMintLimitPerBlock");
            ValidateFToken(fTokenHash);
            ValidatePositiveNumber(mintLimitPerBlock, "mintLimitPerBlock");

            MINT_LIMIT_PER_BLOCK_MAP.Put(fTokenHash, mintLimitPerBlock);
        }

        public static void SetMintLimitPerDay(UInt160 fTokenHash, BigInteger mintLimitPerDay)
        {
            ValidateOwner("setMintLimitPerDay");
            ValidateFToken(fTokenHash);
            ValidatePositiveNumber(mintLimitPerDay, "mintLimitPerDay");

            MINT_LIMIT_PER_DAY_MAP.Put(fTokenHash, mintLimitPerDay);
        }

        [Safe]
        public static BigInteger GetMintLimitPerBlock(UInt160 fTokenHash)
        {
            ValidateFToken(fTokenHash);

            var storageVal = MINT_LIMIT_PER_BLOCK_MAP.Get(fTokenHash);
            return storageVal is null ? INITIAL_MINT_LIMIT_PER_BLOCK : (BigInteger) storageVal;
        }

        [Safe]
        public static BigInteger GetMintLimitPerDay(UInt160 fTokenHash)
        {
            ValidateFToken(fTokenHash);

            var storageVal = MINT_LIMIT_PER_DAY_MAP.Get(fTokenHash);
            return storageVal is null ? INITIAL_MINT_LIMIT_PER_DAY : (BigInteger) storageVal;
        }

        private static void DeductMintedThisTime(UInt160 fTokenHash, BigInteger value)
        {
            ValidateFToken(fTokenHash);

            BigInteger time = Runtime.Time / MILLIS_IN_DAY;
            StorageMap timeMintedMap = GetTimeMintedMap(fTokenHash);
            BigInteger prevMintedPerDay = timeMintedMap.GetIntegerOrZero((ByteString) time);

            if (prevMintedPerDay > value)
            {
                timeMintedMap.Put((ByteString) time, prevMintedPerDay - value);
            }
            else
            {
                timeMintedMap.Put((ByteString) time, 0);
            }
        }

        private static void AddMintedThisTime(UInt160 fTokenHash, BigInteger mintQuantity)
        {
            ValidateFToken(fTokenHash);

            var fTokenBytes = fTokenHash;
            BigInteger curHeight = Ledger.CurrentIndex;
            BigInteger lastMintedBlock = LAST_MINTED_BLOCK_MAP.GetIntegerOrZero(fTokenBytes);

            // If the last minted block == curHeight, then we add the quantities
            // Else, we use the current minted quantity
            BigInteger prevMintedPerBlock = lastMintedBlock == curHeight ? LAST_MINTED_QUANTITY_MAP.GetIntegerOrZero(fTokenBytes) : 0;

            BigInteger time = Runtime.Time / MILLIS_IN_DAY;
            StorageMap timeMintedMap = GetTimeMintedMap(fTokenHash);
            BigInteger prevMintedPerDay = timeMintedMap.GetIntegerOrZero((ByteString) time);

            LAST_MINTED_BLOCK_MAP.Put(fTokenBytes, curHeight);
            LAST_MINTED_QUANTITY_MAP.Put(fTokenBytes, prevMintedPerBlock + mintQuantity);
            timeMintedMap.Put((ByteString) time, prevMintedPerDay + mintQuantity);
        }

        private static void ValidateMintLimit(UInt160 fTokenHash, BigInteger mintQuantity)
        {
            BigInteger mintedThisBlock = GetMintedThisBlock(fTokenHash);
            BigInteger newMintedThisBlock = mintedThisBlock + mintQuantity;
            BigInteger mintLimitPerBlock = GetMintLimitPerBlock(fTokenHash);
            if (newMintedThisBlock > mintLimitPerBlock)
            {
                throw new Exception($"The total minted quantity this block={newMintedThisBlock} > per block mint limit={mintLimitPerBlock}");
            }

            BigInteger mintedThisTime = GetMintedThisDay(fTokenHash);
            BigInteger newMintedThisTime = mintedThisTime + mintQuantity;
            BigInteger mintLimitPerDay = GetMintLimitPerDay(fTokenHash);
            if (newMintedThisTime > mintLimitPerDay)
            {
                throw new Exception($"The total minted quantity this day={newMintedThisTime} > per day mint limit={mintLimitPerDay}");
            }
        }

        [Safe]
        public static BigInteger GetMintedThisBlock(UInt160 fTokenHash)
        {
            ValidateFToken(fTokenHash);

            var fTokenBytes = fTokenHash;
            BigInteger curHeight = Ledger.CurrentIndex;

            BigInteger lastMintedBlock = LAST_MINTED_BLOCK_MAP.GetIntegerOrZero(fTokenBytes);
            // If the last minted block == curHeight, then we retrieve this value
            // Else, 0
            return lastMintedBlock == curHeight ? LAST_MINTED_QUANTITY_MAP.GetIntegerOrZero(fTokenBytes) : 0;
        }

        [Safe]
        public static BigInteger GetMintedThisDay(UInt160 fTokenHash)
        {
            ValidateFToken(fTokenHash);
            BigInteger time = Runtime.Time / MILLIS_IN_DAY;
            StorageMap timeMintedMap = GetTimeMintedMap(fTokenHash);
            return timeMintedMap.GetIntegerOrZero((ByteString) time);
        }

        public static void SetLiquidateLimitPerBlock(UInt160 fTokenHash, BigInteger liquidateLimitPerBlock)
        {
            ValidateOwner("setLiquidateLimitPerBlock");
            ValidateFToken(fTokenHash);
            ValidatePositiveNumber(liquidateLimitPerBlock, "liquidateLimitPerBlock");

            LIQUIDATE_LIMIT_PER_BLOCK_MAP.Put(fTokenHash, liquidateLimitPerBlock);
        }

        /// <summary>
        /// Return the max quantity of fTokens that can be liquidated per block.
        /// </summary>
        /// <param name="fTokenHash">The fToken for which we query the liquidate limit.</param>
        /// <returns>The liquidate limit for the fToken.</returns>
        [Safe]
        public static BigInteger GetLiquidateLimitPerBlock(UInt160 fTokenHash)
        {
            ValidateFToken(fTokenHash);

            var storageVal = LIQUIDATE_LIMIT_PER_BLOCK_MAP.Get(fTokenHash);
            return storageVal is null ? INITIAL_LIQUIDATE_LIMIT_PER_BLOCK : (BigInteger) storageVal;
        }

        private static void AddLiquidatedThisBlock(UInt160 fTokenHash, BigInteger liquidateQuantity)
        {
            ValidateFToken(fTokenHash);

            UInt160 fTokenBytes = fTokenHash;
            BigInteger curHeight = Ledger.CurrentIndex;
            BigInteger lastLiquidatedBlock = LAST_LIQUIDATED_BLOCK_MAP.GetIntegerOrZero(fTokenBytes);
            // If the last liquidated block == curHeight, then we add the quantities
            // Else, we use the current liquidated quantity
            BigInteger prevLiquidated = lastLiquidatedBlock == curHeight ? LAST_LIQUIDATED_QUANTITY_MAP.GetIntegerOrZero(fTokenBytes) : 0;

            LAST_LIQUIDATED_BLOCK_MAP.Put(fTokenBytes, curHeight);
            LAST_LIQUIDATED_QUANTITY_MAP.Put(fTokenBytes, prevLiquidated + liquidateQuantity);
        }

        private static void ValidateLiquidateLimit(UInt160 fTokenHash, BigInteger liquidateQuantity)
        {
            BigInteger liquidatedThisBlock = GetLiquidatedThisBlock(fTokenHash);
            BigInteger newLiquidatedThisBlock = liquidatedThisBlock + liquidateQuantity;
            BigInteger liquidateLimitPerBlock = GetLiquidateLimitPerBlock(fTokenHash);
            if (newLiquidatedThisBlock > liquidateLimitPerBlock)
            {
                throw new Exception($"The total liquidated quantity this block={newLiquidatedThisBlock} > per block liquidate limit={liquidateLimitPerBlock}");
            }
        }

        [Safe]
        public static BigInteger GetLiquidatedThisBlock(UInt160 fTokenHash)
        {
            ValidateFToken(fTokenHash);

            UInt160 fTokenBytes = fTokenHash;
            BigInteger curHeight = Ledger.CurrentIndex;

            BigInteger lastLiquidatedBlock = LAST_LIQUIDATED_BLOCK_MAP.GetIntegerOrZero(fTokenBytes);
            // If the last liquidated block == curHeight, then we retrieve this value
            // Else, 0
            return lastLiquidatedBlock == curHeight ? LAST_LIQUIDATED_QUANTITY_MAP.GetIntegerOrZero(fTokenBytes) : 0;
        }

        public static void WhitelistLiquidator(UInt160 liquidator)
        {
            ValidateOwner("whitelistLiquidator");
            ValidateHash160(liquidator, "liquidator");

            LIQUIDATOR_WHITELIST_MAP.Put(liquidator, 1);
        }

        public static void UnwhitelistLiquidator(UInt160 liquidator)
        {
            ValidateOwner("unwhitelistLiquidator");
            ValidateHash160(liquidator, "liquidator");

            LIQUIDATOR_WHITELIST_MAP.Delete(liquidator);
        }

        /**
         * Return whether the liquidator is allowed to use on-chain prices
         * exclusively for liquidation
         *
         * @return the whitelist status of the liquidator
         */
        [Safe]
        public static bool IsLiquidatorWhitelisted(UInt160 liquidator)
        {
            ValidateHash160(liquidator, "liquidator");

            return LIQUIDATOR_WHITELIST_MAP.Get(liquidator) != null;
        }

        public static void ClaimGASFrombNEO(UInt160 toAddress)
        {
            ValidateHash160(toAddress, "toAddress");
            ValidateAccount(GetGasAdmin(), "claimGASFrombNeo");

            UInt160 vault = Runtime.ExecutingScriptHash;
            UInt160 bNEOHash = GetbNEOHash();

            BigInteger beforeBalance = (BigInteger) Contract.Call(GAS.Hash, "balanceOf", CallFlags.All, new object[] {vault});
            bool transferSuccess = (bool) Contract.Call(bNEOHash, "transfer", CallFlags.All, new object[] {vault, bNEOHash, 0, null});
            if (!transferSuccess)
            {
                throw new Exception("bNEOHash GAS claim failed");
            }

            BigInteger afterBalance = (BigInteger) Contract.Call(GAS.Hash, "balanceOf", CallFlags.All, new object[] {vault});
            transferSuccess = (bool) Contract.Call(GAS.Hash, "transfer", CallFlags.All, new object[] {vault, toAddress, afterBalance - beforeBalance, null});
            if (!transferSuccess)
            {
                throw new Exception("bNEOHash GAS claim failed");
            }
        }

        public static void WithdrawLRBFund(UInt160 fTokenHash, UInt160 toAddress)
        {
            ValidateFToken(fTokenHash);
            ValidateHash160(toAddress, "toAddress");
            ValidateAccount(GetLRBFundAdmin(), "withdrawLRBFund");

            UInt160 vault = Runtime.ExecutingScriptHash;
            BigInteger lrbFundQuantity = GetLRBFundBalance(fTokenHash);
            DeductFromLrbFund(lrbFundQuantity, fTokenHash);
            bool transferSuccess = (bool) Contract.Call(fTokenHash, "transfer", CallFlags.All, new object[] {vault, toAddress, lrbFundQuantity, null});
            if (!transferSuccess)
            {
                throw new Exception("LRB Fund withdraw failed");
            }
        }

        public static void WithdrawSecurityFund(UInt160 fTokenHash, UInt160 toAddress)
        {
            ValidateFToken(fTokenHash);
            ValidateHash160(toAddress, "toAddress");
            ValidateAccount(GetSecurityFundAdmin(), "withdrawSecurityFund");

            UInt160 vault = Runtime.ExecutingScriptHash;
            BigInteger securityFundQuantity = GetSecurityFundBalance(fTokenHash);
            DeductFromSecurityFund(securityFundQuantity, fTokenHash);
            bool transferSuccess = (bool) Contract.Call(fTokenHash, "transfer", CallFlags.All, new object[] {vault, toAddress, securityFundQuantity, null});
            if (!transferSuccess)
            {
                throw new Exception("Security Fund withdraw failed");
            }
        }

        public static void Pause()
        {
            ValidateOwner("pause");
            Storage.Put(ctx, PAUSED_KEY, 1);
        }

        public static void Resume()
        {
            ValidateOwner("resume");
            Storage.Put(ctx, PAUSED_KEY, 0);
        }

        [Safe]
        public static bool IsPaused()
        {
            var isPaused = Storage.Get(rtx, PAUSED_KEY);
            return isPaused is null || (BigInteger) isPaused == 1;
        }

        // This will be the default price method for external callers to avoid confusion
        // Once clients move over, #GetOnChainPrice should be deprecated
        [Safe]
        public static BigInteger GetPrice(UInt160 tokenHash, int decimals)
        {
            return GetOnChainPrice(tokenHash, decimals);
        }

        // Helper Methods
        private static BigInteger GetFTokenPrice(Map<string, object> dataMap, UInt160 tokenHash, UInt160 account)
        {
            // FUSD price is always 1 for the purposes of computing LTV
            var decimals = GetPriceDecimals(dataMap);
            return BigInteger.Pow(10, (int) decimals);
        }

        /**
         * Return the price to use depending on the account
         * <p/>
         * 1. For the contract owner, we only use the off-chain price.
         * This allows the contract owner to mint tokens before pools are active and
         * to perform emergency liquidations.
         * 2. For all other users, we use the average of the on-chain and off-chain
         * price.
         *
         * @param dataMap   the off-chain price map
         * @param tokenHash the token for which we fetch the price
         * @param account   the calling address
         * @return the price to use
         * @throws Exception
         */
        private static BigInteger GetCollateralPrice(Map<string, object> dataMap, UInt160 tokenHash, UInt160 account)
        {
            if (GetOwner() == account)
            {
                return GetTrueOffChainPrice(dataMap, tokenHash);
            }
            else
            {
                return GetCombinedPriceInternal(dataMap, tokenHash);
            }
        }

        private static void FireErrorAndAbort(string msg, string method)
        {
            OnError(msg, method);
            ExecutionEngine.Abort();
        }

        private static void ValidateCollateralCurrent(UInt160 collateralHash)
        {
            if (!IsCollateralSupported(collateralHash))
            {
                throw new Exception($"The collateral '{collateralHash}' is not supported");
            }
        }

        private static void ValidateCollateral(UInt160 collateralHash)
        {
            if (!IsCollateralSupportedOrRetired(collateralHash))
            {
                throw new Exception($"The collateral '{collateralHash}' was never supported");
            }
        }

        private static void ValidateFTokenCurrent(UInt160 fTokenHash)
        {
            if (!IsFTokenSupported(fTokenHash))
            {
                throw new Exception($"The fToken '{fTokenHash}' is not supported");
            }
        }

        private static void ValidateFToken(UInt160 fTokenHash)
        {
            if (!IsFTokenSupportedOrRetired(fTokenHash))
            {
                throw new Exception($"The fToken '{fTokenHash}' was never supported");
            }
        }

        private static void ValidateECPoint(byte[] pubKey, string hashName)
        {
            ECPoint ecPoint = (ECPoint) pubKey;
            if (!ecPoint.IsValid)
            {
                throw new Exception($"The parameter '{hashName}' must be a 33-byte address");
            }
        }

        private static void ValidateHash160(UInt160 hash, string hashName)
        {
            if (hash is null || hash == UInt160.Zero)
            {
                throw new Exception($"The parameter '{hashName}' must be a 20-byte address");
            }
        }

        private static void ValidateContract(UInt160 hash, string hashName)
        {
            ValidateHash160(hash, hashName);
            if (ContractManagement.GetContract(hash) == null)
            {
                throw new Exception($"The parameter '{hashName}' must be a contract hash");
            }
        }

        private static void ValidatePositiveNumber(BigInteger number, string numberName)
        {
            if (number <= 0)
            {
                throw new Exception($"The parameter '{numberName}' must be positive");
            }
        }

        private static void ValidatePositiveNumberLessThan(BigInteger number, BigInteger limit, string numberName)
        {
            if (number <= 0 || number > limit)
            {
                throw new Exception($"The parameter '{numberName}' must be in the range (0, {limit})");
            }
        }

        private static void ValidateNonNegativeNumber(BigInteger number, string numberName)
        {
            if (number < 0)
            {
                throw new Exception($"The parameter '{numberName}' must be non-negative");
            }
        }

        private static void ValidateOwner(string method)
        {
            if (!Runtime.CheckWitness(GetOwner()))
            {
                FireErrorAndAbort("Not authorized", method);
            }
        }

        private static void ValidateAccount(UInt160 account, string method)
        {
            if (!Runtime.CheckWitness(account))
            {
                FireErrorAndAbort("Invalid witness", method);
            }
        }

        private static void ValidateNotPaused(string method)
        {
            if (IsPaused())
            {
                FireErrorAndAbort("System is paused", method);
            }
        }

        // Used to deny functionality during an emergency to all other than the contract owner
        private static void ValidateNotPausedWithBypass(UInt160 caller, string method)
        {
            if (IsPaused() && caller != GetOwner())
            {
                FireErrorAndAbort("System is paused and caller is not owner", method);
            }
        }

        private static void SetLastTimestamp(UInt160 collateralHash, BigInteger lastTimestamp)
        {
            INTEREST_LAST_TIMESTAMP_MAP.Put(collateralHash, lastTimestamp);
        }

        private static void SetInterestMultiplier(UInt160 collateralHash, BigInteger interestMultiplier)
        {
            INTEREST_MULTIPLIER_MAP.Put(collateralHash, interestMultiplier);
        }

        private static void AccrueInterest(UInt160 collateralHash)
        {
            // This interest multiplier takes into account
            // interest accrued since the last "set" operation
            BigInteger interestMultiplier = GetInterestMultiplier(collateralHash);
            BigInteger newTimestamp = Runtime.Time;
            SetInterestMultiplier(collateralHash, interestMultiplier);
            SetLastTimestamp(collateralHash, newTimestamp);
        }

        private static void AddToBalance(StorageMap balanceMap, UInt160 key, BigInteger value)
        {
            balanceMap.Put(key, GetBalance(balanceMap, key) + value);
        }

        private static void DeductFromBalance(StorageMap balanceMap, UInt160 key, BigInteger value)
        {
            AddToBalance(balanceMap, key, -value);
        }

        private static BigInteger GetBalance(StorageMap balanceMap, UInt160 key)
        {
            return balanceMap.GetIntegerOrZero(key);
        }

        /// <summary>
        /// Set a scaled balance in the contract storage.
        /// Because rounding down can cause problems whereas rounding up doesn't,
        /// we add 1 to the entry going into the balanceMap if there is a remainder.
        /// </summary>
        /// <param name="balanceMap">The StorageMap that holds the account -> balance.</param>
        /// <param name="key">The key to use for the balanceMap.</param>
        /// <param name="value">The unscaled value to set in contract storage.</param>
        /// <param name="scaleFactor">The factor by which to scale.</param>
        private static void SetScaledBalance(StorageMap balanceMap, UInt160 key, BigInteger value, BigInteger scaleFactor, BigInteger scaleMultiplier)
        {
            BigInteger rawUnscaledValue = (value * scaleMultiplier) / scaleFactor;
            BigInteger unscaledValue = (value * scaleMultiplier) % scaleFactor == 0 ? rawUnscaledValue : rawUnscaledValue + 1;
            balanceMap.Put(key, unscaledValue);
        }

        /// <summary>
        /// Retrieve a scaled balance from the contract storage.
        /// For example, if the scale factor is 10 and the value in contract storage is
        /// 5, we return 5 * 10 = 50
        /// </summary>
        /// <param name="balanceMap">The StorageMap that holds the account -> balance.</param>
        /// <param name="key">The key to use for the balanceMap.</param>
        /// <param name="scaleFactor">The factor by which to scale.</param>
        /// <param name="scaleMultiplier">The multiplier to use for scaling.</param>
        private static BigInteger GetScaledBalance(StorageMap balanceMap, UInt160 key, BigInteger scaleFactor, BigInteger scaleMultiplier)
        {
            return GetScaledBalance(balanceMap.GetIntegerOrZero(key), scaleFactor, scaleMultiplier);
        }

        private static BigInteger GetScaledBalance(BigInteger balance, BigInteger scaleFactor, BigInteger scaleMultiplier)
        {
            return balance * scaleFactor / scaleMultiplier;
        }

        // Add to the Vault's token balance as well as the total token balance
        private static void AddToFTokenBalance(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, BigInteger value)
        {
            StorageMap balanceMap = GetFTokenBalanceMap(collateralHash, fTokenHash);
            BigInteger scaleFactor = GetInterestMultiplier(collateralHash);
            BigInteger existingBalance = GetScaledBalance(balanceMap, account, scaleFactor, FLOAT_MULTIPLIER);
            SetScaledBalance(balanceMap, account, existingBalance + value, scaleFactor, FLOAT_MULTIPLIER);
        }

        private static void DeductFromFTokenBalance(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, BigInteger value)
        {
            AddToFTokenBalance(collateralHash, fTokenHash, account, -value);
        }

        private static BigInteger GetFTokenBalance(UInt160 collateralHash, UInt160 fTokenHash, UInt160 account)
        {
            StorageMap balanceMap = GetFTokenBalanceMap(collateralHash, fTokenHash);
            BigInteger scaleFactor = GetInterestMultiplier(collateralHash);

            // Add 1 to the returned value because it is almost certainly rounded down
            // through int division
            // Otherwise, we can run into issues with the principal balance being larger
            // than the FToken balance
            return GetScaledBalance(balanceMap, account, scaleFactor, FLOAT_MULTIPLIER);
        }

        private static StorageMap GetCollateralBalanceMap(UInt160 collateralHash, UInt160 fTokenHash)
        {
            byte[] collateralBalanceKey = Concat(COLLATERAL_PREFIX, (byte[]) collateralHash, (byte[]) fTokenHash);
            return new StorageMap(Storage.CurrentContext, collateralBalanceKey);
        }

        private static StorageMap GetFTokenBalanceMap(UInt160 collateralHash, UInt160 fTokenHash)
        {
            byte[] fTokenBalanceKey = Concat(FTOKEN_PREFIX, (byte[]) collateralHash, (byte[]) fTokenHash);
            return new StorageMap(Storage.CurrentContext, fTokenBalanceKey);
        }

        private static StorageMap GetPrincipalBalanceMap(UInt160 principalHash, UInt160 fTokenHash)
        {
            byte[] principalBalanceKey = Concat(PRINCIPAL_PREFIX, (byte[]) principalHash, (byte[]) fTokenHash);
            return new StorageMap(Storage.CurrentContext, principalBalanceKey);
        }

        private static StorageMap GetCumulativePrincipalBalanceMap(UInt160 collateralHash, UInt160 fTokenHash)
        {
            byte[] cumulativePrincipalBalanceKey = Concat(CUMULATIVE_PRINCIPAL_PREFIX, (byte[]) collateralHash, (byte[]) fTokenHash);
            return new StorageMap(Storage.CurrentContext, cumulativePrincipalBalanceKey);
        }

        private static StorageMap GetCumulativeRepaymentBalanceMap(UInt160 collateralHash, UInt160 fTokenHash)
        {
            byte[] cumulativeRepaymentBalanceKey = Concat(CUMULATIVE_REPAYMENT_PREFIX, (byte[]) collateralHash, (byte[]) fTokenHash);
            return new StorageMap(Storage.CurrentContext, cumulativeRepaymentBalanceKey);
        }

        private static StorageMap GetPriceExpiryMap(UInt160 collateralHash, UInt160 fTokenHash)
        {
            byte[] priceExpiryKey = Concat(PRICE_EXPIRY_PREFIX, (byte[]) collateralHash, (byte[]) fTokenHash);
            return new StorageMap(Storage.CurrentContext, priceExpiryKey);
        }

        private static StorageMap GetTimeMintedMap(UInt160 fTokenHash)
        {
            byte[] timeMintedKey = Helper.Concat(LAST_MINTED_TIME_PREFIX, fTokenHash);
            return new StorageMap(Storage.CurrentContext, timeMintedKey);
        }

        private static byte[] Concat(byte[] a, byte[] b, byte[] c)
        {
            return Helper.Concat(Helper.Concat(a, b), c);
        }

        private static void VerifyPriceExpiry(BigInteger priceExpiry, UInt160 collateralHash, UInt160 fTokenHash, UInt160 account, string method)
        {
            // First, we verify that the price is not stale
            BigInteger curSeconds = Runtime.Time / 1000;
            if (priceExpiry < curSeconds)
            {
                FireErrorAndAbort($"Price expiry={priceExpiry} exceeded", method);
            }

            // Next, we verify that a later price has not been used for this Vault
            StorageMap priceExpiryMap = GetPriceExpiryMap(collateralHash, fTokenHash);
            var lastPriceExpiry = priceExpiryMap.GetIntegerOrZero(account);
            if (priceExpiry < lastPriceExpiry)
            {
                FireErrorAndAbort($"Price expiry={priceExpiry} <= a previously used price expiry {lastPriceExpiry}", method);
            }

            // Finally, we update the latest price expiry used for this Vault
            priceExpiryMap.Put(account, priceExpiry);
        }

        private static void VerifySignature(string json64, string signature64, string method)
        {
            ByteString message = StdLib.Base64Decode(json64);
            ByteString signature = StdLib.Base64Decode(signature64);
            List<ByteString> signers = GetSigners();

            foreach (var signer in signers)
            {
                bool verified = CryptoLib.VerifyWithECDsa(message, (ECPoint) signer, signature, NamedCurveHash.secp256r1SHA256);
                if (verified)
                {
                    return;
                }
            }

            FireErrorAndAbort("Signature verification failed", method);
        }

        // This sucks, but we don't want to pay for deserialization multiple times
        [Safe]
        public static Map<string, object> GetDataMap(string json64)
        {
            string json = StdLib.Base64Decode(json64).ToString();
            return (Map<string, object>) StdLib.JsonDeserialize(json);
        }

        private static Map<string, string> GetPriceMap(Map<string, object> dataMap)
        {
            return (Map<string, string>) dataMap["prices"];
        }

        private static BigInteger GetPriceExpiry(Map<string, object> dataMap)
        {
            return (BigInteger) dataMap["expires"];
        }

        private static BigInteger GetPriceDecimals(Map<string, object> dataMap)
        {
            return (BigInteger) dataMap["decimals"];
        }

        private static BigInteger GetTokenMultiplier(UInt160 tokenHash)
        {
            BigInteger decimals = (BigInteger) Contract.Call(tokenHash, "decimals", CallFlags.ReadOnly, new object[] { });
            ValidateNonNegativeNumber(decimals, "decimals");

            return BigInteger.Pow(10, (int) decimals);
        }

        private static BigInteger GetCombinedPriceInternal(Map<string, object> dataMap, UInt160 tokenHash)
        {
            var decimals = GetPriceDecimals(dataMap);
            var offChainPrice = GetTrueOffChainPrice(dataMap, tokenHash);
            var onChainPrice = GetTrueOnChainPrice(tokenHash, ON_CHAIN_DECIMALS);
            var maxPriceDiff = GetMaxPriceDiff(tokenHash);

            if (decimals != ON_CHAIN_DECIMALS)
            {
                FireErrorAndAbort($"offChainDecimals={decimals} and onChainDecimals={ON_CHAIN_DECIMALS} must be the same", "GetCombinedPrice");
            }

            // decimals is the price decimals, not the token decimals
            // We expect this to always be positive
            ValidatePositiveNumber(decimals, "decimals");
            ValidatePositiveNumber(offChainPrice, "offChainPrice");
            ValidatePositiveNumber(onChainPrice, "onChainPrice");

            if (BigInteger.Abs((PERCENT * offChainPrice) / onChainPrice - PERCENT) > maxPriceDiff)
            {
                FireErrorAndAbort($"offChainPrice={offChainPrice} and onChainPrice={onChainPrice} differ by more than maxPriceDiff={maxPriceDiff}", "GetCombinedPrice");
            }

            return (offChainPrice + onChainPrice) / 2;
        }

        private static BigInteger GetTrueOffChainPrice(Map<string, object> dataMap, UInt160 tokenHash)
        {
            string symbol = (string) Contract.Call(tokenHash, "symbol", CallFlags.ReadOnly, new object[] { });

            Map<string, string> priceMap = GetPriceMap(dataMap);
            BigInteger price;

            // FLUND does not have a native price
            // We look for FLM's price in the off-chain feed and compute
            // FLUND price using the FLUND:FLM ratio
            if (GetFLUNDHash() == tokenHash)
            {
                // FlamingoPriceFeedContract priceFeedContract = new FlamingoPriceFeedContract(GetPriceFeedHash());
                List<BigInteger> flundFlmRatio = (List<BigInteger>) Contract.Call(GetPriceFeedHash(), "getFlundFlmRatio", CallFlags.ReadOnly, new object[] { });
                BigInteger flmPrice = StdLib.Atoi(priceMap[FLM], 10);
                price = (flmPrice * flundFlmRatio[0]) / flundFlmRatio[1];
            }
            else
            {
                price = StdLib.Atoi(priceMap[symbol], 10);
            }

            return price;
        }

        [Safe]
        public static BigInteger GetTrueOnChainPrice(UInt160 tokenHash, int decimals)
        {
            UInt160 priceFeedHash = GetPriceFeedHash();
            UInt160 quoteTokenHash = GetQuoteTokenHash();

            return (BigInteger) Contract.Call(priceFeedHash, "getPrice", CallFlags.ReadOnly, new object[] {tokenHash, quoteTokenHash, decimals});
        }

        // Unfortunately we can't rename this due to external clients
        // We will migrate them to #getPrice and deprecate this method
        [Safe]
        public static BigInteger GetOnChainPrice(UInt160 tokenHash, int decimals)
        {
            // FUSD price is always 1 for the purposes of computing LTV
            if (IsFTokenSupported(tokenHash))
            {
                return BigInteger.Pow(10, decimals);
            }

            return GetTrueOnChainPrice(tokenHash, decimals);
        }

        private static BigInteger GetInterestFlundQuantity(BigInteger interestQuantity, UInt160 collateralHash)
        {
            var interestFlundAllocation = GetInterestFlundAllocation(collateralHash);
            return (interestFlundAllocation * interestQuantity) / PERCENT;
        }

        private static BigInteger GetInterestLrbQuantity(BigInteger interestQuantity, UInt160 collateralHash)
        {
            var interestLrbAllocation = GetInterestLrbAllocation(collateralHash);
            return (interestLrbAllocation * interestQuantity) / PERCENT;
        }

        private static BigInteger GetLiquidationFlundQuantity(BigInteger liquidationQuantity, UInt160 collateralHash)
        {
            var liquidationFlundAllocation = GetLiquidationFlundAllocation(collateralHash);
            return (liquidationFlundAllocation * liquidationQuantity) / PERCENT;
        }

        private static BigInteger GetLiquidationLrbQuantity(BigInteger liquidationQuantity, UInt160 collateralHash)
        {
            var liquidationLrbAllocation = GetLiquidationLrbAllocation(collateralHash);
            return (liquidationLrbAllocation * liquidationQuantity) / PERCENT;
        }

        private static void AddToFlund(BigInteger fTokenQuantity, UInt160 fTokenHash, string method)
        {
            UInt160 flund = GetFLUNDHash();
            UInt160 vault = Runtime.ExecutingScriptHash;

            bool transferSuccess = (bool) Contract.Call(fTokenHash, "transfer", CallFlags.All, new object[] {vault, flund, fTokenQuantity, null});
            if (!transferSuccess)
            {
                FireErrorAndAbort("Failed to transfer FTokens to FLUND", method);
            }
        }

        private static void AddToLrbFund(BigInteger fTokenQuantity, UInt160 fTokenHash)
        {
            AddToBalance(LRB_FUND_MAP, fTokenHash, fTokenQuantity);
        }

        private static void DeductFromLrbFund(BigInteger fTokenQuantity, UInt160 fTokenHash)
        {
            AddToBalance(LRB_FUND_MAP, fTokenHash, -fTokenQuantity);
        }

        private static void AddToSecurityFund(BigInteger fTokenQuantity, UInt160 fTokenHash)
        {
            AddToBalance(SECURITY_FUND_MAP, fTokenHash, fTokenQuantity);
        }

        private static void DeductFromSecurityFund(BigInteger fTokenQuantity, UInt160 fTokenHash)
        {
            AddToBalance(SECURITY_FUND_MAP, fTokenHash, -fTokenQuantity);
        }
    }
}
