using System.Security.Cryptography;

namespace SMSChat
{
    internal class AES256GCMTextSpectialCrypter : ITextSpectialCrypter
    {        
        public static int Encrypt(ReadOnlySpan<byte> input, AesGcmExtended aes, Span<byte> output)
        {
            if (input.IsEmpty || output.IsEmpty)
            {
                return 0;
            }
            int expected_length = aes.HEADER_LENGTH + input.Length;
            if (output.Length < expected_length)
            {
                return -1;
            }
            Span<byte> nonce = output.Slice(0, aes.NONCE_LENGTH);
            Span<byte> tag = output.Slice(aes.NONCE_LENGTH, aes.TAG_LENGTH);
            Span<byte> ciphertext = output.Slice(aes.HEADER_LENGTH, input.Length);
            RandomNumberGenerator.Fill(nonce);
            try
            {                
                aes.Encrypt(nonce, input, ciphertext, tag);
            }
            catch (CryptographicException)
            {
                return -4;
            }
            return expected_length;
        }

        public static int Decrypt(ReadOnlySpan<byte> input, AesGcmExtended aes, Span<byte> output)
        {
            if (input.IsEmpty || output.IsEmpty)
            {
                return 0;
            }
            if (input.Length < aes.HEADER_LENGTH)
            {
                return -2;
            }
            int ciphertext_length = input.Length - aes.HEADER_LENGTH;
            if (output.Length < ciphertext_length)
            {
                return -3;
            }
            ReadOnlySpan<byte> nonce = input.Slice(0, aes.NONCE_LENGTH);
            ReadOnlySpan<byte> tag = input.Slice(aes.NONCE_LENGTH, aes.TAG_LENGTH);
            ReadOnlySpan<byte> ciphertext = input.Slice(aes.HEADER_LENGTH);
            try
            {
                aes.Decrypt(nonce, ciphertext, tag, output.Slice(0, ciphertext_length));             
            }
            catch (CryptographicException)
            {                
                return -4;
            }
            return ciphertext_length;
        }
    }
}
