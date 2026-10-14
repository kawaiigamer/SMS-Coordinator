using System;
using System.Collections.Generic;
using System.Text;

namespace SMSChat
{
    internal interface IBinaryDataSpecialEncoder
    {
        public int Encode(ReadOnlySpan<byte> input, Span<char> output);
        public int Decode(ReadOnlySpan<char> input, Span<byte> output);
        public int GetMaxEncodedLength(int byteLength);
        public int GetMaxDecodedLength(int charLength);
    }
}
