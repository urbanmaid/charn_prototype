using System.Collections;
using UnityEngine;

[System.Serializable] 
public class EffectApplicationAction : GadgetUseAction
{
    [SerializeReference, SubclassSelector]
    [SerializeField] IngameEffectData[] applyingEffects;
    
    protected override bool OnExecute(GameObject target)
    {
        ApplyEffectCo(target);
        return true;
    }

    void ApplyEffectCo(GameObject target)
    {
        IngameEffectBehavior ie = target.GetComponent<IngameEffectBehavior>();
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