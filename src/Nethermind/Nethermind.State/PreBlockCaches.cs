// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Concurrent;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Trie;

namespace Nethermind.State;

public class PreBlockCaches
{
    public ConcurrentDictionary<StorageCell, byte[]> StorageCache { get; } = new(Environment.ProcessorCount * 2, 4096 * 4);
    public ConcurrentDictionary<AddressAsKey, Account> StateCache { get; } = new(Environment.ProcessorCount * 2, 4096 * 4);
    public ConcurrentDictionary<NodeKey, byte[]?> RlpCache { get; } = new(Environment.ProcessorCount * 2, 4096 * 4);
    public ConcurrentDictionary<PrecompileCacheKey, (ReadOnlyMemory<byte>, bool)> PrecompileCache { get; } = new(Environment.ProcessorCount * 2, 4096 * 4);

    public bool IsDirty => !(StorageCache.IsEmpty && StateCache.IsEmpty && RlpCache.IsEmpty && PrecompileCache.IsEmpty);

    public void Clear()
    {
        StorageCache.Clear();
        StateCache.Clear();
        RlpCache.Clear();
    }

    public readonly struct PrecompileCacheKey(Address address, ReadOnlyMemory<byte> data) : IEquatable<PrecompileCacheKey>
    {
        private Address Address { get; } = address;
        private ReadOnlyMemory<byte> Data { get; } = data;
        public bool Equals(PrecompileCacheKey other) => Address == other.Address && Data.Span.SequenceEqual(other.Data.Span);
        public override bool Equals(object? obj) => obj is PrecompileCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Address, Data);
    }
}
