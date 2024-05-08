// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using Nethermind.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Facade.Proxy.Models.Simulate;
using Nethermind.Int256;
using Log = Nethermind.Facade.Proxy.Models.Simulate.Log;

namespace Nethermind.Facade.Simulate;

internal sealed class SimulateTxTracer : TxTracer, ILogsTxTracer
{
    private static readonly Hash256 transferSignature =
        new AbiSignature("Transfer", AbiType.Address, AbiType.Address, AbiType.UInt256).Hash;

    private static readonly Address Erc20Sender = new("0xEeeeeEeeeEeEeeEeEeEeeEEEeeeeEeeeeeeeEEeE");
    private readonly Hash256 _currentBlockHash;
    private readonly ulong _currentBlockNumber;
    private readonly Hash256 _txHash;
    private readonly ulong _txIndex;

    public SimulateTxTracer(bool isTracingTransfers, Hash256 txHash, ulong currentBlockNumber, Hash256 currentBlockHash,
        ulong txIndex)
    {
        _txHash = txHash;
        _currentBlockNumber = currentBlockNumber;
        _currentBlockHash = currentBlockHash;
        _txIndex = txIndex;
        IsTracingReceipt = true;
        IsTracingEvmActionLogs = isTracingTransfers;
        if (isTracingTransfers) IsTracingActions = true;
    }


    public SimulateCallResult? TraceResult { get; set; }

    public bool IsTracingEvmActionLogs { get; }

    public IEnumerable<LogEntry> ReportActionAndAddResultsToState(long gas, UInt256 value, Address from, Address to,
        ReadOnlyMemory<byte> input, ExecutionType callType, bool isPrecompileCall = false)
    {
        ReportAction(gas, value, from, to, input, callType, isPrecompileCall);
        var data = AbiEncoder.Instance.Encode(AbiEncodingStyle.Packed,
            new AbiSignature("", AbiType.UInt256), value);
        yield return new LogEntry(Erc20Sender, data, [transferSignature, AddressToHash256(from), AddressToHash256(to)]);
    }

    public override void MarkAsSuccess(Address recipient, long gasSpent, byte[] output, LogEntry[] logs,
        Hash256? stateRoot = null)
    {
        TraceResult = new SimulateCallResult
        {
            GasUsed = (ulong)gasSpent,
            ReturnData = output,
            Status = StatusCode.Success,
            Logs = logs.Select((entry, i) => new Log
            {
                Address = entry.LoggersAddress,
                Topics = entry.Topics,
                Data = entry.Data,
                LogIndex = (ulong)i,
                TransactionHash = _txHash,
                TransactionIndex = _txIndex,
                BlockHash = _currentBlockHash,
                BlockNumber = _currentBlockNumber
            }).ToList()
        };
    }

    public override void MarkAsFailed(Address recipient, long gasSpent, byte[] output, string error,
        Hash256? stateRoot = null)
    {
        TraceResult = new SimulateCallResult
        {
            GasUsed = (ulong)gasSpent,
            Error = new Error
            {
                Code = -32015, // revert error code stub
                Message = error
            },
            ReturnData = null,
            Status = StatusCode.Failure
        };
    }

    private Hash256 AddressToHash256(Address input)
    {
        var addressBytes = new byte[32];
        Array.Copy(input.Bytes, 0, addressBytes, 32 - Address.Size, Address.Size);
        return new Hash256(addressBytes);
    }
}
