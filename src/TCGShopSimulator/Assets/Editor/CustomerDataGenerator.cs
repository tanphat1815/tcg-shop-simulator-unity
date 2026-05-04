// Assets/Editor/CustomerDataGenerator.cs

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Tạo thư mục và cấu hình cần thiết cho Customer system.
/// Chạy qua menu: TCGShop > Setup > Generate Customer Config
/// </summary>
public static class CustomerDataGenerator
{
    [MenuItem("TCGShop/Setup/Generate Customer Config")]
    public static void GenerateCustomerConfig()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/Customer"))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Customer");

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Customer"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Customer");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Customer Config",
            "Thư mục đã được tạo:\n" +
            "• Assets/ScriptableObjects/Customer/\n" +
            "• Assets/Prefabs/Customer/\n\n" +
            "Tiếp theo:\n" +
            "1. Tạo Prefab_Customer.prefab với CustomerFSM + CharacterMovement\n" +
            "2. Tạo Prefab_SpeechBubble.prefab với Canvas (World Space) + SpeechBubble\n" +
            "3. Assign prefabs vào CustomerSpawner và CustomerFSM",
            "OK");
    }
}
#endif
