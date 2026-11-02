namespace MhguSave.Format;

public sealed record CatalogChoice(int Id, string Name, int Slots = 0)
{
    public string Display => Slots > 0 ? $"{Name} · {Slots} slot(s)" : Name;
    public string IconKey => Id > 0 ? $"item:{Id}" : string.Empty;
    public override string ToString() => Display;
}
