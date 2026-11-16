using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDGadgets : MonoBehaviour
{
    private PlayerGadgetManager playerGadgetManager;
    private TeamGadgetManager teamGadgetManager;

    [SerializeField] private Image[] itemSpriteArray;
    /*
    Array of image of gadget sprite
    Index 0 for currently focus gadget
    Index 1- for remained
    */
    [SerializeField] private RectTransform restGadgetArray; 
    [SerializeField] private TextMeshProUGUI focusedGadgetText; 
    [SerializeField] private TextMeshProUGUI extraCountText; // +2 등을 표시할 추가 텍스트 컴포넌트

    [SerializeField] private float durationOfShowingFocusedGadget = 0.6f;
    private Coroutine nameDisplayCoroutine;

    public void Init(PlayerGadgetManager pG, TeamGadgetManager tG = null)
    {
        OnDestroy();
        playerGadgetManager = null;
        teamGadgetManager = null;
        ResetNameDisplay();
        if (pG != null && tG != null)
        {
            Debug.LogWarning("Only one component of Gadget should be registered");
        }

        if (pG)
        {
            playerGadgetManager = pG;
            playerGadgetManager.OnGadgetOrderChanged += RefreshUI;
            playerGadgetManager.OnShowingItemName += ShowItemNameOnTraverse;
        }

        if (tG)
        {
            teamGadgetManager = tG;
            // Todo
        }

        // 초기 UI 상태 설정
        if (focusedGadgetText) focusedGadgetText.gameObject.SetActive(false);
        if (extraCountText) extraCountText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (playerGadgetManager)
        {
            playerGadgetManager.OnGadgetOrderChanged -= RefreshUI;
            playerGadgetManager.OnShowingItemName -= ShowItemNameOnTraverse;
        }
    }

    private void ResetNameDisplay()
    {
        if (nameDisplayCoroutine != null) StopCoroutine(nameDisplayCoroutine);
        nameDisplayCoroutine = null;
        if (focusedGadgetText) focusedGadgetText.gameObject.SetActive(false);
        if (restGadgetArray) restGadgetArray.gameObject.SetActive(true);
    }

    private void OnDisable() => ResetNameDisplay();

    void RefreshUI(List<Gadget> data)
    {
        ResetNameDisplay();
        if (itemSpriteArray == null || itemSpriteArray.Length == 0) return;

        int maxDisplayCount = itemSpriteArray.Length;
        int currentGadgetCount = data.Count;

        // 1. 이미지 슬롯 활성화 및 스프라이트 바인딩
        for (int i = 0; i < maxDisplayCount; i++)
        {
            if (i < currentGadgetCount)
            {
                itemSpriteArray[i].gameObject.SetActive(true);
                itemSpriteArray[i].sprite = data[i].icon; // 가젯 데이터의 아이콘 매핑
            }
            else
            {
                itemSpriteArray[i].gameObject.SetActive(false);
            }
        }

        // 2. 최대 표시 개수를 초과하는 잔여 가젯 개수 계산 (+X)
        if (extraCountText)
        {
            if (currentGadgetCount > maxDisplayCount)
            {
                extraCountText.gameObject.SetActive(true);
                int extraCount = currentGadgetCount - maxDisplayCount;
                extraCountText.text = $"+{extraCount}";
            }
            else
            {
                extraCountText.gameObject.SetActive(false);
            }
        }

        // 3. 가젯이 순회될 때 포커스된 가젯 이름 동기화
        if (focusedGadgetText && currentGadgetCount > 0)
        {
            focusedGadgetText.text = data[0].gadgetName;
        }
    }

    void ShowItemNameOnTraverse()
    {
        if (nameDisplayCoroutine != null)
        {
            StopCoroutine(nameDisplayCoroutine);
        }
        nameDisplayCoroutine = StartCoroutine(ShowItemNameOnTraverseCo());
    }

    IEnumerator ShowItemNameOnTraverseCo()
    {
        // 순회(Traverse) 연출: 가젯 이름을 잠시 보여주고 남은 가젯 리스트를 숨김
        if (focusedGadgetText) focusedGadgetText.gameObject.SetActive(true);
        if (restGadgetArray) restGadgetArray.gameObject.SetActive(false);

        yield return new WaitForSeconds(durationOfShowingFocusedGadget);

        if (focusedGadgetText) focusedGadgetText.gameObject.SetActive(false);
        if (restGadgetArray) restGadgetArray.gameObject.SetActive(true);
        
        nameDisplayCoroutine = null;
    }
}