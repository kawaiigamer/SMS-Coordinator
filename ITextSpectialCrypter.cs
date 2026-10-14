using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SMSChat
{
    internal interface ITextSpectialCrypter
    {
        public static abstract int Encrypt(ReadOnlySpan<byte> input, AesGcmExtended aes, Span<byte> output);
        public static abstract int Decrypt(ReadOnlySpan<byte> input, AesGcmExtended aes, Span<byte> output);

    }
}
