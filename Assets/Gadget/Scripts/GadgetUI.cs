using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GadgetUI : MonoBehaviour
{
    private TextMeshProUGUI gadgetText;
    private PlayerGadgetManager gadgetManager;

    public int maxUICount = 4;//UI�� ǥ�õ� �����̸� ��
    [SerializeField]
    private List<string> gadgetTexts = new List<string>();//���� UI�� ǥ�õ� ����

    private void Start()
    {
        gadgetText = GameObject.Find("TestHaveGadgetUI").GetComponent<TextMeshProUGUI>();
        gadgetManager = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerGadgetManager>();
        gadgetText.text = "";

        if (gadgetText == null)
        {
            Debug.Log("gadgetText: ��ã�� ");
        }
        if (gadgetManager == null)
        {
            Debug.Log("gadgetManager ��ã��: ");
        }
    }   

    public void AddGadgetText(string text)
    {
        Debug.Log("AddGadgetText ���� ");        
        if (gadgetTexts.Count < 4)
        {
            gadgetTexts.Add(text);
        }
        else
        {
            gadgetTexts.RemoveAt(0); // �� �� ����
            gadgetTexts.Add(text);   // �� �� �߰�
        }
        RefreshGadgetUI();
    }

    public void RemoveGadgetText()
    {
        //Debug.Log(gadgetTexts.Count);
        if (gadgetTexts.Count > 0)
        {
            //Debug.Log("RemoveGadgetText");
            gadgetTexts.RemoveAt(0);
            RefreshGadgetUI();
        }
    }

    //ǥ�õǴ� �ؽ�Ʈ ��ĭ�� �̵�
    public void TraverseOnceGadgetText()
    {
        if (gadgetTexts.Count > 0)
        {
            string first = gadgetTexts[0];
            gadgetTexts.RemoveAt(0);
            gadgetTexts.Add(first);

            //AddGadgetText(gadgetTexts[0]);           
            RefreshGadgetUI();
        }
    }

    public void RefreshGadgetUI()
    {
        gadgetText.text = "";
        /*
        foreach (var ggadgetText in gadgetTexts)
        {
            gadgetText.text += ggadgetText + "\n";
        }
        */
        for (int i = 0; i < gadgetTexts.Count; i++)
        {
            //Debug.Log("RefreshGadgetUI:" + gadgetTexts[i]);
            string cleanText = gadgetTexts[i].Trim();  // �ٹٲ� ����
            if (i == 0)
                gadgetText.text += "> " + cleanText + "\n";
            else
                gadgetText.text += cleanText + "\n";
        }
    }
}