namespace MhguSave.Format;

public sealed record CatalogChoice(int Id, string Name, int Slots = 0, bool IsItem = false)
{
    public string Display => (Slots > 0 ? $"{Name} · {Slots} slot(s)" : Name) + (Rarity > 0 ? $" · {RarityLabel}" : "");
    public string IconKey => Id > 0 ? $"item:{Id}" : string.Empty;
    public int Rarity => IsItem ? GameRarity.Item(Id) : 0;
    public string RarityLabel => GameRarity.Label(Rarity);
    public override string ToString() => Display;
}
