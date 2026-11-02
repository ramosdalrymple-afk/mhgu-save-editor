namespace MhguSave.App.ViewModels;

/// <summary>A search hit when picking an item by name.</summary>
public sealed record ItemPick(int Id, string Name)
{
    public string IconKey => $"item:{Id}";
    public string Display => $"{Id}  {Name}";
}
