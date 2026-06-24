using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

public class DoorGapReceiver : MonoBehaviour
{
    [Header("内部铁片")]
    public GameObject transferMetalPiece;


    private bool hasTransfered = false;

    private void Start()
    {
        if (transferMetalPiece != null)
            transferMetalPiece.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTransfered)
            return;

        MetalPiece metal = other.GetComponent<MetalPiece>();

        if (metal == null)
            return;

        if (metal.isPicking)
        {
            GiveMetalPiece(other.gameObject);
        }
    }

    public void  GiveMetalPiece(GameObject outsideMetal)
    {
        if (hasTransfered)
            return;

        hasTransfered = true;

        StartCoroutine(TransferRoutine(outsideMetal));
    }

    IEnumerator TransferRoutine(GameObject outsideMetal)
    {
        if(outsideMetal != null)
        {
            XRGrabInteractable grab =
outsideMetal.GetComponent<XRGrabInteractable>();

            if (grab != null)
            {
                if (grab.isSelected)
                {
                    var manager = FindObjectOfType<XRInteractionManager>();

                    if (manager != null)
                    {
                        manager.SelectExit(
                            grab.firstInteractorSelecting,
                            grab);
                    }
                }
            }

            yield return null;

            // 删除外面的铁片
            Destroy(outsideMetal);
        }


        // 激活动画铁片
        transferMetalPiece.SetActive(true);



    }
}