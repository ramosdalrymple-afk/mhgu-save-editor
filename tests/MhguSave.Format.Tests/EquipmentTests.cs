namespace MhguSave.Format.Tests;

public class EquipmentCatalogTests
{
    // Save IDs from Cotto's parallel Equip*IDs arrays, not positions in Equip*Names.
    [Theory]
    [InlineData(1, 446, "Rathalos Helm S")]
    [InlineData(1, 447, "Rathalos Cap S")]
    [InlineData(2, 446, "Rathalos Mail S")]
    [InlineData(2, 447, "Rathalos Vest S")]
    [InlineData(3, 446, "Rathalos Braces S")]
    [InlineData(3, 447, "Rathalos Guards S")]
    [InlineData(4, 446, "Rathalos Faulds S")]
    [InlineData(4, 447, "Rathalos Coat S")]
    [InlineData(5, 446, "Rathalos Greaves S")]
    [InlineData(5, 447, "Rathalos Leggings S")]
    public void RathalosSNamesResolveToActualSaveIds(int type, int saveId, string name)
    {
        var catalog = EquipmentCatalog.LoadEmbedded();
        Assert.Equal(name, catalog.NameOf(type, saveId));
        var pick = Assert.Single(catalog.Search(name, type, 5000), p => p.Name == name);
        Assert.Equal(saveId, pick.Id);
    }

    [Fact]
    public void OldRathalosListPositionsDescribeTheArmorActuallyStoredInTheGame()
    {
        var catalog = EquipmentCatalog.LoadEmbedded();
        Assert.Equal("Garuga Cap S", catalog.NameOf(1, 408));
        Assert.Equal("Ceanataur Mail R", catalog.NameOf(2, 401));
        Assert.Equal("Lavasioth Braces", catalog.NameOf(3, 393));
        Assert.Equal("Khezu Tassets R", catalog.NameOf(4, 377));
        Assert.Equal("Ceanataur Greaves R", catalog.NameOf(5, 401));
    }

    [Fact]
    public void OtherRanksAndPalicoArmorUseTheirExplicitIdMappings()
    {
        var catalog = EquipmentCatalog.LoadEmbedded();
        Assert.Equal("Rathalos Helm", catalog.NameOf(1, 150));
        Assert.Equal("Rathalos Helm X", catalog.NameOf(1, 916));
        Assert.Equal("F Rathalos Helm X", catalog.NameOf(23, 385));
        Assert.Equal("F Rathalos Mail X", catalog.NameOf(24, 385));
    }

    [Fact]
    public void EmbeddedCatalogNamesWeaponsArmorAndTalismans()
    {
        var catalog = EquipmentCatalog.LoadEmbedded();

        Assert.Equal("Petrified Blade", catalog.NameOf(7, 1));
        Assert.Equal("Leather Headgear", catalog.NameOf(1, 1));
        Assert.Equal("Petrified Daggers", catalog.NameOf(18, 1));
        Assert.Equal("Pawn Talisman", catalog.NameOf(6, 1));
        Assert.Equal(11, catalog.MaxLevel(7, 1));
    }

    [Fact]
    public void TypesCoverArmorWeaponsAndPalicoGear()
    {
        var catalog = EquipmentCatalog.LoadEmbedded();

        Assert.Equal("Head", catalog.TypeOf(1).Name);
        Assert.True(catalog.TypeOf(1).IsArmor);
        Assert.True(catalog.TypeOf(18).IsWeapon);
        Assert.True(catalog.TypeOf(22).IsPalico);
        Assert.Contains(catalog.Types, type => type.Kind == "talisman");
    }

    [Fact]
    public void SkillAndDecorationTablesAreNamed()
    {
        var catalog = EquipmentCatalog.LoadEmbedded();

        Assert.Equal("Poison", catalog.SkillName(1));
        Assert.Equal("Paralysis", catalog.SkillName(2));
        Assert.Equal("Antidote Jwl 1", catalog.DecorationName(2638));
        Assert.Equal(1, catalog.DecorationSlots(2638));
        Assert.Equal(2, catalog.DecorationSlots(2639));
    }

