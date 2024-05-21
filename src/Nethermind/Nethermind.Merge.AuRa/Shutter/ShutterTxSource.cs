// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Abi;
using Nethermind.Crypto;
using Nethermind.Blockchain.Filters;
using Nethermind.Blockchain.Find;
using Nethermind.Int256;
using Nethermind.Consensus.Producers;
using System.Runtime.CompilerServices;
using Nethermind.Serialization.Rlp;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Consensus.AuRa.Config;
using Nethermind.Logging;
using Nethermind.Consensus.Processing;
using Nethermind.Merge.AuRa.Shutter.Contracts;
using Nethermind.Core.Collections;
using Nethermind.Specs;

[assembly: InternalsVisibleTo("Nethermind.Merge.AuRa.Test")]

namespace Nethermind.Merge.AuRa.Shutter;

using G1 = Bls.P1;

public class ShutterTxSource : ITxSource
{
    public Dto.DecryptionKeys? DecryptionKeys;
    private bool _validatorsRegistered = false;
    private readonly ReadOnlyTxProcessingEnvFactory _readOnlyTxProcessingEnvFactory;
    private readonly IAbiEncoder _abiEncoder;
    private readonly ISpecProvider _specProvider;
    private readonly IAuraConfig _auraConfig;
    private readonly ILogger _logger;
    private readonly IEthereumEcdsa _ethereumEcdsa;
    private readonly SequencerContract _sequencerContract;
    private readonly Address ValidatorRegistryContractAddress;
    private readonly IEnumerable<(ulong, byte[])> ValidatorsInfo;
    private readonly UInt256 EncryptedGasLimit;

    public ShutterTxSource(ILogFinder logFinder, IFilterStore filterStore, ReadOnlyTxProcessingEnvFactory readOnlyTxProcessingEnvFactory, IAbiEncoder abiEncoder, IAuraConfig auraConfig, ISpecProvider specProvider, ILogManager logManager, IEthereumEcdsa ethereumEcdsa, IEnumerable<(ulong, byte[])> validatorsInfo)
        : base()
    {
        _readOnlyTxProcessingEnvFactory = readOnlyTxProcessingEnvFactory;
        _abiEncoder = abiEncoder;
        _auraConfig = auraConfig;
        _specProvider = specProvider;
        _logger = logManager.GetClassLogger();
        _ethereumEcdsa = ethereumEcdsa;
        _sequencerContract = new(auraConfig.ShutterSequencerContractAddress, logFinder, filterStore);
        ValidatorRegistryContractAddress = new(_auraConfig.ShutterValidatorRegistryContractAddress);
        ValidatorsInfo = validatorsInfo;
        EncryptedGasLimit = _auraConfig.ShutterEncryptedGasLimit;
    }

    public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit, PayloadAttributes? payloadAttributes = null)
    {
        IReadOnlyTransactionProcessor readOnlyTransactionProcessor = _readOnlyTxProcessingEnvFactory.Create().Build(parent.StateRoot!);
        ValidatorRegistryContract validatorRegistryContract = new(readOnlyTransactionProcessor, _abiEncoder, ValidatorRegistryContractAddress, _auraConfig, _specProvider, _logger);

        if (!_validatorsRegistered)
        {
            foreach ((ulong validatorIndex, byte[] validatorPubKey) in ValidatorsInfo)
            {
                if (!validatorRegistryContract!.IsRegistered(parent, validatorIndex, validatorPubKey))
                {
                    throw new Exception("Validator " + validatorIndex + " not registered as Shutter validator.");
                }
                _validatorsRegistered = true;
            }
        }

        // todo: add to specprovider
        ulong nextSlot = (((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ChiadoSpecProvider.BeaconChainGenesisTimestamp) / 5) + 1;
        if (DecryptionKeys is null || DecryptionKeys.Gnosis.Slot != nextSlot)
        {
            if (_logger.IsWarn) _logger.Warn($"Decryption keys not received for slot {nextSlot}, cannot include Shutter transactions");
            return [];
        }

        IEnumerable<SequencedTransaction> sequencedTransactions = GetNextTransactions(DecryptionKeys.Eon, DecryptionKeys.Gnosis.TxPointer);
        if (_logger.IsInfo) _logger.Info($"Got {sequencedTransactions.Count()} transactions from Shutter mempool...");

        IEnumerable<Transaction> transactions = sequencedTransactions.Zip(DecryptionKeys.Keys.Skip(1)).Select(x => DecryptSequencedTransaction(x.First, x.Second)).OfType<Transaction>();
        if (_logger.IsInfo) _logger.Info($"Decrypted {transactions.Count()} Shutter transactions...");

        transactions.ForEach((tx) =>
        {
            _logger.Info(tx.ToShortString());
        });

        return transactions;
    }

    internal Transaction? DecryptSequencedTransaction(SequencedTransaction sequencedTransaction, Dto.Key decryptionKey)
    {
        ShutterCrypto.EncryptedMessage encryptedMessage = ShutterCrypto.DecodeEncryptedMessage(sequencedTransaction.EncryptedTransaction);

        G1 key = new(decryptionKey.Key_.ToArray());
        G1 identity = ShutterCrypto.ComputeIdentity(decryptionKey.Identity.Span);

        if (!identity.is_equal(sequencedTransaction.Identity))
        {
            if (_logger.IsDebug) _logger.Debug("Could not decrypt Shutter transaction: Transaction identity did not match decryption key.");
            return null;
        }

        Transaction transaction;
        try
        {
            byte[] encodedTransaction = ShutterCrypto.Decrypt(encryptedMessage, key);
            transaction = Rlp.Decode<Transaction>(new Rlp(encodedTransaction));
            transaction.SenderAddress = _ethereumEcdsa.RecoverAddress(transaction, true);
        }
        catch (Exception e)
        {
            if (_logger.IsDebug) _logger.Debug("Could not decrypt Shutter transaction: " + e.Message);
            return null;
        }

        return transaction;
    }

    internal IEnumerable<SequencedTransaction> GetNextTransactions(ulong eon, ulong txPointer)
    {
        IEnumerable<ISequencerContract.TransactionSubmitted> events = _sequencerContract.GetEvents();
        _logger.Info("total events: " + events.Count());
        events = events.Where(e => e.Eon == eon);
        _logger.Info("tx pointer: " + txPointer);
        _logger.Info("this eon: " + events.Count());
        events = events.Skip((int)txPointer); //todo: check overflow
        _logger.Info("after skipping: " + events.Count());

        List<SequencedTransaction> txs = new List<SequencedTransaction>();
        UInt256 totalGas = 0;

        foreach (ISequencerContract.TransactionSubmitted e in events)
        {
            if (totalGas + e.GasLimit > EncryptedGasLimit)
            {
                _logger.Info("reached gas limit!");
                break;
            }

            SequencedTransaction sequencedTransaction = new()
            {
                Eon = eon,
                EncryptedTransaction = e.EncryptedTransaction,
                GasLimit = e.GasLimit,
                Identity = ShutterCrypto.ComputeIdentity(e.IdentityPrefix, e.Sender)
            };
            txs.Add(sequencedTransaction);

            _logger.Info("added tx");

            totalGas += e.GasLimit;
        }

        return txs;
    }

    internal struct SequencedTransaction
    {
        public ulong Eon;
        public byte[] EncryptedTransaction;
        public UInt256 GasLimit;
        public G1 Identity;
    }
}