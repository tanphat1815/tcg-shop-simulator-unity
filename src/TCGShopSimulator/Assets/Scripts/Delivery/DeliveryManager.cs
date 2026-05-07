// Assets/Scripts/Delivery/DeliveryManager.cs

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton quản lý lifecycle của delivery boxes.
///
/// RESPONSIBILITIES:
///   1. Nhận yêu cầu delivery từ ShopTransactionProcessor (khi mua pack)
///   2. Spawn DeliveryBox prefab tại door cell sau deliveryDelay
///   3. Quản lý tối đa deliveryQueue để tránh spam box
///   4. Cleanup expired boxes
///
/// MAPPING TỪ HỆ THỐNG CŨ:
///   deliveryStore.scheduleDelivery(items) → ScheduleDelivery()
///   pendingDeliveries[]                   → _deliveryQueue list
///   deliveryBox sprite in Phaser          → DeliveryBox GameObject
/// </summary>
public class DeliveryManager : MonoBehaviour
{
    // ========================================================================
    // SINGLETON
    // ========================================================================

    public static DeliveryManager Instance { get; private set; }

    // ========================================================================
    // CONFIGURATION
    // ========================================================================

    [Header("Spawn Settings")]
    [Tooltip("Prefab DeliveryBox. Phải có DeliveryBox component và SpriteRenderer.")]
    [SerializeField] private GameObject _deliveryBoxPrefab;

    [Tooltip("Vị trí spawn (door cell). Để null = tự tính từ GridSystem.")]
    [SerializeField] private Transform _doorSpawnPoint;

    [Tooltip("Số delivery box tối đa chờ trong shop cùng lúc.")]
    [SerializeField] private int _maxPendingBoxes = 5;

    [Tooltip("Thời gian (giây) từ khi mua đến khi box xuất hiện tại cửa.")]
    [SerializeField] private float _deliveryDelay = 2f;

    [Tooltip("Thời gian (giây) box tồn tại trước khi tự hủy nếu không nhặt.")]
    [SerializeField] private float _boxLifetime = 120f;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = true;

    // ========================================================================
    // QUEUE
    // ========================================================================

    private class PendingDelivery
    {
        public string BoxId;
        public string ItemId;
        public int Quantity;
        public float SpawnAtTime;
        public DeliveryBox SpawnedBox;
    }

    private readonly List<PendingDelivery> _deliveryQueue = new List<PendingDelivery>();
    private int _boxCounter = 0;

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

    private void Update()
    {
        ProcessDeliveryQueue();
    }

    // ========================================================================
    // PUBLIC API
    // ========================================================================

    /// <summary>
    /// Lên lịch delivery cho một item (pack).
    /// Gọi bởi ShopTransactionProcessor khi player mua pack.
    /// Box sẽ spawn sau _deliveryDelay giây.
    /// </summary>
    public void ScheduleDelivery(string itemId, int quantity)
    {
        if (string.IsNullOrEmpty(itemId) || quantity <= 0)
        {
            Debug.LogWarning($"[DeliveryManager] Invalid delivery request: {itemId} x{quantity}");
            return;
        }

        if (_deliveryQueue.Count >= _maxPendingBoxes)
        {
            Debug.LogWarning($"[DeliveryManager] Delivery queue full ({_maxPendingBoxes}). " +
                           $"Skipping delivery of {itemId} x{quantity}");
            return;
        }

        _boxCounter++;
        string boxId = $"delivery_{_boxCounter:D4}_{Time.time:F0}";

        var delivery = new PendingDelivery
        {
            BoxId      = boxId,
            ItemId     = itemId,
            Quantity   = quantity,
            SpawnAtTime = Time.time + _deliveryDelay,
            SpawnedBox = null
        };

        _deliveryQueue.Add(delivery);

        if (_verboseLogging)
        {
            Debug.Log($"[DeliveryManager] Scheduled delivery: {itemId} x{quantity} " +
                     $"(BoxId={boxId}). Will spawn in {_deliveryDelay}s.");
        }
    }