    [Fact]
    public void UnknownIdsFallBackToLabels()
    {
        var catalog = EquipmentCatalog.LoadEmbedded();

        Assert.Equal("Great Sword #9999", catalog.NameOf(7, 9999));
        Assert.Equal("Skill 900", catalog.SkillName(900));
        Assert.Equal(1, catalog.MaxLevel(7, 9999));
    }
}

public class EquipmentBoxTests
{
    private static EquipmentBox NewBox() => SaveContainer.Parse(SyntheticSave.Create(), "system").Slots[0].EquipmentBox;

    [Fact]
    public void AddingAPieceFillsTheFirstEmptyRecord()
    {
        var box = NewBox();
        box[0] = new EquipmentSlot { Type = 7, Id = 1, Level = 3 };

        var index = box.Add(new EquipmentSlot { Type = 18, Id = 2, Level = 1 });

        Assert.Equal(1, index);
        Assert.Equal(18, box[1].Type);
        Assert.Equal(2, box[1].Id);
        Assert.Equal(2, box.UsedCount());
    }

    [Fact]
    public void DecorationsAndTransmogRoundTrip()
    {
        var box = NewBox();

        box[5] = new EquipmentSlot
        {
            Type = 1,
            Id = 12,
            Level = 7,
            TransmogId = 1,
            TransmogLevel = 5,
            Deco1 = 2638,
            Deco2 = 2641,
        };

        var piece = box[5];
        Assert.Equal(1, piece.Type);
        Assert.Equal(12, piece.Id);
        Assert.Equal(7, piece.Level);
        Assert.Equal(1, piece.TransmogId);
        Assert.Equal(5, piece.TransmogLevel);
        Assert.Equal(2638, piece.Deco1);
        Assert.Equal(2641, piece.Deco2);
        Assert.Equal(0, piece.Deco3);
    }

    [Fact]
    public void TalismanSkillsAndCharmRoundTrip()
    {
        var box = NewBox();

        box[9] = new EquipmentSlot
        {
            Type = EquipmentSlot.TalismanType,
            Id = 3,
            Level = 1,
            Skill1 = 2,
            SkillPoints1 = 5,
            Skill2 = 15,
            SkillPoints2 = -3,
            SlotCount = 2,
            CharmItem = 356,
        };

        var piece = box[9];
        Assert.Equal(2, piece.Skill1);
        Assert.Equal(5, piece.SkillPoints1);
        Assert.Equal(15, piece.Skill2);
        Assert.Equal(-3, piece.SkillPoints2);
        Assert.Equal(2, piece.SlotCount);
        Assert.Equal(356, piece.CharmItem);
    }

    [Fact]
    public void KinsectIsKeptForInsectGlaivesOnly()
    {
        var box = NewBox();

        box[3] = new EquipmentSlot { Type = 20, Id = 4, Level = 1, KinsectId = 2, KinsectLevel = 6 };

        var piece = box[3];
        Assert.Equal(20, piece.Type);
        Assert.Equal(2, piece.KinsectId);
        Assert.Equal(6, piece.KinsectLevel);
    }

    [Fact]
    public void ClearingARecordZeroesEveryByte()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var box = save.Slots[0].EquipmentBox;
        box[0] = new EquipmentSlot { Type = 2, Id = 30, Level = 4 };

        box[0] = new EquipmentSlot();

        Assert.True(box[0].IsEmpty);
        Assert.Equal(0, box.UsedCount());
    }

    [Fact]
    public void LevelsOutsideTheBitFieldAreRejected()
    {
        var box = NewBox();

        Assert.Throws<ArgumentOutOfRangeException>(() => box[0] = new EquipmentSlot { Type = 1, Id = 1, Level = 33 });
        Assert.Throws<ArgumentOutOfRangeException>(() => box[0] = new EquipmentSlot { Type = 32, Id = 1, Level = 1 });
    }
}
