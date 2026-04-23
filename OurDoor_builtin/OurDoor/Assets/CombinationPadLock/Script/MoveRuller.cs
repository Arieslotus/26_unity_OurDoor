// Script by Marcelli Michele

using System.Collections.Generic;
using UnityEngine;

public class MoveRuller : MonoBehaviour
{
    PadLockPassword _lockPassword;
    PadLockEmissionColor _pLockColor;

    [HideInInspector]
    public List <GameObject> _rullers = new List<GameObject>();
    private int _scroolRuller = 0;
    private int _changeRuller = 0;
    [HideInInspector]
    public int[] _numberArray = {0,0,0,0};

    private int _numberRuller = 0;

    private bool _isActveEmission = false;

    //[Header("VR Settings")]
    //public float snapAngle = 36f; // 每个数字对应的旋转角度
    //public float rotationSensitivity = 1.5f; // 旋转灵敏度
    //public float resetDelay = 0.2f; // 重置延迟4

    private Dictionary<GameObject, VRInteract_Ruller> rullerInteracts;


    InputMode mode;

    void Awake()
    {
        _lockPassword = FindObjectOfType<PadLockPassword>();
        _pLockColor = FindObjectOfType<PadLockEmissionColor>();

        _rullers.Add(GameObject.Find("Ruller1"));
        _rullers.Add(GameObject.Find("Ruller2"));
        _rullers.Add(GameObject.Find("Ruller3"));
        _rullers.Add(GameObject.Find("Ruller4"));

        foreach (GameObject r in _rullers)
        {
            r.transform.Rotate(-144, 0, 0, Space.Self);
        }

    }

    private void Start()
    {
        mode = InputManager.Instance.GetCurrentMode();
        if (mode == InputMode.VR)
        {
            // 初始化VR交互组件
            rullerInteracts = new Dictionary<GameObject, VRInteract_Ruller>();

            for (int i = 0; i < _rullers.Count; i++)
            {
                GameObject ruller = _rullers[i];
                // 添加VR交互组件
                VRInteract_Ruller interact = ruller.AddComponent<VRInteract_Ruller>();
                interact.rullerIndex = i;
                interact.moveRuller = this;
                rullerInteracts.Add(ruller, interact);
            }
        }
    }


    void Update()
    {
        if(mode == InputMode.PC)
        {
            MoveRulles();
            RotateRullers();
        }
       

        _lockPassword.Password();
    }

    void MoveRulles()
    {
        if (Input.GetKeyDown(KeyCode.D)) 
        {
            _isActveEmission = true;
            _changeRuller ++;
            _numberRuller += 1;

            if (_numberRuller > 3)
            {
                _numberRuller = 0;
            }
        }
        if (Input.GetKeyDown(KeyCode.A)) 
        {
            _isActveEmission = true;
            _changeRuller --;
            _numberRuller -= 1;

            if (_numberRuller < 0)
            {
                _numberRuller = 3;
            }
        }
        _changeRuller = (_changeRuller + _rullers.Count) % _rullers.Count;


        for (int i = 0; i < _rullers.Count; i++)
        {
            if (_isActveEmission)
            {
                if (_changeRuller == i)
                {

                    _rullers[i].GetComponent<PadLockEmissionColor>()._isSelect = true;
                    _rullers[i].GetComponent<PadLockEmissionColor>().BlinkingMaterial();
                }
                else
                {
                    _rullers[i].GetComponent<PadLockEmissionColor>()._isSelect = false;
                    _rullers[i].GetComponent<PadLockEmissionColor>().BlinkingMaterial();
                }
            }
        }

    }

    void RotateRullers()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            _isActveEmission = true;
            _scroolRuller = 36;
            _rullers[_changeRuller].transform.Rotate(-_scroolRuller, 0, 0, Space.Self);

            _numberArray[_changeRuller] += 1;

            if (_numberArray[_changeRuller] > 9)
            {
                _numberArray[_changeRuller] = 0;
            }
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            _isActveEmission = true;
            _scroolRuller = 36;
            _rullers[_changeRuller].transform.Rotate(_scroolRuller, 0, 0, Space.Self);

            _numberArray[_changeRuller] -= 1;

            if (_numberArray[_changeRuller] < 0)
            {
                _numberArray[_changeRuller] = 9;
            }
        }
    }


    // ========== VR交互方法 ==========
    public void UpdateNumber(int rullerIndex, int delta)
    {
        // 更新数字
        _numberArray[rullerIndex] += delta;

        // 循环处理
        if (_numberArray[rullerIndex] > 9)
            _numberArray[rullerIndex] = 0;
        if (_numberArray[rullerIndex] < 0)
            _numberArray[rullerIndex] = 9;

        // 触发高亮效果
        _isActveEmission = true;
        _changeRuller = rullerIndex;

        // 更新高亮显示
        for (int i = 0; i < _rullers.Count; i++)
        {
            if (_rullers[i].GetComponent<PadLockEmissionColor>() != null)
            {
                _rullers[i].GetComponent<PadLockEmissionColor>()._isSelect = (i == rullerIndex);
                _rullers[i].GetComponent<PadLockEmissionColor>().BlinkingMaterial();
            }
        }

        Debug.Log($"滚轮{rullerIndex} 数字改为: {_numberArray[rullerIndex]}");
    }

    //public void SnapToNearestNumber(GameObject ruller, int rullerIndex)
    //{
    //    // 获取当前旋转角度
    //    Vector3 currentRotation = ruller.transform.localEulerAngles;
    //    float currentAngle = currentRotation.x;

    //    // 标准化角度到0-360
    //    if (currentAngle > 180) currentAngle -= 360;

    //    // 计算应该吸附到哪个数字
    //    int targetNumber = _numberArray[rullerIndex];
    //    float targetAngle = -targetNumber * snapAngle - 144;

    //    // 平滑旋转到目标角度
    //    StartCoroutine(SmoothRotate(ruller, currentAngle, targetAngle));
    //}

    //private System.Collections.IEnumerator SmoothRotate(GameObject ruller, float fromAngle, float toAngle)
    //{
    //    float elapsed = 0;
    //    Quaternion startRot = ruller.transform.localRotation;
    //    Quaternion endRot = Quaternion.Euler(toAngle, 0, 0);

    //    while (elapsed < resetDelay)
    //    {
    //        elapsed += Time.deltaTime;
    //        float t = elapsed / resetDelay;
    //        ruller.transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
    //        yield return null;
    //    }

    //    ruller.transform.localRotation = endRot;
    //}
}
