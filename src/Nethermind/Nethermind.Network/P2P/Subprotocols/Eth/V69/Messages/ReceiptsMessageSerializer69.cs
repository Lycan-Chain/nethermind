// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using DotNetty.Buffers;
using Nethermind.Core.Specs;

namespace Nethermind.Network.P2P.Subprotocols.Eth.V69.Messages
{
    public class ReceiptsMessageSerializer69 :
        Nethermind.Network.P2P.Subprotocols.Eth.V66.Messages.ReceiptsMessageSerializer,
        IZeroInnerMessageSerializer<ReceiptsMessage69>
    {
        public ReceiptsMessageSerializer69(ISpecProvider specProvider) : base(new ReceiptsMessageInnerSerializer69(specProvider)) { }

        int IZeroInnerMessageSerializer<ReceiptsMessage69>.GetLength(ReceiptsMessage69 message, out int contentLength) =>
            base.GetLength(message, out contentLength);

        void IZeroMessageSerializer<ReceiptsMessage69>.Serialize(IByteBuffer byteBuffer, ReceiptsMessage69 message) =>
            base.Serialize(byteBuffer, message);

        ReceiptsMessage69 IZeroMessageSerializer<ReceiptsMessage69>.Deserialize(IByteBuffer byteBuffer)
        {
            V66.Messages.ReceiptsMessage message = base.Deserialize(byteBuffer);
            return new ReceiptsMessage69(message.RequestId, message.EthMessage);
        }
    }
}
