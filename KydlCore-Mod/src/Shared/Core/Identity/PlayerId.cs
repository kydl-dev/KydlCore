namespace KydlCore.Core;

/// <summary>A player as pure code sees it: the game's player id (ZDO owner / ZNet peer uid) and a name for logs. 0 = nobody.</summary>
public readonly struct PlayerId
{
    public long Id { get; }
    public string Name { get; }
    public PlayerId(long id, string name) { Id = id; Name = name ?? ""; }
    public bool IsNobody => Id == 0;
    public static readonly PlayerId Nobody = new(0, "");
    public override string ToString() { return IsNobody ? "nobody" : Name + " (" + Id + ")"; }
}
