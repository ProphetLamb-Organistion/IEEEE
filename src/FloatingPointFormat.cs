using System.Runtime.InteropServices;
using System.Buffers;
using System;
using System.Runtime.CompilerServices;

namespace IEEE754StorageStringParser
{
    public enum Radix : byte
    {
        Two = 2,
        Ten = 10,
    }

    public unsafe class FloatingPointFormat
    {
        public readonly Radix Radix;
        public readonly int ByteSize, BitSize;
        public readonly int SignOffset, SignLength;
        public readonly int ExponentOffset, ExponentLength;
        public readonly int MantissaOffset, MantissaLength;
        public readonly string Name;

        public readonly byte[] SignMask;
        public readonly byte[] ExponentMask;
        public readonly byte[] MantissaMask;

        public FloatingPointFormat(Radix radix, int size,
            int signOffset, int signLength,
            int exponentOffset, int exponentLength,
            int mantissaOffset, int mantissaLength,
            string name)
        {
            Radix = radix;
            ByteSize = size;
            BitSize = size * 8;
            SignOffset = signOffset;
            SignLength = signLength;
            ExponentOffset = exponentOffset;
            ExponentLength = exponentLength;
            MantissaOffset = mantissaOffset;
            MantissaLength = mantissaLength;
            Name = name;

            // Validate arguments
            ThrowWhenOutOfRange(signOffset, signLength, BitSize);
            ThrowWhenOutOfRange(exponentOffset, exponentLength, BitSize);
            ThrowWhenOutOfRange(mantissaOffset, mantissaLength, BitSize);

            // Comute masks
            SignMask = new byte[size];
            ComputeMask(SignMask, signOffset, signLength);
            ExponentMask = new byte[size];
            ComputeMask(ExponentMask, exponentOffset, exponentLength);
            MantissaMask = new byte[size];
            ComputeMask(MantissaMask, mantissaOffset, mantissaLength);
        }

        private static void ThrowWhenOutOfRange(int bitOffset, int bitCount, int bitLength)
        {
            if (bitOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(bitOffset));
            if (bitCount < 0)
                throw new ArgumentOutOfRangeException(nameof(bitCount));
            if (bitOffset + bitCount > bitLength)
                throw new IndexOutOfRangeException();
        }

        /// <summary>
        /// Computes the bit mask for the storage array.
        /// </summary>
        /// <param name="storage">The storage array representing the bits of the floating point number.</param>
        /// <param name="bitOffset">Number of bits from the 0th bit to the 1st bit to set.</param>
        /// <param name="bitCount">Number of bits from the offset bit to set.</param>
        private static void ComputeMask(in Span<byte> storage, int bitOffset, int bitCount)
        {
            int bitLength = storage.Length * 8;
            if (bitCount > bitLength)
                bitCount = bitLength;
            if (bitCount == bitLength)
            {
                if (bitOffset < 0)
                {
                    // (1 << (count + offset))- 1;
                    OneLeftShfitByNMinusOne(storage, bitCount + bitOffset);
                }
                else //if (offset >= 0)
                {
                    // 0xFF... << offset
                    FillMax(storage);
                    LeftShift(storage, bitOffset);
                }
            }
            else if (bitOffset < 0)
            {
                int totalBits = bitCount - bitOffset;
                if (totalBits <= 0)
                {
                    // Leave empty, nothing to fill: out of range
                }
                else
                {
                    // (1 << total) - 1
                    OneLeftShfitByNMinusOne(storage, totalBits);
                }
            }
            else
            {
                // ((1 << count) - 1) << offset
                OneLeftShfitByNMinusOne(storage, bitCount);
                LeftShift(storage, bitOffset);
            }
        }

        /// <summary>
        /// Performs following integer arithmetic operation on byte arrays: storage = storage << n.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LeftShift(in Span<byte> storage, int n)
        {
            if (n < 0)
                throw new ArgumentOutOfRangeException(nameof(n));
            fixed(byte* storagePtr = &MemoryMarshal.GetReference(storage))
            {
                switch (storage.Length)
                {
                    case 1:
                        *storagePtr <<= 1;
                        break;
                    case 2:
                        *(ushort*)storagePtr <<= 1;
                        break;
                    case 4:
                        *(uint*)storagePtr <<= 1;
                        break;
                    case 8:
                        *(ulong*)storagePtr <<= 1;
                        break;
                    default:
                        byte carryMask = 0x00;
                        for (int i = 0; i < storage.Length; i++)
                        {
                            // Shift and apply carry bit
                            byte temp = (byte)(((storagePtr[i] << 1) | carryMask) & 0xFF);
                            // Carry HI bit to next LO
                            carryMask = (byte)((storagePtr[i] >> 7) & 0xFF);
                            storagePtr[i] = temp;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Performs following integer arithmetic operation on byte arrays: (1 << n) - 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void OneLeftShfitByNMinusOne(in Span<byte> storage, int n)
        {
            if (n < 0)
                throw new ArgumentOutOfRangeException(nameof(n));
            fixed(byte* storagePtr = &MemoryMarshal.GetReference(storage))
            {
                switch (storage.Length)
                {
                    case 1:
                        *storagePtr = (byte)(((1 << n) - 1) & 0xFF);
                        break;
                    case 2:
                        *(ushort*)storagePtr = (ushort)(((1 << n) - 1) & 0xFFFF);
                        break;
                    case 4:
                        *(uint*)storagePtr = ((1u << n) - 1) & 0xFFFFFFFF;
                        break;
                    case 8:
                        *(ulong*)storagePtr = ((1ul << n) - 1) & 0xFFFFFFFFFFFFFFFF;
                        break;
                    default:
                        int bytes = n / 8,
                            remainder = n % 8;
                        for (int i = 0; i < bytes; i++)
                            storagePtr[i] = 0xFF;
                        if (remainder != 0)
                            storagePtr[bytes + 1] = (byte)((1 << remainder) - 1);
                        break;
                }
            }
        }
        /// <summary>
        /// Sets all bytes to 0xFF.
        /// </summary>
        /// <param name="storage"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FillMax(in Span<byte> storage)
        {
            fixed(byte* storagePtr = &MemoryMarshal.GetReference(storage))
            {
                switch (storage.Length)
                {
                    case 1:
                        *storagePtr = 0xFF;
                        break;
                    case 2:
                        *(ushort*)storagePtr = 0xFFFF;
                        break;
                    case 4:
                        *(uint*)storagePtr = 0xFFFFFFFF;
                        break;
                    case 8:
                        *(ulong*)storagePtr = 0xFFFFFFFFFFFFFFFF;
                        break;
                    default:
                        for(int i = 0; i < storage.Length; i++)
                            storagePtr[i] = 0xFF;
                    break;
                }
            }
        }

        public static readonly FloatingPointFormat Binary16 =   new FloatingPointFormat(Radix.Two, 16,  15,  1, 10,  5,  0, 10 , "Half");
        public static readonly FloatingPointFormat Binary32 =   new FloatingPointFormat(Radix.Two, 32,  31,  1, 22,  8,  0, 22 , "Single");
        public static readonly FloatingPointFormat Binary64 =   new FloatingPointFormat(Radix.Two, 64,  63,  1, 52,  11, 0, 52 , "Double");
        public static readonly FloatingPointFormat Binary128 =  new FloatingPointFormat(Radix.Two, 128, 127, 1, 112, 15, 0, 112, "Quadruple");
        public static readonly FloatingPointFormat Binary256 =  new FloatingPointFormat(Radix.Two, 256, 255, 1, 236, 19, 0, 236, "Octuple");
    }
}