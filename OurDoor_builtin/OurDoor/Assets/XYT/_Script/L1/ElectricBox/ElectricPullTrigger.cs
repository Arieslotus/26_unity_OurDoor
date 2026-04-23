using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElectricPullTrigger : MonoBehaviour
{
    private Renderer rend;


    public bool isCurrentOn = false;

    private void Start()
    {
        rend = GetComponent<Renderer>();
        rend.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCurrentOn) return;

        if (other.CompareTag("ElectricPull"))
        {
            isCurrentOn = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (isCurrentOn) return;
        if (other.CompareTag("ElectricPull"))
        {
            isCurrentOn = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if(!isCurrentOn) return;
        if (other.CompareTag("ElectricPull"))
        {
            isCurrentOn = false;
        }
    }
}
