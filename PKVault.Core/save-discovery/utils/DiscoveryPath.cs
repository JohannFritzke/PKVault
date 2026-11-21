namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// Purely lexical path helpers, OS-independent (so Linux/macOS rules are testable on Windows and vice versa).
/// Normalized form: forward slashes, no trailing slash, "." and ".." resolved,
/// uppercase drive letter. Roots are "C:/", "/" or "//server/share".
/// </summary>
public static class DiscoveryPath
{
    /// <summary>
    /// Returns the normalized absolute path, or null if path is empty or not absolute.
    /// </summary>
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        path = path.Trim().Trim('"').Replace('\\', '/');

        string prefix;
        string rest;

        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            if (path.Length > 2 && path[2] != '/')
                return null;    // "C:foo" is relative to drive current dir

            prefix = $"{char.ToUpperInvariant(path[0])}:/";
            rest = path.Length > 3 ? path[3..] : "";
        }
        else if (path.StartsWith("//"))
        {
            var parts = path[2..].Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return null;
            prefix = $"//{parts[0]}/{parts[1]}";
            rest = string.Join('/', parts.Skip(2));
            if (rest.Length > 0)
                prefix += '/';
        }
        else if (path.StartsWith('/'))
        {
            prefix = "/";
            rest = path[1..];
        }
        else
        {
            return null;
        }

        List<string> segments = [];
        foreach (var segment in rest.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }

        if (segments.Count == 0)
            return prefix.EndsWith('/') && prefix.StartsWith("//") ? prefix.TrimEnd('/') : prefix;

        return prefix + string.Join('/', segments);
    }

    /// <summary>
    /// Lexical combine. If relative is absolute it is returned (normalized).
    /// </summary>
    public static string? Combine(string? baseDir, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return Normalize(baseDir);

        var absolute = Normalize(relative);
        if (absolute != null)
            return absolute;

        if (baseDir == null)
            return null;

        return Normalize($"{baseDir.Replace('\\', '/').TrimEnd('/')}/{relative.Trim().Trim('"').Replace('\\', '/')}");
    }

    public static bool IsRoot(string normalized)
    {
        return normalized == "/"
            || (normalized.Length == 3 && normalized[1] == ':' && normalized[2] == '/')
            || (normalized.StartsWith("//") && normalized[2..].Split('/', StringSplitOptions.RemoveEmptyEntries).Length <= 2);
    }

    public static string? GetParent(string normalized)
    {
        if (IsRoot(normalized))
            return null;

        var index = normalized.LastIndexOf('/');
        if (index < 0)
            return null;

        var parent = normalized[..index];
        if (parent.Length == 0)
            return "/";
        if (parent.Length == 2 && parent[1] == ':')
            return parent + '/';
        return parent;
    }

    public static string GetFileName(string normalized)
    {
        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    public static StringComparison GetComparison(bool ignoreCase) => ignoreCase
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static StringComparer GetComparer(bool ignoreCase) => ignoreCase
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static bool PathEquals(string a, string b, bool ignoreCase) => string.Equals(a, b, GetComparison(ignoreCase));

    /// <summary>
    /// True if path is parent itself or any of its descendants (both normalized).
    /// </summary>
    public static bool IsSameOrInside(string path, string parent, bool ignoreCase)
    {
        if (PathEquals(path, parent, ignoreCase))
            return true;

        var parentWithSlash = parent.EndsWith('/') ? parent : parent + '/';
        return path.StartsWith(parentWithSlash, GetComparison(ignoreCase));
    }

    /// <summary>
    /// Expands "~" and "~/..." with the given home folder, then normalizes,
    /// resolving relative paths against baseDir.
    /// </summary>
    public static string? Resolve(string? value, string? baseDir, string? homeFolder)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim().Trim('"');

        if (homeFolder != null && (value == "~" || value.StartsWith("~/") || value.StartsWith("~\\")))
            return Combine(homeFolder, value.Length > 2 ? value[2..] : "");

        return Combine(baseDir, value);
    }
}
