namespace QuickResponseBao.Core.Services;

public readonly record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var core = value.Trim().TrimStart('v', 'V').Split('-', '+')[0].Split('.');
        var patch = 0;
        if (core.Length < 2 || !int.TryParse(core[0], out var major) ||
            !int.TryParse(core[1], out var minor) ||
            (core.Length > 2 && !int.TryParse(core[2], out patch))) return false;
        version = new SemanticVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(SemanticVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major) :
        Minor != other.Minor ? Minor.CompareTo(other.Minor) : Patch.CompareTo(other.Patch);

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
