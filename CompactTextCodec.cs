
using System;
using System.Runtime.CompilerServices;

public static class CompactTextCodec
{
    private const int MaxChars = 1024;

    private const byte CommonBase = 0;
    private const byte MainBase = 42;

    private const byte SpaceCode = 141;
    private const byte NewLineCode = 142;

    private const byte HiraganaMode = 143;
    private const byte KatakanaMode = 144;
    private const byte MainMode = 145;
    private const byte UnknownCode = 146;

    private const byte Invalid = 255;

    private const string Common =
        "!\"#$%&'()*+,-./0123456789:;<=>?@[\\]^_`{|}~！";

    private const string Main =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
        "БГДЖЗИЙЛПФЦЧШЩЪЫЬЭЮЯЇЄҐ" +
        "бвгджзийлпфцчшщъыьэюяїєґ";

    private const string Hiragana =
        "あいうえおかきくけこさしすせそたちつてとなにぬねの" +
        "はひふへほまみむめもやゆよらりるれろわをん" +
        "がぎぐげござじずぜぞだぢづでどばびぶべぼ" +
        "ぱぴぷぺぽぁぃぅぇぉっゃゅょゎ";

    // Исправлены ゼ・ゾ вместо ошибочно повторённых デ・ド
    // в последовательности, указанной в исходном сообщении.
    private const string Katakana =
        "アイウエオカキクケコサシスセソタチツテトナニヌネノ" +
        "ハヒフヘホマミムメモヤユヨラリルレロワヲン" +
        "ガギグゲゴザジズゼゾダヂヅデドバビブベボ" +
        "パピプペポァィゥェォッャュョヮヵヶヴー";

    private static readonly byte[] CharToCode = BuildCharTable();

    private static byte[] BuildCharTable()
    {
        var table = new byte[char.MaxValue + 1];
        Array.Fill(table, Invalid);

        if (Common.Length != 42)
            throw new InvalidOperationException("Common length must be 42.");

        if (Main.Length > 99)
            throw new InvalidOperationException("Main row exceeds 99 symbols.");

        if (Hiragana.Length > Main.Length ||
            Katakana.Length > Main.Length)
            throw new InvalidOperationException(
                "Japanese rows must fit the main row.");

        for (int i = 0; i < Common.Length; i++)
        {
            char c = Common[i];
            if (table[c] != Invalid)
                throw new InvalidOperationException("Duplicate common symbol.");

            table[c] = (byte)(CommonBase + i);
        }

        for (int i = 0; i < Main.Length; i++)
        {
            char c = Main[i];
            if (table[c] != Invalid)
                throw new InvalidOperationException("Duplicate main symbol.");

            table[c] = (byte)(MainBase + i);
        }

        table[' '] = SpaceCode;
        table['\n'] = NewLineCode;

        return table;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindIndex(string row, char c)
    {
        return row.IndexOf(c);
    }

    // Максимальный результат: 2 * input.Length байт.
    // Возвращает фактическую длину записанных данных.
    public static unsafe int Compress(
        ReadOnlySpan<char> input,
        Span<byte> output)
    {
        if (output.Length < input.Length * 2)
            throw new ArgumentException("Output buffer is too small.");

        int written = 0;
        int mode = 0; // 0 = main, 1 = hiragana, 2 = katakana

        fixed (char* src = input)
        fixed (byte* dst = output)
        {
            for (int i = 0; i < input.Length; i++)
            {
                if (written >= output.Length)
                {
                    return -1;
                }
                char c = src[i];

                int hi = FindIndex(Hiragana, c);
                int ki = FindIndex(Katakana, c);

                if (hi >= 0 || ki >= 0)
                {
                    int nextMode = hi >= 0 ? 1 : 2;

                    if (mode != nextMode)
                    {
                        dst[written++] =
                            nextMode == 1 ? HiraganaMode : KatakanaMode;

                        mode = nextMode;
                    }

                    int index = hi >= 0 ? hi : ki;
                    dst[written++] = (byte)(MainBase + index);
                    continue;
                }

                // Неяпонский символ требует возврата в основной ряд.
                if (mode != 0)
                {
                    dst[written++] = MainMode;
                    mode = 0;
                }

                byte code = CharToCode[c];

                dst[written++] =
                    code == Invalid ? UnknownCode : code;
            }
        }

        return written;
    }

    // Возвращает число записанных UTF-16 символов.
    public static unsafe int Decompress(
        ReadOnlySpan<byte> input,
        Span<char> output)
    {
        if (output.Length < input.Length * 2)
            throw new ArgumentException("Output buffer is too small."); 

        int written = 0;
        int mode = 0;

        fixed (byte* src = input)
        fixed (char* dst = output)
        {
            for (int i = 0; i < input.Length; i++)
            {   
                if (written >= output.Length)
                {
                    return -1;
                }
                byte code = src[i];

                if (code == HiraganaMode)
                {
                    mode = 1;
                    continue;
                }

                if (code == KatakanaMode)
                {
                    mode = 2;
                    continue;
                }

                if (code == MainMode)
                {
                    mode = 0;
                    continue;
                }

                if (code == UnknownCode)
                {
                    if (mode != 0)
                        throw new FormatException(
                            "Unknown symbol must be in main mode.");

                    dst[written++] = '¿';
                    continue;
                }

                if (code == SpaceCode)
                {
                    if (mode != 0)
                        throw new FormatException("Invalid space code.");

                    dst[written++] = ' ';
                    continue;
                }

                if (code == NewLineCode)
                {
                    if (mode != 0)
                        throw new FormatException("Invalid newline code.");

                    dst[written++] = '\n';
                    continue;
                }

                if (code < MainBase)
                {
                    if (mode != 0)
                        throw new FormatException(
                            "Common symbols require main mode.");

                    int index = code - CommonBase;

                    if ((uint)index >= (uint)Common.Length)
                        throw new FormatException("Invalid common code.");

                    dst[written++] = Common[index];
                    continue;
                }

                int mainIndex = code - MainBase;

                if (mode == 1)
                {
                    if ((uint)mainIndex >= (uint)Hiragana.Length)
                        throw new FormatException("Invalid hiragana code.");

                    dst[written++] = Hiragana[mainIndex];
                }
                else if (mode == 2)
                {
                    if ((uint)mainIndex >= (uint)Katakana.Length)
                        throw new FormatException("Invalid katakana code.");

                    dst[written++] = Katakana[mainIndex];
                }
                else
                {
                    if ((uint)mainIndex >= (uint)Main.Length)
                        throw new FormatException("Invalid main code.");

                    dst[written++] = Main[mainIndex];
                }
            }
        }

        return written;
    }
}
