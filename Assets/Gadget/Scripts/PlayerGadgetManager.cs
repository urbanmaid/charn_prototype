using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class PlayerGadgetManager : MonoBehaviour
{
    [Header("Gadget Settings")]
    [SerializeField] private List<GameObject> gadgets = new List<GameObject>();
    private const int MAX_GADGETS = 4;
    private readonly int gadgetsFrontIndex = 0;

    [SerializeField] private List<GameObject> externalManagersToTransfer = new List<GameObject>();
    [SerializeField] private Inventory gadgetInventory;

    [Header("Inventory & UI")]
    [SerializeField] private HUDGadgets hUDGadgets;

    private bool isGadgetInventoryOn = false;
    private Inventory cachedInventory;
    private PlayerCommander cachedCommander;

    [Header("Input Bindings")]
    public string pickButton = "Interact";
    public string traverseButton = "Traverse";
    public string useButton = "Use";

    private InputAction pickAction;
    private InputAction useAction;
    private InputAction traverseAction;

    public event Action<List<Gadget>> OnGadgetOrderChanged;
    public event Action OnShowingItemName;
    private void Awake()
    {
        cachedInventory = gadgetInventory?.GetComponent<Inventory>();
        cachedCommander = GetComponent<PlayerCommander>();

        if(hUDGadgets)
        {
            hUDGadgets.Init(this);
            OnGadgetOrderChanged?.Invoke(GetCurrentGadgetDatas());
        }
    }

    private void Start()
    {
        InitializeInput();
    }

    private void OnDestroy()
    {
        UnbindInputActions();
    }

    #region Input Initialization & Binding

    private void InitializeInput()
    {
        if (!TryGetComponent<PlayerInput>(out var playerInput))
        {
            Debug.LogError("PlayerInput 컴포넌트가 누락되었습니다.");
            return;
        }

        pickAction = playerInput.actions[pickButton];
        traverseAction = playerInput.actions[traverseButton];
        useAction = playerInput.actions[useButton];

        // 단일 performed 이벤트 내에서어떤 인터랙션(Hold/Tap)이 완료되었는지 식별합니다.
        traverseAction.performed += OnTraverseActionExecuted;
        useAction.performed += OnUseActionExecuted;

        pickAction.Enable();
        traverseAction.Enable();
        useAction.Enable();
    }

    private void UnbindInputActions()
    {
        if (traverseAction != null) traverseAction.performed -= OnTraverseActionExecuted;
        if (useAction != null) useAction.performed -= OnUseActionExecuted;
    }

    #endregion

    #region Input Action Event Handlers

    /// <summary> Traverse 액션 처리 (인벤토리 토글 혹은 가젯 순회) </summary>
    private void OnTraverseActionExecuted(InputAction.CallbackContext context)
    {
        if (Time.timeScale <= 0f) return;
        if (gadgets.Count <= 0) return;

        // 1. 길게 누르기 (Hold)가 성립되었을 때
        if (context.interaction is HoldInteraction)
        {
            ToggleGadgetInventory();
        }
        // 2. 짧게 탭하기 (Tap)가 성립되었을 때
        else if (context.interaction is TapInteraction)
        {
            RotateGadgets();
        }
    }

    /// <summary> Use 액션 처리 (팀원 양도 혹은 가젯 사용) </summary>
    private void OnUseActionExecuted(InputAction.CallbackContext context)
    {
        if (Time.timeScale <= 0f) return;
        if (gadgets.Count <= 0) return;

        // 1. 길게 누르기 (Hold) -> 가젯 던지기 / 팀원 양도
        if (context.interaction is HoldInteraction)
        {
            //TransferGadgetInternal();
        }
        // 2. 짧게 탭하기 (Tap) -> 가젯 직접 사용
        else if (context.interaction is TapInteraction)
        {
            UseCurrentGadget();
        }
    }

    #endregion

    #region Core Logic Methods

    private void ToggleGadgetInventory()
    {
        isGadgetInventoryOn = !isGadgetInventoryOn;
    }

    private void RotateGadgets()
    {
        if(gadgets.Count > 1)
        {
            GameObject frontGadget = gadgets[0];
            gadgets.RemoveAt(0);
            gadgets.Add(frontGadget);

            RefreshInventoryData();

            // Refresh UI
            OnGadgetOrderChanged?.Invoke(GetCurrentGadgetDatas());
            OnShowingItemName?.Invoke();
        }
        else
        {
            Debug.Log("There is no gadgets to traverse.");
            return;
        }
    }

    private void RefreshInventoryData()
    {
        if (cachedInventory == null) return;

        foreach (var gadgetObj in gadgets)
        {
            if (gadgetObj.TryGetComponent<Gadget>(out var gadget))
                cachedInventory.RemoveItem(gadget.GetGadgetData());
        }

        foreach (var gadgetObj in gadgets)
        {
            if (gadgetObj.TryGetComponent<Gadget>(out var gadget))
                cachedInventory.AddItem(gadget.GetGadgetData());
        }
    }

    private void RebuildInventoryData()
    {
        if (cachedInventory == null) return;

        cachedInventory.itemList.Clear();
        cachedInventory.quantityList.Clear();

        foreach (var gadgetObj in gadgets)
        {
            if (gadgetObj != null && gadgetObj.TryGetComponent<Gadget>(out var gadget))
                cachedInventory.AddItem(gadget.GetGadgetData());
        }

        cachedInventory.UpdateInventoryUI();
    }

    private void TransferGadgetInternal()
    {
        if (cachedCommander == null) return;

        string selectedPAName = cachedCommander.singleSelectedUnitName;
        if (externalManagersToTransfer.Count <= 0 || string.IsNullOrEmpty(selectedPAName)) return;

        foreach (var managerObj in externalManagersToTransfer)
        {
            if (managerObj == null) continue;

            var personaBehavior = managerObj.GetComponent<PersonaBehavior>();
            if (personaBehavior == null || personaBehavior.GetPersona().personaName != selectedPAName) continue;

            var teamGadgetManager = managerObj.GetComponent<TeamGadgetManager>();
            if (teamGadgetManager == null) continue;

            teamGadgetManager.EnrollGadget(gadgets[gadgetsFrontIndex]);

            if (gadgets[gadgetsFrontIndex].TryGetComponent<Gadget>(out var borrowGadget))
            {
                cachedInventory?.RemoveItem(borrowGadget.GetGadgetData());
            }

            gadgets.RemoveAt(gadgetsFrontIndex);
            break;
        }
    }

    private void UseCurrentGadget()
    {
        if (!gadgets[gadgetsFrontIndex].TryGetComponent<Gadget>(out var gadget)) return;
        
        GadgetData gadgetData = gadget.GetGadgetData();

        if (UseGadgetOutside(gadgetData, gameObject))
        {
            Debug.LogFormat("Gadget {0} used successfully", gadget.gadgetName);

            // [수정] 1. 삭제할 실제 게임 오브젝트를 먼저 변수에 안전하게 보관합니다.
            GameObject objToDestroy = gadgets[gadgetsFrontIndex];

            // 2. 인벤토리 및 가젯 관리 리스트에서 데이터를 제거합니다.
            gadgets.RemoveAt(gadgetsFrontIndex);
            RebuildInventoryData();

            // 3. 리스트가 가리키는 인덱스들이 정렬된 후 UI를 갱신합니다.
            OnGadgetOrderChanged?.Invoke(GetCurrentGadgetDatas());

            // [수정] 4. 미리 빼놓았던 가젯 오브젝트를 파괴합니다.
            if (objToDestroy != null)
            {
                objToDestroy.GetComponent<Gadget>()?.MarkConsumed();
                Destroy(objToDestroy);
            }
        }
        else
        {
            Debug.LogFormat("Gadget {0} had some problems while using", gadget.gadgetName);
        }
    }

    #endregion

    #region Public API Methods

    public void EnrollGadget(GameObject gadgetObject)
    {
        if (gadgetObject == null) return;

        gadgets.Add(gadgetObject);

        OnGadgetOrderChanged?.Invoke(GetCurrentGadgetDatas());
    }

    public bool UseGadget(GadgetData gadget, GameObject target)
    {
        if (gadget.command == null)
        {
            Debug.LogWarning("Invalid Gadget Detection: Execution command 누락");
            return true;
        }

        return gadget.gadgetType == GadgetActiveType.Triggered ? gadget.Use(target) : true;
    }

    public bool UseGadgetOutside(GadgetData searchGadgetData, GameObject target)
    {
        if (gadgets.Count == 0) return false;

        for (int i = 0; i < gadgets.Count; i++)         
        {
            if (!gadgets[gadgetsFrontIndex].TryGetComponent<Gadget>(out var currentGadget)) continue;
            
            GadgetData currentData = currentGadget.GetGadgetData();

            if (currentData == searchGadgetData)
            {
                return UseGadget(currentData, gameObject);
            }

            RotateGadgets();
        }       
        return false;
    }

    public bool CanAddItem() => gadgets.Count < MAX_GADGETS;
    public void EnrollTeamGadgetManager(GameObject managerObject) => externalManagersToTransfer.Add(managerObject);
    public Inventory GetGadgetInventory() => gadgetInventory;

    public List<GameObject> CaptureGadgetObjects()
    {
        return new List<GameObject>(gadgets);
    }

    public void RestoreGadgetObjects(List<GameObject> newGadgets)
    {
        gadgets = new List<GameObject>();
        if (newGadgets != null)
        {
            foreach (var gadget in newGadgets)
            {
                if (gadget == null) continue;
                if (gadgets.Count >= MAX_GADGETS) break;

                gadgets.Add(gadget);
            }
        }

        RebuildInventoryData();
        OnGadgetOrderChanged?.Invoke(GetCurrentGadgetDatas());
    }

    #endregion

    #region UI
    private List<Gadget> GetCurrentGadgetDatas()
    {
        List<Gadget> datas = new List<Gadget>();
        foreach (var gadgetObj in gadgets)
        {
            if (gadgetObj != null && gadgetObj.TryGetComponent<Gadget>(out var gadget))
            {
                datas.Add(gadget);
            }
        }
        return datas;
    }
    #endregion
}
