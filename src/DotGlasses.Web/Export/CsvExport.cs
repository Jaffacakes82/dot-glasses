using System.Text;

namespace DotGlasses.Web.Export;

/// <summary>Hand-rolled rather than a dependency — every export here is a flat, small-to-medium
/// row set with no need for streaming or RFC 4180 edge cases beyond basic quoting.</summary>
public static class CsvExport
{
    public static byte[] Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var sb = new StringBuilder();
        AppendRow(sb, headers);
        foreach (var row in rows)
        {
            AppendRow(sb, row);
        }

        // UTF-8 BOM so Excel (the realistic consumer of an admin CSV export) doesn't mis-detect
        // encoding and mangle non-ASCII names.
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(sb.ToString());
    }

    private static void AppendRow(StringBuilder sb, IEnumerable<string?> fields)
    {
        sb.AppendJoin(',', fields.Select(Escape));
        sb.Append("\r\n");
    }

    private static string Escape(string? field)
    {
        field ??= string.Empty;
        return field.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
    }
}
