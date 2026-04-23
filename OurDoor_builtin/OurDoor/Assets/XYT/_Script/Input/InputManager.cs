using UnityEngine;

public enum InputMode { PC, VR }

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    public static IInputHandler Input { get; private set; }


    public InputMode mode;

    private void Awake()
    {
        // 单例控制
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 跨场景不销毁
        DontDestroyOnLoad(gameObject);

        // 初始化输入
        InitInput();
    }

    void InitInput()
    {
        if (mode == InputMode.PC)
        {
            Input = new PCInputHandler();
        }
        else
        {
            Input = new VRInputHandler();
        }
    }


    //

    public InputMode GetCurrentMode()
    {
        return mode;
    }
}
//if (InputManager.Input.InteractPressed())
//{
//    OnPulled();
//}