using JetBrains.Annotations;
using Realms;

namespace OsuPlayer.IO.Storage.LazerModels.Beatmaps;

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
[MapTo("Ruleset")]
public class RulesetInfo : RealmObject, IEquatable<RulesetInfo>, IComparable<RulesetInfo>, IRulesetInfo
{
    public RulesetInfo(string shortName, string name, string instantiationInfo, int onlineID)
    {
        ShortName = shortName;
        Name = name;
        InstantiationInfo = instantiationInfo;
        OnlineID = onlineID;
    }

    [UsedImplicitly]
    public RulesetInfo()
    {
    }

    public bool Available { get; set; }

    public int CompareTo(RulesetInfo other)
    {
        if (OnlineID >= 0 && other.OnlineID >= 0)
            return OnlineID.CompareTo(other.OnlineID);

        // Official rulesets are always given precedence for the time being.
        if (OnlineID >= 0)
            return -1;
        if (other.OnlineID >= 0)
            return 1;

        return string.Compare(ShortName, other.ShortName, StringComparison.Ordinal);
    }

    public bool Equals(RulesetInfo? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other == null) return false;

        return ShortName == other.ShortName;
    }

    [PrimaryKey] public string ShortName { get; set; } = string.Empty;

    [Indexed] public int OnlineID { get; set; } = -1;

    public string Name { get; set; } = string.Empty;

    public string InstantiationInfo { get; set; } = string.Empty;

    public bool Equals(IRulesetInfo? other)
    {
        return other is RulesetInfo r && Equals(r);
    }

    public int CompareTo(IRulesetInfo other)
    {
        if (!(other is RulesetInfo ruleset))
            throw new ArgumentException($@"Object is not of type {nameof(RulesetInfo)}.", nameof(other));

        return CompareTo(ruleset);
    }

    public override int GetHashCode()
    {
        // Importantly, ignore the underlying realm hash code, as it will usually not match.
        var hashCode = new HashCode();
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        hashCode.Add(ShortName);
        return hashCode.ToHashCode();
    }

    public override string ToString()
    {
        return Name;
    }
}