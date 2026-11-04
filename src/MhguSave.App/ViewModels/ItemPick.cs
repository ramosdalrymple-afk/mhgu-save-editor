using MhguSave.Format;

namespace MhguSave.App.ViewModels;

/// <summary>A search hit when picking an item by name.</summary>
public sealed record ItemPick(int Id, string Name)
{
    public string IconKey => $"item:{Id}";
    public int Rarity => GameRarity.Item(Id);
    public string RarityLabel => GameRarity.Label(Rarity);

    public string Display => $"{Id}  {Name}" + (Rarity > 0 ? $" · {RarityLabel}" : "");
}
