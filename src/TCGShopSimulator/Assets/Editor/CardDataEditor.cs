// Assets/Editor/CardDataEditor.cs

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector cho CardData ScriptableObject.
/// Thêm các tính năng tiện ích:
///   - Preview thẻ trực quan trong Inspector
///   - Nút validate dữ liệu
///   - Hiển thị computed properties (IsHighRarity, XpReward)
///   - Nút tạo nhanh card mới từ template
/// </summary>
[CustomEditor(typeof(CardData))]
public class CardDataEditor : Editor
{
    private CardData _target;
    private bool _showBattleStats = true;
    private bool _showEconomy = true;
    private bool _showComputedProps = true;

    private void OnEnable()
    {
        _target = (CardData)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawCardPreviewHeader();
        EditorGUILayout.Space(10);

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        DrawComputedProperties();

        EditorGUILayout.Space(10);
        DrawActionButtons();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCardPreviewHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField(
            string.IsNullOrEmpty(_target.cardName) ? "(Chưa đặt tên)" : _target.cardName,
            titleStyle,
            GUILayout.Height(30)
        );

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(30));
        EditorGUILayout.LabelField(
            string.IsNullOrEmpty(_target.cardId) ? "(chưa có ID)" : _target.cardId,
            EditorStyles.miniLabel
        );
        EditorGUILayout.EndHorizontal();

        if (_target.rarity != null)
        {
            Color oldColor = GUI.color;
            GUI.color = _target.rarity.rarityColor;
            EditorGUILayout.LabelField(
                $"★ {_target.rarity.displayName}",
                EditorStyles.boldLabel
            );
            GUI.color = oldColor;
        }
        else
        {
            EditorGUILayout.LabelField("⚠ Chưa gán Rarity!", EditorStyles.miniLabel);
        }

        if (_target.cardSprite != null)
        {
            Rect spriteRect = GUILayoutUtility.GetRect(100, 140, GUILayout.ExpandWidth(false));
            spriteRect.x = (EditorGUIUtility.currentViewWidth - 100) / 2;
            GUI.DrawTextureWithTexCoords(
                spriteRect,
                _target.cardSprite.texture,
                new Rect(0, 0, 1, 1)
            );
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawComputedProperties()
    {
        _showComputedProps = EditorGUILayout.Foldout(_showComputedProps,
            "Computed Properties (Read Only)", true);

        if (!_showComputedProps) return;

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.Toggle("Is High Rarity", _target.IsHighRarity);
        EditorGUILayout.IntField("Rarity Rank", _target.RarityRank);
        EditorGUILayout.IntField("XP Reward", _target.XpReward);
        EditorGUILayout.Toggle("Is Valid", _target.IsValid());

        EditorGUILayout.EndVertical();
        EditorGUI.EndDisabledGroup();
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Validate Data", GUILayout.Height(30)))
        {
            if (_target.IsValid())
            {
                EditorUtility.DisplayDialog("Validation PASS",
                    $"CardData '{_target.cardName}' hợp lệ!\n\n" +
                    $"ID: {_target.cardId}\n" +
                    $"Rarity: {_target.rarity?.displayName}\n" +
                    $"HP: {_target.baseHp}\n" +
                    $"Market Value: ${_target.marketValue:F2}",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Validation FAIL",
                    "CardData không hợp lệ! Kiểm tra:\n" +
                    $"• cardId: {(string.IsNullOrEmpty(_target.cardId) ? "TRỐNG" : "OK")}\n" +
                    $"• cardName: {(string.IsNullOrEmpty(_target.cardName) ? "TRỐNG" : "OK")}\n" +
                    $"• rarity: {(_target.rarity == null ? "CHƯA GÁN" : "OK")}",
                    "OK");
            }
        }

        if (GUILayout.Button("Copy ID", GUILayout.Height(30)))
        {
            EditorGUIUtility.systemCopyBuffer = _target.cardId;
            Debug.Log($"[CardDataEditor] Copied to clipboard: '{_target.cardId}'");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        if (GUILayout.Button("+ Tạo Card Mới Từ Template Này", GUILayout.Height(25)))
        {
            CreateCardFromTemplate();
        }
    }

    private void CreateCardFromTemplate()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Tạo Card Mới",
            $"Card_New_{System.DateTime.Now:yyyyMMdd_HHmmss}",
            "asset",
            "Chọn vị trí lưu card mới",
            "Assets/ScriptableObjects/Cards"
        );

        if (string.IsNullOrEmpty(path)) return;

        var newCard = CreateInstance<CardData>();
        newCard.rarity = _target.rarity;
        if (_target.cardTypes != null)
            newCard.cardTypes = (EnergyType[])_target.cardTypes.Clone();
        newCard.retreatCost = _target.retreatCost;
        newCard.baseHp = _target.baseHp;
        newCard.setId = _target.setId;
        newCard.seriesId = _target.seriesId;

        AssetDatabase.CreateAsset(newCard, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = newCard;

        Debug.Log($"[CardDataEditor] Đã tạo card mới từ template tại: {path}");
    }
}
#endif
