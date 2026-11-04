namespace MhguSave.Format.Tests;

public class GameRarityTests
{
    [Fact]
    public void NativeRaritiesUseZeroBasedPaletteAndKeepUnknownIdsNeutral()
    {
        Assert.Equal(0, GameRarity.Item(65535));
        Assert.Equal(0, GameRarity.Equipment(7, 65535));
        Assert.Equal("", GameRarity.Label(0));
        Assert.Equal("RARE X", GameRarity.Label(11));
        Assert.Equal(0, new CatalogChoice(9, "Skill").Rarity);
        if (!GameRarity.IsAvailable) return;
        Assert.Equal(1, GameRarity.Equipment(7, 1));
        Assert.Equal(6, GameRarity.Equipment(1, 446));
        Assert.Equal(1, GameRarity.Equipment(22, 1));
        Assert.Equal(1, GameRarity.Equipment(23, 1));
        Assert.Equal(1, GameRarity.Equipment(24, 1));
        Assert.Equal("#E6E6E6", GameRarity.Color(1));
        Assert.Equal("#708EF8", GameRarity.Color(6));
        Assert.Equal("#7D00FF", GameRarity.Color(11));
        for (var id = 1; id <= 10; id++) Assert.Equal(id, GameRarity.Equipment(6, id));
        for (var id = 1; id <= 2990; id++) Assert.InRange(GameRarity.Item(id), 1, 11);
        var catalog = EquipmentCatalog.LoadEmbedded();
        foreach (var type in catalog.Types)
            foreach (var piece in catalog.Search("", type.Code, 5000))
                Assert.True(piece.Rarity is >= 1 and <= 11, $"Missing rarity for {type.Name} #{piece.Id}: {piece.Name}");
        foreach (var jewel in EquipmentCatalog.LoadEmbedded().Decorations.Where(d => d.Id > 0))
        {
            Assert.Equal(GameRarity.Item(jewel.Id), jewel.Rarity);
            Assert.NotEmpty(jewel.RarityLabel);
        }
    }
}
