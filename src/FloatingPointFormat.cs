using System.Runtime.InteropServices;
using System.Buffers;
using System;

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
        /// Does: storage = storage << n for byte arrays.
        /// </summary>
        private static void LeftShift(in Span<byte> storage, int n)
        {
            if (n < 0)
                throw new ArgumentOutOfRangeException(nameof(n));
            byte carryMask = 0x00;
            for (int i = 0; i < storage.Length; i++)
            {
                // Shift and apply carry bit
                byte temp = (byte)(((storage[i] << 1) | carryMask) & 0xFF);
                // Carry HI bit to next LO
                carryMask = (byte)((storage[i] >> 8) & 0xFF);
                storage[i] = temp;
            }
        }

        /// <summary>
        /// Does: (1 << n) - 1 for byte arrays.
        /// </summary>
        private static void OneLeftShfitByNMinusOne(in Span<byte> storage, int n)
        {
            if (n < 0)
                throw new ArgumentOutOfRangeException(nameof(n));
            int bytes = n / 8,
                remainder = n % 8;
            for (int i = 0; i < bytes; i++)
                storage[i] = 0xFF;
            if (remainder != 0)
                storage[bytes + 1] = (byte)((1 << remainder) - 1);
        }

        private static void FillMax(in Span<byte> storage)
        {
            for(int i = 0; i < storage.Length; i++)
                storage[i] = 0xFF;
        }

        private ushort* MarshalSpan2(in Span<byte> storage)
        {
            return &MemoryMarshal.GetReference(storage);
        }

        public static readonly FloatingPointFormat Binary16 =   new FloatingPointFormat(Radix.Two, 16,  15,  1, 10,  5,  0, 10 , "Half");
        public static readonly FloatingPointFormat Binary32 =   new FloatingPointFormat(Radix.Two, 32,  31,  1, 22,  8,  0, 22 , "Single");
        public static readonly FloatingPointFormat Binary64 =   new FloatingPointFormat(Radix.Two, 64,  63,  1, 52,  11, 0, 52 , "Double");
        public static readonly FloatingPointFormat Binary128 =  new FloatingPointFormat(Radix.Two, 128, 127, 1, 112, 15, 0, 112, "Quadruple");
        public static readonly FloatingPointFormat Binary256 =  new FloatingPointFormat(Radix.Two, 256, 255, 1, 236, 19, 0, 236, "Octuple");
    }
}