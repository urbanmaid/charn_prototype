using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public class StartMenuManager : MonoBehaviour
{
    public GameObject menuPanel;
    public GameObject backGroundSoundManager;
    //controls object
    public GameObject[] objectList;

    //startmenuImg
    [Header("Start Menu background")]
    public string playerTag = "Player";
    public RawImage previewImage;
    private Camera previewCamera;
    private RenderTexture renderTexture;
    private Transform player;    
    private Vector3 cameraOffset =
        new Vector3(1.42f, 1.24f, 1.43f);    
    private Vector3 lookOffset =
        new Vector3(-1.03f, 0.85f, 0.13f);
    //---

    [Header("Start Menu sound")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip startMenuBGM;

    [Header("Button Onhit")]
    [SerializeField] private float normalAlpha = 0.5f;
    [SerializeField] private float hoverAlpha = 1.0f;

    [Header("Objects Disable On Start")]
    [SerializeField] private GameObject[] disableOnStartObjects;

    void Start()
    {
        DisableStartObjects();
        CreatePreviewCamera();
        PlayBGM();
    }

    private void DisableStartObjects()
    {
        foreach (GameObject obj in disableOnStartObjects)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }
    void CreatePreviewCamera()
    {
        // 플레이어 찾기
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObj == null)
        {
            Debug.LogError("플레이어 못 찾음");
            return;
        }

        player = playerObj.transform;

        // RenderTexture 동적 생성
        renderTexture = new RenderTexture(512, 512, 16);

        // 카메라 GameObject 생성
        GameObject camObj = new GameObject("PreviewCamera");
        previewCamera = camObj.AddComponent<Camera>();

        // 플레이어 자식으로 넣기
        camObj.transform.SetParent(player);

        // 플레이어 정면 기준 위치
        camObj.transform.localPosition = cameraOffset;

        // 플레이어 얼굴 바라보기
        previewCamera.transform.LookAt(
            player.position + lookOffset
        );

        // 카메라 설정
        previewCamera.clearFlags =
            CameraClearFlags.SolidColor;

        previewCamera.backgroundColor =
            Color.clear;

        previewCamera.targetTexture =
            renderTexture;

        // UI 연결
        previewImage.texture =
            renderTexture;
    }

    public void PlayBGM()
    {
        bgmSource.clip = startMenuBGM;
        bgmSource.loop = true;
        bgmSource.Play();
    }


    //startgameButton
    public void CloseMenu()
    {
        EnableStartObjects();
        backGroundSoundManager.SetActive(true);
        menuPanel.SetActive(false);
    }

    private void EnableStartObjects()
    {
        foreach (GameObject obj in disableOnStartObjects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
    }

    //contorlButton
    public void OpenControls()
    {                
        foreach (GameObject obj in objectList)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
    }

    //contorlButton
    public void CloseControls()
    {
        foreach(GameObject obj in objectList)
    {
            if (obj != null) obj.SetActive(false);
        }          
    }

    //endgameButton
    public void QuitGame()
    {
        Application.Quit();
    }

    public void PointerEnter(BaseEventData data)
    {
        PointerEventData eventData = (PointerEventData)data;
        
        GameObject currentObj = eventData.pointerEnter;

        if (currentObj != null)
        {
            Image img = currentObj.GetComponent<Image>();
            if (img != null)
            {
                SetAlpha(img, hoverAlpha); // 마우스 올렸을 때 알파값
                //Debug.Log("Enter 성공: " + currentObj.name);
                return;
            }
        }
        //Debug.Log("Enter 호출은 되었으나 이미지를 찾지 못함");
    }

    public void PointerExit(BaseEventData data)
    {
        PointerEventData eventData = (PointerEventData)data;
        
        GameObject lastObj = eventData.pointerEnter;

        if (lastObj != null)
        {
            Image img = lastObj.GetComponent<Image>();
            if (img != null)
            {
                SetAlpha(img, normalAlpha); // 마우스 나갔을 때 원래 알파값
                //Debug.Log("Exit 성공: " + lastObj.name);
                return;
            }
        }               
        //Debug.Log("Exit 호출은 되었으나 대상을 특정하지 못함");
    }

    private void SetAlpha(Image img, float alpha)
    {
        Color color = img.color;
        color.a = alpha;
        img.color = color;
    }
}
