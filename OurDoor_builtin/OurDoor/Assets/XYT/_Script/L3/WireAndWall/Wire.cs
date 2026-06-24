using UnityEngine;

public class Wire : MonoBehaviour
{
    [Header("ÌáÊ¾Á£×Ó")]
    public ParticleSystem hintParticle;

    private void Start()
    {
        RefreshState();

        if (L3Manager.Instance != null)
        {
            L3Manager.Instance.OnMetalPieceFound += OnMetalPieceFound;
        }
    }

    private void OnDestroy()
    {
        if (L3Manager.Instance != null)
        {
            L3Manager.Instance.OnMetalPieceFound -= OnMetalPieceFound;
        }
    }

    void OnMetalPieceFound()
    {
        RefreshState();
    }

    void RefreshState()
    {
        if (hintParticle == null)
            return;

        bool show = L3Manager.Instance != null &&
                    L3Manager.Instance.MetalPieceFound;

        if (show)
        {
            if (!hintParticle.isPlaying)
                hintParticle.Play();
        }
        else
        {
            hintParticle.Stop(true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}