// Assets/Scripts/Persistence/SaveSystem.cs
using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Static utility class quản lý đọc/ghi file save game.
///
/// CÁCH HOẠT ĐỘNG:
///   Save:  GameData → JsonUtility.ToJson() → UTF8 bytes → Base64 → File
///   Load:  File → Base64 → UTF8 string → JsonUtility.FromJsonOverwrite() → GameData
///
/// LƯU Ý:
///   - Dùng JsonUtility vì tương thích tốt với [Serializable] classes
///   - KHÔNG dùng Newtonsoft/Json.NET (cần external package)
///   - Base64 đảm bảo file text ASCII thuần túy
///   - File path: Application.persistentDataPath + "/gamesave.dat"
/// </summary>
public static class SaveSystem
{
    // ========================================================================
    // CONSTANTS
    // ========================================================================
    private const string SAVE_FILE_NAME = "gamesave.dat";

    // ========================================================================
    // PATH
    // ========================================================================
    /// <summary>
    /// Đường dẫn đầy đủ đến file save.
    /// </summary>
    public static string SaveFilePath
    {
        get
        {
            string folder = Application.persistentDataPath;
            return Path.Combine(folder, SAVE_FILE_NAME);
        }
    }

    /// <summary>
    /// Thư mục lưu save (persistentDataPath).
    /// </summary>
    public static string SaveDirectory => Application.persistentDataPath;

    // ========================================================================
    // SAVE
    // ========================================================================
    /// <summary>
    /// Lưu GameData vào file.
    /// </summary>
    public static bool Save(GameData data)
    {
        if (data == null)
        {
            Debug.LogError("[SaveSystem] Save: data is null!");
            return false;
        }

        try
        {
            // Step 1: Serialize to JSON
            string json = JsonUtility.ToJson(data, true); // pretty print for debugging
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[SaveSystem] Save: JsonUtility.ToJson returned empty!");
                return false;
            }

            // Step 2: UTF8 bytes
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            // Step 3: Base64 encode
            string encoded = System.Convert.ToBase64String(jsonBytes);

            // Step 4: Write to file
            EnsureSaveDirectory();
            File.WriteAllText(SaveFilePath, encoded);

            long fileSize = new FileInfo(SaveFilePath).Length;
            Debug.Log($"[SaveSystem] Saved: {data} | File size: {fileSize} bytes | Path: {SaveFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Save FAILED: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    // ========================================================================
    // LOAD
    // ========================================================================
    /// <summary>
    /// Đọc GameData từ file.
    /// </summary>
    /// <returns>GameData nếu load thành công, null nếu thất bại.</returns>
    public static GameData Load()
    {
        try
        {
            if (!File.Exists(SaveFilePath))
            {
                Debug.Log("[SaveSystem] Load: No save file found. Starting fresh.");
                return null;
            }

            // Step 1: Read file
            string encoded = File.ReadAllText(SaveFilePath);
            if (string.IsNullOrWhiteSpace(encoded))
            {
                Debug.LogWarning("[SaveSystem] Load: File is empty. Starting fresh.");
                return null;
            }

            // Step 2: Base64 decode
            byte[] jsonBytes = System.Convert.FromBase64String(encoded);

            // Step 3: UTF8 string
            string json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[SaveSystem] Load: Decoded JSON is empty. Starting fresh.");
                return null;
            }

            // Step 4: Deserialize
            var data = new GameData();
            JsonUtility.FromJsonOverwrite(json, data);

            if (!data.IsValid())
            {
                Debug.LogWarning($"[SaveSystem] Load: Data is invalid: {data}");
                return null;
            }

            Debug.Log($"[SaveSystem] Loaded: {data}");
            return data;
        }
        catch (FormatException ex)
        {
            // Base64 decode fail — file có thể bị corrupt hoặc là file cũ
            Debug.LogWarning($"[SaveSystem] Load: File corrupt or legacy format. {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Load FAILED: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    // ========================================================================
    // UTILITY
    // ========================================================================
    /// <summary>
    /// Kiểm tra file save có tồn tại không.
    /// </summary>
    public static bool HasSaveFile() => File.Exists(SaveFilePath);

    /// <summary>
    /// Xóa file save (dùng khi user muốn reset game).
    /// </summary>
    public static bool DeleteSave()
    {
        try
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                Debug.Log($"[SaveSystem] Save file deleted: {SaveFilePath}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] DeleteSave FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Lấy thông tin file save (size, timestamp).
    /// </summary>
    public static (long bytes, DateTime lastModified) GetSaveFileInfo()
    {
        if (!File.Exists(SaveFilePath)) return (0, DateTime.MinValue);
        var info = new FileInfo(SaveFilePath);
        return (info.Length, info.LastWriteTimeUtc);
    }

    private static void EnsureSaveDirectory()
    {
        if (!Directory.Exists(Application.persistentDataPath))
            Directory.CreateDirectory(Application.persistentDataPath);
    }
}
