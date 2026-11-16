using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))] // CanvasGroup이 누락되는 것을 방지
public class HUDHurtBG : MonoBehaviour
{
    [SerializeField] private float duration = 0.4f;
    
    private CanvasGroup cg;
    private Coroutine fxCoroutine;

    private void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 0f; 
    }

    public void ShowFX() => ShowFX(1f);

    public void ShowFX(float strength)
    {
        if (!isActiveAndEnabled || cg == null || duration <= 0f) return;
        if (fxCoroutine != null)
        {
            StopCoroutine(fxCoroutine);
        }

        fxCoroutine = StartCoroutine(ShowFXCo(Mathf.Clamp01(strength)));
    }

    private void OnDisable()
    {
        if (fxCoroutine != null) StopCoroutine(fxCoroutine);
        fxCoroutine = null;
        if (cg != null) cg.alpha = 0f;
    }

    private IEnumerator ShowFXCo(float strength)
    {
        float current = 0f;

        while (current < duration)
        {
            cg.alpha = Mathf.Lerp(strength, 0f, current / duration);
            
            current += Time.deltaTime;
            yield return null;
        }

        cg.alpha = 0f;
        fxCoroutine = null;
    }
}
