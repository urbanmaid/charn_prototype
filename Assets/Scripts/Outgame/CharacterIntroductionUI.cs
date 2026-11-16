using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// Builds the reference-style profile without changing the serialized menu scene.
public sealed class CharacterIntroductionUI : MonoBehaviour
{
    [Serializable] public class Catalog { public Profile[] characters; }
    [Serializable] public class Profile
    {
        public string key, name, english, role, position, affiliation, description;
        public string stats, weapon, weaponStats, lore, age, height;
        public Skill[] skills;
    }
    [Serializable] public class Skill { public string title, body; }
    Catalog catalog;
    Font font;
    RectTransform canvasRoot, page, content;
    int selected;
    int videoSlot;
    int view; // overview, detail, lore, artwork
    readonly Color blue = new Color(.22f, .36f, .88f);
    readonly Color ink = new Color(.10f, .15f, .26f);
    readonly Color muted = new Color(.43f, .49f, .60f);
    Profile Current => catalog.characters[selected];

    void Start()
    {
        var data = Resources.Load<TextAsset>("CharacterProfiles/catalog");
        if (!data) { Debug.LogError("Character profile catalog missing.", this); return; }
        catalog = JsonUtility.FromJson<Catalog>(data.text);
        if (catalog == null || catalog.characters == null || catalog.characters.Length == 0) return;
        font = Resources.Load<Font>("CharacterProfiles/ProfileFont");
        if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvasRoot = Rect("Character profiles", transform, 0, 0, 1920, 1080);
        canvasRoot.SetParent(null, false);
        canvasRoot.localScale = Vector3.one;
        var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        canvasRoot.gameObject.AddComponent<GraphicRaycaster>();

        page = Rect("Profile panel", canvasRoot, 0, 0, 1920, 1080);
        page.anchorMin = Vector2.zero; page.anchorMax = Vector2.one;
        page.offsetMin = page.offsetMax = Vector2.zero;
        Fill(page, new Color(.94f, .96f, .99f));
        page.gameObject.SetActive(false);
    }

