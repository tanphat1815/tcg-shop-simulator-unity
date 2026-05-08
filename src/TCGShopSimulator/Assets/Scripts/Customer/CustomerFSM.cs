// Assets/Scripts/Customer/CustomerFSM.cs

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finite State Machine điều khiển toàn bộ vòng đời hành vi của khách hàng NPC.
///
/// SÁU STATES:
///   EnterShop → Wander → SeekingShelf → ExamineShelf → QueueAtCheckout
///       ↑____________↓               │
///       (chưa tìm được kệ)          │ (từ chối mua)
///                                   ↓
///                           ExitShop ←── WaitingInLine
///
/// MAPPING TỪ HỆ THỐNG CŨ (NPCManager.ts):
///   'SPAWN'      → EnterShop       (500ms delay)
///   'WANDER'     → Wander          (scan kệ mỗi DECISION_INTERVAL)
///   'SEEK_ITEM'  → SeekingShelf    (di chuyển đến kệ qua A*)
///   'INTERACT'   → ExamineShelf    (đứng 2s, tính xác suất, hiện bong bóng)
///   'GO_CASHIER' → QueueAtCheckout (di chuyển đến slot cashier)
///   'WAITING'    → WaitingInLine   (chờ được phục vụ)
///   'LEAVE'      → ExitShop        (di chuyển đến cửa, tự hủy)
///
/// NÂNG CẤP SO VỚI HỆ THỐNG CŨ:
///   1. ExamineShelf có EconomicDecisionEngine → NPC tính xác suất mua theo giá
///   2. SpeechBubble hiện bong bóng phản ứng (Heart/Neutral/Angry)
///   3. checkedShelfIds dùng instance ID thay vì string ID
///   4. CashierQueueManager quản lý offset xếp hàng chính xác
/// </summary>
[RequireComponent(typeof(CharacterMovement))]
[DisallowMultipleComponent]
public class CustomerFSM : MonoBehaviour
{
    // =========================================================================
    // ENUMS
    // =========================================================================

    public enum CustomerState
    {
        EnterShop,        // 'SPAWN' cũ — NPC vừa xuất hiện, chờ 0.5s
        Wander,           // 'WANDER' cũ — Tìm kệ có hàng
        SeekingShelf,     // 'SEEK_ITEM' cũ — Di chuyển đến kệ mục tiêu
        ExamineShelf,     // 'INTERACT' cũ + Economic Decision
        QueueAtCheckout,  // 'GO_CASHIER' cũ — Di chuyển đến slot cashier
        WaitingInLine,    // 'WAITING' cũ — Chờ được phục vụ
        WantToPlay,       // ← MỚI: Intent=Play, đang tìm bàn
        SeekingTable,     // ← MỚI: Di chuyển đến bàn
        Playing,          // ← MỚI: Ngồi chơi 12 giây
        ExitShop          // 'LEAVE' cũ — Đi ra cửa, tự hủy
    }

    public enum CustomerIntent { Buy, Play }

    // =========================================================================
    // CONSTANTS
    // =========================================================================

    private const float EXAMINE_DURATION      = 2f;     // Yêu cầu test: đứng 2s
    private const float DECISION_INTERVAL     = 1.5f;   // Scan kệ mỗi 1.5s
    private const float BOREDOM_THRESHOLD    = 45f;    // Rời đi sau 45s không mua
    private const float SPAWN_DELAY          = 0.5f;   // Chờ trước khi wander
    private const float SHELF_INTERACT_DISTANCE = 0.5f; // Ngưỡng "đã đến kệ"

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Configuration")]
    [Tooltip("Prefab của SpeechBubble để Instantiate khi cần.")]
    [SerializeField] private GameObject _speechBubblePrefab;

    [Tooltip("Offset Y để spawn bong bóng phía trên đầu NPC.")]
    [SerializeField] private float _bubbleYOffset = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = false;

    // =========================================================================
    // COMPONENT REFERENCES
    // =========================================================================

    private CharacterMovement _movement;

    // =========================================================================
    // FSM STATE
    // =========================================================================

