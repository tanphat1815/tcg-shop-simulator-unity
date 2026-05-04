// Assets/Scripts/Placement/PlacedFurnitureInstance.cs

using UnityEngine;

/// <summary>
/// Component gan len moi furniture GameObject da duoc dat vao scene.
/// Luu tru "identity" cua instance: ai la no, dat o dau, xoay bao nhieu.
///
/// TUONG DUONG HE THONG CU:
///   sprite.setData('id', shelf.id)   -> InstanceId
///   sprite.setData('type', 'shelf')  -> Definition.furnitureType
///   shelf.x, shelf.y                 -> OriginCell
///   table.rotation                  -> PlacedRotation
/// </summary>
[DisallowMultipleComponent]
public class PlacedFurnitureInstance : MonoBehaviour
{
    public string InstanceId { get; private set; }
    public FurnitureDefinition Definition { get; private set; }
    public Vector2Int OriginCell { get; private set; }
    public int PlacedRotation { get; private set; }
    public float PlacedAt { get; private set; }

    public void Initialize(
        string instanceId,
        FurnitureDefinition definition,
        Vector2Int originCell,
        int rotation)
    {
        InstanceId = instanceId;
        Definition = definition;
        OriginCell = originCell;
        PlacedRotation = rotation;
        PlacedAt = Time.time;

        gameObject.name = $"[Furniture] {definition.furnitureType} ({instanceId[^8..]})";
    }

    public override string ToString() =>
        $"FurnitureInstance[{InstanceId}|{Definition?.furnitureType}|" +
        $"cell={OriginCell}|rot={PlacedRotation}deg]";
}
