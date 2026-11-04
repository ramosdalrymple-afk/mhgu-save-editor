using System.Text;

namespace MhguSave.Format;

/// <summary>
/// One character slot. Every field is <c>base + offset</c>, with <c>base</c> taken from
/// the pointer table at <c>0x34</c> (relative to <c>0x24</c>) so the layout also holds for
/// slots 2 and 3 and for saves with a different stride.
/// </summary>
public sealed partial class CharacterSlot
{
    // Slot header (summary rebuilt by the game from live state on each save).
    private const int NameOffset = 0x000;
    private const int PlayTimeOffset = 0x020;
    private const int HeaderZennyOffset = 0x024;
    private const int HrOffset = 0x028;
    private const int BodyTypeOffset = 0x02A;
    private const int HidePlayTimeOffset = 0x02B;

    private const int ItemBoxOffset = 0x278;
    private const int PouchOffset = 0x27BF;

    // Authoritative copies inside sUserInfo / sPlayer.
    private const int HrPointsOffset = 0x280B;
    private const int ZennyOffset = 0x280F;
    private const int WycademyOffset = 0x2817;
    private const int PlayerRecordNameOffset = 0x23B7D;

    private const int QuestClearMapOffset = 0x2C77;
    private const int QuestSeenMapOffset = 0x2D77;
    private const int QuestFailedMapOffset = 0x2E77;
    private const int QuestSetMapOffset = 0x3187;
    private const int QuestClearMapBits = 1509;
    private const int QuestSetBits = 100;
    private const int QuestCounterOffset = 0x5E4E;

    private const int VillageStarOffset = 0x2C4DA;
    private const int HubStarOffset = 0x2C4DC;

    private const int HunterArtsOffset = 0x2C13;
    private const int HunterArtHighFirst = 83;
    private const int HunterArtHighLast = 190;

    private const int AwardsOffset = 0xC8115;
    private const int AwardBits = 132;

    private const int ItemsObtainedOffset = 0x22497;
    private const int ItemIdBits = 3008;

    private const int EquipmentBoxOffset = 0x62EE;

    /// <summary>Zenny and Wycademy points cap enforced by the game.</summary>
    public const int PointCap = 9_999_999;

    private readonly SaveContainer _save;

    internal CharacterSlot(SaveContainer save, int index, int baseOffset, int usedFlagOffset)
    {
        _save = save;
        Index = index;
        BaseOffset = baseOffset;
        UsedFlagOffset = usedFlagOffset;

        if (baseOffset < SaveContainer.HeaderSize || baseOffset + SaveContainer.SlotStride > save.Raw.Length)
        {
            throw new SaveFormatException(
                $"Slot {index + 1} offset 0x{baseOffset:X} is outside the file; the pointer table is corrupt.");
        }

        ItemBox = new PackedItemList(save, baseOffset + ItemBoxOffset, 2300);
        Pouch = new PackedItemList(save, baseOffset + PouchOffset, 32);

        ClearedQuests = new SaveBitField(save, baseOffset + QuestClearMapOffset, QuestClearMapBits);
        SeenQuests = new SaveBitField(save, baseOffset + QuestSeenMapOffset, QuestClearMapBits);
        FailedQuests = new SaveBitField(save, baseOffset + QuestFailedMapOffset, QuestClearMapBits);
        CompletedQuestSets = new SaveBitField(save, baseOffset + QuestSetMapOffset, QuestSetBits);
        Awards = new SaveBitField(save, baseOffset + AwardsOffset, AwardBits, baseOffset + 0x3157);
        ItemsObtained = new SaveBitField(save, baseOffset + ItemsObtainedOffset, ItemIdBits);

        EquipmentBox = new EquipmentBox(save, baseOffset + EquipmentBoxOffset,
            references: index => FindEquipmentReferences(false, index));
        PalicoEquipmentBox = new EquipmentBox(save, baseOffset + 0x17C2E, 1000, true,
            index => FindEquipmentReferences(true, index));
    }

    /// <summary>Zero-based slot number.</summary>
    public int Index { get; }

    /// <summary>Absolute offset of the slot in the file.</summary>
    public int BaseOffset { get; }

    /// <summary>Offset of the "slot in use" byte.</summary>
    public int UsedFlagOffset { get; }

