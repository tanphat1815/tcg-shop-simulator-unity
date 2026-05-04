// Assets/Editor/FurnitureDataGenerator.cs

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Tao FurnitureDefinition ScriptableObject assets cho tat ca 5 loai furniture.
/// Chay qua menu: TCGShop > Setup > Generate Furniture Definitions
/// </summary>
public static class FurnitureDataGenerator
{
    [MenuItem("TCGShop/Setup/Generate Furniture Definitions")]
    public static void GenerateFurnitureDefinitions()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/Furniture"))
        {
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Furniture");
        }

        CreateFurnitureDef("Furniture_ShelfSingle",  FurnitureType.ShelfSingle,
            "Single Sided Shelf", 1, 1, false, 300f, 3, 3, 16, ShelfRole.Selling,
            "Ke go 1 mat tieu chuan. NPCs co the mua hang tu ke nay.");

        CreateFurnitureDef("Furniture_ShelfDouble",  FurnitureType.ShelfDouble,
            "Double Sided Shelf", 2, 1, true, 750f, 11, 4, 32, ShelfRole.Selling,
            "Ke trung tam 2 mat cao cap. Sinh loi cuc manh.");

        CreateFurnitureDef("Furniture_StorageShelf", FurnitureType.StorageShelf,
            "Storage Shelf", 1, 1, false, 150f, 1, 3, 4, ShelfRole.Storage,
            "Ke kho don gian. Dung de cat thung hang. NPCs KHONG mua tu day.");

        CreateFurnitureDef("Furniture_PlayTable",    FurnitureType.PlayTable,
            "Play Table", 2, 2, true, 400f, 5, 0, 0, ShelfRole.Selling,
            "Ban choi bai cho khach hang. Tao XP thu dong khi co nguoi thi dau.");

        CreateFurnitureDef("Furniture_CashierDesk",  FurnitureType.CashierDesk,
            "Cashier Desk", 1, 1, false, 500f, 1, 0, 0, ShelfRole.Selling,
            "Quay thu ngan tieu chuan. Noi khach mang hang toi thanh toan.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[FurnitureDataGenerator] Da tao 5 FurnitureDefinition assets.");
        EditorUtility.DisplayDialog("Done",
            "Da tao 5 FurnitureDefinition assets tai Assets/ScriptableObjects/Furniture/",
            "OK");
    }

    private static void CreateFurnitureDef(
        string fileName, FurnitureType type, string displayName,
        int width, int height, bool canRotate,
        float buyCost, int reqLevel,
        int numTiers, int slotsPerTier, ShelfRole role,
        string description)
    {
        string path = $"Assets/ScriptableObjects/Furniture/{fileName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path);
        if (existing != null)
        {
            Debug.Log($"[FurnitureDataGenerator] Skipped (already exists): {path}");
            return;
        }

        var def = ScriptableObject.CreateInstance<FurnitureDefinition>();
        def.furnitureType = type;
        def.displayName = displayName;
        def.footprintWidth = width;
        def.footprintHeight = height;
        def.canRotate = canRotate;
        def.buyCost = buyCost;
        def.requiredShopLevel = reqLevel;
        def.numberOfTiers = numTiers;
        def.slotsPerTier = slotsPerTier;
        def.shelfRole = role;
        def.description = description;

        AssetDatabase.CreateAsset(def, path);
        Debug.Log($"[FurnitureDataGenerator] Created: {path}");
    }
}
#endif
