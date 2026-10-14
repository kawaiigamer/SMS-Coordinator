using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SMSChat
{
    public sealed class AesGcmExtended
    {
        private readonly AesGcm _instance;
        private readonly string[] ErrorsDescr =  { 
                                                "Buffer is empty!",
                                                "Output buffer length must be >= input buffer length + AES header length",                                                
                                                "Input buffer length must be > AES header length",
                                                "Output buffer length must be >= input buffer length - AES header length",
                                                "CryptographicException while crypting operation!"
                                                };
        public readonly int NONCE_LENGTH = 12;
        public readonly int TAG_LENGTH;
        public readonly int HEADER_LENGTH;
        public readonly ReadOnlyMemory<byte> KEY;
        public string GetErrorDescr(int code)
        {
            return ErrorsDescr[Math.Abs(code)];
        }
        public AesGcmExtended(ReadOnlySpan<byte> key, int tag_length)
        {
            TAG_LENGTH = tag_length;
            HEADER_LENGTH = NONCE_LENGTH + TAG_LENGTH;
            KEY = key.ToArray();
            _instance = new AesGcm(key, tag_length);
        }
        public void Decrypt(ReadOnlySpan<Byte> nonce, ReadOnlySpan<Byte> ciphertext, ReadOnlySpan<Byte> tag, Span<Byte> plaintext) => _instance.Decrypt(nonce, ciphertext, tag, plaintext);
        public void Encrypt(ReadOnlySpan<Byte> nonce, ReadOnlySpan<Byte> plaintext, Span<Byte> ciphertext, Span<Byte> tag) => _instance.Encrypt(nonce, plaintext, ciphertext, tag);
    }
}
