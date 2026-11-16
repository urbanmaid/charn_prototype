using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDIngameEffects : MonoBehaviour
{
    //[SerializeReference, SubclassSelector]
    private IIngameEffectsStorage targetEffectStorage;
    [SerializeField] private bool usedAsIngameUI;

    [Header("Basis")]
    [SerializeField, Tooltip("Icon Pack for expressing each category of effects.")] private IngameEffectIconStorage ingameEffectIconStorage;
    [SerializeField, Tooltip("Basic theme unit for expressing each unit of effects.")] private IngameEffectIconUnit ingameEffectIconUnit;

    [Header("Expression")]
    [SerializeField, Tooltip("Layout")] private HorizontalOrVerticalLayoutGroup iconLayoutPosition;

    [Header("Excessive Amount Replacer")]
    [SerializeField] private int maximunShowingEffectCount = 6;
    [SerializeField] private TextMeshProUGUI excessiveEffectCountShowText;

    // Dictionary for saving up currently activated Icons.
    private Dictionary<IngameEffectRuntimeState, IngameEffectIconUnit> activeIcons = new Dictionary<IngameEffectRuntimeState, IngameEffectIconUnit>();

    public void Init(IIngameEffectsStorage iES)
    {
        targetEffectStorage = iES;
        if(excessiveEffectCountShowText != null)
        {
            excessiveEffectCountShowText.text = "";
        }
    }

    public void RefreshRuntimeEffects()
    {
        UpdateIngameEffects();

        foreach (var kvp in activeIcons)
        {
            if (kvp.Value != null)
            {
                kvp.Value.BindRuntimeState(kvp.Key);
                kvp.Value.UpdateRuntimeGauge();
            }
        }
    }

    public void UpdateIngameEffects()
    {
        RectTransform lp = iconLayoutPosition.GetComponent<RectTransform>();
        if(targetEffectStorage == null)
        {
            Debug.LogError("Target effect storage is null!");
            return;
        }
        List<IngameEffectRuntimeState> currentEffects = targetEffectStorage.GetIngameEffectRuntimeStates();

        // 1. 표시할 이펙트 개수 결정 및 리스트 추출
        // 가장 최근에 추가된 효과를 우선적으로 보여주기 위해 리스트의 뒷부분을 가져옴
        int showingCount = Mathf.Min(currentEffects.Count, maximunShowingEffectCount);
        int startIndex = currentEffects.Count - showingCount;
        List<IngameEffectRuntimeState> effectsToShow = currentEffects.GetRange(startIndex, showingCount);

        // 2. 화면에 표시되지 않아야 할 아이콘 제거
        List<IngameEffectRuntimeState> toRemove = new List<IngameEffectRuntimeState>();
        foreach (var kvp in activeIcons)
        {
            if (!effectsToShow.Contains(kvp.Key))
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (var key in toRemove)
        {
            activeIcons.Remove(key);
        }

        // 3. 새로운 아이콘 생성 및 레이아웃 상의 순서 보장
        for (int i = 0; i < effectsToShow.Count; i++)
        {
            IngameEffectRuntimeState state = effectsToShow[i];
            if (state?.effect == null)
            {
                continue;
            }

            if (!activeIcons.ContainsKey(state))
            {
                IngameEffectIconUnit icoU = Instantiate(ingameEffectIconUnit, lp);
                icoU.Init(state, ingameEffectIconStorage, usedAsIngameUI);
                activeIcons.Add(state, icoU);

                // For debug
                // Debug.Log("Added Ingame Effect:" + ed.effectName + " / Inten: " + ed.intensity + " / Dur: " + (ed.deactivateOnReload ? ed.duration : "Cur Magazine"));
            }
            
            // UI에 나타나는 순서를 최신 효과가 앞(왼쪽)에 오도록 설정
            // 순차적으로 첫 번째 자리에 넣으면, 가장 마지막에 처리되는(가장 최근) 효과가 0번 인덱스가 됨
            if (activeIcons.TryGetValue(state, out IngameEffectIconUnit unit) && unit != null)
            {
                unit.transform.SetAsFirstSibling();
            }
        }

        /*
        for (int i = 0; i < effectsToShow.Count; i++)
        {
            IngameEffectData ed = effectsToShow[i];
            if (!activeIcons.ContainsKey(ed))
            {
                IngameEffectIconUnit icoU = Instantiate(ingameEffectIconUnit, lp);
                icoU.Init(ed, ingameEffectIconStorage, usedAsIngameUI);
                activeIcons.Add(ed, icoU);

                // For debug
                Debug.Log("Added Ingame Effect:" + ed.effectName + " / Inten: " + ed.intensity + " / Dur: " + ed.duration);
            }
            
            // UI에 나타나는 순서를 실제 리스트의 순서와 맞춤
            if (activeIcons.TryGetValue(ed, out IngameEffectIconUnit unit) && unit != null)
            {
                unit.transform.SetSiblingIndex(i);
            }
        }
        */

        // 4. 최대 표시 개수를 초과하는 경우 텍스트 업데이트
        if (excessiveEffectCountShowText != null)
        {
            int excessiveCount = currentEffects.Count - maximunShowingEffectCount;
            if (excessiveCount > 0)
            {
                excessiveEffectCountShowText.text = $"+{excessiveCount}";
                excessiveEffectCountShowText.gameObject.SetActive(true);
                // 초과 텍스트가 항상 아이콘들 뒤(우측/하단)에 오도록 맨 뒤로 보냄
                excessiveEffectCountShowText.transform.SetAsLastSibling();
            }
            else
            {
                excessiveEffectCountShowText.text = "";
                excessiveEffectCountShowText.gameObject.SetActive(false);
            }
        }
    }

    void Update() { }
}
