// Assets/Scripts/Core/TimeManager.cs

using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton quản lý thời gian game và day lifecycle.
///
/// TIME CONVENTIONS:
///   timeInMinutes = 480 → 8:00 AM  (shop opens)
///   timeInMinutes = 1200 → 8:00 PM (closing time, hard cap)
///
/// TIME SCALE:
///   1 real second = 1 game minute
///   Full day (8:00 → 20:00) = 720 real seconds = 12 minutes
///
/// RESPONSIBILITIES:
///   1. Advance game time mỗi real second
///   2. Fire OnHourChanged, OnDayChanged events
///   3. Trigger End of Day process khi time >= 1200
///   4. Notify CustomerSpawner để dừng spawn khi closing time
/// </summary>
public class TimeManager : MonoBehaviour
{
    // =========================================================================
    // SINGLETON
    // =========================================================================

    public static TimeManager Instance { get; private set; }

    // =========================================================================
    // CONSTANTS
    // =========================================================================

    public const int START_MINUTE     = 480;    // 8:00 AM
    public const int END_MINUTE       = 1200;   // 8:00 PM
    public const int MINUTES_PER_DAY  = END_MINUTE - START_MINUTE; // 720 minutes

    // =========================================================================
    // STATE
    // =========================================================================

    [Header("Game Time State")]
    [SerializeField] private int _currentDay     = 1;
    [SerializeField] private int _timeInMinutes = START_MINUTE;

    [Header("Time Control")]
    [Tooltip("Bật/tắt time advancement. Tắt khi paused, build mode, etc.")]
    [SerializeField] private bool _isPaused = false;

    [Tooltip("Shop đang mở hay đóng. Đóng khi đang xử lý End of Day.")]
    [SerializeField] private bool _isShopOpen = true;

    [Header("Speed Multiplier")]
    [Tooltip("Speed multiplier cho test. 1.0 = normal (1 real sec = 1 game min).")]
    [SerializeField] private float _timeSpeedMultiplier = 1f;

    // =========================================================================
    // PROPERTIES
    // =========================================================================

    public int CurrentDay     => _currentDay;
    public int TimeInMinutes  => _timeInMinutes;
    public bool IsShopOpen    => _isShopOpen && !_isClosing;
    public bool IsClosingTime => _timeInMinutes >= END_MINUTE;
    public bool IsPaused      => _isPaused;

    /// <summary>Format: "HH:MM AM/PM"</summary>
    public string FormattedTime
    {
        get
        {
            int totalHours   = _timeInMinutes / 60;
            int minutes      = _timeInMinutes % 60;
            bool isPM        = totalHours >= 12;
            int displayHour  = totalHours % 12;
            if (displayHour == 0) displayHour = 12;
            return $"{displayHour}:{minutes:D2} {(isPM ? "PM" : "AM")}";
        }
    }

    /// <summary>Phần trăm ngày đã trôi qua (0.0 → 1.0).</summary>
    public float DayProgress => MINUTES_PER_DAY > 0
        ? Mathf.Clamp01((float)(_timeInMinutes - START_MINUTE) / MINUTES_PER_DAY)
        : 0f;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// <summary>Khi một giờ game trôi qua (8:00 → 9:00, v.v.).</summary>
    public event Action<int> OnHourChanged;

    /// <summary>Khi một ngày mới bắt đầu.</summary>
    public event Action<int> OnDayChanged;

    /// <summary>Khi shop đóng cửa (time >= 1200).</summary>
    public event Action OnClosingTime;

    /// <summary>Khi time bị pause/resume.</summary>
    public event Action<bool> OnPausedChanged;

    // =========================================================================
    // INTERNAL STATE
    // =========================================================================

    private Coroutine _timeCoroutine;
    private int _lastHour = -1;
    private bool _isClosing = false;

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
        DontDestroyOnLoad(gameObject);

