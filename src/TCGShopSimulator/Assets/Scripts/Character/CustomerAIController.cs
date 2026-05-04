// Assets/Scripts/Character/CustomerAIController.cs

using UnityEngine;

/// <summary>
/// Finite State Machine điều khiển hành vi khách hàng.
///
/// PORT TỪ HỆ THỐNG CŨ (NPCManager.ts):
///   NPCState enum:
///     'SPAWN'      → CustomerState.Spawning
///     'WANDER'     → CustomerState.Wandering
///     'SEEK_ITEM'  → CustomerState.SeekingItem
///     'INTERACT'   → CustomerState.Interacting
///     'GO_CASHIER' → CustomerState.GoingToCashier
///     'WAITING'    → CustomerState.WaitingInLine
///     'LEAVE'      → CustomerState.Leaving
///
///   physics.moveTo(sprite, x, y, speed) → _movement.RequestPath(cellCoord)
///   handleStuckRecovery()               → CharacterMovement.HandlePathStale() (tự động)
///   boredomThreshold (45000ms)          → BOREDOM_THRESHOLD (45s)
///
/// THAY ĐỔI QUAN TRỌNG:
///   - Không dùng world coordinates trực tiếp cho movement
///   - Tất cả movement đi qua CharacterMovement → A* path
///   - Targets được convert từ world → cell trước khi RequestPath
/// </summary>
[RequireComponent(typeof(CharacterMovement))]
public class CustomerAIController : MonoBehaviour
{
    // =========================================================================
    // CONSTANTS
    // =========================================================================

    private const float BOREDOM_THRESHOLD = 45f;    // 45s như hệ thống cũ
    private const float DECISION_INTERVAL = 1.5f;   // 1500ms như hệ thống cũ
    private const float INTERACT_DURATION = 1f;     // 1000ms như hệ thống cũ

    // =========================================================================
    // CUSTOMER STATE MACHINE
    // =========================================================================

    public enum CustomerState
    {
        Spawning,
        Wandering,
        SeekingItem,
        Interacting,
        GoingToCashier,
        WaitingInLine,
        Leaving,
        Stuck  // State mới khi A* không tìm được đường
    }

    public CustomerState CurrentState { get; private set; } = CustomerState.Spawning;

    // =========================================================================
    // COMPONENT REFERENCES
    // =========================================================================

    private CharacterMovement _movement;

    // =========================================================================
    // AI STATE
    // =========================================================================

    private float _spawnTime;
    private float _lastDecisionTime;
    private float _interactStartTime;

    public enum CustomerIntent { Buy, Play }
    public CustomerIntent Intent { get; private set; }

    public string InstanceId { get; private set; }

    [SerializeField] private bool verboseLogging = false;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        _movement = GetComponent<CharacterMovement>();

