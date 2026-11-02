namespace MhguSave.Format;

public sealed record EquipmentPick(int Id, string Name, int Type = 0)
{
    public string IconKey => $"equipment:{Type}";
    public string Display => $"{Name} · #{Id}";
}
