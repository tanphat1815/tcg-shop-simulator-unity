// Assets/Scripts/Staff/WorkerManager.cs

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton quản lý danh sách nhân viên đã thuê.
///
/// RESPONSIBILITIES:
///   1. Quản lý danh sách hired workers
///   2. Hire/fire workers
///   3. Tính tổng salary mỗi ngày (cho TimeManager)
///   4. Spawn WorkerController prefab khi thuê
/// </summary>
public class WorkerManager : MonoBehaviour
{
    // =========================================================================
    // SINGLETON
    // =========================================================================

    public static WorkerManager Instance { get; private set; }

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Database")]
    [Tooltip("WorkerDatabase ScriptableObject.")]
    [SerializeField] private WorkerDatabase _workerDatabase;

    [Header("Spawn Settings")]
    [Tooltip("Prefab WorkerController. Phải có WorkerController + CharacterMovement.")]
    [SerializeField] private GameObject _workerPrefab;

    [Tooltip("Vị trí spawn worker (tại cashier desk area).")]
    [SerializeField] private Transform _workerSpawnPoint;

    // =========================================================================
    // STATE
    // =========================================================================

    private readonly List<WorkerController> _hiredWorkers = new List<WorkerController>();
    private int _workerCounter = 0;

    // =========================================================================
    // EVENTS
    // =========================================================================

    public event Action<WorkerController> OnWorkerHired;
    public event Action<WorkerController> OnWorkerFired;

    // =========================================================================
    // PROPERTIES
    // =========================================================================

    public IReadOnlyList<WorkerController> HiredWorkers => _hiredWorkers;

    public float TotalDailySalary
    {
        get
        {
            float total = 0f;
            foreach (var worker in _hiredWorkers)
            {
                if (worker.Definition != null)
                    total += worker.Definition.dailySalary;
            }
            return total;
        }
    }

    public WorkerController FastestCheckoutWorker
    {
        get
        {
            WorkerController fastest = null;
            float fastestSpeed = float.MaxValue;

            foreach (var worker in _hiredWorkers)
            {
                if (worker.Definition == null) continue;
                float cooldown = worker.Definition.CheckoutCooldownSeconds;
                if (cooldown < fastestSpeed)
                {
                    fastestSpeed = cooldown;
                    fastest = worker;
                }
            }

            return fastest;
        }
    }

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_workerDatabase == null || _workerDatabase.workers.Count == 0)
        {
            Debug.LogWarning("[WorkerManager] No worker database. Creating default.");
            _workerDatabase = ScriptableObject.CreateInstance<WorkerDatabase>();
            _workerDatabase.CreateDefaultDatabase();
        }
    }

    // =========================================================================
    // PUBLIC API — HIRING
    // =========================================================================

    public bool TryHireWorker(string workerId)
    {
        var definition = _workerDatabase.GetWorker(workerId);
        if (definition == null)
        {
            Debug.LogError($"[WorkerManager] Unknown workerId: {workerId}");
            return false;
        }

        if (_hiredWorkers.Exists(w => w.Definition?.workerId == workerId))
        {
            Debug.LogWarning($"[WorkerManager] Already hired worker: {workerId}");
            return false;
        }

        int currentLevel = GameManager.Instance?.CurrentLevel ?? 1;
        if (currentLevel < definition.requiredShopLevel)
        {
            Debug.LogWarning($"[WorkerManager] Need level {definition.requiredShopLevel} to hire {workerId}. " +
                           $"Current: {currentLevel}");
            return false;
        }

        float currentMoney = ShopFloorManager.Instance?.TotalMoney ?? 0f;
        if (currentMoney < definition.hiringFee)
        {
            Debug.LogWarning($"[WorkerManager] Need ${definition.hiringFee:F2} to hire {workerId}. " +
                           $"Current: ${currentMoney:F2}");
            return false;
        }

        return HireWorker(definition);
    }

    private bool HireWorker(WorkerDefinition definition)
    {
        float prevMoney = ShopFloorManager.Instance?.TotalMoney ?? 0f;
        float newMoney = prevMoney - definition.hiringFee;

        if (ShopFloorManager.Instance != null)
        {
            ShopFloorManager.Instance.SetMoney(newMoney);
            GameEconomyEvents.FireMoneyChanged(prevMoney, newMoney);
        }

        _workerCounter++;
        string instanceId = $"worker_{_workerCounter:D4}_{Time.time:F0}";

        Vector3 spawnPos = _workerSpawnPoint != null
            ? _workerSpawnPoint.position
            : (GridSystem.Instance != null
                ? GridSystem.Instance.CellToWorld(new Vector2Int(0, GridSystem.Instance.ShopMinCell.y))
                : Vector3.zero);

        var workerGO = Instantiate(_workerPrefab, spawnPos, Quaternion.identity);
        workerGO.name = $"[Worker] {definition.displayName} ({instanceId})";

        var workerController = workerGO.GetComponent<WorkerController>();
        if (workerController != null)
        {
            workerController.Initialize(instanceId, definition);
            _hiredWorkers.Add(workerController);
        }

        GameEconomyEvents.FireWorkerHired(instanceId, definition.workerId, definition.hiringFee);

        Debug.Log($"[WorkerManager] Hired worker: {definition.displayName}. " +
                 $"Fee: ${definition.hiringFee:F2}, Salary: ${definition.dailySalary:F2}/day. " +
                 $"Total workers: {_hiredWorkers.Count}");

        OnWorkerHired?.Invoke(workerController);
        return true;
    }

    public bool FireWorker(string instanceId)
    {
        var worker = _hiredWorkers.Find(w => w.InstanceId == instanceId);
        if (worker == null)
        {
            Debug.LogWarning($"[WorkerManager] Worker not found: {instanceId}");
            return false;
        }

        _hiredWorkers.Remove(worker);
        Destroy(worker.gameObject);

        GameEconomyEvents.FireWorkerFired(instanceId);

        Debug.Log($"[WorkerManager] Fired worker: {instanceId}. " +
                 $"Remaining: {_hiredWorkers.Count}");
        return true;
    }

    // =========================================================================
    // REGISTRATION
    // =========================================================================

    public void RegisterWorker(WorkerController worker)
    {
        if (worker == null || _hiredWorkers.Contains(worker)) return;
        _hiredWorkers.Add(worker);
    }

    public void UnregisterWorker(WorkerController worker)
    {
        if (worker == null) return;
        _hiredWorkers.Remove(worker);
    }

    // =========================================================================
    // DATABASE ACCESS
    // =========================================================================

    public WorkerDatabase Database => _workerDatabase;

    public List<WorkerDefinition> GetAvailableWorkers()
    {
        int currentLevel = GameManager.Instance?.CurrentLevel ?? 1;
        return _workerDatabase.GetAvailableWorkers(currentLevel);
    }

    public bool IsWorkerHired(string workerId)
    {
        return _hiredWorkers.Exists(w => w.Definition?.workerId == workerId);
    }
}
