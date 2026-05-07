// Assets/Scripts/Shop/PlayTableInstance.cs

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component gắn trên PlayTable prefab GameObject.
/// Quản lý trạng thái ghế ngồi và match timer.
///
/// SEAT STRUCTURE:
///   Table có N ghế (default = 2)
///   Mỗi ghế có:
///     - worldOffset (Vector3): Offset từ table center (local)
///     - facingRotation (Quaternion): Hướng nhìn khi ngồi
///     - occupant (CustomerFSM hoặc null): ai đang ngồi
///
/// ISOMETRIC NOTE:
///   Seat positions được tính từ table.transform.position + offset
///   Offset phụ thuộc vào footprint và rotation của bàn
///   Dùng GridSystem.CellToWorld() cho alignment chính xác
/// </summary>
[RequireComponent(typeof(PlacedFurnitureInstance))]
public class PlayTableInstance : MonoBehaviour
{
    // ========================================================================
    // CONFIGURATION
    // ========================================================================

    [Header("Seating")]
    [Tooltip("Số ghế ngồi tối đa (2 cho bàn đôi).")]
    [SerializeField] private int _seatCount = 2;

    [Header("Visual Effects")]
    [Tooltip("Prefab particle cho hiệu ứng bài (card flip/sparkle). Để null nếu chưa có.")]
    [SerializeField] private GameObject _cardParticlePrefab;

    [Header("Match Settings")]
    [Tooltip("Thời gian match (giây). 12 giây = 12000ms trong hệ thống cũ.")]
    [SerializeField] private float _matchDuration = 12f;

    // ========================================================================
    // SEAT DATA
    // ========================================================================

    [Serializable]
    public class Seat
    {
        public Vector3 worldOffset;
        public Quaternion facingRotation;
        [NonSerialized] public CustomerFSM occupant;
    }

    [Header("Runtime State")]
    [SerializeField] private List<Seat> _seats = new List<Seat>();

    // ========================================================================
    // MATCH STATE
    // ========================================================================

    private float _matchStartedAt = 0f;
    private bool _isMatchActive = false;
    private bool _matchFinished = false;
    private float _lastCardEffectTime = -1f;

    // ========================================================================
    // EVENTS
    // ========================================================================

    public event Action<PlayTableInstance> OnSeatTaken;
    public event Action<PlayTableInstance> OnSeatFreed;
    public event Action<PlayTableInstance> OnMatchStarted;
    public event Action<PlayTableInstance> OnMatchFinished;

    // ========================================================================
    // PROPERTIES
    // ========================================================================

    public int SeatCount => _seatCount;
    public bool IsMatchActive => _isMatchActive;
    public bool IsFullyOccupied => GetEmptySeatIndex() < 0;

    public int OccupiedSeatCount
    {
        get
        {
            int count = 0;
            foreach (var seat in _seats)
                if (seat.occupant != null) count++;
            return count;
        }
    }

    public float MatchProgress =>
        _isMatchActive ? Mathf.Clamp01((Time.time - _matchStartedAt) / _matchDuration) : 0f;

    // ========================================================================
    // VÒNG ĐỜI
    // ========================================================================

    private void Awake()
    {
        InitializeSeats();
    }

    private void Start()
    {
        if (PlayTableManager.Instance != null)
            PlayTableManager.Instance.RegisterTable(this);
    }

    private void OnDestroy()
    {
        if (PlayTableManager.Instance != null)
            PlayTableManager.Instance.UnregisterTable(this);
    }

    private void Update()
    {
        if (_isMatchActive && !_matchFinished)
        {
            if (Time.time - _matchStartedAt >= _matchDuration)
            {
                FinishMatch();
            }
            else
            {
                UpdateVisuals();
            }
        }
    }

    // ========================================================================
    // SEAT INITIALIZATION
    // ========================================================================

    private void InitializeSeats()
    {
        _seats.Clear();

        if (GridSystem.Instance == null)
        {
            Debug.LogError("[PlayTableInstance] GridSystem.Instance is null! Cannot initialize seats.");
            return;
        }

        var placedFurniture = GetComponent<PlacedFurnitureInstance>();
        int rotation = placedFurniture != null ? placedFurniture.PlacedRotation : 0;

        float cellSize = GridSystem.Instance.IsometricGrid.cellSize.y;
        float tableWidth = 1f;
        float tableDepth = 1f;

        if (placedFurniture?.Definition != null)
        {
            tableWidth = placedFurniture.Definition.footprintWidth * cellSize;
            tableDepth = placedFurniture.Definition.footprintHeight * cellSize;
        }

        for (int i = 0; i < _seatCount; i++)
        {
            Vector3 offset;
            Quaternion facing;

            if (_seatCount == 2)
            {
                if (rotation == 0 || rotation == 180)
                {
                    // Bàn đặt ngang: ghế trên/dưới (Y-axis)
                    offset = (i == 0)
                        ? new Vector3(0, tableDepth * 0.5f, 0)
                        : new Vector3(0, -tableDepth * 0.5f, 0);

                    facing = (i == 0)
                        ? Quaternion.Euler(0, 0, 180f)
                        : Quaternion.identity;
                }
                else
                {
                    // Bàn đặt dọc: ghế trái/phải (X-axis)
                    offset = (i == 0)
                        ? new Vector3(tableWidth * 0.5f, 0, 0)
                        : new Vector3(-tableWidth * 0.5f, 0, 0);

                    facing = (i == 0)
                        ? Quaternion.Euler(0, 0, -90f)
                        : Quaternion.Euler(0, 0, 90f);
                }
            }
            else
            {
                float angle = (360f / _seatCount) * i;
                offset = Quaternion.Euler(0, 0, angle) * Vector3.right * (tableWidth * 0.5f);
                facing = Quaternion.Euler(0, 0, angle + 180f);
            }

            _seats.Add(new Seat
            {
                worldOffset = offset,
                facingRotation = facing,
                occupant = null
            });
        }
    }

