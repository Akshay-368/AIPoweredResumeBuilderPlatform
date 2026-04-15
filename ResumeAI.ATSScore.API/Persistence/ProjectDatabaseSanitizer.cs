using System.Text;
using System.Text.RegularExpressions;

namespace ResumeAI.ATSScore.API.Persistence;

public static class ProjectDatabaseSanitizer
{
    private static readonly Encoding DbEncoding = CreateDbSafeEncoding();
    private static readonly Encoding StrictDbEncoding = CreateStrictDbEncoding();
    private static readonly Regex JsonUnicodeEscapeRegex = new("\\\\u([0-9a-fA-F]{4})", RegexOptions.Compiled);

    public static string? SanitizeText(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var withoutNulls = input.Replace("\0", string.Empty);
        var encoded = DbEncoding.GetBytes(withoutNulls);
        return DbEncoding.GetString(encoded);
    }

    public static string? SanitizeJson(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var withoutNulls = input.Replace("\0", string.Empty);

        // JSONB parsing in PostgreSQL resolves \uXXXX escapes before encoding conversion.
        // On WIN1252 clusters, some Unicode points (for example \u2011) are unsupported,
        // so normalize those escapes to safe ASCII before writing JSON.
        var normalizedEscapes = JsonUnicodeEscapeRegex.Replace(withoutNulls, match =>
        {
            if (!int.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
            {
                return string.Empty;
            }

            if (IsRepresentableInDb(codePoint))
            {
                return match.Value;
            }

            return codePoint switch
            {
                0x00A0 => " ",
                0x2010 or 0x2011 or 0x2012 or 0x2013 or 0x2014 or 0x2015 or 0x2212 => "-",
                0x2022 => "-",
                0x2026 => "...",
                0xFEFF => string.Empty,
                _ => string.Empty
            };
        });

        var encoded = DbEncoding.GetBytes(normalizedEscapes);
        return DbEncoding.GetString(encoded);
    }

    private static Encoding CreateDbSafeEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            1252,
            new EncoderReplacementFallback(string.Empty),
            new DecoderReplacementFallback(string.Empty));
    }

    private static Encoding CreateStrictDbEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            1252,
            EncoderExceptionFallback.ExceptionFallback,
            DecoderExceptionFallback.ExceptionFallback);
    }

    private static bool IsRepresentableInDb(int codePoint)
    {
        try
        {
            var text = char.ConvertFromUtf32(codePoint);
            StrictDbEncoding.GetBytes(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}