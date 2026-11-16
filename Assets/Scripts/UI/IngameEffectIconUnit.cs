using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IngameEffectIconUnit : MonoBehaviour
{
    [Header("Basis")]
    [SerializeField] private Image bg;
    [SerializeField] private Image basis;

    [Header("Gauge")]
    [SerializeField] bool deactivateOnReload;
    [SerializeField] private Image gaugePercentage;
    [SerializeField] private Image gaugeMagazine;
    [SerializeField] private Image gaugeBindedToPA;

    [Header("Pos-Neg Indicator in compact view")]
    [SerializeField] private RectTransform layerCompact;
    [SerializeField] private Image powerIndicatorPosLv1, powerIndicatorPosLv2, powerIndicatorPosLv3;
    [SerializeField] private Image powerIndicatorNegLv1, powerIndicatorNegLv2, powerIndicatorNegLv3;

    [Header("Pos-Neg Indicator with text in fullsize view")]
    [SerializeField] private RectTransform layerFullsize;
    [SerializeField] private Image powerIndicatorPos;
    [SerializeField] private Image powerIndicatorNeg;
    [SerializeField] private TextMeshProUGUI powerTextPos;
    [SerializeField] private TextMeshProUGUI powerTextNeg;

    [SerializeField] private float duration = 0, durationCur = 0;
    private IngameEffectRuntimeState runtimeState;

    public void Init(IngameEffectRuntimeState state, IngameEffectIconStorage iconStorage, bool usedAsIngameUI = true)
    {
        runtimeState = state;
        IngameEffectData data = runtimeState?.effect;
        if(data == null) return;
        deactivateOnReload = runtimeState.deactivateOnReload;

        // Apply icon by comparing the exact runtime types
        if (iconStorage != null && iconStorage.iconArray != null)
        {
            foreach (var icon in iconStorage.iconArray)
            {
                if (icon.behavior != null && data.GetType() == icon.behavior.GetType())
                {
                    if (basis != null)
                    {
                        basis.sprite = icon.sprite;
                    }

                    if (bg != null)
                    {
                        bg.color = icon.color;
                    }

                    break;
                }
            }
        }

        // Set Duration
        gaugePercentage.gameObject.SetActive(!deactivateOnReload); // If Set as periodical limits
        gaugeMagazine.gameObject.SetActive(deactivateOnReload); // If Set as deactivated on reload
        if(!deactivateOnReload)
        {
            duration = runtimeState.originalDuration;
            durationCur = runtimeState.remainingDuration;
        }

        // If it is not used as Ingame UI, set activation of intensity
        layerFullsize.gameObject.SetActive(!usedAsIngameUI);
        layerCompact.gameObject.SetActive(usedAsIngameUI);

        if(!usedAsIngameUI)
        {
            // Full size layer indicator
            if
            (
                powerIndicatorPos == null || 
                powerIndicatorNeg == null || 
                powerTextPos == null || 
                powerTextNeg == null
            )
            {
                return;
            }

            powerIndicatorPos.gameObject.SetActive(data.intensity > 0);
            powerIndicatorNeg.gameObject.SetActive(data.intensity < 0);

            powerTextPos.gameObject.SetActive(true);
            if(powerTextPos.IsActive())
            {
                powerTextPos.text = data.intensity.ToString();
            }

            powerTextNeg.gameObject.SetActive(true);
            if(powerTextNeg.IsActive())
            {
                powerTextNeg.text = data.intensity.ToString();
            }
        }
        else if(usedAsIngameUI)
        {
            // Compact layer indicator
            if
            (
                powerIndicatorPosLv1 == null || 
                powerIndicatorPosLv2 == null || 
                powerIndicatorPosLv3 == null || 
                powerIndicatorNegLv1 == null || 
                powerIndicatorNegLv2 == null || 
                powerIndicatorNegLv3 == null
            )
            {
                return;
            }

            int lv1Margin = 20, lv2Margin = 50;

            powerIndicatorPosLv1.gameObject.SetActive(data.intensity > 0 && lv1Margin > data.intensity);
            powerIndicatorPosLv2.gameObject.SetActive(data.intensity > lv1Margin && lv2Margin > data.intensity);
            powerIndicatorPosLv3.gameObject.SetActive(data.intensity > lv2Margin);

            powerIndicatorNegLv1.gameObject.SetActive(data.intensity < 0 && lv1Margin * -1 < data.intensity);
            powerIndicatorNegLv2.gameObject.SetActive(data.intensity < lv1Margin * -1 && lv2Margin * -1 < data.intensity);
            powerIndicatorNegLv3.gameObject.SetActive(data.intensity < lv2Margin * -1);
        }

        UpdateRuntimeGauge();
    }

    public void BindRuntimeState(IngameEffectRuntimeState state)
    {
        runtimeState = state;
        if (runtimeState == null)
            return;

        deactivateOnReload = runtimeState.deactivateOnReload;
        duration = runtimeState.originalDuration;
        durationCur = runtimeState.remainingDuration;
        UpdateRuntimeGauge();
    }

    public void UpdateRuntimeGauge()
    {
        if (runtimeState == null)
            return;

        durationCur = runtimeState.remainingDuration;

        if (gaugePercentage != null)
        {
            gaugePercentage.gameObject.SetActive(!runtimeState.deactivateOnReload);
            gaugePercentage.fillAmount = runtimeState.IsTimed ? runtimeState.RemainingRatio : 1f;
        }

        if (gaugeMagazine != null)
        {
            gaugeMagazine.gameObject.SetActive(runtimeState.deactivateOnReload);
        }
    }
}
