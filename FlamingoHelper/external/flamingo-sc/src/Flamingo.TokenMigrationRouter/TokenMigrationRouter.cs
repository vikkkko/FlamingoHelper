﻿using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System.Numerics;
using System;

namespace Flamingo.TokenMigrationRouter;

public struct MigrationData
{
    public UInt160 v2;
    public BigInteger ratio;
}

[ManifestExtra("Author", "Neo")]
[ManifestExtra("Email", "dev@neo.org")]
[ManifestExtra("Description", "Flamingo Migration Contract")]
[ContractPermission("*", "*")]
public class TokenMigrationRouter : SmartContract
{
    private static readonly UInt160 owner = "0x091fea0578e9bb1ea859fd1711e5a8e3f65d4bea";

    private const byte Prefix_Owner = 0x02;
    public static readonly StorageMap OwnerMap = new StorageMap(Storage.CurrentContext, Prefix_Owner);
    private static readonly byte[] ownerKey = "owner".ToByteArray();
    private static bool IsOwner() => Runtime.CheckWitness(GetOwner());

    private const byte Prefix_MigrationData = 0x03;
    public static readonly StorageMap MigrationDataMap = new StorageMap(Storage.CurrentContext, Prefix_MigrationData);

    public static void _deploy(object data, bool update)
    {
        if (update) return;
        OwnerMap.Put(ownerKey, owner);
    }

    public static void SetOwner(UInt160 newOwner)
    {
        ExecutionEngine.Assert(IsOwner(), "No Authorization!");
        OwnerMap.Put(ownerKey, newOwner);
    }

    public static UInt160 GetOwner()
    {
        return (UInt160)OwnerMap.Get(ownerKey);
    }

    private static bool IsCalledByContract()
    {
        return Runtime.CallingScriptHash != null && ContractManagement.GetContract(Runtime.CallingScriptHash) != null;
    }

    private static bool IsValid()
    {
        return Runtime.CallingScriptHash.IsValid;
    }

    public static void SetData(UInt160 _v1, UInt160 _v2, BigInteger _ratio)
    {
        ExecutionEngine.Assert(IsOwner(), "No Authorization!");
        ExecutionEngine.Assert(_ratio <= 100 && _ratio > 0, "Invalid Ratio");
        var data = new MigrationData() { ratio = _ratio, v2 = _v2 };
        MigrationDataMap.Put(_v1, StdLib.Serialize(data));
    }

    public static void Update(ByteString nefFile, string manifest)
    {
        ExecutionEngine.Assert(IsOwner(), "No Authorization!");
        ContractManagement.Update(nefFile, manifest);
    }

    public static MigrationData GetData(UInt160 _v1)
    {
        var data = MigrationDataMap.Get(_v1);

        if (data is null)
            throw new InvalidOperationException("SetData First");

        return (MigrationData)StdLib.Deserialize(data);
    }

    public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
    {
    }

    [NoReentrant]
    public static void Migrate(UInt160 from, UInt160 v1, BigInteger amount)
    {
        ExecutionEngine.Assert(IsValid(), "Invalid address type.");
        ExecutionEngine.Assert(!IsCalledByContract(), "From can't be contract address.");
        ExecutionEngine.Assert(Runtime.CheckWitness(from), "Mismatched signature of from.");

        MigrationData data = GetData(v1);
        SafeTransfer(v1, from, Runtime.ExecutingScriptHash, amount);
        //SafeTransfer(data.v2, Runtime.ExecutingScriptHash, from, amount * data.ratio / 100);
        SafeMint(data.v2, from, amount * data.ratio / 100);
    }

    private static void SafeTransfer(UInt160 token, UInt160 from, UInt160 to, BigInteger amount)
    {
        try
        {
            var result = (bool)Contract.Call(token, "transfer", CallFlags.All, new object[] { from, to, amount, null });
            ExecutionEngine.Assert(result, "Transfer Fail in Router");
        }
        catch (Exception)
        {
            ExecutionEngine.Assert(false, "Transfer Error in Router");
        }
    }

    private static void SafeMint(UInt160 token, UInt160 to, BigInteger amount)
    {
        try
        {
            Contract.Call(token, "mint", CallFlags.All, new object[] { to, amount });
        }
        catch (Exception)
        {
            ExecutionEngine.Assert(false, "Mint Error in Router");
        }
    }
}
