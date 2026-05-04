using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Script ho tro test nhanh he thong Placement bang phim tat.
/// Bam E: Dat ban thu ngan (Cashier Desk)
/// Bam B: Dat ke doi (Shelf Double)
/// </summary>
public class PlacementDebugTester : MonoBehaviour
{
    [Header("Test Assets")]
    [SerializeField] private FurnitureDefinition cashierDesk;
    [SerializeField] private FurnitureDefinition shelfDouble;

    private Keyboard _keyboard;

    private void Update()
    {
        if (_keyboard == null) _keyboard = Keyboard.current;
        if (_keyboard == null) return;

        // Bam E: Bat dau dat ban thu ngan
        if (_keyboard.eKey.wasPressedThisFrame)
        {
            if (cashierDesk != null)
            {
                PlacementManager.Instance.StartPlacement(cashierDesk);
            }
            else
            {
                Debug.LogWarning("[PlacementDebugTester] Cashier Desk definition chua duoc assign!");
            }
        }

        // Bam B: Bat dau dat ke doi
        if (_keyboard.bKey.wasPressedThisFrame)
        {
            if (shelfDouble != null)
            {
                PlacementManager.Instance.StartPlacement(shelfDouble);
            }
            else
            {
                Debug.LogWarning("[PlacementDebugTester] Shelf Double definition chua duoc assign!");
            }
        }
    }
}
