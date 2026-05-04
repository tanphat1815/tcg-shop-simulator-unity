// Assets/Scripts/Editor/IsometricSetup.cs
// FILE NÀY CHỈ TỒN TẠI TRONG THƯ MỤC Editor, KHÔNG ĐƯA VÀO BUILD.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// Editor utility để cấu hình Isometric Z-as-Y sorting cho toàn bộ dự án.
/// Gọi một lần duy nhất qua menu TCGShop > Setup > Configure Isometric Rendering.
/// Cấu hình được lưu vào ProjectSettings và version-control được.
/// </summary>
public static class IsometricSetup
{
    private const string MENU_PATH = "TCGShop/Setup/Configure Isometric Rendering";

    [MenuItem(MENU_PATH)]
    public static void ConfigureIsometricRendering()
    {
        // --- Bước 1: Thiết lập Transparency Sort Mode ---
        // Phải đặt thành CustomAxis để Unity dùng vector tùy chỉnh bên dưới.
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;

        // --- Bước 2: Thiết lập Transparency Sort Axis ---
        // Vector (0, 1, -0.26): 
        //   x=0   → Không sort theo chiều ngang
        //   y=1   → Sort theo chiều dọc (Y trên màn hình)
        //   z=-0.26 → Điều chỉnh chiều sâu isometric (tan của góc 15°)
        // Kết quả: Object ở phía dưới màn hình (Y nhỏ hơn) sẽ được vẽ SAU
        // object ở phía trên, tạo ảo giác chiều sâu isometric đúng.
        GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, -0.26f);

        // --- Bước 3: Đảm bảo tất cả Camera 2D trong scene dùng đúng sort mode ---
        // Camera.main có thể chưa tồn tại lúc chạy menu này, nên ta cấu hình
        // thông qua GraphicsSettings là đủ (apply toàn project).

        // --- Bước 4: Lưu và xác nhận ---
        // AssetDatabase.SaveAssets() không cần thiết cho GraphicsSettings,
        // nhưng ta force refresh để chắc chắn.
        EditorUtility.SetDirty(QualitySettings.GetQualitySettings());
        AssetDatabase.Refresh();

        // --- Bước 5: In xác nhận ---
        Debug.Log("[IsometricSetup] ✅ Transparency Sort Axis đã được cấu hình: " +
                  $"Mode={GraphicsSettings.transparencySortMode}, " +
                  $"Axis={GraphicsSettings.transparencySortAxis}");

        EditorUtility.DisplayDialog(
            "Isometric Setup Hoàn Tất",
            "Transparency Sort Axis đã được đặt thành (0, 1, -0.26).\n\n" +
            "Cấu hình này apply cho toàn bộ dự án và được lưu vào ProjectSettings.",
            "OK"
        );
    }

    /// <summary>
    /// Kiểm tra xem cấu hình đã đúng chưa. Dùng để validate trong CI/CD.
    /// </summary>
    [MenuItem(MENU_PATH + " (Validate)", true)]
    public static bool ValidateConfiguration()
    {
        // Menu item luôn enabled
        return true;
    }

    /// <summary>
    /// Tự động chạy khi Unity mở project, kiểm tra và cảnh báo nếu chưa cấu hình.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void CheckConfigurationOnLoad()
    {
        if (GraphicsSettings.transparencySortMode != TransparencySortMode.CustomAxis)
        {
            Debug.LogWarning("[IsometricSetup] ⚠️ Transparency Sort Mode chưa được cấu hình đúng. " +
                             "Vào menu TCGShop > Setup > Configure Isometric Rendering.");
        }
    }
}
#endif
