using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

namespace Flamingo.Staking
{
    partial class FlamingoStaking
    {
        [Safe]
        public static bool IsPaused()
        {
            return PauseStorage.Get();
        }

        [Safe]
        public static bool IsStakingPaused()
        {
            return PauseStakingStorage.Get();
        }

        [Safe]
        public static bool IsRefundPaused()
        {
            return PauseRefundStorage.Get();
        }

        public static bool Pause(UInt160 author)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(author), $"Pause: CheckWitness failed, author: {author.ToAddress()}");
            ExecutionEngine.Assert(IsAuthor(author), $"Pause: not author: {author.ToAddress()}");
            PauseStorage.Put(1);
            return true;
        }

        public static bool UnPause(UInt160 author)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(author), $"Unpause: CheckWitness failed, author: {author.ToAddress()}");
            ExecutionEngine.Assert(IsAuthor(author), $"Unpause: not author: {author.ToAddress()}");
            PauseStorage.Put(0);
            return true;
        }

        public static bool PauseStaking(UInt160 author)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(author), $"PauseStaking: CheckWitness failed, author: {author.ToAddress()}");
            ExecutionEngine.Assert(IsAuthor(author), $"PauseStaking: not author: {author.ToAddress()}");
            PauseStakingStorage.Put(1);
            return true;
        }

        public static bool UnPauseStaking(UInt160 author)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(author), $"UnPauseStaking: CheckWitness failed, author: {author.ToAddress()}");
            ExecutionEngine.Assert(IsAuthor(author), $"UnPauseStaking: not author: {author.ToAddress()}");
            PauseStakingStorage.Put(0);
            return true;
        }

        public static bool PauseRefund(UInt160 author)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(author), $"PauseRefund: CheckWitness failed, author: {author.ToAddress()}");
            ExecutionEngine.Assert(IsAuthor(author), $"PauseRefund: not author: {author.ToAddress()}");
            PauseRefundStorage.Put(1);
            return true;
        }

        public static bool UnPauseRefund(UInt160 author)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(author), $"UnPauseRefund: CheckWitness failed, author: {author.ToAddress()}");
            ExecutionEngine.Assert(IsAuthor(author), $"UnPauseRefund: not author: {author.ToAddress()}");
            PauseRefundStorage.Put(0);
            return true;
        }
    }
}
