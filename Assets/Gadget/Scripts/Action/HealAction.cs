using UnityEngine;

[System.Serializable]
public class HealAction : GadgetUseAction
{
    [SerializeField, Min(1)] private int amount = 150;

    public HealAction(int amount)
    {
        this.amount = amount;
    }

    protected override bool OnExecute(GameObject target)
    {
        return target != null && target.TryGetComponent<IHealable>(out var receiver)
            && receiver.TryHeal(amount);
    }
}
