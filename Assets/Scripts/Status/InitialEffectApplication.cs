using System.Collections;
using UnityEngine;

public class InitialEffectApplication : MonoBehaviour
{
    private IngameEffectBehavior ie;

    [SerializeReference, SubclassSelector]
    [SerializeField] IngameEffectData[] applyingEffects;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(ApplyEffectCo());
    }

    IEnumerator ApplyEffectCo()
    {
        yield return new WaitForSeconds(0.25f);

        ie = GetComponent<IngameEffectBehavior>();
        if(ie != null)
        {
            foreach(IngameEffectData iedEach in applyingEffects)
            {
                ie.AddIngameEffect(iedEach);
            }
        }
        else
        {
            Debug.LogWarning("Cannot find ingame effects");
        }
    }
}