    /// <summary>False for a slot the player never created.</summary>
    public bool InUse => _save.ReadByte(UsedFlagOffset) != 0;

    /// <summary>Hunter name from the slot header (the game also stores it in the player record).</summary>
    public string Name
    {
        get => ReadString(BaseOffset + NameOffset, 32);
        set
        {
            var text = value ?? string.Empty;
            if (Encoding.UTF8.GetByteCount(text) > 31)
            {
                throw new ArgumentException("Hunter name must fit in 31 UTF-8 bytes.", nameof(value));
            }

            WriteString(BaseOffset + NameOffset, 32, text);
            WriteString(BaseOffset + PlayerRecordNameOffset, 32, text);
        }
    }

    /// <summary>Total play time in seconds; writes synchronize the loaded record and both summaries.</summary>
    public uint PlayTimeSeconds
    {
        get => _save.ReadUInt32(BaseOffset + 0x2248B);
        set
        {
            _save.WriteUInt32(BaseOffset + 0x2248B, value);
            _save.WriteUInt32(BaseOffset + PlayTimeOffset, value);
            _save.WriteUInt32(BaseOffset + 0xC7AD1, value);
        }
    }

    /// <summary>0 = male, 1 = female, 2 = Prowler.</summary>
    public byte BodyType => _save.ReadByte(BaseOffset + BodyTypeOffset);

    /// <summary>True when the slot screen hides the play time.</summary>
    public bool HidePlayTime => _save.ReadByte(BaseOffset + HidePlayTimeOffset) != 0;

    /// <summary>Hunter Rank shown in game. Derived from <see cref="HrPoints"/> by the game.</summary>
    public ushort HunterRank => _save.ReadUInt16(BaseOffset + HrOffset);

    /// <summary>Hunter Rank points; the game maps these to a displayed rank.</summary>
    public uint HrPoints
    {
        get => _save.ReadUInt32(BaseOffset + HrPointsOffset);
        set => _save.WriteUInt32(BaseOffset + HrPointsOffset, value);
    }

