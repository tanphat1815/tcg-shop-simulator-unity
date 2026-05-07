// Assets/Scripts/UI/ResponsiveLayoutValidator.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor window for testing UI responsiveness across multiple resolutions
/// without building the project.
///
/// Usage:
///   Menu: Window > TCGShop > Responsive Layout Validator
/// </summary>
public class ResponsiveLayoutValidator : EditorWindow
{
    // ─────────────────────────────────────────────────────────────────
    // Preset Resolutions
    // ─────────────────────────────────────────────────────────────────
    private class ResolutionPreset
    {
        public string Name;
        public int Width;
        public int Height;
        public string Category;
        public ResolutionPreset(string name, int w, int h, string cat)
        {
            Name = name; Width = w; Height = h; Category = cat;
        }
        public override string ToString() => $"{Name} ({Width}x{Height})";
    }

    private static readonly ResolutionPreset[] PRESETS = new[]
    {
        // PC
        new ResolutionPreset("PC 1080p",    1920, 1080, "PC"),
        new ResolutionPreset("PC 1440p",    2560, 1440, "PC"),
        new ResolutionPreset("PC 4K",       3840, 2160, "PC"),
        // Ultrawide
        new ResolutionPreset("UW 2560x1080", 2560, 1080, "Ultrawide"),
        new ResolutionPreset("UW 3440x1440", 3440, 1440, "Ultrawide"),
        // Laptop
        new ResolutionPreset("Laptop 1366x768", 1366, 768, "Laptop"),
        new ResolutionPreset("Laptop 1600x900",  1600, 900, "Laptop"),
        // iPhone Portrait (most critical for mobile)
        new ResolutionPreset("iPhone 14 Pro",  1179, 2556, "iPhone"),
        new ResolutionPreset("iPhone 14",      1170, 2532, "iPhone"),
        new ResolutionPreset("iPhone SE",       750, 1334, "iPhone"),
        // iPhone Landscape
        new ResolutionPreset("iPhone 14 Pro Land.", 2556, 1179, "iPhone (Land.)"),
        new ResolutionPreset("iPhone SE Land.",      1334,  750, "iPhone (Land.)"),
        // Android
        new ResolutionPreset("Pixel 6",     1080, 2340, "Android"),
        new ResolutionPreset("Samsung S21", 1440, 3200, "Android"),
        new ResolutionPreset("Xiaomi 12",   1080, 2400, "Android"),
        // iPad
        new ResolutionPreset("iPad Pro 11\"",  1668, 2388, "iPad"),
        new ResolutionPreset("iPad Pro 12.9\"", 2048, 2732, "iPad"),
        new ResolutionPreset("iPad Mini",      1333, 2000, "iPad"),
    };

    // ─────────────────────────────────────────────────────────────────
    // GUI State
    // ─────────────────────────────────────────────────────────────────
    private Vector2 _scrollPos;
    private string _currentCategory = "All";

    // ─────────────────────────────────────────────────────────────────
    // Menu Entry
    // ─────────────────────────────────────────────────────────────────
    [MenuItem("Window/TCGShop/Responsive Layout Validator")]
    public static void ShowWindow()
    {
        var window = GetWindow<ResponsiveLayoutValidator>("Responsive Validator");
        window.minSize = new Vector2(300, 400);
        window.Show();
    }

    // ─────────────────────────────────────────────────────────────────
    // On Enable
    // ─────────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    // ─────────────────────────────────────────────────────────────────
    // GUI
    // ─────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        EditorGUILayout.Space(8);
        DrawCategoryTabs();
        EditorGUILayout.Space(8);
        DrawResolutionList();

        EditorGUILayout.EndScrollView();

        DrawFooter();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Responsive Layout Validator", titleStyle);

