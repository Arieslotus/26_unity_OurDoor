using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElectricScollerTrigger : MonoBehaviour
{
    private Renderer rend;

    [Header("≈‰÷√")]
    public bool isOn = true;

    [Header("◊¥Ã¨")]
    public bool hasTriggered = false;

    private void Start()
    {
        rend = GetComponent<Renderer>();
        rend.enabled = false;


    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasTriggered) return;

        if (collision.gameObject.CompareTag("ElectricScoller"))
        {
            hasTriggered = true;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (hasTriggered) return;

        if (collision.gameObject.CompareTag("ElectricScoller"))
        {
            hasTriggered = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!hasTriggered) return;
        if (collision.gameObject.CompareTag("ElectricScoller"))
        {
            hasTriggered = false;
        }
    }

}
