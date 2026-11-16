using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public sealed class ControlsGuideUI : MonoBehaviour
{
    RectTransform root, sheet;
    TMP_FontAsset font;
    readonly Dictionary<string, string> labels = new Dictionary<string, string>
    {
        { "Move", "이동" }, { "Attack", "사격" }, { "Aim", "조준" },
        { "Sprint", "달리기" }, { "Jump", "점프" }, { "Interact", "상호작용" },
        { "Reload", "재장전" }, { "Drop", "무기 버리기" }, { "Cover", "엄폐" },
        { "Skill1", "스킬 1" }, { "Skill2", "스킬 2" }, { "GameChanger", "궁극기" },
        { "Next", "다음 무기" }, { "Previous", "이전 무기" }, { "Crouch", "자세 변경" },
        { "Scroll", "스크롤 선택" }, { "Aim Shoulder", "조준 어깨 전환" },
        { "Change", "전환" }, { "Use", "장비 사용" }, { "Traverse", "넘기" },
        { "AccessSwap", "PA 전환 (길게 눌러 선택)" },
        { "SquadRegroup", "분대 집결 (간격 축소)" }, { "SquadSpread", "분대 산개 (간격 확대)" }
    };

    public void Open()
    {
        if (!root) Build();
        root.gameObject.SetActive(true);
        GameplayCursor.SetUIOpen(this, true);
        MenuControllerNavigation.SetPanel(root, true, Close);
    }
    void Close()
    {
        MenuControllerNavigation.SetPanel(root, false);
        GameplayCursor.SetUIOpen(this, false);
        if (root) root.gameObject.SetActive(false);
    }
    void Update()
    {
        if (!root || !root.gameObject.activeSelf) return;
        sheet.localScale = Vector3.one * Mathf.Min(root.rect.width / 1920f, root.rect.height / 1080f);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }
    void Build()
    {
        var source = Resources.Load<Font>("CharacterProfiles/ProfileFont");
        if (!source) source = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        font = TMP_FontAsset.CreateFontAsset(source);
        root = new GameObject("Controls guide", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        root.gameObject.AddComponent<Image>().color = new Color(.94f, .96f, .99f);
        sheet = Rect(root, 0, 0, 1920, 1080);
        sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(.5f, .5f);
        Text("조작키 설명", 90, 55, 1400, 70, 42);
        Text("키보드 · 마우스  /  입력 설정 기준", 94, 135, 1500, 42, 23);
        var close = Rect(sheet, 1620, 65, 210, 64);
        close.gameObject.AddComponent<Image>().color = new Color(.8f, .86f, 1f);
        close.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        Text("닫기  ESC", 1620, 76, 210, 40, 23);
        var asset = Resources.Load<InputActionAsset>("Controls/PlayerControls");
        var map = asset != null ? asset.FindActionMap("Player", false) : null;
        int index = 0;
        if (map != null)
            foreach (var action in map.actions)
            {
                var keys = new List<string>();
                foreach (var binding in action.bindings)
                {
                    string path = binding.effectivePath;
                    if (binding.isComposite || string.IsNullOrEmpty(path) ||
                        (!path.StartsWith("<Keyboard>") && !path.StartsWith("<Mouse>"))) continue;
                    var key = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                    if (!keys.Contains(key)) keys.Add(key);
                }
                if (keys.Count == 0) continue;
                int column = index / 14, row = index % 14;
                float x = 94 + column * 900, y = 224 + row * 48;
                Text(labels.TryGetValue(action.name, out var label) ? label : action.name, x, y, 390, 48, 24);
                string display = action.name == "Move" ? "WASD / 방향키" : string.Join(" / ", keys);
                if (action.name == "SquadRegroup") display = "[ / Alt + 휠 아래";
                if (action.name == "SquadSpread") display = "] / Alt + 휠 위";
                Text(display, x + 390, y, 460, 48, 21);
                index++;
            }
        Text("집결: 교전 중에도 주변으로 복귀 · 산개: 평상시만 · PA 선택 중 휠은 PA 선택", 94, 910, 1720, 45, 23);
        Text("스킬 연습장: ESC 일시정지 · 메뉴 복귀   |   F6 테스트 안내 표시/숨기기", 94, 970, 1720, 50, 24);
    }
    RectTransform Rect(Transform parent, float x, float y, float w, float h)
    {
        var r = new GameObject("Guide element", typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
    }
    void Text(string value, float x, float y, float w, float h, int size)
    {
        var t = Rect(sheet, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = value; t.fontSize = size; t.color = new Color(.1f, .15f, .26f);
        t.raycastTarget = false; t.enableWordWrapping = false;
    }
    void OnDisable() { Close(); }
    void OnDestroy()
    {
        if (root) Destroy(root.gameObject);
        if (font)
        {
            foreach (var texture in font.atlasTextures) if (texture) Destroy(texture);
            if (font.material) Destroy(font.material);
            Destroy(font);
        }
    }
}