        _movement.OnReachedGoal += HandleReachedGoal;
        _movement.OnPathNotFound += HandlePathNotFound;
        _movement.OnGoalAbandoned += HandleGoalAbandoned;
    }

    private void OnDestroy()
    {
        if (_movement != null)
        {
            _movement.OnReachedGoal -= HandleReachedGoal;
            _movement.OnPathNotFound -= HandlePathNotFound;
            _movement.OnGoalAbandoned -= HandleGoalAbandoned;
        }
    }

    /// <summary>
    /// Khởi tạo NPC với intent và ID.
    /// </summary>
    public void Initialize(string instanceId, CustomerIntent intent)
    {
        InstanceId = instanceId;
        Intent = intent;
        _spawnTime = Time.time;
        _lastDecisionTime = Time.time;

        TransitionToState(CustomerState.Spawning);
    }

    private void Update()
    {
        UpdateStateMachine();
        CheckBoredom();
    }

    // =========================================================================
    // STATE MACHINE
    // =========================================================================

    private void UpdateStateMachine()
    {
        switch (CurrentState)
        {
            case CustomerState.Spawning:
                HandleSpawning();
                break;
            case CustomerState.Wandering:
                HandleWandering();
                break;
            case CustomerState.Interacting:
                HandleInteracting();
                break;
            case CustomerState.WaitingInLine:
                HandleWaitingInLine();
                break;
            case CustomerState.Stuck:
                HandleStuck();
                break;
            // SeekingItem, GoingToCashier, Leaving:
            // CharacterMovement đang di chuyển → đợi OnReachedGoal event
        }
    }

    private void HandleSpawning()
    {
        if (Time.time - _spawnTime > 0.5f)
        {
            TransitionToState(CustomerState.Wandering);
            MoveToRandomWanderPoint();
        }
    }

    private void HandleWandering()
    {
        if (Time.time - _lastDecisionTime < DECISION_INTERVAL) return;
        _lastDecisionTime = Time.time;

        if (Intent == CustomerIntent.Buy)
        {
            TryFindShelf();
        }
        // PLAY intent: TryFindTable() — implement ở bước sau
    }

    private void HandleInteracting()
    {
        if (Time.time - _interactStartTime >= INTERACT_DURATION)
        {
            Debug.Log($"[CustomerAI] {InstanceId}: Đã tương tác với kệ. " +
                      "Đi về cashier...");
            TransitionToState(CustomerState.GoingToCashier);
            MoveToNearestCashier();
        }
    }

    private void HandleWaitingInLine()
    {
        // Placeholder: Chờ được phục vụ
    }

    private void HandleStuck()
    {
        if (Time.time - _spawnTime > BOREDOM_THRESHOLD)
        {
            TransitionToState(CustomerState.Leaving);
            MoveToExit();
        }
    }

    // =========================================================================
    // STATE TRANSITIONS
    // =========================================================================

    private void TransitionToState(CustomerState newState)
    {
        if (verboseLogging)
            Debug.Log($"[CustomerAI] {InstanceId}: {CurrentState} → {newState}");

        CurrentState = newState;
    }

    // =========================================================================
    // MOVEMENT REQUESTS
    // =========================================================================

    private void MoveToRandomWanderPoint()
    {
        if (GridSystem.Instance == null) return;

        var bounds = GridSystem.Instance;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int x = Random.Range(bounds.ShopMinCell.x, bounds.ShopMaxCell.x + 1);
            int y = Random.Range(bounds.ShopMinCell.y, bounds.ShopMaxCell.y + 1);
            var cell = new Vector2Int(x, y);

            if (PathfindingGrid.Instance != null && PathfindingGrid.Instance.IsWalkable(cell))
            {
                _movement.RequestPath(cell);
                return;
            }
        }
    }

    private void TryFindShelf()
    {
        // Placeholder: Tìm kệ gần nhất có hàng
        Debug.Log($"[CustomerAI] {InstanceId}: Đang tìm kệ...");
    }

    private void MoveToNearestCashier()
    {
        // Placeholder: Di chuyển đến cashier
        Debug.Log($"[CustomerAI] {InstanceId}: Di chuyển đến cashier...");
    }

    private void MoveToExit()
    {
        if (GridSystem.Instance == null) return;
        var exitCell = new Vector2Int(0, GridSystem.Instance.ShopMinCell.y);
        _movement.RequestPath(exitCell);
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    private void HandleReachedGoal()
    {
        switch (CurrentState)
        {
            case CustomerState.Wandering:
                _lastDecisionTime = 0; // Force decision ngay
                break;

            case CustomerState.SeekingItem:
                _interactStartTime = Time.time;
                TransitionToState(CustomerState.Interacting);
                break;

            case CustomerState.GoingToCashier:
                TransitionToState(CustomerState.WaitingInLine);
                break;

            case CustomerState.Leaving:
                Destroy(gameObject);
                break;
        }
    }

    private void HandlePathNotFound()
    {
        Debug.LogWarning($"[CustomerAI] {InstanceId}: Path not found, switching to Wander.");
        TransitionToState(CustomerState.Wandering);
        MoveToRandomWanderPoint();
    }

    private void HandleGoalAbandoned()
    {
        Debug.LogWarning($"[CustomerAI] {InstanceId}: Goal abandoned, leaving shop.");
        TransitionToState(CustomerState.Leaving);
        MoveToExit();
    }

    // =========================================================================
    // UTILITIES
    // =========================================================================

    private void CheckBoredom()
    {
        if (CurrentState == CustomerState.Leaving) return;
        if (Time.time - _spawnTime > BOREDOM_THRESHOLD)
        {
            TransitionToState(CustomerState.Leaving);
            MoveToExit();
        }
    }
}
