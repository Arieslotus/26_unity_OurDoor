using UnityEngine;

public class PCInputHandler : IInputHandler
{
    public bool InteractPressed()
    {
        return Input.GetKeyDown(KeyCode.E);
    }
}