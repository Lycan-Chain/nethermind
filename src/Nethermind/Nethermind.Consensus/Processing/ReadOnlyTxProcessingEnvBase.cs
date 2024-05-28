// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Blockchain;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Consensus.Processing;

public class ReadOnlyTxProcessingEnvBase
{
    public IStateReader StateReader { get; protected set; }
    public IWorldState StateProvider { get; protected set; }
    public IBlockTree BlockTree { get; protected set; }
    public IBlockhashProvider BlockhashProvider { get; protected set; }

    public ISpecProvider SpecProvider { get; }
    protected ReadOnlyTxProcessingEnvBase(
        IWorldStateManager worldStateManager,
        IBlockTree readOnlyBlockTree,
        ISpecProvider? specProvider,
        ILogManager? logManager,
        PreBlockCaches? preBlockCaches = null
    ) {
        ArgumentNullException.ThrowIfNull(specProvider);
        ArgumentNullException.ThrowIfNull(worldStateManager);
        SpecProvider = specProvider;
        StateReader = worldStateManager.GlobalStateReader;
        StateProvider = worldStateManager.CreateResettableWorldState(preBlockCaches);
        BlockTree = readOnlyBlockTree ?? throw new ArgumentNullException(nameof(readOnlyBlockTree));
        BlockhashProvider = new BlockhashProvider(BlockTree, specProvider, StateProvider, logManager);
    }
    public void Reset()
    {
        StateProvider.Reset();
    }
}
