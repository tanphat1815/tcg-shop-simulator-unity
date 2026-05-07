// Assets/Scripts/Data/ExpansionConfig.cs

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject khai báo cấu hình expansion levels.
/// Tạo 1 asset: Right-click > Create > TCGShop > Data > Expansion Config
///
/// CẤU TRÚC:
///   ExpansionConfig.asset lưu toàn bộ EXPANSION_LEVELS[]
/// </summary>
[CreateAssetMenu(
    fileName = "ExpansionConfig",
    menuName = "TCGShop/Data/Expansion Config",
    order = 5
)]
public class ExpansionConfig : ScriptableObject
{
    [Serializable]
    public class ExpansionLevel
    {
        [Tooltip("Thứ tự expansion (1-20).")]
        public int levelId;

        [Tooltip("Shop level tối thiểu để mua.")]
        public int requiredShopLevel;

        [Tooltip("Chi phí mua ($).")]
        public float cost;

        [Tooltip("Tiền thuê tăng thêm mỗi ngày ($).")]
        public float rentIncrease;

        [Tooltip("Số cell thêm theo X (mở rộng ngang).")]
        public int addedCellsX;

        [Tooltip("Số cell thêm theo Y (mở rộng dọc).")]
        public int addedCellsY;

        [Tooltip("Tên hiển thị trong UI.")]
        public string displayName;
    }

    [Header("Expansion Levels")]
    public List<ExpansionLevel> levels = new List<ExpansionLevel>();

    /// <summary>Lấy cấu hình cho một expansion level cụ thể.</summary>
    public ExpansionLevel GetLevel(int id)
    {
        return levels.Find(l => l.levelId == id);
    }

    /// <summary>Lấy cấu hình expansion TIẾP THEO (levelId = current + 1).</summary>
    public ExpansionLevel GetNextExpansion(int currentLevel)
    {
        return GetLevel(currentLevel + 1);
    }

    /// <summary>Tổng số expansion levels.</summary>
    public int MaxLevel => levels.Count > 0 ? levels[levels.Count - 1].levelId : 0;

    /// <summary>Tạo config mặc định (20 levels).</summary>
    public void CreateDefaultConfig()
    {
        levels.Clear();

        int[] requiredLevels = {
            2, 3, 5, 7, 10, 13, 16, 20, 24, 28,
            32, 36, 40, 43, 45, 46, 47, 48, 49, 50
        };
        float[] costs = {
            300, 400, 500, 600, 800, 1000, 1200, 1500, 1800, 2200,
            2600, 3000, 3500, 4000, 4500, 4800, 5200, 5500, 5800, 6000
        };
        float[] rentIncreases = {
            20, 20, 20, 20, 60, 60, 60, 60, 60, 60,
            60, 60, 60, 60, 60, 60, 60, 60, 60, 60
        };

        for (int i = 0; i < 20; i++)
        {
            levels.Add(new ExpansionLevel
            {
                levelId = i + 1,
                requiredShopLevel = requiredLevels[i],
                cost = costs[i],
                rentIncrease = rentIncreases[i],
                addedCellsX = 2,
                addedCellsY = 1,
                displayName = $"Expansion Level {i + 1}"
            });
        }
    }
}
