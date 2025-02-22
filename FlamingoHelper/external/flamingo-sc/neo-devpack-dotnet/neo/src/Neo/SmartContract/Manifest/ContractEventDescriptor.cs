// Copyright (C) 2015-2024 The Neo Project.
//
// ContractEventDescriptor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Array = Neo.VM.Types.Array;

namespace Neo.SmartContract.Manifest
{
    /// <summary>
    /// Represents an event in a smart contract ABI.
    /// </summary>
    public class ContractEventDescriptor : IInteroperable, IEquatable<ContractEventDescriptor>
    {
        /// <summary>
        /// The name of the event or method.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The parameters of the event or method.
        /// </summary>
        public ContractParameterDefinition[] Parameters { get; set; }

        public virtual void FromStackItem(StackItem stackItem)
        {
            Struct @struct = (Struct)stackItem;
            Name = @struct[0].GetString();
            Parameters = ((Array)@struct[1]).Select(p => p.ToInteroperable<ContractParameterDefinition>()).ToArray();
        }

        public virtual StackItem ToStackItem(IReferenceCounter referenceCounter)
        {
            return new Struct(referenceCounter)
            {
                Name,
                new Array(referenceCounter, Parameters.Select(p => p.ToStackItem(referenceCounter)))
            };
        }

        /// <summary>
        /// Converts the event from a JSON object.
        /// </summary>
        /// <param name="json">The event represented by a JSON object.</param>
        /// <returns>The converted event.</returns>
        public static ContractEventDescriptor FromJson(JObject json)
        {
            ContractEventDescriptor descriptor = new()
            {
                Name = json["name"].GetString(),
                Parameters = ((JArray)json["parameters"]).Select(u => ContractParameterDefinition.FromJson((JObject)u)).ToArray(),
            };
            if (string.IsNullOrEmpty(descriptor.Name)) throw new FormatException();
            _ = descriptor.Parameters.ToDictionary(p => p.Name);
            return descriptor;
        }

        /// <summary>
        /// Converts the event to a JSON object.
        /// </summary>
        /// <returns>The event represented by a JSON object.</returns>
        public virtual JObject ToJson()
        {
            var json = new JObject();
            json["name"] = Name;
            json["parameters"] = new JArray(Parameters.Select(u => u.ToJson()).ToArray());
            return json;
        }

        public bool Equals(ContractEventDescriptor other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;

            return Name == other.Name && Parameters.SequenceEqual(other.Parameters);
        }

        public override bool Equals(object other)
        {
            if (other is not ContractEventDescriptor ev)
                return false;

            return Equals(ev);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Parameters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ContractEventDescriptor left, ContractEventDescriptor right)
        {
            if (left is null || right is null)
                return Equals(left, right);

            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ContractEventDescriptor left, ContractEventDescriptor right)
        {
            if (left is null || right is null)
                return !Equals(left, right);

            return !left.Equals(right);
        }
    }
}
