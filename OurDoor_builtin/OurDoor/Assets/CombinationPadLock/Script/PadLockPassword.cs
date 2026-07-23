// Script by Marcelli Michele

using System.Linq;
using TMPro;
using UnityEngine;

public class PadLockPassword : MonoBehaviour
{
    MoveRuller _moveRull;

    public int[] _numberPassword = {0,0,0,0};

    bool hasUnlock = false;

    public TextMeshPro text;

    private void Awake()
    {
        _moveRull = FindObjectOfType<MoveRuller>();
    }

    public void Password()
    {
        if (text != null)
        {
            text.text = $"{_moveRull._numberArray[0]}{_moveRull._numberArray[1]}{_moveRull._numberArray[2]}{_moveRull._numberArray[3]}";
        }

        if (_moveRull._numberArray.SequenceEqual(_numberPassword))
        {
            if (!hasUnlock)
            {
                // Here enter the event for the correct combination
                Debug.Log("Password correct");

                //
                OurDoorLevelActionGateway.RequestLevel1LockOpened();

                // Es. Below the for loop to disable Blinking Material after the correct password
                for (int i = 0; i < _moveRull._rullers.Count; i++)
                {
                    _moveRull._rullers[i].GetComponent<PadLockEmissionColor>()._isSelect = false;
                    _moveRull._rullers[i].GetComponent<PadLockEmissionColor>().BlinkingMaterial();
                }

                hasUnlock = true;
            }


        }
    }
}
