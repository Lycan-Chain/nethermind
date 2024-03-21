// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading;

using Nethermind.Core.Extensions;
using Nethermind.Serialization.Json;

namespace Nethermind.Core.Crypto
{
    [JsonConverter(typeof(PublicKeyConverter))]
    public class PublicKey : IEquatable<PublicKey>
    {
        // Ensure that hashes are different for every run of the node and every node, so if are any hash collisions on
        // one node they will not be the same on another node or across a restart so hash collision cannot be used to degrade
        // the performance of the network as a whole.
        private static readonly uint s_instanceRandom = (uint)System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

        public const int PrefixedLengthInBytes = 65;
        public const int LengthInBytes = 64;
        private Address? _address;

        private byte[]? _prefixedBytes;
        private readonly int _hashCode;

        public PublicKey(string? hexString)
            : this(Core.Extensions.Bytes.FromHexString(hexString ?? throw new ArgumentNullException(nameof(hexString))))
        {
        }

        public PublicKey(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != LengthInBytes && bytes.Length != PrefixedLengthInBytes)
            {
                throw new ArgumentException($"{nameof(PublicKey)} should be {LengthInBytes} bytes long",
                    nameof(bytes));
            }

            if (bytes.Length == PrefixedLengthInBytes && bytes[0] != 0x04)
            {
                throw new ArgumentException(
                    $"Expected prefix of 0x04 for {PrefixedLengthInBytes} bytes long {nameof(PublicKey)}");
            }

            Bytes = bytes.Slice(bytes.Length - 64, 64).ToArray();
            _hashCode = GetHashCode(Bytes);
        }

        public Address Address
        {
            get
            {
                if (_address is null)
                {
                    LazyInitializer.EnsureInitialized(ref _address, ComputeAddress);
                }

                return _address;
            }
        }

        public byte[] Bytes { get; }

        public byte[] PrefixedBytes
        {
            get
            {
                if (_prefixedBytes is null)
                {
                    return LazyInitializer.EnsureInitialized(ref _prefixedBytes,
                        () => Core.Extensions.Bytes.Concat(0x04, Bytes));
                }

                return _prefixedBytes;
            }
        }

        public bool Equals(PublicKey? other)
        {
            if (other is null)
            {
                return false;
            }

            return Core.Extensions.Bytes.AreEqual(Bytes, other.Bytes);
        }

        private Address ComputeAddress()
        {
            Span<byte> hash = ValueKeccak.Compute(Bytes).BytesAsSpan;
            return new Address(hash[12..].ToArray());
        }

        public static Address ComputeAddress(ReadOnlySpan<byte> publicKeyBytes)
        {
            Span<byte> hash = ValueKeccak.Compute(publicKeyBytes).BytesAsSpan;
            return new Address(hash[12..].ToArray());
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PublicKey);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        private static int GetHashCode(byte[] bytes)
        {
            uint hash = s_instanceRandom;
            hash = BitOperations.Crc32C(hash, Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetArrayDataReference(bytes)));
            hash = BitOperations.Crc32C(hash, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(bytes), sizeof(long))));
            hash = BitOperations.Crc32C(hash, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(bytes), sizeof(long) * 2)));
            hash = BitOperations.Crc32C(hash, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(bytes), sizeof(long) * 3)));
            hash = BitOperations.Crc32C(hash, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(bytes), sizeof(long) * 4)));
            hash = BitOperations.Crc32C(hash, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(bytes), sizeof(long) * 5)));
            hash = BitOperations.Crc32C(hash, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(bytes), sizeof(long) * 6)));
            hash = BitOperations.Crc32C(hash, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(bytes), sizeof(long) * 7)));
            return (int)hash;
        }

        public override string ToString()
        {
            return Bytes.ToHexString(true);
        }

        public string ToString(bool with0X)
        {
            return Bytes.ToHexString(with0X);
        }

        public string ToShortString()
        {
            string value = Bytes.ToHexString(false);
            return $"{value[..6]}...{value[^6..]}";
        }

        public static bool operator ==(PublicKey? a, PublicKey? b)
        {
            if (a is null)
            {
                return b is null;
            }

            if (b is null)
            {
                return false;
            }

            return Core.Extensions.Bytes.AreEqual(a.Bytes, b.Bytes);
        }

        public static bool operator !=(PublicKey? a, PublicKey? b)
        {
            return !(a == b);
        }
    }

    public readonly struct PublicKeyAsKey(PublicKey key) : IEquatable<PublicKeyAsKey>
    {
        private readonly PublicKey _key = key;
        public PublicKey Value => _key;

        public static implicit operator PublicKey(PublicKeyAsKey key) => key._key;
        public static implicit operator PublicKeyAsKey(PublicKey key) => new(key);

        public bool Equals(PublicKeyAsKey other) => _key.Equals(other._key);
        public override int GetHashCode() => _key.GetHashCode();
    }
}
