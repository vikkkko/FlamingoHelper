using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.FLM
{
    partial class FLM
    {
        [DisplayName("AddAuthor")]
        public static event Action<UInt160> OnAddAuthor;

        [DisplayName("RemoveAuthor")]
        public static event Action<UInt160> OnRemoveAuthor;

        private static bool IsOwner() => Runtime.CheckWitness(GetOwner());

        [Safe]
        public static UInt160 GetOwner()
        {
            return OwnerStorage.Get();
        }

        public static bool SetOwner(UInt160 owner)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(GetOwner()), "SetOwner: CheckWitness failed, owner");
            ExecutionEngine.Assert(CheckAddrValid(true, owner), "SetOwner: invalid owner");
            OwnerStorage.Put(owner);
            return true;
        }

        [Safe]
        public static bool IsAuthor(UInt160 usr)
        {
            return AuthorStorage.Get(usr) || usr.Equals(GetOwner());
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

        public static bool AddAuthor(UInt160 newAuthor)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, newAuthor), "addAuthor: invalid newAuthor, newAuthor");
            ExecutionEngine.Assert(IsOwner() && newAuthor != GetOwner(), "addAuthor: CheckWitness failed, only owner can add other author");
            ExecutionEngine.Assert(!IsAuthor(newAuthor), "addAuthor: newAuthor is already a author");
            AuthorStorage.Put(newAuthor);
            OnAddAuthor(newAuthor);
            return true;
        }

        public static bool RemoveAuthor(UInt160 author)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, author), "removeAuthor: invalid author, author");
            ExecutionEngine.Assert(IsOwner() && author != GetOwner(), "removeAuthor: CheckWitness failed, only first pika can remove other author");
            ExecutionEngine.Assert(IsAuthor(author), "removeAuthor: author is not a author");
            AuthorStorage.Delete(author);
            OnRemoveAuthor(author);
            return true;
        }

        public static bool Mint(UInt160 minter, UInt160 receiver, BigInteger amount)
        {
            ExecutionEngine.Assert(CheckAddrValid(true, minter, receiver), "approve: invalid minter or receiver, usr");
            ExecutionEngine.Assert(amount >= 0, "mint:invalid amount");
            ExecutionEngine.Assert(IsAuthor(minter), "mint: author is an author");
            ExecutionEngine.Assert(Runtime.CheckWitness(minter) || minter.Equals(Runtime.CallingScriptHash), "mint: CheckWitness failed, author");

            amount = amount / ConvertDecimal;
            TransferInternal(UInt160.Zero, receiver, amount);
            return true;
        }

        public static void Update(ByteString nefFile, string manifest, object data)
        {
            ExecutionEngine.Assert(IsOwner(), "upgrade: Only allowed to be called by owner.");
            ContractManagement.Update(nefFile, manifest, data);
        }
    }
}
