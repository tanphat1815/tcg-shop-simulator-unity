// Assets/Scripts/Delivery/DeliveryEvents.cs

using System;
using UnityEngine;

/// <summary>
/// C# Events cho Delivery subsystem.
/// Dùng Observer Pattern thay thế Vue reactive stores.
///
/// CÁCH DÙNG:
///   DeliveryEvents.OnBoxSpawned += MyMethod;
///   DeliveryEvents.FireBoxSpawned(box);
/// </summary>
public static class DeliveryEvents
{
    /// <summary>Khi một DeliveryBox spawn trong world.</summary>
    public static event Action<DeliveryBox> OnBoxSpawned;

    /// <summary>Khi Player nhặt box lên (bắt đầu carry).</summary>
    public static event Action<DeliveryBox> OnBoxPickedUp;

    /// <summary>Khi Player đặt box xuống shelf (hoàn tất delivery).</summary>
    public static event Action<DeliveryBox, ShelfInstance> OnBoxDeposited;

    /// <summary>Khi box bị hủy (deposit xong hoặc expire).</summary>
    public static event Action<DeliveryBox> OnBoxDestroyed;

    public static void FireBoxSpawned(DeliveryBox box) =>
        OnBoxSpawned?.Invoke(box);

    public static void FireBoxPickedUp(DeliveryBox box) =>
        OnBoxPickedUp?.Invoke(box);

    public static void FireBoxDeposited(DeliveryBox box, ShelfInstance shelf) =>
        OnBoxDeposited?.Invoke(box, shelf);

    public static void FireBoxDestroyed(DeliveryBox box) =>
        OnBoxDestroyed?.Invoke(box);
}
