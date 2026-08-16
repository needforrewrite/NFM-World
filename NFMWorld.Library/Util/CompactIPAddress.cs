﻿using System.Buffers.Binary;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[MemoryPackable]
public readonly partial struct CompactIpAddress
{
    // ipv6: address
    private readonly InlineArray8<ushort> _addressBytes;
    
    private readonly bool _isv6;
 
    // ipv4: address. ipv4: scope id
    private readonly uint _addressOrScopeId;

    public CompactIpAddress(ReadOnlySpan<byte> address)
    {
        if (address.Length == 4)
        {
            _addressOrScopeId = BinaryPrimitives.ReadUInt32BigEndian(address);
            _isv6 = false;
        }
        else if (address.Length == 16)
        {
            ReadUInt16NumbersFromBytes(address, ref _addressBytes);
            _isv6 = true;
        }
        else
        {
            throw new ArgumentException("Bad IP address", nameof(address));
        }
    }

    public CompactIpAddress(ReadOnlySpan<byte> address, long scopeid)
    {
        ReadUInt16NumbersFromBytes(address, ref _addressBytes);
        _isv6 = true;
        _addressOrScopeId = (uint)scopeid;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadUInt16NumbersFromBytes(ReadOnlySpan<byte> address, ref InlineArray8<ushort> addressBytes)
    {
        if (Vector128.IsHardwareAccelerated && BitConverter.IsLittleEndian)
        {
            Vector128<ushort> ushorts = Vector128.Create(address).AsUInt16();
            // Reverse endianness of each ushort
            ushorts = (ushorts << 8) | (ushorts >> 8);
            ushorts.CopyTo(addressBytes);
        }
        else
        {
            for (int i = 0; i < 8; i++)
            {
                addressBytes[i] = BinaryPrimitives.ReadUInt16BigEndian(address[(i * 2)..]);
            }
        }
    }

    public static implicit operator IPAddress(CompactIpAddress address)
    {
        if (address._isv6)
        {
            Span<byte> bytes = stackalloc byte[16];
            for (int i = 0; i < 8; i++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(bytes.Slice(i * 2), address._addressBytes[i]);
            }
            return new IPAddress(bytes, (long)address._addressOrScopeId);
        }
        else
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, address._addressOrScopeId);
            return new IPAddress(bytes);
        }
    }
}