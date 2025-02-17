using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Staking
{
    partial class FlamingoStaking
    {
        private static readonly UInt160 InitialOwner = "NMXY5eaTH1jBTMW8DinT4sRX8oSJ2RrNdK";

        [Safe]
        public static UInt160 GetOwner()
        {
            return OwnerStorage.Get();
        }

        public static bool SetOwner(UInt160 owner)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), $"SetOwner: CheckWitness failed, owner: ${owner.ToAddress()}");
            ExecutionEngine.Assert(CheckAddrValid(true, owner), $"SetOwner: invalid owner: {owner.ToAddress()}");
            OwnerStorage.Put(owner);
            return true;
        }

        public static bool AddAuthor(UInt160 author)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), $"AddAuthor: CheckWitness failed, owner: {GetOwner().ToAddress()}");
            ExecutionEngine.Assert(CheckAddrValid(true, author), $"AddAuthor: invalid author: {author.ToAddress()}");
            AuthorStorage.Put(author);
            return true;
        }

        [Safe]
        public static bool IsAuthor(UInt160 author)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, author), $"IsAuthor: invalid author: {author.ToAddress()}");
            return AuthorStorage.Get(author);
        }

        [Safe]
        public static BigInteger GetAuthorCount()
        {
            return AuthorStorage.Count();
        }

        [Safe]
        public static UInt160[] GetAllAuthor()
        {
            BigInteger count = GetAuthorCount();
            return AuthorStorage.Find(count);
        }

        public static bool RemoveAuthor(UInt160 author)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), $"RemoveAuthor: CheckWitness failed, owner: {GetOwner().ToAddress()}");
            ExecutionEngine.Assert(CheckAddrValid(true, author), $"RemoveAuthor: invalid author: {author.ToAddress()}");
            ExecutionEngine.Assert(AuthorStorage.Get(author), $"RemoveAuthor: not author: {author.ToAddress()}");
            AuthorStorage.Delete(author);
            return true;
        }
    }
}
