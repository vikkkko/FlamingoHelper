using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;


namespace ThirdParty.NeoBurger
{
    [ManifestExtra("Author", "NEOBURGER")]
    [ManifestExtra("Email", "developer@neo.org")]
    [ManifestExtra("Description", "NeoBurger Core Contract")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "*")]
    public class BurgerNEO : TokenContract
    {
        private static readonly UInt160 INITIAL_OWNER = "6288e8a5e3d92f6aa645e6f749b4cb6cc024b211";

        private const byte PREFIXOWNER = 0x01;
        private const byte PREFIXAGENT = 0x02;
        private const byte PREFIXSTRATEGIST = 0x03;
        private const byte PREFIXREWARDPERTOKENSTORED = 0x04;
        private const byte PREFIXREWARD = 0x05;
        private const byte PREFIXPAID = 0x06;
        private const byte PREFIXCANDIDATEWHITELIST = 0x07;

        private static readonly BigInteger DEFAULTCLAIMREMAIN = 99000000;
        private static readonly BigInteger DEFAULTWITHDRAWFACTOR = 1000;

        // public override byte Decimals => 8;
        // public override string Symbol => "bNEO";
        public override byte Decimals { [Safe] get => 8; }
        public override string Symbol { [Safe] get => "bNEO"; }

        public static UInt160 Owner() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXOWNER });
        public static UInt160 Agent(BigInteger i) => (UInt160)new StorageMap(Storage.CurrentContext, PREFIXAGENT).Get((ByteString)i);
        public static UInt160 Strategist() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXSTRATEGIST });
        public static ByteString Candidate(ECPoint target) => new StorageMap(Storage.CurrentContext, PREFIXCANDIDATEWHITELIST).Get(target);
        public static BigInteger Reward(UInt160 account) => SyncAccount(account) ? (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account) : 0;
        public static BigInteger RPS() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED });

        public delegate void OnTransferDelegate(UInt160 from, UInt160 to, BigInteger amount);

        [DisplayName("Transfer")]
        public static event OnTransferDelegate OnTransfer;

        public static void _deploy(object data, bool update)
        {
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNER }, INITIAL_OWNER);
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            if (Runtime.CallingScriptHash == GAS.Hash && amount > 0)
            {
                BigInteger ts = TotalSupply;
                if (ts > 0)
                {
                    BigInteger rps = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED });
                    Storage.Put(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED }, amount * DEFAULTCLAIMREMAIN / ts + rps);
                }
            }
            SyncAccount(from);
            if (Runtime.CallingScriptHash == NEO.Hash)
            {
                ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, Agent(0), NEO.BalanceOf(Runtime.ExecutingScriptHash)));
                Mint(from, amount * 100000000);
            }
            else if (Runtime.CallingScriptHash == GAS.Hash && amount > 0 && data is null)
            {
                amount *= DEFAULTWITHDRAWFACTOR;
                Burn(from, amount);
                amount /= 100000000;
                for (BigInteger i = 0; amount > 0; i++)
                {
                    UInt160 agent = Agent(i);
                    BigInteger balance = NEO.BalanceOf(agent) - 1;
                    if (amount > balance)
                    {
                        amount -= balance;
                        Contract.Call(agent, "transfer", CallFlags.All, new object[] { from, balance });
                    }
                    else
                    {
                        Contract.Call(agent, "transfer", CallFlags.All, new object[] { from, amount });
                        break;
                    }
                }
            }
            else if (Runtime.CallingScriptHash == Runtime.ExecutingScriptHash)
            {
                BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(from);
                if (reward > 0)
                {
                    new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(from, 0);
                    ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, from, reward));
                }
                if (amount > 0)
                {
                    ExecutionEngine.Assert(Nep17Token.Transfer(Runtime.ExecutingScriptHash, from, amount, null));
                }
            }
        }

        public static bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data)
        {
            if (amount > 0)
            {
                SyncAccount(from);
                SyncAccount(to);
            }
            if (from is null || !from.IsValid)
                throw new Exception("The argument \"from\" is invalid.");
            if (to is null || !to.IsValid)
                throw new Exception("The argument \"to\" is invalid.");
            if (amount < 0)
                throw new Exception("The amount must be a positive number.");
            if (!Runtime.CheckWitness(from)) return false;
            if (amount != 0)
            {
                if (!UpdateBalance(from, -amount))
                    return false;
                UpdateBalance(to, +amount);
            }
            PostTransfer(from, to, amount, data);
            return true;
        }

        public static bool SyncAccount(UInt160 account)
        {
            BigInteger rps = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED });
            BigInteger balance = BalanceOf(account);
            if (balance > 0)
            {
                BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
                BigInteger paid = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXPAID).Get(account);
                BigInteger earned = balance * (rps - paid) / 100000000 + reward;
                new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(account, earned);
            }
            new StorageMap(Storage.CurrentContext, PREFIXPAID).Put(account, rps);
            return true;
        }

        public static void TrigVote(BigInteger i, ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Strategist()));
            ExecutionEngine.Assert(new StorageMap(Storage.CurrentContext, PREFIXCANDIDATEWHITELIST).Get(target) is not null);
            Contract.Call(Agent(i), "vote", CallFlags.All, new object[] { target });
        }
        public static void TrigTransfer(BigInteger i, BigInteger j, BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Strategist()));
            Contract.Call(Agent(i), "transfer", CallFlags.All, new object[] { Agent(j), amount });
        }
        public static void SetOwner(UInt160 owner)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNER }, owner);
        }
        public static void SetAgent(BigInteger i, UInt160 agent)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            new StorageMap(Storage.CurrentContext, PREFIXAGENT).Put((ByteString)i, agent);
        }
        public static void SetStrategist(UInt160 strategist)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXSTRATEGIST }, strategist);
        }
        public static void AllowCandidate(ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            StorageMap candidates = new StorageMap(Storage.CurrentContext, PREFIXCANDIDATEWHITELIST);
            candidates.Put(target, 1);
        }
        public static void DisallowCandidate(ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            StorageMap candidates = new StorageMap(Storage.CurrentContext, PREFIXCANDIDATEWHITELIST);
            candidates.Delete(target);
        }
        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ContractManagement.Update(nefFile, manifest, null);
        }
        public static void Pika(BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, Owner(), amount));
        }

        protected static void Mint(UInt160 account, BigInteger amount)
        {
            if (amount.Sign < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount.IsZero) return;
            UpdateBalance(account, +amount);
            TotalSupply += amount;
            PostTransfer(null, account, amount, null);
        }

        protected static void Burn(UInt160 account, BigInteger amount)
        {
            if (amount.Sign < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount.IsZero) return;
            if (!UpdateBalance(account, -amount))
                throw new InvalidOperationException();
            TotalSupply -= amount;
            PostTransfer(account, null, amount, null);
        }

        protected static void PostTransfer(UInt160 from, UInt160 to, BigInteger amount, object data)
        {
            OnTransfer(from, to, amount);
            if (to is not null && ContractManagement.GetContract(to) is not null)
                Contract.Call(to, Method.OnNEP17Payment, CallFlags.All, from, amount, data);
        }
    }
}
