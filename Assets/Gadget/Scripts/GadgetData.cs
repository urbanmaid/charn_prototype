using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GadgetData", menuName = "Gadget/GadgetData")]
public class GadgetData : ScriptableObject
{
    [Header("Abstract")]
    public string itemName;
    public Sprite itemIcon;
    public string description;

    [Header("Scriptable objects of item usage")]
    [SerializeReference, SubclassSelector] public GadgetUseAction command;

    [Header("Types and inventory action")]
    public GadgetActiveType gadgetType;
    public bool Stackable;

    public bool Use(GameObject target)
    {
        if (command == null)
        {
            Debug.LogWarning(
                $"Command of {itemName} has inappropriate action script.");
            return true;
        }
        else
        {
            var actor = Charm.Mechanism.Missions.CombatTelemetry.Capture(target);
            bool succeeded = command.Execute(target);
            if (succeeded) Charm.Mechanism.Missions.CombatTelemetry.GadgetUsed(actor);
            return succeeded;
        }
    }

    /// <summary>
    /// For the case that Interactionaction is used as key for Stage Interaction. This is intended to be used if (overrideValue != 0).
    /// </summary>
    public void OverrideValue(int overrideValue)
    {
        if (command is HealAction)
        {
            command = new HealAction(overrideValue);
        }
        else if (command is ShieldAction)
        {
            command = new ShieldAction { shieldValue = overrideValue };
        }
        else if(command is InteractionAction interactionAction)
        {
            InteractionAction ia = interactionAction.DeepCopy(overrideValue);
            //Debug.Log("Interaction Action value has overriden as: " + overrideValue);
            command = ia;
        }
    }
}

public enum GadgetActiveType
{
    Comsumable,//������ ���
    Passive,//����ٴϰ������� ȿ������
    Triggered//��ùߵ�
}