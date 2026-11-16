using System.Collections.Generic;
using UnityEngine;

public class TeamGadgetManager : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> gadgets = new List<GameObject>();
    //private int gadgetsFrontIndex = 0;//0�̰� �ȹٲ�ϴ� �������� ���� ����
    private int MAX_GADGETS = 4;//���������̽� ���߿� ��𼱰� �޾ƿ� �� �ӽ÷� 4

    private PlayerGadgetManager gadgetManager;
    [SerializeField]
    private GameObject teamGadgetInventory;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {        
        gadgetManager = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerGadgetManager>();
        gadgetManager.EnrollTeamGadgetManager(gameObject);

        if(teamGadgetInventory)
        {
            teamGadgetInventory.GetComponent<Inventory>().inventoryPanel.GetComponent<ItemInfoUpdate>().PAnameText.text = GetComponent<PersonaBehavior>().GetPersona().personaName;
            teamGadgetInventory.GetComponent<Inventory>().inventoryPanel.GetComponent<ItemInfoUpdate>().PAicon.sprite = GetComponent<PersonaBehavior>().GetPersona().icon;
        }
    }

    public void EnrollGadget(GameObject gadgetObject)
    {
        Debug.Log("TeamEnrollGadget");
        //����������Ʈ�� ����Ʈ�� ���
        Gadget gadget = gadgetObject.GetComponent<Gadget>();
        GadgetData gadgetData = gadget.GetGadgetData();
        gadgets.Add(gadgetObject);

        if(teamGadgetInventory)
        {
            teamGadgetInventory.GetComponent<Inventory>()?.AddItem(gadgetData);
        }
    }

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
    }

    private void RebuildInventoryData()
    {
        if (!teamGadgetInventory) return;

        Inventory inventory = teamGadgetInventory.GetComponent<Inventory>();
        if (inventory == null) return;

        inventory.itemList.Clear();
        inventory.quantityList.Clear();

        foreach (var gadgetObj in gadgets)
        {
            if (gadgetObj != null && gadgetObj.TryGetComponent<Gadget>(out var gadget))
            {
                inventory.AddItem(gadget.GetGadgetData());
            }
        }

        inventory.UpdateInventoryUI();
    }

    public void UseGadget(GadgetData gadget)
    {
        if (gadget.command != null)
        {
            //��ùߵ� TriggeredŸ��
            if (gadget.gadgetType == GadgetActiveType.Triggered)
            {
                gadget.Use(this.gameObject);
            }
            else
            {
                //Comsumable, Passive Ÿ�� �̱���
            }
        }
        else
        {
            Debug.Log("����Ŀ�ǵ尡 ��� �ߵ�����");
        }
    }

    public bool CanAddItem()
    {
        if (gadgets.Count >= MAX_GADGETS)
        {
            return false;
        }
        else
        {
            return true;
        }
    }
}
