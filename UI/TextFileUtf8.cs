using System.Text;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// テキストファイルを UTF-8 で読み書きする。BOM の有無を吸収し、
/// 旧来の ANSI（CP932）で保存されたファイルも読めるようにする。
/// </summary>
internal static class TextFileUtf8
{
    private static readonly UTF8Encoding Utf8StrictNoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static bool _codePagesRegistered;

    public static string ReadAllText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return DecodeBytes(bytes);
    }

    public static void WriteAllText(string path, string contents, bool emitBom = true)
    {
        File.WriteAllText(path, contents, emitBom ? Utf8WithBom : Utf8NoBom);
    }

    private static string DecodeBytes(byte[] bytes)
    {
        if (bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        try
        {
            return Utf8StrictNoBom.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            EnsureCodePagesRegistered();
            return Encoding.GetEncoding(932).GetString(bytes);
        }
    }

    private static void EnsureCodePagesRegistered()
    {
        if (_codePagesRegistered)
        {
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _codePagesRegistered = true;
    }
}
