// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;

namespace Nethermind.Core.Extensions
{
    public static class ByteArrayExtensions
    {
        public static byte[] Xor(this byte[] bytes, byte[] otherBytes)
        {
            if (bytes.Length != otherBytes.Length)
            {
                throw new InvalidOperationException($"Trying to xor arrays of different lengths: {bytes.Length} and {otherBytes.Length}");
            }

            byte[] result = new byte[bytes.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (byte)(bytes[i] ^ otherBytes[i]);
            }

            return result;
        }

        public static byte[] Slice(this byte[] bytes, int startIndex)
        {
            byte[] slice = new byte[bytes.Length - startIndex];
            Buffer.BlockCopy(bytes, startIndex, slice, 0, bytes.Length - startIndex);
            return slice;
        }

        public static byte[] Slice(this byte[] bytes, int startIndex, int length)
        {
            if (length == 1)
            {
                return [bytes[startIndex]];
            }

            byte[] slice = new byte[length];
            Buffer.BlockCopy(bytes, startIndex, slice, 0, length);
            return slice;
        }

        public static ReadOnlySpan<byte> SliceWithZeroPaddingEmptyOnError(this ReadOnlySpan<byte> bytes, int startIndex, int length)
        {
            int copiedFragmentLength = Math.Min(bytes.Length - startIndex, length);
            return copiedFragmentLength <= 0 ? ReadOnlySpan<byte>.Empty : bytes.Slice(startIndex, copiedFragmentLength);
        }

        public static Span<byte> SliceWithZeroPaddingEmptyOnError(this Span<byte> bytes, int startIndex, int length)
        {
            int copiedFragmentLength = Math.Min(bytes.Length - startIndex, length);
            return copiedFragmentLength <= 0 ? Span<byte>.Empty : bytes.Slice(startIndex, copiedFragmentLength);
        }
    }
}
