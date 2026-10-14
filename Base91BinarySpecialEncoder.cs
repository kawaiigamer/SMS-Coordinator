using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace SMSChat
{
    internal class Base91BinarySpecialEncoder : IBinaryDataSpecialEncoder
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&()*+,./:;<=>?@[]^_`{|}~\"";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Encode(ReadOnlySpan<byte> input, Span<char> output)
        {
            int queue = 0;
            int nbits = 0;
            int charCount = 0;

            for (int i = 0; i < input.Length; i++)
            {
                queue |= (input[i] & 255) << nbits;
                nbits += 8;

                if (nbits > 13)
                {
                    int val = queue & 8191;
                    if (val > 88)
                    {
                        queue >>= 13;
                        nbits -= 13;
                    }
                    else
                    {
                        val = queue & 16383;
                        queue >>= 14;
                        nbits -= 14;
                    }
                    output[charCount++] = Alphabet[val % 91];
                    output[charCount++] = Alphabet[val / 91];
                }
            }

            if (nbits > 0)
            {
                if (nbits > 7 || (queue & 8191) > 88)
                {
                    int val = queue & 16383;
                    output[charCount++] = Alphabet[val % 91];
                    if (nbits > 7 || val > 90)
                    {
                        output[charCount++] = Alphabet[val / 91];
                    }
                }
                else
                {
                    output[charCount++] = Alphabet[queue % 91];
                }
            }

            return charCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decode(ReadOnlySpan<char> input, Span<byte> output)
        {
            int queue = 0;
            int nbits = 0;
            int val = -1;
            int byteCount = 0;
            for (int i = 0; i < input.Length; i++)
            {
                int idx = Alphabet.IndexOf(input[i]);
                if (idx == -1) continue;
                if (val < 0)
                {
                    val = idx;
                }
                else
                {
                    val += idx * 91;
                    queue |= val << nbits;
                    nbits += ((val & 8191) > 88) ? 13 : 14;
                    do
                    {
                        output[byteCount++] = (byte)(queue & 255);
                        queue >>= 8;
                        nbits -= 8;
                    } while (nbits > 7);
                    val = -1;
                }
            }
            if (val >= 0)
            {
                output[byteCount++] = (byte)((queue | (val << nbits)) & 255);
            }
            return byteCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMaxEncodedLength(int byteLength) => (int)Math.Ceiling(byteLength *1.23) + 2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMaxDecodedLength(int charLength) => (int)Math.Ceiling(charLength * 0.82);
    }


}

