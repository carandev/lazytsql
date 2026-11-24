using System.Text;

namespace LazyTsql.Infrastructure;

/// <summary>
/// The scratch script DataGrip calls a console: one per connection profile, opened by default the first time a tab
/// connects to it and reused after, created on disk only on its first save like any other untitled script.
/// </summary>
public static class ConnectionConsole
{
    /// <summary>Where <paramref name="profileName"/>'s console lives, next to the saved connections.</summary>
    public static string PathFor(string profileName) =>
        PathFor(Path.GetDirectoryName(FileConnectionStore.Default().Path)!, profileName);

    /// <summary>Overload taking the connections directory explicitly, so tests never touch the real config directory.</summary>
    public static string PathFor(string connectionsDirectory, string profileName) =>
        Path.Combine(connectionsDirectory, "consoles", $"{Slug(profileName)}.sql");

    /// <summary>
    /// A profile name as a safe file name: letters, digits, dash and underscore kept, runs of anything else collapsed
    /// to one dash and trimmed from the ends, falling back to a fixed name when nothing safe is left.
    /// </summary>
    private static string Slug(string name)
    {
        var slug = new StringBuilder(name.Length);
        var lastWasDash = true;
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
            {
                slug.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                slug.Append('-');
                lastWasDash = true;
            }
        }

        while (slug.Length > 0 && slug[^1] == '-') slug.Length--;
        return slug.Length == 0 ? "connection" : slug.ToString();
    }
}
