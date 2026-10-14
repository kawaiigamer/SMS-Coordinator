using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static unsafe class CompactTextCodec
{
    private const byte CommonBase = 0;
    private const byte MainBase = 42;

    private const byte SpaceCode = 248;
    private const byte NewLineCode = 249;
    private const byte HiraganaMode = 250;
    private const byte KatakanaMode = 251;
    private const byte MainMode = 252;
    private const byte UnknownCode = 253;

    private const byte Invalid = 255;

    private const string Common =
        "!\"#$%&'()*+,-./0123456789:;<=>?@[\\]^_`{|}~！";

    private const string Main =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
        "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ" +
        "абвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
        "ЇЄҐїєґ";

    private const string Hiragana =
        "あいうえおかきくけこさしすせそたちつてとなにぬねの" +
        "はひふへほまみむめもやゆよらりるれろわをん" +
        "がぎぐげござじずぜぞだぢづでどばびぶべぼ" +
        "ぱぴぷぺぽぁぃぅぇぉっゃゅょゎ";

    private const string Katakana =
        "アイウエオカキクケコサシスセソタチツテトナニヌネノ" +
        "ハヒフヘホマミムメモヤユヨラリルレロワヲン" +
        "ガギグゲゴザジズゼゾダヂヅデドバビブベボ" +
        "パピプペポァィゥェォッャュョヮヵヶヴー";

    private const string JapanesePunctuation =
        "。，、〝〟「」『』【】⟨⟩〜：！？《》";

    private const string AsciiPunctuation =
        ".,^\"'[]{}()<>~:!?#@";

    private static readonly ushort* CharLookup;
    private static readonly byte* JapanesePunctToCode;
    private static readonly char* CodeToJapanesePunct;

    static CompactTextCodec()
    {
        CharLookup = (ushort*)NativeMemory.Alloc(
            (nuint)(char.MaxValue + 1), sizeof(ushort));

        JapanesePunctToCode = (byte*)NativeMemory.Alloc(
            (nuint)(char.MaxValue + 1), 1);

        CodeToJapanesePunct = (char*)NativeMemory.Alloc(
            (nuint)(char.MaxValue + 1), sizeof(char));

        NativeMemory.Fill(
            CharLookup,
            (nuint)(char.MaxValue + 1) * sizeof(ushort),
            0xFF);

        NativeMemory.Fill(
            JapanesePunctToCode,
            (nuint)(char.MaxValue + 1),
            Invalid);

        NativeMemory.Clear(
            CodeToJapanesePunct,
            (nuint)(char.MaxValue + 1) * sizeof(char));

        AddRow(Main, 0);
        AddRow(Hiragana, 1);
        AddRow(Katakana, 2);

        CharLookup['І'] = CharLookup['I'];
        CharLookup['і'] = CharLookup['i'];

        if (JapanesePunctuation.Length != AsciiPunctuation.Length)
            throw new InvalidOperationException(
                "Punctuation rows must have equal length.");

        if (MainBase + Main.Length >= SpaceCode)
            throw new InvalidOperationException(
                "Main row overlaps service codes.");

        for (int i = 0; i < JapanesePunctuation.Length; i++)
        {
            int code = Common.IndexOf(AsciiPunctuation[i]);

            if (code < 0)
                throw new InvalidOperationException(
                    "ASCII punctuation is absent in Common.");

            JapanesePunctToCode[JapanesePunctuation[i]] = (byte)code;
            CodeToJapanesePunct[code] = JapanesePunctuation[i];
        }
    }

    private static void AddRow(string row, byte mode)
    {
        for (int i = 0; i < row.Length; i++)
        {
            char c = row[i];

            if (CharLookup[c] != ushort.MaxValue)
                throw new InvalidOperationException("Duplicate symbol.");

            CharLookup[c] = (ushort)((mode << 8) | i);
        }
    }

    public static int Compress(
        ReadOnlySpan<char> input,
        Span<byte> output)
    {
        fixed (char* src = input)
        fixed (byte* dst = output)
        {
            int length = input.Length;
            int capacity = output.Length;
            int written = 0;
            byte mode = 0;

            for (int i = 0; i < length; i++)
            {
                char c = src[i];
                ushort packed = CharLookup[c];

                if (packed != ushort.MaxValue)
                {
                    byte nextMode = (byte)(packed >> 8);

                    if (mode != nextMode)
                    {
                        if (written >= capacity)
                            return -1;

                        dst[written++] = nextMode switch
                        {
                            1 => HiraganaMode,
                            2 => KatakanaMode,
                            _ => MainMode
                        };

                        mode = nextMode;
                    }

                    if (written >= capacity)
                        return -1;

                    dst[written++] = (byte)(MainBase + (packed & 0xFF));
                    continue;
                }

                byte punctuation = JapanesePunctToCode[c];

                if (punctuation != Invalid)
                {
                    if (mode == 0)
                    {
                        if (written >= capacity)
                            return -1;

                        dst[written++] = HiraganaMode;
                        mode = 1;
                    }

                    if (written >= capacity)
                        return -1;

                    dst[written++] = punctuation;
                    continue;
                }

                if (mode != 0)
                {
                    if (written >= capacity)
                        return -1;

                    dst[written++] = MainMode;
                    mode = 0;
                }

                byte code = c switch
                {
                    ' ' => SpaceCode,
                    '\n' => NewLineCode,
                    _ => GetCode(c)
                };

                if (written >= capacity)
                    return -1;

                dst[written++] = code;
            }

            return written;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetCode(char c)
    {
        int common = Common.IndexOf(c);

        if (common >= 0)
            return (byte)common;

        int main = Main.IndexOf(c);
        return main >= 0 ? (byte)(MainBase + main) : UnknownCode;
    }

    public static int Decompress(
        ReadOnlySpan<byte> input,
        Span<char> output)
    {
        fixed (byte* src = input)
        fixed (char* dst = output)
        {
            int length = input.Length;
            int capacity = output.Length;
            int written = 0;
            byte mode = 0;

            for (int i = 0; i < length; i++)
            {
                byte code = src[i];

                if (written >= capacity)
                    return -1;

                switch (code)
                {
                    case HiraganaMode:
                        mode = 1;
                        continue;

                    case KatakanaMode:
                        mode = 2;
                        continue;

                    case MainMode:
                        mode = 0;
                        continue;

                    case SpaceCode:
                        RequireMainMode(mode);
                        dst[written++] = ' ';
                        continue;

                    case NewLineCode:
                        RequireMainMode(mode);
                        dst[written++] = '\n';
                        continue;

                    case UnknownCode:
                        RequireMainMode(mode);
                        dst[written++] = '¿';
                        continue;
                }

                if (code < MainBase)
                {
                    if (code >= Common.Length)
                        throw new FormatException("Invalid common code.");

                    char japanese = CodeToJapanesePunct[code];

                    if (mode != 0)
                    {
                        if (japanese == '\0')
                            throw new FormatException(
                                "Only mapped punctuation is valid in Japanese mode.");

                        dst[written++] = japanese;
                        continue;
                    }

                    dst[written++] = Common[code];
                    continue;
                }

                int index = code - MainBase;

                if (mode == 0)
                {
                    if ((uint)index >= (uint)Main.Length)
                        throw new FormatException("Invalid main code.");

                    dst[written++] = Main[index];
                }
                else if (mode == 1)
                {
                    if ((uint)index >= (uint)Hiragana.Length)
                        throw new FormatException("Invalid hiragana code.");

                    dst[written++] = Hiragana[index];
                }
                else
                {
                    if ((uint)index >= (uint)Katakana.Length)
                        throw new FormatException("Invalid katakana code.");

                    dst[written++] = Katakana[index];
                }
            }

            return written;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RequireMainMode(byte mode)
    {
        if (mode != 0)
            throw new FormatException("This code requires main mode.");
    }
}