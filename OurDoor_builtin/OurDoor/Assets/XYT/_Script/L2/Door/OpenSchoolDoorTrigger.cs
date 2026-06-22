using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit;

public class OpenSchoolDoorTrigger : MonoBehaviour
{
    SchoolDoorController controller;
    private void Awake()
    {
        controller = FindObjectOfType<SchoolDoorController>();
    }
    void OnTriggerEnter(Collider other)
    {
        SchoolKey key = other.GetComponent<SchoolKey>();

        if (key == null)
            return;

        XRGrabInteractable grab = key.GetComponent<XRGrabInteractable>();
        if (grab.isSelected)
        {
            grab.interactionManager.SelectExit(
                grab.firstInteractorSelecting,
                grab);
        }
        key.gameObject.SetActive(false); // close key

        controller.OpenDoor(); // *
        
    }
}
