using UnityEngine;

[System.Serializable]
public class ShieldAction : GadgetUseAction
{
    [Min(1)] public int shieldValue = 100;

    protected override bool OnExecute(GameObject target)
    {
        return target != null && target.TryGetComponent<IShieldReceiver>(out var receiver)
            && receiver.TryAddShield(shieldValue);
    }
}
