namespace QuickResponseBao.Core.Services;

public readonly record struct SemanticVersion(int Major, int Minor, int Patch, string? Prerelease = null) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim().TrimStart('v', 'V');
        var withoutBuild = normalized.Split('+', 2)[0];
        var split = withoutBuild.Split('-', 2);
        var core = split[0].Split('.');
        var patch = 0;
        if (core.Length is < 2 or > 3 || !int.TryParse(core[0], out var major) ||
            !int.TryParse(core[1], out var minor) ||
            (core.Length == 3 && !int.TryParse(core[2], out patch)) || major < 0 || minor < 0 || patch < 0) return false;
        var prerelease = split.Length == 2 ? split[1] : null;
        if (prerelease is not null && (prerelease.Length == 0 || prerelease.Split('.').Any(x => x.Length == 0))) return false;
        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var core = Major != other.Major ? Major.CompareTo(other.Major) :
            Minor != other.Minor ? Minor.CompareTo(other.Minor) : Patch.CompareTo(other.Patch);
        if (core != 0) return core;
        if (Prerelease is null) return other.Prerelease is null ? 0 : 1;
        if (other.Prerelease is null) return -1;
        var left = Prerelease.Split('.'); var right = other.Prerelease.Split('.');
        for (var index = 0; index < Math.Max(left.Length, right.Length); index++)
        {
            if (index >= left.Length) return -1;
            if (index >= right.Length) return 1;
            var leftNumber = int.TryParse(left[index], out var ln); var rightNumber = int.TryParse(right[index], out var rn);
            var comparison = leftNumber && rightNumber ? ln.CompareTo(rn) :
                leftNumber ? -1 : rightNumber ? 1 : string.CompareOrdinal(left[index], right[index]);
            if (comparison != 0) return comparison;
        }
        return 0;
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}{(Prerelease is null ? string.Empty : $"-{Prerelease}")}";
}
