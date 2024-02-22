// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Trie.Pruning
{
    /// <summary>
    /// Safe to be reused for the same wrapped store.
    /// </summary>
    public class ReadOnlyTrieStore : IReadOnlyTrieStore
    {
        private readonly TrieStore _trieStore;
        private readonly IReadOnlyKeyValueStore _publicStore;

        public ReadOnlyTrieStore(TrieStore trieStore)
        {
            _trieStore = trieStore ?? throw new ArgumentNullException(nameof(trieStore));
            _publicStore = _trieStore.TrieNodeRlpStore;
        }

        public TrieNode FindCachedOrUnknown(Hash256 hash) =>
            _trieStore.FindCachedOrUnknown(hash, true);

        public byte[]? TryLoadRlp(Hash256 hash, ReadFlags flags) => _trieStore.TryLoadRlp(hash, flags | ReadFlags.SkipWitness);
        public byte[] LoadRlp(Hash256 hash, ReadFlags flags) => _trieStore.LoadRlp(hash, flags | ReadFlags.SkipWitness);

        public bool IsPersisted(in ValueHash256 keccak) => _trieStore.IsPersisted(keccak);

        public IReadOnlyTrieStore AsReadOnly()
        {
            return new ReadOnlyTrieStore(_trieStore);
        }

        public void CommitNode(long blockNumber, NodeCommitInfo nodeCommitInfo, WriteFlags flags = WriteFlags.None) { }

        public void FinishBlockCommit(TrieType trieType, long blockNumber, TrieNode? root, WriteFlags flags = WriteFlags.None) { }

        public event EventHandler<ReorgBoundaryReached> ReorgBoundaryReached
        {
            add { }
            remove { }
        }

        public IReadOnlyKeyValueStore TrieNodeRlpStore => _publicStore;

        public void Set(in ValueHash256 hash, byte[] rlp)
        {
        }

        public bool HasRoot(Hash256 stateRoot)
        {
            return _trieStore.HasRoot(stateRoot);
        }

        public void Dispose() { }
    }
}