    public CustomerState   CurrentState { get; private set; } = CustomerState.EnterShop;
    public CustomerIntent  Intent       { get; private set; } = CustomerIntent.Buy;
    public string          InstanceId   { get; private set; }

    /// <summary>Giá của item đã mua (dùng bởi WorkerController khi fire OnCustomerServedByWorker).</summary>
    public float CarriedItemPrice => _carriedItemPrice;

    // =========================================================================
    // AI VARIABLES
    // =========================================================================

    private float _spawnTime;
    private float _lastDecisionTime;
    private float _examineStartTime;

    private ShelfInstance _targetShelf;

    /// <summary>
    /// Danh sách InstanceID của các kệ NPC đã kiểm tra (và thấy hết hàng hoặc từ chối).
    /// Reset mỗi lần NPC bắt đầu mới.
    /// </summary>
    private HashSet<string> _checkedShelfIds = new HashSet<string>();

    private string _carriedItemId   = string.Empty;
    private float  _carriedItemPrice = 0f;
    private int    _queueSlotIndex  = -1;

    // Play table state
    private PlayTableInstance _assignedTable;
    private int _assignedSeatIndex = -1;

    // =========================================================================
    // COROUTINES
    // =========================================================================

    private Coroutine _examineCoroutine;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        _movement = GetComponent<CharacterMovement>();

