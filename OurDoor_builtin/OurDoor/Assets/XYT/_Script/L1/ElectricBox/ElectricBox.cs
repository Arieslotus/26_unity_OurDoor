using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElectricBox : MonoBehaviour
{
    List<ElectricBoxScoller> scollers = new();

    ElectricPullTrigger pull;


    bool isPowerOn = false;
    bool flag = false;

    public Light redLight, greenLight;

    private void Awake()
    {
        var scs = GetComponentsInChildren<ElectricBoxScoller>();
        scollers.AddRange(scs);

        pull = GetComponentInChildren<ElectricPullTrigger>();
    }

    private void Start()
    {
        //light
        greenLight.gameObject.SetActive(false);
        redLight.gameObject.SetActive(true);
    }

    private void Update()
    {
        // check
        bool isAllScollerOn = true;
        foreach (var scoller in scollers)
        {
            if (!scoller.isCurrentOn) isAllScollerOn = false;
        }

        bool isPullOn = false;
        if(pull!= null)
        {
            isPullOn = pull.isCurrentOn;
        }

        // is POWER ON ?
        if(isAllScollerOn && isPullOn)
        {
            if (!flag)
            {

                Level1Manager.Instance.SetPowerOn(true); // *

                //light
                greenLight.gameObject.SetActive(true);
                redLight.gameObject.SetActive(false);

                flag = true;
            }

        }
        else
        {
            if (flag)
            {
                Level1Manager.Instance.SetPowerOn(false); // *


                //light
                greenLight.gameObject.SetActive(false);
                redLight.gameObject.SetActive(true);

                flag = false;
            }

        }


    }
}