    public void Open()
    {
        if (!page) return;
        view = 0;
        page.gameObject.SetActive(true);
        Draw();
        GameplayCursor.SetUIOpen(this, true);
        MenuControllerNavigation.SetPanel(page, true, Back);
    }
    void Back()
    {
        if (view != 0) { view = 0; Draw(); return; }
        Close();
    }
    void Close()
    {
        MenuControllerNavigation.SetPanel(page, false);
        page.gameObject.SetActive(false);
        GameplayCursor.SetUIOpen(this, false);
    }
    void OnDisable()
    {
        GameplayCursor.SetUIOpen(this, false);
        if (page) MenuControllerNavigation.SetPanel(page, false);
    }
    void OnDestroy() { if (canvasRoot) Destroy(canvasRoot.gameObject); }
    void Update()
    {
        if (!page || !page.gameObject.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Back();
        else if (Input.GetKeyDown(KeyCode.X)) { view = view == 1 ? 0 : 1; Draw(); }
        else if (Input.GetKeyDown(KeyCode.T)) Train();
    }
    void Draw()
    {
        if (content) { content.gameObject.SetActive(false); Destroy(content.gameObject); }
        content = Rect("Content", page, 0, 0, 1920, 1080);
        content.anchorMin = content.anchorMax = new Vector2(.5f, .5f);
        content.anchoredPosition = new Vector2(-960, 540);
        FitContent();
        var p = Current;
        Label(content, "CHARM  /  LOADOUT", 60, 32, 600, 36, 23, muted);
        Button(content, "닫기  ×", 1680, 24, 180, 54, Close, ink);
        Fill(Rect("Header line", content, 60, 96, 1800, 2), new Color(.8f, .84f, .9f));
        Label(content, "PERSONA ARCHIVE", 60, 128, 470, 26, 18, blue);
        Label(content, p.name, 60, 172, 480, 68, 42, ink);
        Label(content, p.english.ToUpperInvariant(), 64, 246, 450, 35, 21, muted);
        Box(content, p.role, 60, 306, 225, 46, 22, blue);
        Box(content, p.position + " · " + p.age + "세", 300, 306, 210, 46, 21, ink);
        Label(content, p.affiliation, 64, 382, 440, 70, 19, muted);
        Scroll(content, p.description, 64, 466, 440, 168, 22);
        string[] sections = { "캐릭터 소개", "능력치 및 스킬", "배경 이야기", "스킬 영상" };
        for (int i = 0; i < sections.Length; i++)
        {
            int tab = i;
            Button(content, sections[i], 60, 664 + i * 64, 450, 52,
                () => { view = tab; Draw(); }, view == i ? blue : ink);
        }
        Button(content, PlayerPrefs.GetInt("Profile.Bookmark." + p.key, 0) == 1 ? "★ 즐겨찾기" : "☆ 즐겨찾기",
            60, 938, 200, 42, () => {
                string key = "Profile.Bookmark." + p.key;
                PlayerPrefs.SetInt(key, 1 - PlayerPrefs.GetInt(key, 0)); PlayerPrefs.Save(); Draw();
            }, ink);
        var panel = Rect("Information card", content, 560, 122, 1300, 850);
        Fill(panel, Color.white);
        if (view == 0)
        {
            Portrait(content, p.key);
            Label(content, "PRIMARY EQUIPMENT", 1390, 194, 410, 30, 17, muted);
            Label(content, p.weapon, 1390, 240, 400, 100, 28, ink);
            for (int i = 0; i < p.skills.Length; i++)
            {
                int slot = i;
                Label(content, i == 2 ? "GAME CHANGER" : "SKILL 0" + (i + 1), 1390, 396 + i * 156, 410, 30, 17, blue);
                Button(content, p.skills[i].title, 1380, 440 + i * 156, 430, 76, () => { view = 1; Draw(); }, ink);
            }
            Label(content, "CHARACTER PRESENTATION", 620, 920, 700, 26, 16, muted);
        }
        else if (view == 1)
        {
            Label(content, "기본 능력치", 606, 160, 430, 48, 30, ink);
            Scroll(content, p.stats, 606, 230, 460, 264, 22);
            Label(content, p.weapon, 1120, 160, 670, 60, 26, ink);
            Scroll(content, p.weaponStats, 1120, 242, 670, 252, 22);
            for (int i = 0; i < p.skills.Length; i++)
            {
                float x = 606 + i * 416;
                Box(content, i == 2 ? "X / GAME CHANGER" : (i == 0 ? "Q / SKILL 1" : "E / SKILL 2"), x, 534, 384, 46, 21, blue);
                Label(content, p.skills[i].title, x, 604, 384, 74, 25, ink);
                Scroll(content, p.skills[i].body, x, 694, 384, 170, 21);
                int slot = i;
                Button(content, (i == 0 ? "Q" : i == 1 ? "E" : "X") + " 영상 보기  ▶", x, 874, 384, 44, () => { videoSlot = slot; view = 3; Draw(); }, ink);
            }
            Label(content, "설정집 기준 · 실제 게임 밸런스와 다를 수 있습니다", 606, 934, 1100, 26, 17, muted);
        }
        else if (view == 2)
        {
            Label(content, "비하인드 스토리", 614, 164, 900, 60, 34, ink);
            Scroll(content, p.lore, 614, 260, 1180, 650, 25);
        }
        else
        {
            for (int i = 0; i < p.skills.Length; i++)
            {
                int slot = i;
                Button(content, i == 0 ? "Q · 스킬 1" : i == 1 ? "E · 스킬 2" : "X · 게임 체인저", 614 + i * 400, 156, 380, 48,
                    () => { videoSlot = slot; Draw(); }, i == videoSlot ? blue : ink);
            }
            var screen = Rect("Skill video", content, 614, 230, 1180, 570);
            Fill(screen, ink);
            var video = screen.gameObject.AddComponent<CharacterSkillVideo>();
            var status = Label(content, "영상 준비 중", 638, 460, 1110, 90, 25, Color.white);
            status.alignment = TextAnchor.MiddleCenter;
            var raw = Rect("Video image", screen, 0, 0, 1180, 570).gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            video.Initialize(p.key, videoSlot, raw, status);
            Button(content, "재생 / 일시정지", 614, 820, 280, 48, video.Toggle, blue);
            Button(content, "처음부터", 914, 820, 220, 48, video.Replay, ink);
            Scroll(content, p.skills[videoSlot].body, 614, 886, 1180, 70, 21);
        }

        for (int i = 0; i < catalog.characters.Length; i++)
        {
            int index = i;
            Button(content, catalog.characters[i].name, 60 + i * 302, 1008, 282, 48,
                () => { selected = index; Draw(); }, i == selected ? blue : ink);
        }
        Label(content, "Esc  뒤로가기     X  상세 정보", 1000, 1018, 480, 30, 19, muted);
        var training = Button(content, "트레이닝 센터  →  T", 1532, 1008, 328, 48, Train, blue);
        training.interactable = CanTrain();
        if (!training.interactable) Label(content, "테스트 씬이 빌드에 포함되지 않았습니다", 1450, 978, 420, 24, 16, muted);
    }
    void FitContent()
    {
        if (!content || !page) return;
        float scale = Mathf.Min(page.rect.width / 1920f, page.rect.height / 1080f);
        content.localScale = Vector3.one * scale;
        content.anchoredPosition = new Vector2(-960 * scale, 540 * scale);
    }
    void LateUpdate() { if (page && page.gameObject.activeSelf) FitContent(); }
    void Portrait(Transform parent, string key)
    {
        var raw = Rect("Live character", parent, 590, 144, 750, 760).gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        var stage = raw.gameObject.AddComponent<CharacterProfileStage>();
        if (!stage.Show(key, raw))
        {
            raw.enabled = false;
            Destroy(stage);
            Label(parent, "캐릭터 모델 또는 모션을 불러오지 못했습니다.", 620, 460, 680, 90, 22, muted);
        }
    }
    string TrainingScene => Current.key + "SkillTest";
    bool CanTrain()
    {
#if UNITY_EDITOR
        return true;
#else
        return Application.CanStreamedLevelBeLoaded(TrainingScene);
#endif
    }
    void Train()
    {
        if (!CanTrain()) return;
        Time.timeScale = 1;
        GameplayCursor.SetUIOpen(this, false);
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Test/" + TrainingScene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(TrainingScene);
#endif
    }
    RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    void Fill(RectTransform rect, Color color) { rect.gameObject.AddComponent<Image>().color = color; }
    Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color? color = null)
    {
        var label = Rect("Text", parent, x, y, w, h).gameObject.AddComponent<Text>();
        label.font = font; label.text = text; label.fontSize = size; label.color = color ?? ink;
        label.raycastTarget = false; label.supportRichText = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }
    void Box(Transform parent, string text, float x, float y, float w, float h, int size, Color? color = null)
    {
        var box = Rect(text, parent, x, y, w, h); Fill(box, color ?? ink);
        var label = Label(box, text, 14, 0, w - 28, h, size, Color.white); label.alignment = TextAnchor.MiddleLeft;
    }
    Button Button(Transform parent, string text, float x, float y, float w, float h, UnityEngine.Events.UnityAction action, Color? color = null)
    {
        var rect = Rect(text, parent, x, y, w, h); Fill(rect, color ?? ink);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors; colors.highlightedColor = new Color(.65f, .8f, 1); colors.selectedColor = colors.highlightedColor; button.colors = colors;
        button.onClick.AddListener(action);
        var label = Label(rect, text, 10, 0, w - 20, h, 22, Color.white); label.alignment = TextAnchor.MiddleCenter;
        return button;
    }
    void Scroll(Transform parent, string text, float x, float y, float w, float h, int size)
    {
        var viewport = Rect("Scroll viewport", parent, x, y, w, h);
        Fill(viewport, new Color(1, 1, 1, .01f)); viewport.gameObject.AddComponent<RectMask2D>();
        var label = Label(viewport, text, 0, 0, w - 12, h, size);
        label.rectTransform.sizeDelta = new Vector2(w - 12, Mathf.Max(h, label.preferredHeight + 12));
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport;
        scroll.content = label.rectTransform; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
    }
}