        _movement.OnReachedGoal    += HandleMovementReachedGoal;
        _movement.OnPathNotFound   += HandleMovementPathNotFound;
        _movement.OnGoalAbandoned += HandleMovementGoalAbandoned;
    }

    private void OnDestroy()
    {
        if (_movement != null)
        {
            _movement.OnReachedGoal    -= HandleMovementReachedGoal;
            _movement.OnPathNotFound   -= HandleMovementPathNotFound;
            _movement.OnGoalAbandoned -= HandleMovementGoalAbandoned;
        }

        if (_queueSlotIndex >= 0 && ShopFloorManager.Instance?.CashierQueue != null)
        {
            ShopFloorManager.Instance.CashierQueue.DequeueCustomer(this);
        }

        // Cleanup table seat nếu đang ngồi
        if (_assignedTable != null)
        {
            _assignedTable.FreeSeat(this);
            _assignedTable = null;
        }
    }

    private void Start()
    {
        _spawnTime       = Time.time;
        _lastDecisionTime = Time.time;
    }

    private void Update()
    {
        UpdateFSM();
        CheckBoredom();
    }

    // =========================================================================
    // KHỞI TẠO
    // =========================================================================

    /// <summary>
    /// Khởi tạo NPC với ID và intent.
    /// Gọi bởi CustomerSpawner ngay sau khi Instantiate prefab.
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   new Customer({ intent: 'BUY', instanceId: uuid() }) trong NPCManager.ts
    /// </summary>
    public void Initialize(string instanceId, CustomerIntent intent)
    {
        InstanceId    = instanceId;
        Intent        = intent;
        _spawnTime    = Time.time;
        _lastDecisionTime = Time.time;
        _checkedShelfIds.Clear();
        _examineStartTime = 0f;
        _targetShelf = null;
        _queueSlotIndex = -1;
        _carriedItemId = string.Empty;
        _carriedItemPrice = 0f;
        _assignedTable = null;
        _assignedSeatIndex = -1;
        TransitionToState(CustomerState.EnterShop);
    }

    // =========================================================================
    // FSM UPDATE
    // =========================================================================

    private void UpdateFSM()
    {
        switch (CurrentState)
        {
            case CustomerState.EnterShop:
                HandleEnterShop();
                break;

            case CustomerState.Wander:
                HandleWander();
                break;

            case CustomerState.Playing:
                HandlePlaying();
                break;

            case CustomerState.WaitingInLine:
                HandleWaitingInLine();
                break;

            // SeekingShelf, SeekingTable, QueueAtCheckout, ExitShop:
            // CharacterMovement đang di chuyển → đợi OnReachedGoal
        }
    }

    // =========================================================================
    // STATE HANDLERS
    // =========================================================================

    private void HandleEnterShop()
    {
        if (Time.time - _spawnTime >= SPAWN_DELAY)
        {
            TransitionToState(CustomerState.Wander);
            MoveToRandomWanderPoint();
        }
    }

    private void HandleWander()
    {
        if (Time.time - _lastDecisionTime < DECISION_INTERVAL) return;
        _lastDecisionTime = Time.time;

        if (Intent == CustomerIntent.Buy)
        {
            TryScanForShelf();
        }
        else if (Intent == CustomerIntent.Play)
        {
            TryFindPlayTable();
        }
    }

    private void HandleWaitingInLine()
    {
        // Logic xử lý qua CashierQueueManager callbacks:
        //   UpdateQueuePosition() → di chuyển lên hàng
        //   OnServed() → chuyển ExitShop
    }

    // =========================================================================
    // SCAN & TARGETING
    // =========================================================================

    private void TryScanForShelf()
    {
        if (ShopFloorManager.Instance == null) return;

        ShelfInstance shelf = ShopFloorManager.Instance
            .GetNearestAvailableShelf(transform.position, _checkedShelfIds);

        if (shelf != null)
        {
            _targetShelf = shelf;
            TransitionToState(CustomerState.SeekingShelf);

            // Di chuyển đến cell phía trước kệ
            Vector3 shelfPos   = shelf.WorldPosition;
            Vector2Int shelfCell = GridSystem.Instance.WorldToCell(shelfPos);
            Vector2Int targetCell = shelfCell + new Vector2Int(0, -1);

            _movement.RequestPath(targetCell);

            if (_verboseLogging)
                Debug.Log($"[CustomerFSM] {InstanceId}: Found shelf. Moving to it.");
        }
        else
        {
            // Không tìm thấy kệ — 40% chance rời đi
            if (Random.value < 0.4f)
            {
                if (_verboseLogging)
                    Debug.Log($"[CustomerFSM] {InstanceId}: No shelves found. Leaving (40% chance).");

                TransitionToState(CustomerState.ExitShop);
                MoveToExit();
            }
            else
            {
                MoveToRandomWanderPoint();
            }
        }
    }

    // =========================================================================
    // EXAMINE SHELF
    // =========================================================================

    private IEnumerator ExamineShelfCoroutine()
    {
        // Guard: shelf đã bị hủy giữa lúc di chuyển và Examine
        if (_targetShelf == null)
        {
            if (_verboseLogging)
                Debug.Log($"[CustomerFSM] {InstanceId}: Target shelf was destroyed. Wandering.");
            TransitionToState(CustomerState.Wander);
            MoveToRandomWanderPoint();
            yield break;
        }

        if (!_targetShelf.HasStock)
        {
            _checkedShelfIds.Add(_targetShelf.GetEntityId().ToString());
            if (_verboseLogging)
                Debug.Log($"[CustomerFSM] {InstanceId}: Shelf out of stock. Wandering.");

            TransitionToState(CustomerState.Wander);
            MoveToRandomWanderPoint();
            yield break;
        }

        _examineStartTime = Time.time;

        // BƯỚC 1: Tính xác suất mua
        bool willBuy = EconomicDecisionEngine.DecidePurchase(
            _targetShelf.CurrentSellPrice,
            _targetShelf.MarketPrice,
            out float probability,
            out PurchaseDecision decisionType
        );

        if (_verboseLogging)
        {
            Debug.Log($"[CustomerFSM] {InstanceId} examining shelf: " +
                      $"SellPrice=${_targetShelf.CurrentSellPrice:F2}, " +
                      $"Market=${_targetShelf.MarketPrice:F2}, " +
                      $"Probability={probability:P0}, " +
                      $"Decision={decisionType}, WillBuy={willBuy}");
        }

        // BƯỚC 2: Hiện bong bóng phản ứng
        BubbleReactionType bubbleType = EconomicDecisionEngine.GetBubbleReaction(decisionType);
        ShowReactionBubble(bubbleType, EXAMINE_DURATION);

        // BƯỚC 3: Đứng chờ EXAMINE_DURATION (2 giây)
        yield return new WaitForSeconds(EXAMINE_DURATION);

        // BƯỚC 4: Thực thi quyết định
        if (willBuy && _targetShelf.HasStock)
        {
            if (_targetShelf.TakeOneItem(out string takenItemId, out float paidPrice))
            {
                _carriedItemId    = takenItemId;
                _carriedItemPrice = paidPrice;

                if (_verboseLogging)
                    Debug.Log($"[CustomerFSM] {InstanceId}: Decided to BUY {takenItemId} for ${paidPrice:F2}.");

                TransitionToState(CustomerState.QueueAtCheckout);
                MoveToJoinQueue();
            }
            else
            {
                _checkedShelfIds.Add(_targetShelf.GetEntityId().ToString());
                TransitionToState(CustomerState.Wander);
                MoveToRandomWanderPoint();
            }
        }
        else
        {
            if (_verboseLogging)
                Debug.Log($"[CustomerFSM] {InstanceId}: REFUSED to buy. Reason: {decisionType}. Leaving shop.");

            if (_targetShelf != null)
                _checkedShelfIds.Add(_targetShelf.GetEntityId().ToString());

            _targetShelf = null;

            if (decisionType == PurchaseDecision.AbsoluteRefusal)
            {
                TransitionToState(CustomerState.ExitShop);
                MoveToExit();
            }
            else
            {
                TransitionToState(CustomerState.Wander);
                MoveToRandomWanderPoint();
            }
        }

        _examineCoroutine = null;
    }

    // =========================================================================
    // MOVEMENT HELPERS
    // =========================================================================

    private void MoveToRandomWanderPoint()
    {
        if (GridSystem.Instance == null || PathfindingGrid.Instance == null) return;

        for (int attempt = 0; attempt < 15; attempt++)
        {
            int x = Random.Range(
                GridSystem.Instance.ShopMinCell.x,
                GridSystem.Instance.ShopMaxCell.x + 1);
            int y = Random.Range(
                GridSystem.Instance.ShopMinCell.y,
                GridSystem.Instance.ShopMaxCell.y + 1);

            var cell = new Vector2Int(x, y);
            if (PathfindingGrid.Instance.IsWalkable(cell))
            {
                _movement.RequestPath(cell);
                return;
            }
        }
    }

    private void MoveToExit()
    {
        if (GridSystem.Instance == null) return;
        var exitCell = new Vector2Int(0, GridSystem.Instance.ShopMinCell.y);
        _movement.RequestPath(exitCell);
    }

    private void MoveToJoinQueue()
    {
        var cashierQueue = ShopFloorManager.Instance?.CashierQueue;
        if (cashierQueue == null || !cashierQueue.HasCashier)
        {
            Debug.LogWarning($"[CustomerFSM - {InstanceId}] No cashier available at state {CurrentState}. " +
                            "Triggering Fallback to ExitShop.");
            TransitionToState(CustomerState.ExitShop);
            MoveToExit();
            return;
        }

        _queueSlotIndex = cashierQueue.EnqueueCustomer(this, _carriedItemId, _carriedItemPrice);

        if (_queueSlotIndex < 0)
        {
            Debug.LogWarning($"[CustomerFSM - {InstanceId}] Queue full at state {CurrentState}. " +
                            "Triggering Fallback to ExitShop.");
            TransitionToState(CustomerState.ExitShop);
            MoveToExit();
            return;
        }

        Vector3 slotWorldPos = cashierQueue.GetSlotWorldPosition(_queueSlotIndex);
        Vector2Int slotCell  = GridSystem.Instance.WorldToCell(slotWorldPos);
        _movement.RequestPath(slotCell);
    }

    // =========================================================================
    // PLAY TABLE
    // =========================================================================

    private void TryFindPlayTable()
    {
        if (PlayTableManager.Instance == null) return;

        var table = PlayTableManager.Instance.FindAvailableTable(transform.position);

        if (table != null)
        {
            _assignedTable = table;

            if (table.TryAssignSeat(this, out int seatIndex))
            {
                _assignedSeatIndex = seatIndex;
                TransitionToState(CustomerState.SeekingTable);

                Vector3 seatWorldPos = table.GetSeatWorldPosition(seatIndex);
                Vector2Int seatCell = GridSystem.Instance.WorldToCell(seatWorldPos);
                _movement.RequestPath(seatCell);

                if (_verboseLogging)
                    Debug.Log($"[CustomerFSM] {InstanceId}: Found table. Moving to seat {seatIndex}.");
            }
            else
            {
                Debug.Log($"[CustomerFSM] {InstanceId}: Cannot assign seat. Falling back to BUY.");
                Intent = CustomerIntent.Buy;
                TryScanForShelf();
            }
        }
        else
        {
            // Không tìm được bàn → 20% chance rời đi, 80% tiếp tục wander
            if (Random.value < 0.2f)
            {
                if (_verboseLogging)
                    Debug.Log($"[CustomerFSM] {InstanceId}: No tables found. Leaving.");
                TransitionToState(CustomerState.ExitShop);
                MoveToExit();
            }
            else
            {
                MoveToRandomWanderPoint();
            }
        }
    }

    private void OnReachedTable()
    {
        if (_assignedTable == null)
        {
            TransitionToState(CustomerState.Wander);
            MoveToRandomWanderPoint();
            return;
        }

        // Dừng movement
        _movement.StopMovement();
        _movement.enabled = false;

        // Xoay hướng nhìn về đối tác
        Quaternion seatRotation = _assignedTable.GetSeatFacingRotation(_assignedSeatIndex);
        transform.rotation = seatRotation;

        TransitionToState(CustomerState.Playing);

        if (_verboseLogging)
            Debug.Log($"[CustomerFSM] {InstanceId}: Seated at table. Playing.");
    }

    private void HandlePlaying()
    {
        if (_assignedTable != null)
        {
            _assignedTable.UpdateVisuals();
        }
        // Đang chờ match kết thúc — OnMatchFinished() sẽ được gọi từ PlayTableInstance
    }

    /// <summary>
    /// PlayTableInstance gọi khi match kết thúc.
    /// </summary>
    public void OnMatchFinished()
    {
        if (CurrentState != CustomerState.Playing) return;

        if (_verboseLogging)
            Debug.Log($"[CustomerFSM] {InstanceId}: Match finished. Leaving shop.");

        // Re-enable movement
        if (_movement != null)
            _movement.enabled = true;

        // Cleanup table seat
        if (_assignedTable != null)
        {
            _assignedTable.FreeSeat(this);
            _assignedTable = null;
        }

        TransitionToState(CustomerState.ExitShop);
        MoveToExit();
    }

    // =========================================================================
    // CLOSING TIME
    // =========================================================================

    /// <summary>
    /// Gọi bởi TimeManager khi shop đóng cửa.
    ///
    /// Rules:
    ///   PLAYING/WANT_TO_PLAY/SEEK_TABLE → LEAVE immediately
    ///   WANDER/SEEKING_SHELF → LEAVE immediately
    ///   QUEUE_AT_CHECKOUT/WAITING_IN_LINE → stay (complete transaction)
    ///   EXIT_SHOP → do nothing
    /// </summary>
    public void HandleClosingTime()
    {
        switch (CurrentState)
        {
            case CustomerState.Playing:
            case CustomerState.WantToPlay:
            case CustomerState.SeekingTable:
                if (_assignedTable != null)
                {
                    _assignedTable.FreeSeat(this);
                    _assignedTable = null;
                }
                if (_movement != null)
                    _movement.enabled = true;
                TransitionToState(CustomerState.ExitShop);
                MoveToExit();
                Debug.Log($"[CustomerFSM] {InstanceId}: Closing time! Leaving (was at table).");
                break;

            case CustomerState.Wander:
            case CustomerState.SeekingShelf:
            case CustomerState.ExamineShelf:
                if (_examineCoroutine != null) StopCoroutine(_examineCoroutine);
                _targetShelf = null;
                TransitionToState(CustomerState.ExitShop);
                MoveToExit();
                Debug.Log($"[CustomerFSM] {InstanceId}: Closing time! Leaving (was wandering/seeking).");
                break;

            case CustomerState.QueueAtCheckout:
            case CustomerState.WaitingInLine:
                Debug.Log($"[CustomerFSM] {InstanceId}: Closing time, but in transaction. Completing checkout first.");
                break;

            case CustomerState.ExitShop:
                break;
        }
    }

    // =========================================================================
    // SPEECH BUBBLE
    // =========================================================================

    private void ShowReactionBubble(BubbleReactionType bubbleType, float duration)
    {
        if (_speechBubblePrefab == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * _bubbleYOffset;
        GameObject bubbleGO = Instantiate(_speechBubblePrefab, spawnPos, Quaternion.identity);
        var bubble = bubbleGO.GetComponent<SpeechBubble>();

        if (bubble != null)
            bubble.Show(bubbleType, transform, duration);
    }

    // =========================================================================
    // MOVEMENT EVENT CALLBACKS
    // =========================================================================

    private void HandleMovementReachedGoal()
    {
        switch (CurrentState)
        {
            case CustomerState.Wander:
                _lastDecisionTime = 0f;  // Scan kệ ngay
                break;

            case CustomerState.SeekingShelf:
                TransitionToState(CustomerState.ExamineShelf);
                if (_examineCoroutine != null) StopCoroutine(_examineCoroutine);
                _examineCoroutine = StartCoroutine(ExamineShelfCoroutine());
                break;

            case CustomerState.QueueAtCheckout:
                TransitionToState(CustomerState.WaitingInLine);
                break;

            case CustomerState.SeekingTable:
                OnReachedTable();
                break;

            case CustomerState.ExitShop:
                // Cleanup table seat nếu có
                if (_assignedTable != null)
                {
                    _assignedTable.FreeSeat(this);
                    _assignedTable = null;
                }
                Destroy(gameObject);
                break;
        }
    }

    private void HandleMovementPathNotFound()
    {
        Debug.LogWarning($"[CustomerFSM - {InstanceId}] PathNotFound in state {CurrentState}. " +
                        "Falling back to Wander.");
        TransitionToState(CustomerState.Wander);
        MoveToRandomWanderPoint();
    }

    private void HandleMovementGoalAbandoned()
    {
        Debug.LogWarning($"[CustomerFSM - {InstanceId}] GoalAbandoned in state {CurrentState}. " +
                        "Triggering Fallback to LeaveShop.");
        TransitionToState(CustomerState.ExitShop);
        MoveToExit();
    }

    // =========================================================================
    // CALLBACKS TỪ CashierQueueManager
    // =========================================================================

    /// <summary>
    /// CashierQueueManager gọi khi vị trí slot thay đổi (người trước rời hàng).
    /// </summary>
    public void UpdateQueuePosition(Vector3 newSlotWorldPos)
    {
        if (CurrentState != CustomerState.WaitingInLine &&
            CurrentState != CustomerState.QueueAtCheckout) return;

        Vector2Int newSlotCell = GridSystem.Instance.WorldToCell(newSlotWorldPos);
        TransitionToState(CustomerState.QueueAtCheckout);
        _movement.RequestPath(newSlotCell);
    }

    /// <summary>
    /// CashierQueueManager gọi khi NPC được phục vụ. Chuyển sang ExitShop.
    /// </summary>
    public void OnServed()
    {
        _queueSlotIndex   = -1;
        _carriedItemId    = string.Empty;
        _carriedItemPrice = 0f;

        if (_verboseLogging)
            Debug.Log($"[CustomerFSM] {InstanceId}: Served! Heading to exit.");

        TransitionToState(CustomerState.ExitShop);
        MoveToExit();
    }

    // =========================================================================
    // UTILITIES
    // =========================================================================

    private void TransitionToState(CustomerState newState)
    {
        if (_verboseLogging)
            Debug.Log($"[CustomerFSM] {InstanceId}: {CurrentState} → {newState}");

        CurrentState = newState;
    }

    private void CheckBoredom()
    {
        if (CurrentState == CustomerState.ExitShop) return;
        if (CurrentState == CustomerState.WaitingInLine) return;

        if (Time.time - _spawnTime > BOREDOM_THRESHOLD)
        {
            if (_verboseLogging)
                Debug.Log($"[CustomerFSM] {InstanceId}: Bored after {BOREDOM_THRESHOLD}s. Leaving.");

            TransitionToState(CustomerState.ExitShop);
            MoveToExit();
        }
    }
}
