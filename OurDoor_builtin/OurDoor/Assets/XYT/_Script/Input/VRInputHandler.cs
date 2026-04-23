using UnityEngine;
using UnityEngine.InputSystem;

public class VRInputHandler : IInputHandler
{
    public InputAction interactAction;

    public bool InteractPressed()
    {
        return interactAction.triggered;
    }
}