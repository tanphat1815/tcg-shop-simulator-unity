// Assets/Scripts/Staff/WorkerController.cs

using System;
using UnityEngine;

/// <summary>
/// Finite State Machine cho nhân viên (Worker).
///
/// FSM STATES:
///   Idle         — Đứng yên tại cashier desk, chờ customer
///   Moving       — Di chuyển đến queue position
///   Serving      — Đang checkout customer (cooldown)
///
/// DIFFERENCES FROM CustomerFSM:
///   - Worker KHÔNG tự di chuyển lung tung — chỉ di chuyển khi cần
///   - Worker KHÔNG có Wander state
///   - Worker KHÔNG tự destroy — tồn tại xuyên ngày
///   - Worker được quản lý bởi WorkerManager
/// </summary>
[RequireComponent(typeof(CharacterMovement))]
public class WorkerController : MonoBehaviour
{
    // =========================================================================
    // FSM STATES
    // =========================================================================

    public enum WorkerState
    {
        Idle,     // Chờ tại desk
        Moving,   // Di chuyển đến queue
        Serving   // Đang checkout
    }

    // =========================================================================
    // IDENTITY
    // =========================================================================

    public string InstanceId { get; private set; }
    public WorkerDefinition Definition { get; private set; }

    // =========================================================================
    // FSM STATE
    // =========================================================================

    public WorkerState CurrentState { get; private set; } = WorkerState.Idle;

    // =========================================================================
    // SERVING STATE
    // =========================================================================

    private float _servingStartTime;
    private float _servingCooldown;
    private CustomerFSM _currentCustomer;

    // =========================================================================
    // COMPONENTS
    // =========================================================================

    private CharacterMovement _movement;
    private CashierQueueManager _cashierQueue;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        _movement = GetComponent<CharacterMovement>();

        _movement.OnReachedGoal += HandleReachedGoal;
        _movement.OnPathNotFound += HandlePathNotFound;
    }

    private void OnDestroy()
    {
        if (_movement != null)
        {
            _movement.OnReachedGoal -= HandleReachedGoal;
            _movement.OnPathNotFound -= HandlePathNotFound;
        }

        if (WorkerManager.Instance != null)
            WorkerManager.Instance.UnregisterWorker(this);
    }

    private void Start()
    {
        if (WorkerManager.Instance != null)
            WorkerManager.Instance.RegisterWorker(this);

        _cashierQueue = ShopFloorManager.Instance?.CashierQueue;
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case WorkerState.Idle:
                UpdateIdle();
                break;

            case WorkerState.Serving:
                UpdateServing();
                break;

            // Moving: CharacterMovement đang di chuyển → đợi OnReachedGoal
        }
    }

    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    public void Initialize(string instanceId, WorkerDefinition definition)
    {
        InstanceId = instanceId;
        Definition = definition;

        _servingCooldown = definition.CheckoutCooldownSeconds;

        gameObject.name = $"[Worker] {definition.displayName} ({instanceId})";

        Debug.Log($"[WorkerController] Initialized: {definition.displayName}, " +
                 $"Checkout speed: {definition.checkoutSpeed} ({_servingCooldown}s)");
    }

    // =========================================================================
    // IDLE STATE
    // =========================================================================

    private void UpdateIdle()
    {
        if (_cashierQueue == null || !_cashierQueue.HasCustomers)
            return;

        TryStartServing();
    }

    private void TryStartServing()
    {
        if (_cashierQueue == null) return;

        var nextCustomer = _cashierQueue.PeekNextCustomer();
        if (nextCustomer == null) return;

        int queueIndex = _cashierQueue.GetSlotIndex(nextCustomer);
        Vector3 queuePos = _cashierQueue.GetSlotWorldPosition(queueIndex);
        Vector2Int queueCell = GridSystem.Instance.WorldToCell(queuePos);

        TransitionToState(WorkerState.Moving);
        _movement.RequestPath(queueCell);

        Debug.Log($"[WorkerController] {InstanceId}: Moving to serve customer at queue position {queueIndex}");
    }

    // =========================================================================
    // SERVING STATE
    // =========================================================================

    private void UpdateServing()
    {
        if (Time.time - _servingStartTime >= _servingCooldown)
            CompleteCheckout();
    }

    private void CompleteCheckout()
    {
        if (_cashierQueue == null)
        {
            TransitionToState(WorkerState.Idle);
            return;
        }

        var servedCustomer = _cashierQueue.ServeNextCustomer();

        if (servedCustomer != null)
        {
            GameEconomyEvents.FireCustomerServedByWorker(
                servedCustomer.InstanceId,
                InstanceId,
                servedCustomer.CarriedItemPrice
            );

            Debug.Log($"[WorkerController] {InstanceId}: Served customer {servedCustomer.InstanceId}. " +
                     $"Price: ${servedCustomer.CarriedItemPrice:F2}");
        }

        MoveToDeskPosition();
        TransitionToState(WorkerState.Idle);
    }

    // =========================================================================
    // MOVEMENT CALLBACKS
    // =========================================================================

    private void HandleReachedGoal()
    {
        switch (CurrentState)
        {
            case WorkerState.Moving:
                _servingStartTime = Time.time;
                _movement.StopMovement();
                TransitionToState(WorkerState.Serving);
                Debug.Log($"[WorkerController] {InstanceId}: Started serving. Cooldown: {_servingCooldown}s");
                break;
        }
    }

    private void HandlePathNotFound()
    {
        Debug.LogWarning($"[WorkerController] {InstanceId}: Path not found. Retrying...");
        Invoke(nameof(RetryMoveToCustomer), 1f);
    }

    private void RetryMoveToCustomer()
    {
        if (CurrentState != WorkerState.Moving) return;
        TryStartServing();
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private void MoveToDeskPosition()
    {
        if (_cashierQueue != null)
        {
            Vector3 deskPos = _cashierQueue.CashierDeskPosition;
            Vector2Int deskCell = GridSystem.Instance.WorldToCell(deskPos);
            _movement.RequestPath(deskCell);
        }
    }

    private void TransitionToState(WorkerState newState)
    {
        if (CurrentState == newState) return;

        Debug.Log($"[WorkerController] {InstanceId}: {CurrentState} → {newState}");
        CurrentState = newState;
    }

    // =========================================================================
    // DEBUG
    // =========================================================================

    public override string ToString() =>
        $"Worker[{InstanceId}|{Definition?.displayName}|{CurrentState}]";
}
