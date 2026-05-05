// Assets/Scripts/Customer/CustomerSpawner.cs

using UnityEngine;

/// <summary>
/// Spawn khách hàng NPC theo interval khi cửa hàng đang mở.
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ (NPCManager.ts):
///   Spawn interval: 3000ms
///   Max NPC count: 15
///   Intent: 30% PLAY, 70% BUY
/// </summary>
public class CustomerSpawner : MonoBehaviour
{
    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Spawn Settings")]
    [Tooltip("Prefab NPC khách hàng. Phải có CustomerFSM và CharacterMovement.")]
    [SerializeField] private GameObject _customerPrefab;

    [Tooltip("Vị trí spawn (cửa vào cửa hàng).")]
    [SerializeField] private Transform _spawnPoint;

    [Tooltip("Thời gian giữa mỗi lần spawn (giây). Tương đương 3000ms.")]
    [SerializeField] private float _spawnInterval = 3f;

    [Tooltip("Số NPC tối đa cùng lúc trong shop. Tương đương 15 trong hệ cũ.")]
    [SerializeField] private int _maxCustomers = 15;

    [Tooltip("Tỷ lệ NPC có intent BUY (0-1). Tương đương 70% BUY, 30% PLAY.")]
    [SerializeField][Range(0f, 1f)] private float _buyIntentRatio = 0.7f;

    [Header("State")]
    [SerializeField] private bool _shopIsOpen = true;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = true;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    private float _nextSpawnTime;
    private int   _spawnCounter = 0;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Start()
    {
        _nextSpawnTime = Time.time + _spawnInterval;
    }

    private void Update()
    {
        if (Time.time < _nextSpawnTime) return;
        _nextSpawnTime = Time.time + _spawnInterval;
        TrySpawnCustomer();
    }

    // =========================================================================
    // SPAWN LOGIC
    // =========================================================================

    private void TrySpawnCustomer()
    {
        if (!_shopIsOpen) return;

        int currentCount = FindObjectsByType<CustomerFSM>().Length;
        if (currentCount >= _maxCustomers) return;

        SpawnCustomer();
    }

    private void SpawnCustomer()
    {
        if (_customerPrefab == null || _spawnPoint == null) return;

        _spawnCounter++;
        string instanceId = $"customer_{_spawnCounter:D4}_{Time.time:F0}";

        CustomerFSM.CustomerIntent intent = Random.value < _buyIntentRatio
            ? CustomerFSM.CustomerIntent.Buy
            : CustomerFSM.CustomerIntent.Play;

        GameObject customerGO = Instantiate(
            _customerPrefab,
            _spawnPoint.position,
            Quaternion.identity
        );
        customerGO.name = $"[Customer] {instanceId}";

        var fsm = customerGO.GetComponent<CustomerFSM>();
        if (fsm != null)
        {
            fsm.Initialize(instanceId, intent);
        }
        else
        {
            Debug.LogError("[CustomerSpawner] CustomerFSM component không tìm thấy trên prefab!");
            Destroy(customerGO);
            return;
        }

        if (_verboseLogging)
        {
            Debug.Log($"[CustomerSpawner] Spawned {instanceId} with intent={intent}. " +
                      $"Total active: {FindObjectsByType<CustomerFSM>().Length}");
        }
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    public void SetShopOpen(bool isOpen) => _shopIsOpen = isOpen;
    public bool IsShopOpen => _shopIsOpen;
}
