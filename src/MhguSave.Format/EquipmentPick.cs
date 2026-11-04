namespace MhguSave.Format;

public sealed record EquipmentPick(int Id, string Name, int Type = 0)
{
    public int Rarity => GameRarity.Equipment(Type, Id);
    public string RarityLabel => GameRarity.Label(Rarity);
    public string IconKey => GameRarity.Icon(Type, Rarity);
    public string Display => $"{Name} · #{Id}";
}
