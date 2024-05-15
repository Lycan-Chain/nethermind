// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.ApiBase;
using Nethermind.ApiBase.Extensions;
using Nethermind.Config;
using Nethermind.Consensus.AuRa.InitializationSteps;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Transactions;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Specs.ChainSpecStyle;

[assembly: InternalsVisibleTo("Nethermind.Merge.AuRa")]

namespace Nethermind.Consensus.AuRa
{
    /// <summary>
    /// Consensus plugin for AuRa setup.
    /// </summary>
    public class AuRaPlugin : IConsensusPlugin, INethermindPlugin<INethermindApi>, ISynchronizationPlugin, IInitializationPlugin<INethermindApi>
    {
        private AuRaNethermindApi? _nethermindApi;
        public string Name => SealEngineType;

        public string Description => $"{SealEngineType} Consensus Engine";

        public string Author => "Nethermind";

        public string SealEngineType => Core.SealEngineType.AuRa;


        public ValueTask DisposeAsync()
        {
            return default;
        }

        public Task Init(INethermindApi nethermindApi)
        {
            _nethermindApi = nethermindApi as AuRaNethermindApi;
            return Task.CompletedTask;
        }

        public Task InitNetworkProtocol()
        {
            return Task.CompletedTask;
        }

        public Task InitRpcModules()
        {
            return Task.CompletedTask;
        }

        public Task InitSynchronization()
        {
            if (_nethermindApi is not null)
            {
                _nethermindApi.BetterPeerStrategy = new AuRaBetterPeerStrategy(_nethermindApi.BetterPeerStrategy!, _nethermindApi.LogManager);
            }

            return Task.CompletedTask;
        }

        public Task<IBlockProducer> InitBlockProducer(IBlockProductionTrigger? blockProductionTrigger = null, ITxSource? additionalTxSource = null)
        {
            if (_nethermindApi is not null)
            {
                StartBlockProducerAuRa blockProducerStarter = new(_nethermindApi);
                DefaultBlockProductionTrigger ??= blockProducerStarter.CreateTrigger();
                return blockProducerStarter.BuildProducer(blockProductionTrigger ?? DefaultBlockProductionTrigger, additionalTxSource);
            }

            return Task.FromResult<IBlockProducer>(null);
        }

        public IBlockProductionTrigger? DefaultBlockProductionTrigger { get; private set; }

        public bool ShouldRunSteps(INethermindApi api) => true;

        public IBasicApiWithPlugins CreateApi(IConfigProvider configProvider, IJsonSerializer jsonSerializer, ILogManager logManager, ChainSpec chainSpec) => new AuRaNethermindApi(configProvider, jsonSerializer, logManager, chainSpec);
    }
}
