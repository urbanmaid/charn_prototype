using UnityEngine;
using UnityEngine.InputSystem;

public class Gadget : MonoBehaviour
{
    [SerializeField]
    public string gadgetName = "Name";
    public Sprite icon;
    public int requiredSpace;
    [SerializeField] private GadgetData gadgetdata;//ScriptableObject
    [SerializeField, Tooltip("If not 0, the value power or key of effect will be overriden by this value.")] private int overrideValueOfEffect = 0;
    [SerializeField] private string saveDefinitionId;
    public string SaveDefinitionId => saveDefinitionId;
    public bool IsConsumed { get; private set; }
    public void MarkConsumed() => IsConsumed = true;
    public int OverrideValue => overrideValueOfEffect;
    private bool runtimeDataPrepared;
    public void PrepareRuntimeData()
    {
        if (runtimeDataPrepared) return;
        runtimeDataPrepared = true;
        if (UseOverrideGadgetValue() && gadgetdata != null)
        {
            gadgetdata = Instantiate(gadgetdata);
            gadgetdata.OverrideValue(overrideValueOfEffect);
        }
    }
    private bool pickable = false;
    private GameObject player;
    private PlayerGadgetManager gadgetManager;
    private InputAction pickAction;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        SetPlayerGadgetManager();
    }

    void SetPlayerGadgetManager()
    {
        player = GameObject.FindGameObjectWithTag("Player");        
        gadgetManager = player != null ? player.GetComponent<PlayerGadgetManager>() : null;
    }

    private void Start()
    {
        if (player == null) SetPlayerGadgetManager();
        var pInput = player != null ? player.GetComponent<PlayerInput>() : null;
        if (pInput == null)
        {
            return;
        }
        pickAction = pInput.actions["Interact"];   
    }
    
    // Update is called once per frame
    void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (pickAction == null)
        {
            Debug.LogError("[Gadget] pickAction null (Update)");
            return;
        }

        if (gadgetManager == null)
        {
            Debug.LogError("[Gadget] gadgetManager null (Update)");
            return;
        }

        //������ ������� ���ϱ� ������ ��ü���� �Է��� �޴������ InteractiveWeapon.cs
        if (pickAction.WasPressedThisFrame() && pickable && gadgetManager.CanAddItem())
        {
            //Debug.Log("���� ȹ��: " + gadgetdata.itemName);
            pickable = false;

            if(gadgetManager == null)
            {
                SetPlayerGadgetManager();
            }

            if(gadgetManager)
            {
                PrepareRuntimeData();
                gadgetManager.EnrollGadget(gameObject);
            }
            else
            {
                Debug.LogWarning("Cannot find gadget manager of player");
                return;
            }

            if(gadgetdata != null && gadgetManager.GetGadgetInventory() != null)
            {
                gadgetManager.GetGadgetInventory().AddItem(gadgetdata);
            }
            else
            {
                Debug.LogWarning("Gadget Data is null");
            }
            

            gameObject.SetActive(false);
            //gadgetManager.GetGadgetInventory().GetComponent<Inventory>()?.AddItem(gadgetdata);
        }
    }

    void OnTriggerEnter(Collider other)
    {                
        if (other.CompareTag("Player"))
        {
            // ���� ȹ�� ó��            
            pickable = true;
            //Debug.Log("���� ȹ�氡��: " + gadgetdata.name);            
        }
    }   

    // Handle player exiting radius of interaction.
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == player)
        {
            //Debug.Log("���� �Ұ���: " + gadgetdata.name);
            pickable = false;            
        }
    }
    public void Drop(Vector3 dropPos)
    {
        this.gameObject.SetActive(true);
        this.transform.position = dropPos;        
    }

    public bool UseOverrideGadgetValue()
    {
        return (overrideValueOfEffect != 0);
    }

    public GadgetData GetGadgetData()
    {
        return gadgetdata;
    }
}
