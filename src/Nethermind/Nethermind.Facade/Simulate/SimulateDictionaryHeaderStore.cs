// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Nethermind.Blockchain.Headers;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Facade.Simulate;

/// <summary>
/// This type is needed for two things:
///  - Bypass issue of networking compatibility and RLPs not supporting BaseFeePerGas of 0
///  - Improve performance to get faster local caching without re-encoding of data in Simulate blocks
/// </summary>
/// <param name="readonlyBaseHeaderStore"></param>
public class SimulateDictionaryHeaderStore(IHeaderStore readonlyBaseHeaderStore) : IHeaderStore
{
    // SyncProgressResolver MaxLookupBack is 256, add 16 wiggle room
    public const int CacheSize = 256 + 16;

    private readonly Dictionary<Hash256, BlockHeader> _headerDict = new();
    private readonly Dictionary<Hash256, long> _blockNumberDict = new();

    public void Insert(BlockHeader header)
    {
        _headerDict[header.Hash] = header;
        InsertBlockNumber(header.Hash, header.Number);
    }

    public BlockHeader? Get(Hash256 blockHash, bool shouldCache = false, long? blockNumber = null)
    {
        if (blockNumber == null)
        {
            blockNumber = GetBlockNumber(blockHash);
        }

        if (blockNumber.HasValue && _headerDict.TryGetValue(blockHash, out var header))
        {
            if (shouldCache)
            {
                Cache(header);
            }
            return header;
        }

        header = readonlyBaseHeaderStore.Get(blockHash, shouldCache, blockNumber);
        if (header != null && shouldCache)
        {
            Cache(header);
        }
        return header;
    }

    public void Cache(BlockHeader header)
    {
        Insert(header);
    }

    public void Delete(Hash256 blockHash)
    {
        _headerDict.Remove(blockHash);
        _blockNumberDict.Remove(blockHash);
    }

    public void InsertBlockNumber(Hash256 blockHash, long blockNumber)
    {
        _blockNumberDict[blockHash] = blockNumber;
    }

    public long? GetBlockNumber(Hash256 blockHash)
    {
        if (_blockNumberDict.TryGetValue(blockHash, out var blockNumber))
        {
            return blockNumber;
        }

        return readonlyBaseHeaderStore.GetBlockNumber(blockHash);
    }
}