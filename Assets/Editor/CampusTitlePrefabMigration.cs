using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class CampusTitlePrefabMigration
{
    const string Path = "Assets/UI/Outgame/Layer_Intro.prefab";
    [MenuItem("Tools/Campus Title/Configure authored prefab")]
    public static void Run()
    {
        Configure();
    }
    static void Configure()
    {
        var prefab = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var layout = prefab.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "Title layout");
            var screen = prefab.GetComponent<CampusTitleScreen>();
            if (!screen) screen = prefab.AddComponent<CampusTitleScreen>();
            var group = prefab.GetComponent<CanvasGroup>();
            if (!group) group = prefab.AddComponent<CanvasGroup>();
            var serialized = new SerializedObject(screen);
            Action<string, UnityEngine.Object> set = (name, value) => serialized.FindProperty(name).objectReferenceValue = value;
            set("root", prefab.GetComponent<RectTransform>());
            set("layout", layout);
            set("visibility", group);
            set("background", prefab.GetComponentsInChildren<RawImage>(true).Single(t => t.name == "Campus illustration"));
            set("versionLabel", layout.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.text.StartsWith("v")));
            string[] names = { "게임 시작", "세이브 관리", "설정", "종료", "공지사항", "가이드", "공식 사이트", "영상/미디어" };
            UnityAction[] actions = { screen.EnterLobby, screen.OpenSaves, screen.OpenSettings, screen.ConfirmQuit, screen.ShowNotices, screen.ShowGuide, screen.ShowWebsite, screen.ShowMedia };
            for (int i = 0; i < names.Length; i++)
            {
                var button = layout.GetComponentsInChildren<Button>(true).Single(b => b.name == names[i]);
                Bind(button, actions[i]);
            }
            var old = layout.Find("Title dialog");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var dialog = Rect("Title dialog", layout, 0, 0, 1920, 1080);
            dialog.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .35f);
            var card = Rect("Dialog card", dialog, 570, 340, 780, 380);
            card.gameObject.AddComponent<Image>().color = Color.white;
            set("dialog", dialog);
            set("dialogTitle", Label("Title", card, "게임 종료", 40, 30, 700, 60, 34));
            set("dialogBody", Label("Body", card, "게임을 종료하시겠습니까?", 40, 112, 700, 150, 24));
            var close = MakeButton(card, "Close", "닫기", 410, screen.CloseDialog);
            set("dialogCloseLabel", close.GetComponentInChildren<Text>());
            set("dialogQuitButton", MakeButton(card, "Quit", "종료", 170, screen.Quit).gameObject);
            dialog.gameObject.SetActive(false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, Path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Outgame/Menu.unity");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Outgame/Menu.unity", OpenSceneMode.Additive);
        var existing = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CampusTitleScreen>(true)).ToArray();
        foreach (var legacy in existing)
            if (legacy.GetComponent<MenuSceneManager>()) UnityEngine.Object.DestroyImmediate(legacy);
        if (!scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CampusTitleScreen>(true)).Any())
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Path), scene);
        EditorSceneManager.SaveScene(scene);
        Validate();
    }
    static void Bind(Button button, UnityAction action)
    {
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }
    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }
    static Text Label(string name, Transform parent, string value, float x, float y, float w, float h, int size)
    {
        var rect = Rect(name, parent, x, y, w * 3, h * 3);
        rect.localScale = Vector3.one / 3;
        var text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.Load<Font>("CharacterProfiles/ProfileFont");
        text.text = value;
        text.fontSize = size * 3;
        text.color = new Color(.09f, .13f, .25f);
        text.raycastTarget = false;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
    static Button MakeButton(Transform card, string name, string label, float x, UnityAction action)
    {
        var rect = Rect(name, card, x, 296, 200, 54);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.09f, .13f, .25f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Bind(button, action);
        var text = Label("Label", rect, label, 0, 0, 200, 54, 24);
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        return button;
    }
    public static void Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var screen = prefab.GetComponent<CampusTitleScreen>();
        if (!screen) throw new Exception("Missing prefab controller");
        var buttons = prefab.GetComponentsInChildren<Button>(true);
        if (buttons.Length != 10) throw new Exception("Expected 10 authored buttons, got " + buttons.Length);
        foreach (var button in buttons)
            if (button.onClick.GetPersistentEventCount() != 1 || button.onClick.GetPersistentTarget(0) != screen)
                throw new Exception("Missing persistent callback: " + button.name);
        var so = new SerializedObject(screen);
        foreach (string field in new[] { "root", "layout", "dialog", "background", "visibility", "dialogTitle", "dialogBody", "dialogCloseLabel", "dialogQuitButton" })
            if (!so.FindProperty(field).objectReferenceValue) throw new Exception("Missing reference: " + field);
        Debug.Log("CAMPUS_TITLE_VALIDATED: 10 authored buttons with persistent callbacks and serialized references.");
    }
}
