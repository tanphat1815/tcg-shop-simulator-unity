// Assets/Editor/SampleDataGenerator.cs

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Tạo tất cả sample ScriptableObject assets cần thiết để test Bước 2.
/// Chạy một lần qua menu TCGShop > Setup > Generate Sample Data.
/// </summary>
public static class SampleDataGenerator
{
    [MenuItem("TCGShop/Setup/Generate Sample Data for Step 2")]
    public static void GenerateAllSampleData()
    {
        EnsureDirectoriesExist();

        var rarities = CreateRarityDefinitions();
        var cards = CreateSampleCards(rarities);
        var pack = CreateSamplePack(rarities, cards);
        CreateCardDatabase(pack);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SampleDataGenerator] ✅ Tất cả sample assets đã được tạo thành công!");
        EditorUtility.DisplayDialog(
            "Sample Data Generated",
            "Đã tạo:\n" +
            "• 9 RarityDefinition assets\n" +
            "• 5 CardData assets (mẫu)\n" +
            "• 1 PackData asset với Drop Table đầy đủ\n" +
            "• 1 CardDatabase asset\n\n" +
            "Kiểm tra thư mục Assets/ScriptableObjects/",
            "OK"
        );
    }

    private static void EnsureDirectoriesExist()
    {
        string[] dirs = {
            "Assets/ScriptableObjects",
            "Assets/ScriptableObjects/Rarities",
            "Assets/ScriptableObjects/Cards",
            "Assets/ScriptableObjects/Packs",
            "Assets/ScriptableObjects/Database"
        };

        foreach (var dir in dirs)
        {
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parts = dir.Split('/');
                var parent = string.Join("/", parts[..^1]);
                AssetDatabase.CreateFolder(parent, parts[^1]);
            }
        }
    }

    private static Dictionary<string, RarityDefinition> CreateRarityDefinitions()
    {
        var rarities = new Dictionary<string, RarityDefinition>();

        var definitions = new[]
        {
            ("Common",                    0, false, Color.gray,                          2),
            ("Uncommon",                  1, false, Color.green,                         2),
            ("Rare",                      3, true,  new Color(0.8f, 0.7f, 0.1f),      15),
            ("Double Rare",               4, true,  new Color(1f, 0.85f, 0f),         15),
            ("Ultra Rare",                5, true,  new Color(0.5f, 0.1f, 0.9f),     15),
            ("Illustration Rare",          7, true,  new Color(0.2f, 0.7f, 1f),       15),
            ("Special Illustration Rare",  8, true,  new Color(1f, 0.4f, 0.8f),       15),
            ("Secret Rare",               6, true,  new Color(1f, 0.8f, 0.2f),      15),
            ("Ghost Rare",               10, true,  new Color(1f, 0.2f, 0.6f),       15),
        };

        foreach (var (name, rank, isHigh, color, xp) in definitions)
        {
            var rarity = ScriptableObject.CreateInstance<RarityDefinition>();
            rarity.displayName = name;
            rarity.sortingRank = rank;
            rarity.isHighRarity = isHigh;
            rarity.rarityColor = color;
            rarity.xpReward = xp;

            string fileName = $"Rarity_{name.Replace(" ", "")}.asset";
            string path = $"Assets/ScriptableObjects/Rarities/{fileName}";
            AssetDatabase.CreateAsset(rarity, path);

            rarities[name] = rarity;
        }

        Debug.Log($"[SampleDataGenerator] Created {rarities.Count} RarityDefinition assets.");
        return rarities;
    }

    private static List<CardData> CreateSampleCards(Dictionary<string, RarityDefinition> rarities)
    {
        var cards = new List<CardData>();

        var cardDefs = new[]
        {
            ("sv01-001", "Charizard ex",  "sv01", "sv", "001", "Ultra Rare",   45.99f, 180),
            ("sv01-002", "Pikachu",       "sv01", "sv", "002", "Common",         0.15f,  60),
            ("sv01-003", "Mewtwo",        "sv01", "sv", "003", "Rare",           3.50f, 120),
            ("sv01-004", "Bulbasaur",     "sv01", "sv", "004", "Common",         0.10f,  70),
            ("sv01-005", "Squirtle",      "sv01", "sv", "005", "Uncommon",       0.50f,  80),
        };

        foreach (var (id, name, setId, seriesId, number, rarityName, value, hp) in cardDefs)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.cardId = id;
            card.cardName = name;
            card.setId = setId;
            card.seriesId = seriesId;
            card.cardNumber = number;
            card.marketValue = value;
            card.baseHp = hp;

            if (rarities.TryGetValue(rarityName, out var rarity))
                card.rarity = rarity;

            string path = $"Assets/ScriptableObjects/Cards/Card_{name.Replace(" ", "_")}.asset";
            AssetDatabase.CreateAsset(card, path);
            cards.Add(card);
        }

        Debug.Log($"[SampleDataGenerator] Created {cards.Count} CardData assets.");
        return cards;
    }

    private static PackData CreateSamplePack(
        Dictionary<string, RarityDefinition> rarities,
        List<CardData> cards)
    {
        var pack = ScriptableObject.CreateInstance<PackData>();
        pack.packId = "pack_sv01";
        pack.packName = "Scarlet & Violet Base Set Booster Pack";
        pack.sourceSetId = "sv01";
        pack.generationName = "GENERATION IX";
        pack.buyCost = 4.99f;
        pack.defaultSellPrice = 7.99f;
        pack.requiredShopLevel = 1;
        pack.cardsPerPack = 6;
        pack.availableCards = new List<CardData>(cards);

        pack.dropTable = new List<DropTableSlot>
        {
            new DropTableSlot
            {
                slotLabel = "Slot 1 (Common Guaranteed)",
                rarityWeights = new List<RarityWeight>
                {
                    new RarityWeight { rarity = rarities["Common"], weight = 100f }
                }
            },
            new DropTableSlot
            {
                slotLabel = "Slot 2 (Common Guaranteed)",
                rarityWeights = new List<RarityWeight>
                {
                    new RarityWeight { rarity = rarities["Common"], weight = 100f }
                }
            },
            new DropTableSlot
            {
                slotLabel = "Slot 3 (Common Guaranteed)",
                rarityWeights = new List<RarityWeight>
                {
                    new RarityWeight { rarity = rarities["Common"], weight = 100f }
                }
            },
            new DropTableSlot
            {
                slotLabel = "Slot 4 (Common/Uncommon)",
                rarityWeights = new List<RarityWeight>
                {
                    new RarityWeight { rarity = rarities["Common"],   weight = 70f },
                    new RarityWeight { rarity = rarities["Uncommon"], weight = 30f }
                }
            },
            new DropTableSlot
            {
                slotLabel = "Slot 5 (Uncommon/Rare Mix)",
                rarityWeights = new List<RarityWeight>
                {
                    new RarityWeight { rarity = rarities["Common"],   weight = 30f },
                    new RarityWeight { rarity = rarities["Uncommon"], weight = 55f },
                    new RarityWeight { rarity = rarities["Rare"],    weight = 15f }
                }
            },
            new DropTableSlot
            {
                slotLabel = "Slot 6 (Rare Guaranteed ★)",
                rarityWeights = new List<RarityWeight>
                {
                    new RarityWeight { rarity = rarities["Rare"],        weight = 70f },
                    new RarityWeight { rarity = rarities["Double Rare"], weight = 20f },
                    new RarityWeight { rarity = rarities["Ultra Rare"],  weight = 10f }
                }
            }
        };

        string path = "Assets/ScriptableObjects/Packs/Pack_SV01_BaseSet.asset";
        AssetDatabase.CreateAsset(pack, path);

        Debug.Log($"[SampleDataGenerator] Created PackData '{pack.packName}' with {pack.dropTable.Count} slots.");
        return pack;
    }

    private static void CreateCardDatabase(PackData pack)
    {
        var db = ScriptableObject.CreateInstance<CardDatabase>();
        db.allPacks = new List<PackData> { pack };

        string path = "Assets/ScriptableObjects/Database/CardDatabase_Main.asset";
        AssetDatabase.CreateAsset(db, path);

        Debug.Log($"[SampleDataGenerator] Created CardDatabase with {db.allPacks.Count} pack(s).");
    }
}
#endif
