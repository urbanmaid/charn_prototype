using System;
using UnityEngine;

[Serializable]
public abstract class GadgetUseAction
{
    protected abstract bool OnExecute(
        GameObject target);

    public bool Execute(GameObject target)
    {
        return OnExecute(target);
    }
}
public enum GadgetType
{
    Heal,
    Shield,
    Buff,
    EnemyDebuff,
    Damage,
    Interaction
}
