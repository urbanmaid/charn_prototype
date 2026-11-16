using UnityEngine;
using UnityEngine.UI;

// All visual objects and button callbacks are authored in Layer_Intro.prefab.
public sealed class CampusTitleScreen : MonoBehaviour
{
    [SerializeField] RectTransform root, layout, dialog;
    [SerializeField] RawImage background;
    [SerializeField] CanvasGroup visibility;
    [SerializeField] Text versionLabel, dialogTitle, dialogBody, dialogCloseLabel;
    [SerializeField] GameObject dialogQuitButton;
    bool enteredLobby, waitingForPanel;
    float nextBackgroundRetry;
    bool backgroundWarning;

    void Start()
    {
        if (!root || !layout || !dialog || !visibility || !background)
        {
            Debug.LogError("CampusTitleScreen requires the configured Layer_Intro prefab.", this);
            enabled = false;
            return;
        }
        if (versionLabel) versionLabel.text = "v" + Application.version;
        dialog.gameObject.SetActive(false);
        LoadBackground();
        SetVisible(true);
        Fit();
    }

    // Keep this component updating while a settings/save panel covers the title.
    void SetVisible(bool visible)
    {
        visibility.alpha = visible ? 1 : 0;
        visibility.interactable = visible;
        visibility.blocksRaycasts = visible;
        MenuControllerNavigation.SetPanel(layout, visible, ConfirmQuit);
        GameplayCursor.SetUIOpen(this, visible);
    }
    public void EnterLobby()
    {
        enteredLobby = true;
        MenuControllerNavigation.SetPanel(layout, false);
        SetVisible(false);
        GameplayCursor.SetUIOpen(this, false);
    }
    public void OpenSaves()
    {
        if (!SaveDataManager.Instance) { Message("세이브 관리", "세이브 관리 화면을 불러오지 못했습니다."); return; }
        Suspend(); SaveDataManager.Instance.ShowSaveDataManagementLayer();
    }
    public void OpenSettings()
    {
        if (!SettingsManager.Instance) { Message("설정", "설정 화면을 불러오지 못했습니다."); return; }
        Suspend(); SettingsManager.Instance.ShowSettingsManagementLayer();
    }
    void Suspend() { waitingForPanel = true; MenuControllerNavigation.SetPanel(layout, false); SetVisible(false); }
    void Update()
    {
        if (waitingForPanel)
        {
            bool saves = SaveDataManager.Instance && SaveDataManager.Instance.IsManagementVisible;
            bool settings = SettingsManager.Instance && SettingsManager.Instance.IsManagementVisible;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (saves) SaveDataManager.Instance.HideSaveDataManagementLayer();
                if (settings) SettingsManager.Instance.HideSettingsManagementLayer();
            }
            if (!saves && !settings) { waitingForPanel = false; SetVisible(true); MenuControllerNavigation.SetPanel(layout, true, ConfirmQuit); }
            return;
        }
        if (!enteredLobby && root && visibility.alpha > 0 && Input.GetKeyDown(KeyCode.Escape))
        { if (dialog.gameObject.activeSelf) CloseDialog(); else ConfirmQuit(); }
    }
    public void ShowNotices() => Message("공지사항", "현재 등록된 공지사항이 없습니다.");
    public void ShowGuide() => Message("시작 가이드", "게임 시작으로 로비에 입장하세요.\n\n로드아웃에서 캐릭터 소개와 스킬 정보를 확인하고,\n로비의 게임플레이 메뉴에서 스테이지를 선택할 수 있습니다.");
    public void ShowWebsite() => Message("공식 사이트", "공식 사이트 주소는 아직 등록되지 않았습니다.");
    public void ShowMedia() => Message("영상/미디어", "영상 콘텐츠는 준비 중입니다.");
    public void ConfirmQuit() => Message("게임 종료", "게임을 종료하시겠습니까?", true);

    void Message(string title, string body, bool quit = false)
    {
        if (dialog.gameObject.activeSelf) return;
        dialogTitle.text = title;
        dialogBody.text = body;
        dialogCloseLabel.text = quit ? "취소" : "닫기";
        dialogQuitButton.SetActive(quit);
        dialog.gameObject.SetActive(true);
        MenuControllerNavigation.SetPanel(dialog, true, CloseDialog);
    }

    public void CloseDialog()
    {
        MenuControllerNavigation.SetPanel(dialog, false);
        dialog.gameObject.SetActive(false);
    }
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    void LateUpdate() { if (root && visibility.alpha > 0) Fit(); }
    void Fit()
    {
        float s = root.rect.width / 1920f;
        float designHeight = root.rect.height / Mathf.Max(.001f, s);
        layout.localScale = Vector3.one * s;
        layout.anchoredPosition = new Vector2(-root.rect.width / 2, root.rect.height / 2);
        if (dialog)
        {
            dialog.sizeDelta = new Vector2(1920, designHeight);
            var card = dialog.Find("Dialog card") as RectTransform;
            if (card) card.anchoredPosition = new Vector2(570, -(designHeight - 380) / 2);
        }
        if (!background.texture && Time.unscaledTime >= nextBackgroundRetry)
        {
            nextBackgroundRetry = Time.unscaledTime + 1;
            LoadBackground();
        }
        if (!background.texture) return;
        float imageAspect = (float)background.texture.width/background.texture.height;
        float screenAspect = root.rect.width/Mathf.Max(1,root.rect.height);
        background.uvRect = screenAspect > imageAspect ? new Rect(0,(1-imageAspect/screenAspect)/2,1,imageAspect/screenAspect) : new Rect((1-screenAspect/imageAspect)/2,0,screenAspect/imageAspect,1);
    }
    void LoadBackground()
    {
        var texture = Resources.Load<Texture2D>("TitleScreen/CampusEnhanced");
        if (!texture)
        {
            var sprite = Resources.Load<Sprite>("TitleScreen/CampusEnhanced");
            if (sprite) texture = sprite.texture;
        }
        background.texture = texture;
        background.color = texture ? Color.white : new Color(.69f, .83f, .96f);
        if (!texture && !backgroundWarning)
        {
            backgroundWarning = true;
            Debug.LogWarning("[TitleScreen] Campus background is not imported yet; retrying resource load.", this);
        }
    }
    void OnDisable()
    {
        GameplayCursor.SetUIOpen(this, false);
        MenuControllerNavigation.SetPanel(layout, false);
        MenuControllerNavigation.SetPanel(dialog, false);
    }
}