    // ========================================================================
    // SEAT ASSIGNMENT
    // ========================================================================

    /// <summary>
    /// Tìm ghế trống đầu tiên. Trả về -1 nếu bàn đầy.
    /// </summary>
    public int GetEmptySeatIndex()
    {
        for (int i = 0; i < _seats.Count; i++)
        {
            if (_seats[i].occupant == null) return i;
        }
        return -1;
    }

    /// <summary>
    /// Thử assign NPC vào bàn.
    /// Trả về true nếu thành công, false nếu bàn đầy.
    /// </summary>
    public bool TryAssignSeat(CustomerFSM npc, out int seatIndex)
    {
        seatIndex = GetEmptySeatIndex();
        if (seatIndex < 0) return false;

        _seats[seatIndex].occupant = npc;
        OnSeatTaken?.Invoke(this);

        if (IsFullyOccupied && !_isMatchActive)
        {
            StartMatch();
        }

        return true;
    }

    /// <summary>
    /// Giải phóng ghế khi NPC rời đi.
    /// </summary>
    public void FreeSeat(CustomerFSM npc)
    {
        foreach (var seat in _seats)
        {
            if (seat.occupant == npc)
            {
                seat.occupant = null;
                OnSeatFreed?.Invoke(this);

                if (_isMatchActive)
                {
                    AbortMatch();
                }
                return;
            }
        }
    }

    /// <summary>
    /// Lấy world position của ghế (để NPC di chuyển đến).
    /// </summary>
    public Vector3 GetSeatWorldPosition(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= _seats.Count)
        {
            Debug.LogWarning($"[PlayTableInstance] Invalid seatIndex: {seatIndex}");
            return transform.position;
        }
        return transform.position + _seats[seatIndex].worldOffset;
    }

    /// <summary>
    /// Lấy rotation hướng nhìn khi ngồi.
    /// </summary>
    public Quaternion GetSeatFacingRotation(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= _seats.Count)
            return Quaternion.identity;
        return _seats[seatIndex].facingRotation;
    }

    // ========================================================================
    // MATCH LIFECYCLE
    // ========================================================================

    private void StartMatch()
    {
        _matchStartedAt = Time.time;
        _isMatchActive = true;
        _matchFinished = false;
        _lastCardEffectTime = Time.time;

        Debug.Log($"[PlayTableInstance] Match started. Duration: {_matchDuration}s.");
        OnMatchStarted?.Invoke(this);
    }

    private void FinishMatch()
    {
        if (_matchFinished) return;
        _matchFinished = true;
        _isMatchActive = false;

        // Chỉ award XP 1 lần — cho NPC ở seat 0
        CustomerFSM seat0Npc = _seats[0].occupant;
        if (seat0Npc != null)
        {
            GameEconomyEvents.FireMatchFinished(50, "MATCH_FINISHED");
            Debug.Log("[PlayTableInstance] Match finished! Awarded 50 XP.");
        }

        OnMatchFinished?.Invoke(this);

        // Notify cả 2 occupant để chuyển sang ExitShop
        foreach (var seat in _seats)
        {
            seat.occupant?.OnMatchFinished();
        }
    }

    private void AbortMatch()
    {
        _isMatchActive = false;
        _matchFinished = false;
        _matchStartedAt = 0f;

        Debug.Log("[PlayTableInstance] Match aborted (player left early).");
    }

    // ========================================================================
    // VISUAL EFFECTS
    // ========================================================================

    /// <summary>
    /// Update visual effects (card particle spawning) each frame.
    /// Called by CustomerFSM.HandlePlaying() while match is active.
    /// </summary>
    public void UpdateVisuals()
    {
        if (_cardParticlePrefab == null) return;

        float elapsed = Time.time - _matchStartedAt;
        float lastSecond = Mathf.Floor(elapsed);
        float currentSecond = Mathf.Floor(elapsed + Time.deltaTime);

        if ((int)lastSecond < (int)currentSecond && Time.time - _lastCardEffectTime >= 1f)
        {
            SpawnCardEffect();
            _lastCardEffectTime = Time.time;
        }
    }

    private void SpawnCardEffect()
    {
        if (_cardParticlePrefab == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        var effect = UnityEngine.Object.Instantiate(_cardParticlePrefab, spawnPos, Quaternion.identity);
        effect.transform.SetParent(transform);
    }

    // ========================================================================
    // PERSISTENCE
    // ========================================================================

    /// <summary>
    /// Lấy dữ liệu bàn để serialize (cho GameData).
    /// </summary>
    public PlacedTableData GetSerializableData()
    {
        var occupants = new List<string>();
        foreach (var seat in _seats)
        {
            occupants.Add(seat.occupant?.InstanceId ?? string.Empty);
        }

        return new PlacedTableData
        {
            instanceId = GetComponent<PlacedFurnitureInstance>()?.InstanceId ?? string.Empty,
            seatCount = _seatCount,
            occupantIds = occupants,
            isMatchActive = _isMatchActive,
            matchStartedAt = _matchStartedAt,
            matchDuration = _matchDuration
        };
    }

    [Serializable]
    public class PlacedTableData
    {
        public string instanceId;
        public int seatCount;
        public List<string> occupantIds;
        public bool isMatchActive;
        public float matchStartedAt;
        public float matchDuration;
    }
}
