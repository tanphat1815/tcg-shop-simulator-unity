// Assets/Scripts/Core/SceneBootstrapper.cs

using UnityEngine;

/// <summary>
/// Chạy khi GameScene được load.
/// Kiểm tra và cấu hình các thành phần bắt buộc của Scene.
/// Gắn vào một GameObject tên "_Bootstrapper" trong Scene.
/// </summary>
public class SceneBootstrapper : MonoBehaviour
{
    [Header("Scene Requirements")]
    [Tooltip("Camera chính của Scene. Phải có CameraController.")]
    [SerializeField] private Camera mainCamera;

    private void Awake()
    {
        ValidateSceneSetup();
    }

    private void Start()
    {
        // Kiểm tra GameManager đã ready chưa trước khi bắt đầu scene logic
        if (!GameManager.IsAvailable)
        {
            Debug.LogError("[SceneBootstrapper] GameManager chưa sẵn sàng! " +
                           "Kiểm tra RuntimeInitializeOnLoadMethod trong GameManager.cs");
            return;
        }

        Debug.Log("[SceneBootstrapper] Scene setup hoàn tất. Tất cả systems sẵn sàng.");
    }

    /// <summary>
    /// Kiểm tra toàn bộ requirements của Scene.
    /// In error rõ ràng thay vì để NullReferenceException xuất hiện ngẫu nhiên.
    /// </summary>
    private void ValidateSceneSetup()
    {
        bool hasErrors = false;

        // Kiểm tra Camera
        if (mainCamera == null)
        {
            // Thử tìm trong Scene nếu chưa assign (chỉ cho phép ở Awake)
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogError("[SceneBootstrapper] ❌ Không tìm thấy Main Camera trong Scene! " +
                               "Thêm Camera với tag 'MainCamera' vào Scene.");
                hasErrors = true;
            }
        }

        // Kiểm tra CameraController
        if (mainCamera != null && !mainCamera.TryGetComponent<CameraController>(out _))
        {
            Debug.LogError("[SceneBootstrapper] ❌ Camera thiếu CameraController component! " +
                           "Thêm CameraController vào Camera GameObject.");
            hasErrors = true;
        }

        // Kiểm tra Orthographic
        if (mainCamera != null && !mainCamera.orthographic)
        {
            Debug.LogWarning("[SceneBootstrapper] ⚠️ Camera không phải Orthographic. " +
                             "Isometric 2D yêu cầu Orthographic camera.");
        }

        if (!hasErrors)
        {
            Debug.Log("[SceneBootstrapper] ✅ Scene validation passed.");
        }
    }
}
