// Assets/Scripts/Character/CharacterMovement.cs

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý di chuyển của một Character (NPC hoặc Player) theo A* path.
///
/// LUỒNG DI CHUYỂN:
///   1. RequestPath(goalCell) → PathfindingCore.FindPath() → lưu _currentPath
///   2. Mỗi frame: MoveAlongPath()
///      - Lấy waypoint hiện tại (cell đầu tiên trong path)
///      - Vector3.MoveTowards đến world position của waypoint
///      - Khi đến waypoint: pop khỏi path, lấy waypoint tiếp theo
///      - FlipX sprite theo hướng di chuyển ngang
///   3. Khi grid thay đổi: StartCoroutine(HandlePathStale())
///      - Dừng 0.1s (STALE_PAUSE_DURATION)
///      - Log "Cập nhật lưới phát sinh"
///      - Recalculate path
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ (NPCManager.ts):
///   physics.moveTo(sprite, targetX, targetY, speed)  → RequestPath() + MoveAlongPath()
///   handleStuckRecovery()                            → HandlePathStale()
///   sprite.anims.play('npc-left')                   → UpdateSpriteDirection() + FlipX
///   customer.state = 'SEEK_ITEM'                    → CustomerAIController states
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterMovement : MonoBehaviour
{
    // =========================================================================
    // CONSTANTS
    // =========================================================================

    /// <summary>
    /// Thời gian dừng khi phát hiện path stale (giây).
    /// 0.1s đủ để grid update propagate và tránh rapid recalculation.
    /// YÊU CẦU: Phải là 0.1s theo spec của Bước 4.
    /// </summary>
    private const float STALE_PAUSE_DURATION = 0.1f;

    /// <summary>
    /// Số lần recalculate tối đa trước khi abandon goal.
    /// Tránh Infinite Loop khi không có đường nào khả thi.
    /// </summary>
    private const int MAX_RECALCULATIONS = 3;

    /// <summary>
    /// Thời gian chờ tối đa khi stuck trước khi abandon goal.
    /// </summary>
    private const float MAX_STUCK_WAIT = 5f;

    /// <summary>
    /// Khoảng cách ngưỡng để coi là "đã đến" waypoint.
    /// </summary>
    private const float WAYPOINT_REACH_THRESHOLD = 0.05f;

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Movement")]
    [Tooltip("Tốc độ di chuyển (world units/giây).")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Sprite Direction")]
    [Tooltip("True nếu sprite mặc định nhìn sang phải (hướng +X).")]
    [SerializeField] private bool defaultFacingRight = true;

    [Header("Debug")]
    [SerializeField] private bool showPathGizmos = true;
    [SerializeField] private bool verboseLogging = false;

    // =========================================================================
    // COMPONENT REFERENCES
    // =========================================================================

    private SpriteRenderer _spriteRenderer;

    // =========================================================================
    // MOVEMENT STATE
    // =========================================================================

    private Queue<Vector2Int> _currentPath;
    private Vector3 _currentWaypointWorldPos;
    private Vector2Int _currentWaypointCell;
    private Vector2Int _goalCell;

    public bool IsMoving { get; private set; }
    public bool HasReachedGoal { get; private set; }

    // =========================================================================
    // PATH STALE STATE
    // =========================================================================

    private Coroutine _staleHandlerCoroutine;
    private bool _isHandlingStale;
    private int _recalculationCount;
    private float _stuckStartTime;

    // =========================================================================
    // DIRECTION
    // =========================================================================

    private Vector2 _movementDirection = Vector2.right;
    private bool _isFacingRight;

    // =========================================================================
    // EVENTS
    // =========================================================================

    public event System.Action OnReachedGoal;
    public event System.Action OnPathNotFound;
    public event System.Action OnGoalAbandoned;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _isFacingRight = defaultFacingRight;
        _currentPath = new Queue<Vector2Int>();
    }

    private void OnEnable()
    {
        if (PathfindingGrid.Instance != null)
            PathfindingGrid.Instance.OnGridChanged += HandleGridChanged;
    }

    private void OnDisable()
    {
        if (PathfindingGrid.Instance != null)
            PathfindingGrid.Instance.OnGridChanged -= HandleGridChanged;
    }

    private void Update()
    {
        if (!IsMoving || _isHandlingStale) return;
        MoveAlongPath();
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    public void RequestPath(Vector2Int goalCell)
    {
        if (GridSystem.Instance == null || PathfindingGrid.Instance == null) return;

        _goalCell = goalCell;
        _recalculationCount = 0;
        HasReachedGoal = false;

        CalculateAndStartPath();
    }

    public void StopMovement()
    {
        IsMoving = false;
        _currentPath.Clear();

        if (_staleHandlerCoroutine != null)
        {
            StopCoroutine(_staleHandlerCoroutine);
            _staleHandlerCoroutine = null;
            _isHandlingStale = false;
        }
    }

    public Vector2Int CurrentCell =>
        GridSystem.Instance != null
            ? GridSystem.Instance.WorldToCell(transform.position)
            : Vector2Int.zero;

    // =========================================================================
    // PATH CALCULATION
    // =========================================================================

    private void CalculateAndStartPath()
    {
        if (PathfindingGrid.Instance == null || GridSystem.Instance == null) return;

        Vector2Int startCell = CurrentCell;

        if (startCell == _goalCell)
        {
            HandleGoalReached();
            return;
        }

        List<Vector2Int> path = PathfindingCore.FindPath(
            startCell,
            _goalCell,
            PathfindingGrid.Instance
        );

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"[CharacterMovement] {gameObject.name}: " +
                             $"Không tìm được đường từ {startCell} đến {_goalCell}.");
            OnPathNotFound?.Invoke();
            IsMoving = false;
            return;
        }

        _currentPath.Clear();
        foreach (var cell in path)
            _currentPath.Enqueue(cell);

        AdvanceToNextWaypoint();
        IsMoving = true;

        if (verboseLogging)
        {
            Debug.Log($"[CharacterMovement] {gameObject.name}: " +
                      $"Path tìm được {path.Count} bước: {startCell} → {_goalCell}.");
        }
    }

    // =========================================================================
    // MOVEMENT
    // =========================================================================

    private void MoveAlongPath()
    {
        if (_currentPath.Count == 0 && !IsMoving) return;

        if (!PathfindingGrid.Instance.IsWalkable(_currentWaypointCell))
        {
            TriggerPathStale();
            return;
        }

        float step = moveSpeed * Time.deltaTime;
        Vector3 newPosition = Vector3.MoveTowards(
            transform.position,
            _currentWaypointWorldPos,
            step
        );

        Vector3 moveDir = newPosition - transform.position;
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            _movementDirection = new Vector2(moveDir.x, moveDir.y).normalized;
            UpdateSpriteDirection(_movementDirection.x);
        }

        transform.position = newPosition;

        float distToWaypoint = Vector3.Distance(transform.position, _currentWaypointWorldPos);

        if (distToWaypoint <= WAYPOINT_REACH_THRESHOLD)
        {
            transform.position = _currentWaypointWorldPos;

            if (_currentPath.Count > 0)
            {
                AdvanceToNextWaypoint();
            }
            else
            {
                HandleGoalReached();
            }
        }
    }

    private void AdvanceToNextWaypoint()
    {
        if (_currentPath.Count == 0) return;

        _currentWaypointCell = _currentPath.Dequeue();
        _currentWaypointWorldPos = GridSystem.Instance.CellToWorld(_currentWaypointCell);
    }

    private void HandleGoalReached()
    {
        IsMoving = false;
        HasReachedGoal = true;
        _currentPath.Clear();
        _recalculationCount = 0;

        if (verboseLogging)
            Debug.Log($"[CharacterMovement] {gameObject.name}: Đã đến đích {_goalCell}.");

        OnReachedGoal?.Invoke();
    }

    // =========================================================================
    // SPRITE DIRECTION
    // =========================================================================

    private void UpdateSpriteDirection(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) < 0.1f) return;

        bool shouldFaceRight = horizontalDirection > 0;

        if (shouldFaceRight != _isFacingRight)
        {
            _isFacingRight = shouldFaceRight;
            _spriteRenderer.flipX = defaultFacingRight ? !_isFacingRight : _isFacingRight;
        }
    }

    // =========================================================================
    // PATH STALE HANDLER
    // =========================================================================

    private void HandleGridChanged(List<Vector2Int> changedCells)
    {
        if (!IsMoving || _isHandlingStale) return;

        bool pathAffected = false;

        foreach (var cell in changedCells)
        {
            if (cell == _currentWaypointCell)
            {
                pathAffected = true;
                break;
            }
        }

        if (!pathAffected)
        {
            foreach (var pathCell in _currentPath)
            {
                foreach (var changedCell in changedCells)
                {
                    if (pathCell == changedCell)
                    {
                        pathAffected = true;
                        break;
                    }
                }
                if (pathAffected) break;
            }
        }

        if (pathAffected)
        {
            TriggerPathStale();
        }
    }

    private void TriggerPathStale()
    {
        if (_isHandlingStale) return;

        if (_staleHandlerCoroutine != null)
            StopCoroutine(_staleHandlerCoroutine);

        _staleHandlerCoroutine = StartCoroutine(HandlePathStaleCoroutine());
    }

    private IEnumerator HandlePathStaleCoroutine()
    {
        _isHandlingStale = true;
        IsMoving = false;
        _stuckStartTime = Time.time;

        yield return new WaitForSeconds(STALE_PAUSE_DURATION);

        Debug.Log($"[CharacterMovement] Cập nhật lưới phát sinh — " +
                  $"{gameObject.name} đang recalculate path đến {_goalCell}. " +
                  $"Lần #{_recalculationCount + 1}/{MAX_RECALCULATIONS}.");

        _recalculationCount++;

        if (_recalculationCount > MAX_RECALCULATIONS)
        {
            Debug.LogWarning($"[CharacterMovement] {gameObject.name}: " +
                             $"Đã recalculate {MAX_RECALCULATIONS} lần, abandon goal {_goalCell}.");
            _isHandlingStale = false;
            IsMoving = false;
            OnGoalAbandoned?.Invoke();
            yield break;
        }

        if (Time.time - _stuckStartTime > MAX_STUCK_WAIT)
        {
            Debug.LogWarning($"[CharacterMovement] {gameObject.name}: " +
                             $"Stuck timeout ({MAX_STUCK_WAIT}s). Abandon goal {_goalCell}.");
            _isHandlingStale = false;
            IsMoving = false;
            OnGoalAbandoned?.Invoke();
            yield break;
        }

        _isHandlingStale = false;
        _currentPath.Clear();
        CalculateAndStartPath();

        if (!IsMoving)
        {
            Debug.LogWarning($"[CharacterMovement] {gameObject.name}: " +
                             "Recalculate thất bại — không tìm được đường sau grid change.");
            OnPathNotFound?.Invoke();
        }
        else if (verboseLogging)
        {
            Debug.Log($"[CharacterMovement] {gameObject.name}: " +
                      "Recalculate thành công — tiếp tục di chuyển theo đường mới.");
        }

        _staleHandlerCoroutine = null;
    }

    // =========================================================================
    // DEBUG GIZMOS
    // =========================================================================

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showPathGizmos || !IsMoving) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(_currentWaypointWorldPos, 0.15f);

        if (GridSystem.Instance == null) return;

        Vector3 prevPos = transform.position;
        Gizmos.color = Color.cyan;

        foreach (var cell in _currentPath)
        {
            Vector3 cellWorld = GridSystem.Instance.CellToWorld(cell);
            Gizmos.DrawLine(prevPos, cellWorld);
            Gizmos.DrawSphere(cellWorld, 0.1f);
            prevPos = cellWorld;
        }
    }
#endif
}