        EditorGUILayout.Space(4);
        GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField(
            "Test UI responsive across multiple resolutions.\nClick a preset to switch Game View.",
            subStyle);

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Current Game View:", GUILayout.Width(130));
        EditorGUILayout.LabelField($"{Screen.width}x{Screen.height}", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawCategoryTabs()
    {
        string[] categories = new[]
        {
            "All", "PC", "Ultrawide", "Laptop", "iPhone", "iPhone (Land.)", "Android", "iPad"
        };

        EditorGUILayout.BeginHorizontal();
        foreach (var cat in categories)
        {
            bool selected = _currentCategory == cat;
            GUIStyle btnStyle = selected
                ? new GUIStyle(EditorStyles.miniButtonSelected)
                : EditorStyles.miniButton;

            if (GUILayout.Button(cat, btnStyle, GUILayout.Height(28)))
            {
                _currentCategory = cat;
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawResolutionList()
    {
        string[] filteredCategories = _currentCategory == "All"
            ? new[] { "PC", "Ultrawide", "Laptop", "iPhone", "iPhone (Land.)", "Android", "iPad" }
            : new[] { _currentCategory };

        string currentCategory = "";
        foreach (var preset in PRESETS)
        {
            if (preset.Category != filteredCategories[0]) continue;

            if (preset.Category != currentCategory)
            {
                currentCategory = preset.Category;
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField($"-- {currentCategory} --", EditorStyles.boldLabel);
            }

            DrawResolutionButton(preset);
        }
    }

    private void DrawResolutionButton(ResolutionPreset preset)
    {
        bool isCurrent = Screen.width == preset.Width && Screen.height == preset.Height;

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(preset.Name, GUILayout.Width(150));
        EditorGUILayout.LabelField($"{preset.Width}x{preset.Height}", EditorStyles.miniLabel);

        float ratio = (float)preset.Width / preset.Height;
        string ratioStr = ratio > 1.78f ? "Ultrawide" :
                         ratio < 0.6f ? "Portrait" :
                         ratio > 0.9f && ratio < 1.1f ? "1:1" : $"{ratio:F2}:1";
        EditorGUILayout.LabelField(ratioStr, EditorStyles.miniLabel, GUILayout.Width(75));

        GUIStyle switchStyle = isCurrent
            ? new GUIStyle(EditorStyles.miniButtonSelected)
            : EditorStyles.miniButton;

        EditorGUI.BeginDisabledGroup(isCurrent);
        if (GUILayout.Button(isCurrent ? "Active" : "Apply", switchStyle, GUILayout.Width(60)))
        {
            SwitchToResolution(preset);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawFooter()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Tips:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("- Portrait resolutions are critical for mobile testing.", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("- Check SafeAreaFitter on Canvas root for notch handling.", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("- Button hitboxes must be >= 100x60 Canvas units.", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("- All TextMeshPro must have fixed font size (no Best Fit).", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    // ─────────────────────────────────────────────────────────────────
    // Resolution Switching
    // ─────────────────────────────────────────────────────────────────
    private void SwitchToResolution(ResolutionPreset preset)
    {
        bool success = SetGameViewResolution(preset.Width, preset.Height);
        if (!success)
            ShowManualInstructions(preset);

        Repaint();
    }

    private bool SetGameViewResolution(int width, int height)
    {
        try
        {
            var gameViewType = System.Type.GetType("UnityEditor.GameView, UnityEditor");
            if (gameViewType == null) return false;

            var gameViewWindow = EditorWindow.GetWindow(gameViewType);
            if (gameViewWindow == null) return false;

            var addMethod = gameViewType.GetMethod("AddCustomResolution");
            if (addMethod != null)
            {
                addMethod.Invoke(gameViewWindow, new object[] { false, (System.Enum)0, width, height });
                Debug.Log($"[ResponsiveValidator] Switched to {width}x{height}");
                return true;
            }
            return false;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ResponsiveValidator] Could not auto-switch: {ex.Message}");
            return false;
        }
    }

    private void ShowManualInstructions(ResolutionPreset preset)
    {
        EditorUtility.DisplayDialog(
            "Manual Resolution Switch",
            $"To test resolution {preset.Width}x{preset.Height}:\n\n" +
            "1. Click the Game tab (or press Ctrl+Shift+W)\n" +
            "2. Click the '+' dropdown in Game View\n" +
            "3. Select 'Add Fixed Resolution'\n" +
            "4. Enter Width=" + preset.Width + ", Height=" + preset.Height + "\n" +
            "5. Select the new size from the dropdown\n\n" +
            "Or install the 'Game View Plus' asset for faster switching.",
            "OK");
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        Repaint();
    }
}
#endif