    /// <summary>
    /// Cancel một delivery đang chờ (nếu chưa spawn).
    /// </summary>
    public void CancelDelivery(string boxId)
    {
        _deliveryQueue.RemoveAll(d => d.BoxId == boxId && d.SpawnedBox == null);
    }

    /// <summary>
    /// Hủy tất cả pending deliveries.
    /// </summary>
    public void ClearAllDeliveries()
    {
        _deliveryQueue.Clear();
    }

    // ========================================================================
    // QUEUE PROCESSING
    // ========================================================================

    private void ProcessDeliveryQueue()
    {
        for (int i = _deliveryQueue.Count - 1; i >= 0; i--)
        {
            var delivery = _deliveryQueue[i];

            // Chưa đến lúc spawn
            if (Time.time < delivery.SpawnAtTime) continue;

            // Đã spawn rồi
            if (delivery.SpawnedBox != null) continue;

            // Spawn box
            SpawnDeliveryBox(delivery);
        }
    }

    private void SpawnDeliveryBox(PendingDelivery delivery)
    {
        if (_deliveryBoxPrefab == null)
        {
            Debug.LogError("[DeliveryManager] _deliveryBoxPrefab chưa được assign!");
            _deliveryQueue.Remove(delivery);
            return;
        }

        // Tính vị trí spawn (door cell)
        Vector3 spawnWorldPos = GetDoorSpawnPosition();

        // Instantiate
        var boxGO = UnityEngine.Object.Instantiate(
            _deliveryBoxPrefab,
            spawnWorldPos,
            Quaternion.identity
        );

        boxGO.name = $"[DeliveryBox] {delivery.ItemId} x{delivery.Quantity} ({delivery.BoxId})";

        // Setup DeliveryBox component
        var boxComponent = boxGO.GetComponent<DeliveryBox>();
        if (boxComponent != null)
        {
            boxComponent.Initialize(delivery.BoxId, delivery.ItemId, delivery.Quantity);

            // Thiết lập expire timer
            boxComponent.gameObject.AddComponent<DeliveryExpireTimer>()
                .Initialize(_boxLifetime, boxComponent);
        }

        delivery.SpawnedBox = boxComponent;
        DeliveryEvents.FireBoxSpawned(boxComponent);

        if (_verboseLogging)
        {
            Debug.Log($"[DeliveryManager] Spawned box: {delivery.ItemId} x{delivery.Quantity} " +
                     $"at {spawnWorldPos}");
        }
    }

    private Vector3 GetDoorSpawnPosition()
    {
        if (_doorSpawnPoint != null)
            return _doorSpawnPoint.position;

        if (GridSystem.Instance != null)
        {
            Vector2Int doorCell = new Vector2Int(
                0,
                GridSystem.Instance.ShopMinCell.y
            );
            return GridSystem.Instance.CellToWorld(doorCell);
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Gọi khi DeliveryBox được nhặt (để xóa khỏi queue).
    /// </summary>
    public void OnBoxPickedUp(string boxId)
    {
        _deliveryQueue.RemoveAll(d => d.BoxId == boxId);

        if (_verboseLogging)
            Debug.Log($"[DeliveryManager] Box {boxId} picked up. Remaining: {_deliveryQueue.Count}");
    }
}

/// <summary>
/// MonoBehaviour tạm thời gắn vào DeliveryBox để track expire time.
/// Tự hủy khi hết thời gian.
/// </summary>
public class DeliveryExpireTimer : MonoBehaviour
{
    private DeliveryBox _box;
    private float _expireTime;
    private bool _initialized;

    public void Initialize(float lifetime, DeliveryBox box)
    {
        _box = box;
        _expireTime = Time.time + lifetime;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;
        if (Time.time >= _expireTime)
        {
            _box?.OnExpired();
            Destroy(this);
        }
    }
}
