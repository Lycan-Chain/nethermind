using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;
using LeafEnumerator = System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<byte[],byte[]>>;

namespace Nethermind.Verkle.Tree;

public partial class VerkleStateStore : IVerkleStore, ISyncTrieStore
{
    private static Span<byte> RootNodeKey => Array.Empty<byte>();
    public Pedersen StateRoot { get; private set; }

    private readonly ILogger _logger;

    public Pedersen GetStateRoot()
    {
        InternalNode rootNode = RootNode ?? throw new InvalidOperationException("Root node should always be present");

        byte[] stateRoot = rootNode.InternalCommitment.Point.ToBytes().ToArray();
        return new Pedersen(stateRoot);
    }

    private static Pedersen? GetStateRoot(IVerkleDb db)
    {
        return db.GetInternalNode(RootNodeKey, out InternalNode? node) ? new Pedersen(node!.InternalCommitment.Point.ToBytes().ToArray()) : null;
    }

    private static Pedersen? GetStateRoot(InternalStore db)
    {
        return db.TryGetValue(RootNodeKey, out InternalNode? node) ? new Pedersen(node!.InternalCommitment.Point.ToBytes().ToArray()) : null;
    }

    // The underlying key value database
    // We try to avoid fetching from this, and we only store at the end of a batch insert
    private VerkleKeyValueDb Storage { get; }

    public VerkleStateStore(IDbProvider dbProvider, ILogManager logManager, int maxNumberOfBlocksInCache = 128)
    {
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(dbProvider);
        History = new VerkleHistoryStore(dbProvider);
        _stateRootToBlocks = new StateRootToBlockMap(dbProvider.StateRootToBlocks);
        BlockCache = maxNumberOfBlocksInCache == 0
            ? null
            : new StackQueue<(long, ReadOnlyVerkleMemoryDb)>(maxNumberOfBlocksInCache);
        MaxNumberOfBlocksInCache = maxNumberOfBlocksInCache;
        InitRootHash();
    }

    public VerkleStateStore(
        IDb leafDb,
        IDb internalDb,
        IDb forwardDiff,
        IDb reverseDiff,
        IDb stateRootToBlocks,
        ILogManager logManager,
        int maxNumberOfBlocksInCache = 128)
    {
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(internalDb, leafDb);
        History = new VerkleHistoryStore(forwardDiff, reverseDiff);
        _stateRootToBlocks = new StateRootToBlockMap(stateRootToBlocks);
        BlockCache = maxNumberOfBlocksInCache == 0
            ? null
            : new StackQueue<(long, ReadOnlyVerkleMemoryDb)>(maxNumberOfBlocksInCache);
        MaxNumberOfBlocksInCache = maxNumberOfBlocksInCache;
        InitRootHash();
    }

    public VerkleStateStore(
        IDb leafDb,
        IDb internalDb,
        IDb stateRootToBlocks,
        ILogManager logManager,
        int maxNumberOfBlocksInCache = 128)
    {
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(internalDb, leafDb);
        _stateRootToBlocks = new StateRootToBlockMap(stateRootToBlocks);
        BlockCache = maxNumberOfBlocksInCache == 0
            ? null
            : new StackQueue<(long, ReadOnlyVerkleMemoryDb)>(maxNumberOfBlocksInCache);
        MaxNumberOfBlocksInCache = maxNumberOfBlocksInCache;
        InitRootHash();
    }
    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore)
    {
        return new ReadOnlyVerkleStateStore(this, keyValueStore);
    }

    public void Reset() => BlockCache?.Clear();
    private InternalNode? RootNode => GetInternalNode(RootNodeKey);

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    private void InitRootHash()
    {
        InternalNode? node = RootNode;
        if (node is not null)
        {
            StateRoot = new Pedersen(node.InternalCommitment.Point.ToBytes());
            LastPersistedBlockNumber = _stateRootToBlocks[StateRoot];
            LatestCommittedBlockNumber = -1;
        }
        else
        {
            Storage.SetInternalNode(RootNodeKey, new InternalNode(VerkleNodeType.BranchNode));
            StateRoot = Pedersen.Zero;
            LastPersistedBlockNumber = LatestCommittedBlockNumber = -1;
        }

        // TODO: why should we store using block number - use stateRoot to index everything
        // but i think block number is easy to understand and it maintains a sequence
        if (LastPersistedBlockNumber == -2) throw new Exception("StateRoot To BlockNumber Cache Corrupted");
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key)
    {
        if (BlockCache is not null)
        {
            using StackQueue<(long, ReadOnlyVerkleMemoryDb)>.StackEnumerator diffs = BlockCache.GetStackEnumerator();
            while (diffs.MoveNext())
            {
                if (diffs.Current.Item2.LeafTable.TryGetValue(key.ToArray(), out byte[]? node)) return node;
            }
        }

        return Storage.GetLeaf(key, out byte[]? value) ? value : null;
    }

    public InternalNode? GetInternalNode(ReadOnlySpan<byte> key)
    {
        if (BlockCache is not null)
        {
            using StackQueue<(long, ReadOnlyVerkleMemoryDb)>.StackEnumerator diffs = BlockCache.GetStackEnumerator();
            while (diffs.MoveNext())
            {
                if (diffs.Current.Item2.InternalTable.TryGetValue(key, out InternalNode? node)) return node.Clone();
            }
        }

        return Storage.GetInternalNode(key, out InternalNode? value) ? value : null;
    }


}
