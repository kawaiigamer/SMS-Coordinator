using SMSChat;
using System;
using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;
class Program
{
    private static bool text_full_cycle_test(string original_text, AesGcmExtended aes, IBinaryDataSpecialEncoder encoder)
    {
        const int MAX_ENCODED_B91_CHARS_LENGTH = 160;
        const int MAX_MESSAGE_BYTES_LENGTH = 140;        
        int MAX_DECODED_ENCRYPTED_BYTES_LENGTH = encoder.GetMaxDecodedLength(MAX_MESSAGE_BYTES_LENGTH);
        int MAX_ENCRYPTED_BYTES_LENGTH = MAX_DECODED_ENCRYPTED_BYTES_LENGTH - aes.HEADER_LENGTH;
        int MAX_DECRYPTED_BYTES_LENGTH = MAX_ENCRYPTED_BYTES_LENGTH;
        Console.WriteLine($"\n    ------ Next test ------\n\n\n0.  Prepearing full cycle[text(utf-8) -> bytes -> AES-256-GCM -> bytes -> base91 ---SENDING---> base91 -> bytes -> AES-256-GCM -> bytes -> text(utf-8)]");
        Console.WriteLine(System.String.Join("\n        ", "    PER EACH MESSAGE:",
                            $"Max base91: {MAX_ENCODED_B91_CHARS_LENGTH} chars",  $"Max: {MAX_MESSAGE_BYTES_LENGTH} bytes", 
                            $"Max encoded with base91: {MAX_ENCODED_B91_CHARS_LENGTH} chars", $"AES-256 GCM uses header: {aes.HEADER_LENGTH} bytes",
                            $"Max decoded from base91 and encrypted with AES-256 GCM(includes header): {MAX_DECODED_ENCRYPTED_BYTES_LENGTH} bytes",
                            $"Max decoded from base91 and encrypted with AES-256 GCM(excludes header): {MAX_ENCRYPTED_BYTES_LENGTH} bytes",
                            $"Max decrypted from AES-256 GCM bytes: {MAX_DECRYPTED_BYTES_LENGTH}",
                            $"All this means each message can constants AT MOST {MAX_ENCRYPTED_BYTES_LENGTH} utf-8 symbols",
                            $"Anyway, length of plain utf-8 text MUST BE smaller then {MAX_ENCRYPTED_BYTES_LENGTH} in bytes!"
            ));

        Span<byte> plain_buffer = stackalloc byte[MAX_ENCRYPTED_BYTES_LENGTH];
        Span<byte> encrypt_buffer = stackalloc byte[MAX_DECODED_ENCRYPTED_BYTES_LENGTH];
        Span<char> encode_buffer = stackalloc char[MAX_ENCODED_B91_CHARS_LENGTH];
        Console.WriteLine($"1.  |Stackallocated| static fixed buffers: key[{aes.KEY.Length}], plain[{plain_buffer.Length}], encrypted[{encrypt_buffer.Length}], encoded[{encode_buffer.Length} chars].\n        |Generated| random crypter key: {Convert.ToHexString(aes.KEY.ToArray())}");

        int original_text_bytes_length = Encoding.UTF8.GetByteCount(original_text);
        Console.WriteLine($"2.  |Original| string[{original_text.Length} symbols/{original_text_bytes_length} bytes]: {original_text}");
        int compressed_bytes = CompactTextCodec.Compress(original_text.AsSpan(), plain_buffer);
        Console.WriteLine($"2.1     |Compressed| to {compressed_bytes} bytes: {Convert.ToHexString(plain_buffer.Slice(0, compressed_bytes))}");
        if (compressed_bytes > MAX_DECRYPTED_BYTES_LENGTH)
        {
            Console.WriteLine($"    |Error| original utf-8 string length in bytes is too long! Max allowed length: {MAX_DECRYPTED_BYTES_LENGTH}");
            return false;
        }

        int encrypted_bytes = AES256GCMTextSpectialCrypter.Encrypt(plain_buffer.Slice(0, compressed_bytes), aes, encrypt_buffer);
        if (encrypted_bytes <= 0)
        {
            Console.WriteLine($"3.  |Error| while AES-256 GCM encrypting: {aes.GetErrorDescr(encrypted_bytes)}");
            return false;
        }
        Console.WriteLine($"3.  |Encrypted| plain bytes using AES-256 GCM[{encrypted_bytes}(includes header)/{encrypted_bytes - aes.HEADER_LENGTH}(excludes header)]: {Convert.ToHexString(encrypt_buffer.Slice(0, encrypted_bytes))}");
               
        int encoded_chars = encoder.Encode(encrypt_buffer.Slice(0, encrypted_bytes), encode_buffer);
        Console.WriteLine($"4.  |Encoded| encrypted message to base91[{encoded_chars} chars]: {encode_buffer.ToString()}");

        plain_buffer.Clear();
        encrypt_buffer.Clear();
        Console.WriteLine($"\n-- Buffers for plain and encrypt were cleared! --\n");

        int decoded_bytes = encoder.Decode(encode_buffer.Slice(0, encoded_chars), encrypt_buffer);
        Console.WriteLine($"5   |Decoded| from base91 to bytes[{decoded_bytes}]: {Convert.ToHexString(encrypt_buffer.Slice(0, decoded_bytes))}");
        
        int decrypted_bytes = AES256GCMTextSpectialCrypter.Decrypt(encrypt_buffer.Slice(0, decoded_bytes), aes, plain_buffer);
        if (decrypted_bytes <= 0)
        {
            Console.WriteLine($"6.  |Error| while AES-256 GCM decrypting: {aes.GetErrorDescr(decrypted_bytes)}");
            return false;
        }
        Console.WriteLine($"6.  |Decrypted| encrypted message using AES-256 GCM to plain bytes[{decrypted_bytes}]: {Convert.ToHexString(plain_buffer.Slice(0, decrypted_bytes))}");

        int decompressed_bytes = CompactTextCodec.Decompress(plain_buffer.Slice(0, compressed_bytes), encode_buffer);
        if (decompressed_bytes < 0) 
        {
            Console.WriteLine($"7.  |Error| while decompressing. The source data is corrupted or malformed!");
            return false;
        }
        Console.WriteLine($"7.  |Decompressed| to {decompressed_bytes} chars: {encode_buffer.Slice(0, decompressed_bytes).ToString()}");

        string final_text = encode_buffer.Slice(0, decompressed_bytes).ToString();
        Console.WriteLine($"8.  |Converted| back plain bytes to utf-8 string: {final_text}");

        Console.WriteLine($"9.  |Result| original and restored string are equlas: {original_text == final_text}");
        return original_text == final_text;
    }

    static void Main(string[] args)
    {
        const string some_utf8_text = "とあるけしたテキストをできるようにするにはどんなをするべきなのかな"; //"とある文字化けしたテキストをできるようにするにはどんな行動をするべきなのかな";
        const string some_ascii_text = "absxy!io12345";
        const string some_overall_text = "Вітаємо! おはよう！ Good morning!";
        const string unexpected_symbols_overall_text = " 時間が経っている" + some_overall_text + "最優先";
        const int tag_length = 16;
        ReadOnlySpan<byte> key = RandomNumberGenerator.GetBytes(32);
        AesGcmExtended aes = new AesGcmExtended(key, tag_length);
        IBinaryDataSpecialEncoder encoder = new Base91BinarySpecialEncoderOptimized();

        text_full_cycle_test(some_utf8_text, aes, encoder);
        text_full_cycle_test(some_ascii_text, aes, encoder);
        text_full_cycle_test(some_overall_text, aes, encoder);
        text_full_cycle_test(unexpected_symbols_overall_text, aes, encoder);

        //byte[] second_key = RandomNumberGenerator.GetBytes(32);


    }
}