    /// <summary>Money. Writing updates the authoritative sUserInfo copy and the slot-header copy.</summary>
    public int Zenny
    {
        get => (int)_save.ReadUInt32(BaseOffset + ZennyOffset);
        set
        {
            if (value is < 0 or > PointCap)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Zenny must be 0-{PointCap}.");
            }

            _save.WriteUInt32(BaseOffset + ZennyOffset, (uint)value);
            _save.WriteUInt32(BaseOffset + HeaderZennyOffset, (uint)value);
        }
    }

    /// <summary>Wycademy points (0-9,999,999).</summary>
    public int WycademyPoints
    {
        get => (int)_save.ReadUInt32(BaseOffset + WycademyOffset);
        set
        {
            if (value is < 0 or > PointCap)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Wycademy points must be 0-{PointCap}.");
            }

            _save.WriteUInt32(BaseOffset + WycademyOffset, (uint)value);
        }
    }

    /// <summary>Item box, 2300 packed slots.</summary>
    public PackedItemList ItemBox { get; }

    /// <summary>Item pouch, 32 packed slots.</summary>
    public PackedItemList Pouch { get; }

    /// <summary>Quests the hunter has cleared (1509 bits, index = quest table position).</summary>
    public SaveBitField ClearedQuests { get; }

    /// <summary>Quests the hunter has seen (the "NEW" copy of the cleared bitmap).</summary>
    public SaveBitField SeenQuests { get; }

    /// <summary>Quests the hunter has failed.</summary>
    public SaveBitField FailedQuests { get; }

    /// <summary>Completed quest sets (100 bits).</summary>
    public SaveBitField CompletedQuestSets { get; }

    /// <summary>Guild Card awards (132 bits).</summary>
    public SaveBitField Awards { get; }

    /// <summary>
    /// "Item obtained" flags: bit = item id, 1-2990. The Smithy draws unknown materials as
    /// question marks until the flag is set.
    /// </summary>
    public SaveBitField ItemsObtained { get; }

    /// <summary>Number of quests whose "cleared" bit is set (1509 quest indices).</summary>
    public int QuestsClearedCount => ClearedQuests.SetCount();

    /// <summary>True when the quest at a 1-based table index is cleared.</summary>
    public bool IsQuestCleared(int questIndex) => ClearedQuests[QuestBit(questIndex)];

    /// <summary>True when the quest at a 1-based table index has been seen (listed on the board).</summary>
    public bool IsQuestSeen(int questIndex) => SeenQuests[QuestBit(questIndex)];

    /// <summary>
    /// Clears or restores one quest by 1-based table index. Clearing also marks the quest
    /// seen, because a finished quest never carries the "NEW" marker.
    /// </summary>
    public void SetQuestCleared(int questIndex, bool cleared)
    {
        var bit = QuestBit(questIndex);
        ClearedQuests[bit] = cleared;
        if (cleared)
        {
            SeenQuests[bit] = true;
        }
    }

    private static int QuestBit(int questIndex)
    {
        if (questIndex < 1 || questIndex > QuestClearMapBits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(questIndex), questIndex, $"Quest index must be 1-{QuestClearMapBits}.");
        }

        return questIndex - 1;
    }

    /// <summary>Number of quests completed, as counted by the game.</summary>
    public uint QuestCounter
    {
        get => _save.ReadUInt32(BaseOffset + QuestCounterOffset);
        set => _save.WriteUInt32(BaseOffset + QuestCounterOffset, value);
    }

    /// <summary>Village star level, 1-10.</summary>
    public ushort VillageStar
    {
        get => _save.ReadUInt16(BaseOffset + VillageStarOffset);
        set
        {
            if (value is < 1 or > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Village star level must be 1-10.");
            }

            _save.WriteUInt16(BaseOffset + VillageStarOffset, value);
        }
    }

    /// <summary>Hub star level, 0-13.</summary>
    public ushort HubStar
    {
        get => _save.ReadUInt16(BaseOffset + HubStarOffset);
        set
        {
            if (value > 13)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Hub star level must be 0-13.");
            }

            _save.WriteUInt16(BaseOffset + HubStarOffset, value);
        }
    }

    /// <summary>
    /// Unlocks every Hunter Art the save can hold: ids 1-70 and 83-190 live in a 24-byte
    /// bitfield at <c>base + 0x2C13</c>.
    /// </summary>
    public void UnlockAllHunterArts()
    {
        for (var id = 1; id <= 70; id++)
        {
            _save.WriteBits(BaseOffset + HunterArtsOffset, id, 1, 1);
        }

        for (var id = HunterArtHighFirst; id <= HunterArtHighLast; id++)
        {
            _save.WriteBits(BaseOffset + HunterArtsOffset, id, 1, 1);
        }
    }

    /// <summary>Number of equipment box records that hold an item.</summary>
    public int EquipmentBoxUsed => EquipmentBox.UsedCount();

    public int UnlockedHunterArtCount => Enumerable.Range(1, 70).Concat(Enumerable.Range(HunterArtHighFirst,
        HunterArtHighLast - HunterArtHighFirst + 1)).Count(id => _save.ReadBits(BaseOffset + HunterArtsOffset, id, 1) != 0);

    public bool IsHunterArtUnlocked(int id)
    {
        ValidateHunterArt(id);
        return _save.ReadBits(BaseOffset + HunterArtsOffset, id, 1) != 0;
    }

    public void SetHunterArtUnlocked(int id, bool value)
    {
        ValidateHunterArt(id);
        _save.WriteBits(BaseOffset + HunterArtsOffset, id, 1, value ? 1u : 0u);
    }

    private static void ValidateHunterArt(int id)
    {
        if (id is not (>= 1 and <= 70 or >= 83 and <= 190)) throw new ArgumentOutOfRangeException(nameof(id));
    }

    /// <summary>The 2000-record hunter equipment box.</summary>
    public EquipmentBox EquipmentBox { get; }

    /// <summary>The separate 1000-record Palico equipment box.</summary>
    public EquipmentBox PalicoEquipmentBox { get; }

    private string ReadString(int offset, int length)
    {
        var bytes = _save.Raw.AsSpan(offset, length);
        var zero = bytes.IndexOf((byte)0);
        if (zero >= 0)
        {
            bytes = bytes[..zero];
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private void WriteString(int offset, int length, string value)
    {
        var span = _save.Raw.AsSpan(offset, length);
        span.Clear();
        Encoding.UTF8.GetBytes(value, span);
        _save.MarkDirty();
    }
}
