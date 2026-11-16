using System.Collections.Generic;
using UnityEngine;

public interface IInteractable
{
    public void Interact(bool asActive);
    public bool InteractWithGadget(GadgetUseAction gua);

    // For Getting Params
    public int GetInteractValue();
    bool IsPlayerInRange();

}
