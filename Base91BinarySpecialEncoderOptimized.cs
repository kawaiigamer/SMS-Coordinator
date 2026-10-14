using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace SMSChat
{
    internal class Base91BinarySpecialEncoderOptimized: Base91BinarySpecialEncoder
    {
        private const string Alphabet =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&()*+,./:;<=>?@[]^_`{|}~\"";

        private const int AlphabetLength = 91;
        private readonly sbyte[] DecodeTable;
        private readonly ushort[] EncodeTable;
        private sbyte[] BuildDecodeTable()
        {
            var table = new sbyte[128];
            table.AsSpan().Fill(-1);

            for (int i = 0; i < AlphabetLength; i++)
            {
                int c = Alphabet[i];
                table[c] = (sbyte)i;
            }

            return table;
        }

        public Base91BinarySpecialEncoderOptimized()
        {
            DecodeTable = BuildDecodeTable();
            EncodeTable = BuildEncodeTable();
        }

        private ushort[] BuildEncodeTable()
        {
            {
                var table = new ushort[16384];

                for (int value = 0; value < table.Length; value++)
                {
                    bool isValid =
                        value <= 8190 ||
                        (value & 8191) <= 88;

                    if (!isValid)
                        continue;

                    int highIndex = value / AlphabetLength;

                    if (highIndex >= AlphabetLength)
                        continue;

                    char low = Alphabet[value % AlphabetLength];
                    char high = Alphabet[highIndex];

                    table[value] = (ushort)(low | (high << 16));
                }

                return table;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Encode(ReadOnlySpan<byte> data, Span<char> output)
        {
            int required = GetMaxEncodedLength(data.Length);

            if (output.Length < required)
                return -1;

            int queue = 0;
            int nbits = 0;
            int charCount = 0;

            ref ushort encodeTableRef = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(EncodeTable);

            for (int i = 0; i < data.Length; i++)
            {
                queue |= data[i] << nbits;
                nbits += 8;

                if (nbits > 13)
                {
                    int value;
                    int consumedBits;

                    if ((queue & 8191) > 88)
                    {
                        value = queue & 8191;
                        queue >>= 13;
                        consumedBits = 13;
                    }
                    else
                    {
                        value = queue & 16383;
                        queue >>= 14;
                        consumedBits = 14;
                    }

                    ushort packed = Unsafe.Add(ref encodeTableRef, value);
                    output[charCount++] = (char)(packed & 0xFFFF);
                    output[charCount++] = (char)(packed >> 16);
                    nbits -= consumedBits;
                }
            }

            if (nbits > 0)
            {
                int value = queue & 16383;

                if (nbits > 7 || value > 88)
                {
                    ushort packed = Unsafe.Add(ref encodeTableRef, value);
                    output[charCount++] = (char)(packed & 0xFFFF);

                    if (nbits > 7 || value > 90)
                    {
                        output[charCount++] = (char)(packed >> 16);
                    }
                }
                else
                {
                    output[charCount++] = Alphabet[queue % AlphabetLength];
                }
            }

            return charCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decode(ReadOnlySpan<char> input, Span<byte> output)
        {
            int required = GetMaxDecodedLength(input.Length);

            if (output.Length < required)
                return -1;

            int queue = 0;
            int nbits = 0;
            int value = -1;
            int byteCount = 0;

            ref sbyte decodeTableRef = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(DecodeTable);

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c > 127)
                    continue;

                int index = Unsafe.Add(ref decodeTableRef, c);

                if (index < 0)
                    continue;

                if (value < 0)
                {
                    value = index;
                    continue;
                }

                value += index * AlphabetLength;
                queue |= value << nbits;
                nbits += (value & 8191) > 88 ? 13 : 14;

                do
                {
                    output[byteCount++] = (byte)queue;
                    queue >>= 8;
                    nbits -= 8;
                }
                while (nbits > 7);

                value = -1;
            }

            if (value >= 0)
            {
                output[byteCount++] = (byte)((queue | (value << nbits)) & 255);
            }

            return byteCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMaxEncodedLength(int byteLength) => byteLength * 123 / 100 + 2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMaxDecodedLength(int charLength) => charLength * 82 / 100 + 1;
    }
}
