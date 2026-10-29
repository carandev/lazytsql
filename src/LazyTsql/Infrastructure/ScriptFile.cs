using System.Text;

namespace LazyTsql.Infrastructure;

/// <summary>
/// A script on disk. The editor works with <c>\n</c>; saving restores the encoding (BOM included) and line endings
/// the file was read with, so scripts from SSMS (UTF-16 or UTF-8 with BOM, CRLF) round-trip unchanged.
/// </summary>
public sealed record ScriptFile(string Path, Encoding Encoding, string NewLine)
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public string Name => System.IO.Path.GetFileName(Path);

    /// <summary>Reads the script; a missing file opens empty and is created on the first save.</summary>
    public static (ScriptFile Script, string Text) Open(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (Directory.Exists(fullPath)) throw new IOException($"{fullPath} is a directory.");
        if (!File.Exists(fullPath)) return (new ScriptFile(fullPath, Utf8WithoutBom, "\n"), string.Empty);

        using var reader = new StreamReader(fullPath, Utf8WithoutBom, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return (new ScriptFile(fullPath, reader.CurrentEncoding, newLine), text.ReplaceLineEndings("\n"));
    }

    public void Save(string text) => File.WriteAllText(Path, text.ReplaceLineEndings(NewLine), Encoding);
}
