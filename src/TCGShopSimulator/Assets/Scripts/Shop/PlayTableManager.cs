// Assets/Scripts/Shop/PlayTableManager.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton quản lý tất cả PlayTableInstance trong scene.
///
/// RESPONSIBILITIES:
///   1. Registry tất cả PlayTableInstance (auto-register khi spawn)
///   2. Cung cấp API cho CustomerFSM tìm bàn trống
///   3. Handle match lifecycle events
/// </summary>
public class PlayTableManager : MonoBehaviour
{
    // ========================================================================
    // SINGLETON
    // ========================================================================

    public static PlayTableManager Instance { get; private set; }

    // ========================================================================
    // REGISTRY
    // ========================================================================

    private readonly List<PlayTableInstance> _allTables = new List<PlayTableInstance>();

    // ========================================================================
    // VÒNG ĐỜI
    // ========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ========================================================================
    // REGISTRATION — Called by PlacedFurnitureInstance
    // ========================================================================

    /// <summary>
    /// Đăng ký bàn chơi. Tự gọi từ PlacedFurnitureInstance.
    /// </summary>
    public void RegisterTable(PlayTableInstance table)
    {
        if (table == null || _allTables.Contains(table)) return;
        _allTables.Add(table);
        Debug.Log($"[PlayTableManager] Registered table. Total: {_allTables.Count}");
    }

    /// <summary>
    /// Hủy đăng ký bàn khi furniture bị xóa.
    /// </summary>
    public void UnregisterTable(PlayTableInstance table)
    {
        if (table == null) return;
        _allTables.Remove(table);
    }

    // ========================================================================
    // TABLE LOOKUP — Gọi từ CustomerFSM
    // ========================================================================

    /// <summary>
    /// Tìm bàn trống cho NPC muốn chơi.
    /// Ưu tiên bàn có 1 occupant (ghép đôi ngay).
    /// Fallback: bàn trống hoàn toàn.
    ///
    /// RETURN: PlayTableInstance có ghế trống, hoặc null.
    /// </summary>
    public PlayTableInstance FindAvailableTable(Vector3 npcPosition)
    {
        PlayTableInstance bestTable = null;
        float bestDistSq = float.MaxValue;

        foreach (var table in _allTables)
        {
            if (table == null) continue;

            // Bàn đang có match active → bỏ qua
            if (table.IsMatchActive) continue;

            int emptySeatIdx = table.GetEmptySeatIndex();
            if (emptySeatIdx < 0) continue;

            float distSq = (table.transform.position - npcPosition).sqrMagnitude;
            bool hasOneOccupant = table.OccupiedSeatCount == 1;

            if (hasOneOccupant || bestTable == null || distSq < bestDistSq)
            {
                if (hasOneOccupant && bestTable != null && !bestTable.IsFullyOccupied)
                {
                    bestTable = table;
                    bestDistSq = distSq;
                }
                else if (!hasOneOccupant)
                {
                    bestTable = table;
                    bestDistSq = distSq;
                }
            }
        }

        return bestTable;
    }

    /// <summary>
    /// Kiểm tra có bất kỳ bàn nào trống không.
    /// </summary>
    public bool HasAvailableTable()
    {
        foreach (var table in _allTables)
        {
            if (table != null && !table.IsMatchActive && !table.IsFullyOccupied)
                return true;
        }
        return false;
    }
}