        _lastHour = _timeInMinutes / 60;
    }

    private void Start()
    {
        StartTimeAdvancement();
    }

    private void OnDestroy()
    {
        StopTimeAdvancement();
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>Bắt đầu đồng hồ (gọi khi game start hoặc resume).</summary>
    public void StartTimeAdvancement()
    {
        if (_timeCoroutine != null) return;
        _timeCoroutine = StartCoroutine(TimeAdvanceCoroutine());
    }

    /// <summary>Dừng đồng hồ (gọi khi pause hoặc shop đóng).</summary>
    public void StopTimeAdvancement()
    {
        if (_timeCoroutine != null)
        {
            StopCoroutine(_timeCoroutine);
            _timeCoroutine = null;
        }
    }

    /// <summary>Pause/resume time advancement.</summary>
    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        OnPausedChanged?.Invoke(_isPaused);
    }

    /// <summary>Skip đến một thời điểm (debug/test).</summary>
    public void SetTime(int minutes)
    {
        _timeInMinutes = Mathf.Clamp(minutes, START_MINUTE, END_MINUTE);
        _lastHour = _timeInMinutes / 60;
    }

    /// <summary>Start ngày mới (gọi bởi End of Day process).</summary>
    public void StartNewDay(int dayNumber)
    {
        _currentDay     = dayNumber;
        _timeInMinutes  = START_MINUTE;
        _lastHour       = START_MINUTE / 60;
        _isClosing      = false;
        _isShopOpen     = true;

        StartTimeAdvancement();
        OnDayChanged?.Invoke(_currentDay);
        Debug.Log($"[TimeManager] Day {_currentDay} started at 8:00 AM.");
    }

    /// <summary>
    /// Gọi từ EndOfDayPanel khi player nhấn "Next Day".
    /// </summary>
    public void TransitionToNextDay(float currentMoney)
    {
        StopTimeAdvancement();
        _currentDay++;
        _timeInMinutes = START_MINUTE;
        _lastHour      = START_MINUTE / 60;
        _isClosing     = false;
        _isShopOpen    = true;

        if (ShopFloorManager.Instance?.CashierQueue != null)
            ShopFloorManager.Instance.CashierQueue.ClearQueue();

        if (CustomerSpawner.Instance != null)
            CustomerSpawner.Instance.SetShopOpen(true);

        StartTimeAdvancement();
        OnDayChanged?.Invoke(_currentDay);

        Debug.Log($"[TimeManager] Transitioned to Day {_currentDay}. Money: ${currentMoney:F2}");
    }

    // =========================================================================
    // TIME ADVANCE COROUTINE
    // =========================================================================

    private IEnumerator TimeAdvanceCoroutine()
    {
        while (true)
        {
            float waitSeconds = 1f / Mathf.Max(_timeSpeedMultiplier, 0.1f);
            yield return new WaitForSeconds(waitSeconds);

            if (_isPaused) continue;

            AdvanceTime();
        }
    }

    private void AdvanceTime()
    {
        if (_timeInMinutes >= END_MINUTE)
        {
            if (!_isClosing)
            {
                TriggerClosingTime();
            }
            return;
        }

        _timeInMinutes++;

        int currentHour = _timeInMinutes / 60;
        if (currentHour != _lastHour)
        {
            _lastHour = currentHour;
            OnHourChanged?.Invoke(currentHour);
        }
    }

    private void TriggerClosingTime()
    {
        if (_isClosing) return;
        _isClosing  = true;
        _isShopOpen = false;

        Debug.Log("[TimeManager] Closing time reached (8:00 PM).");

        OnClosingTime?.Invoke();
        StartCoroutine(ProcessEndOfDay());
    }

    private IEnumerator ProcessEndOfDay()
    {
        yield return new WaitForSeconds(1f);

        NotifyCustomersClosingTime();

        yield return new WaitForSeconds(2f);

        ProcessEndOfDayCalculations();
    }

    private void NotifyCustomersClosingTime()
    {
        var customers = UnityEngine.Object.FindObjectsByType<CustomerFSM>(FindObjectsSortMode.None);
        foreach (var customer in customers)
        {
            if (customer != null)
                customer.HandleClosingTime();
        }

        if (CustomerSpawner.Instance != null)
            CustomerSpawner.Instance.SetShopOpen(false);
    }

    private void ProcessEndOfDayCalculations()
    {
        float totalSalary = CalculateTotalSalary();

        int expansionLevel = 0;
        if (GameDataManager.Instance?.CurrentData != null)
            expansionLevel = GameDataManager.Instance.CurrentData.expansionLevel;

        float rent = CalculateDailyRent(expansionLevel);

        float prevMoney = ShopFloorManager.Instance?.TotalMoney ?? 0f;
        float newMoney  = prevMoney - totalSalary - rent;
        if (newMoney < 0) newMoney = 0;

        if (ShopFloorManager.Instance != null)
            ShopFloorManager.Instance.SetMoney(newMoney);

        ShopFloorManager.Instance?.ResetDailyStats();

        float dailyRevenue      = ShopFloorManager.Instance?.DailyRevenue ?? 0f;
        int customersServed    = ShopFloorManager.Instance?.CustomersServedToday ?? 0;

        if (EndOfDayPanel.Instance != null)
        {
            EndOfDayPanel.Instance.Show(
                _currentDay,
                dailyRevenue,
                totalSalary,
                rent,
                prevMoney,
                newMoney
            );
        }
        else
        {
            TransitionToNextDay(newMoney);
        }
    }

    private float CalculateTotalSalary()
    {
        return WorkerManager.Instance?.TotalDailySalary ?? 0f;
    }

    private float CalculateDailyRent(int expansionLevel)
    {
        if (ExpansionManager.Instance != null)
            return ExpansionManager.Instance.CalculateDailyRent();

        float baseRent = 50f;
        float rentIncrease = 20f;
        return baseRent + (expansionLevel * rentIncrease);
    }
}
