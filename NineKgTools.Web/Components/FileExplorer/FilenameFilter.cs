using System.Text.RegularExpressions;

namespace NineKgTools.Components.FileExplorer;

public class FilenameFilter(string name, string hint, Regex? pattern)
{
    public string Name { get; private set; } = name;
    public string Hint { get; private set; } = hint;

    public static FilenameFilter Any()
    {
        return new FilenameFilter("All Files", ".*", new Regex(@".*"));
    }

    public static FilenameFilter Extension(string name, params string[] exts)
    {
        var combined = string.Join('|', exts.Select(Regex.Escape));
        var hint = string.Join(',', exts.Select(ext => "." + ext));
        return new FilenameFilter(name, hint, new Regex($"\\.({combined})$"));
    }

    public bool Matches(string? filename)
    {
        return filename != null && (pattern?.IsMatch(filename) ?? false);
    }
}