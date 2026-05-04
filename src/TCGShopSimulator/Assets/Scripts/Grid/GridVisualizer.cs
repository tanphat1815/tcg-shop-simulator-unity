// Assets/Scripts/Grid/GridVisualizer.cs

using UnityEngine;

/// <summary>
/// Ve debug visualization cua luoi grid.
/// Chi active trong Editor hoac khi debug mode bat.
/// KHONG anh huong gameplay.
///
/// TUONG DUONG HE THONG CU:
///   drawPlacementVisualizer() trong MainScene.ts
/// </summary>
[ExecuteInEditMode]
public class GridVisualizer : MonoBehaviour
{
    [Header("Visualization")]
    [Tooltip("Bat/tat ve debug grid.")]
    [SerializeField] private bool showGridLines = true;

    [Tooltip("Bat/tat highlight o dang bi occupied.")]
    [SerializeField] private bool highlightOccupied = true;

    [Tooltip("Mau duong ke luoi.")]
    [SerializeField] private Color gridLineColor = new Color(1f, 1f, 1f, 0.1f);

    [Tooltip("Mau highlight o occupied.")]
    [SerializeField] private Color occupiedColor = new Color(1f, 0f, 0f, 0.25f);

    [Tooltip("Mau highlight o empty.")]
    [SerializeField] private Color emptyColor = new Color(0f, 1f, 0f, 0.05f);

    private void OnDrawGizmos()
    {
        if (!showGridLines && !highlightOccupied) return;
        if (GridSystem.Instance == null) return;

        var gridSystem = GridSystem.Instance;

        Vector3 cellSize = gridSystem.IsometricGrid != null
            ? gridSystem.IsometricGrid.cellSize
            : Vector3.one;

        int range = 20;

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                var cellCoord = new Vector2Int(x, y);
                var node = gridSystem.GetNode(cellCoord);

                if (!node.IsWithinShopBounds) continue;

                Vector3 worldPos = gridSystem.CellToWorld(cellCoord);

                if (highlightOccupied)
                {
                    Gizmos.color = node.IsOccupied ? occupiedColor : emptyColor;
                    Gizmos.DrawCube(worldPos, cellSize * 0.9f);
                }

                if (showGridLines)
                {
                    Gizmos.color = gridLineColor;
                    Gizmos.DrawWireCube(worldPos, cellSize);
                }
            }
        }
    }
}
