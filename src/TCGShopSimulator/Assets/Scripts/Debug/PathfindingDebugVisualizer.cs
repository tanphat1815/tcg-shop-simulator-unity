// Assets/Scripts/Debug/PathfindingDebugVisualizer.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visualizer cho pathfinding debug.
/// Vẽ walkable/non-walkable nodes và active paths trong Scene view.
/// Disable trong production build.
/// </summary>
public class PathfindingDebugVisualizer : MonoBehaviour
{
    [Header("Visualization")]
    [SerializeField] private bool showWalkableNodes = true;
    [SerializeField] private bool showNonWalkableNodes = true;
    [SerializeField] private Color walkableColor = new Color(0f, 1f, 0f, 0.1f);
    [SerializeField] private Color nonWalkableColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private Color pathColor = Color.cyan;

    [Header("Test Controls")]
    [Tooltip("Cell xuất phát cho test path (nhấn T).")]
    [SerializeField] private Vector2Int testStartCell = new Vector2Int(-5, -5);
    [Tooltip("Cell đích cho test path (nhấn T).")]
    [SerializeField] private Vector2Int testGoalCell = new Vector2Int(5, 5);

    private List<Vector2Int> _debugPath;

    private void Update()
    {
        // Nhấn T để test pathfinding từ testStartCell đến testGoalCell
        if (UnityEngine.InputSystem.Keyboard.current?.tKey.wasPressedThisFrame == true)
        {
            TestPathfinding();
        }
    }

    private void TestPathfinding()
    {
        if (PathfindingGrid.Instance == null)
        {
            Debug.LogError("[PathfindingDebugVisualizer] PathfindingGrid.Instance is null!");
            return;
        }

        float startTime = Time.realtimeSinceStartup;

        _debugPath = PathfindingCore.FindPath(
            testStartCell,
            testGoalCell,
            PathfindingGrid.Instance
        );

        float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;

        if (_debugPath != null)
        {
            Debug.Log($"[PathfindingDebugVisualizer] Path tìm thấy: " +
                      $"{_debugPath.Count} bước từ {testStartCell} đến {testGoalCell}. " +
                      $"Thời gian: {elapsed:F3}ms.");
        }
        else
        {
            Debug.LogWarning($"[PathfindingDebugVisualizer] Không tìm được path " +
                             $"từ {testStartCell} đến {testGoalCell}.");
        }
    }

    private void OnDrawGizmos()
    {
        if (PathfindingGrid.Instance == null || GridSystem.Instance == null) return;

        // Vẽ nodes
        if (showWalkableNodes || showNonWalkableNodes)
        {
            for (int x = GridSystem.Instance.ShopMinCell.x;
                 x <= GridSystem.Instance.ShopMaxCell.x; x++)
            {
                for (int y = GridSystem.Instance.ShopMinCell.y;
                     y <= GridSystem.Instance.ShopMaxCell.y; y++)
                {
                    var cell = new Vector2Int(x, y);
                    var node = PathfindingGrid.Instance.GetNode(cell);
                    if (node == null) continue;

                    if (node.IsWalkable && showWalkableNodes)
                        Gizmos.color = walkableColor;
                    else if (!node.IsWalkable && showNonWalkableNodes)
                        Gizmos.color = nonWalkableColor;
                    else
                        continue;

                    Vector3 worldPos = GridSystem.Instance.CellToWorld(cell);
                    Gizmos.DrawCube(worldPos, GridSystem.Instance.IsometricGrid.cellSize * 0.85f);
                }
            }
        }

        // Vẽ debug path
        if (_debugPath != null && _debugPath.Count > 0)
        {
            Gizmos.color = pathColor;
            Vector3 prev = GridSystem.Instance.CellToWorld(testStartCell);

            foreach (var cell in _debugPath)
            {
                Vector3 worldPos = GridSystem.Instance.CellToWorld(cell);
                Gizmos.DrawLine(prev, worldPos);
                Gizmos.DrawSphere(worldPos, 0.12f);
                prev = worldPos;
            }
        }
    }
}
