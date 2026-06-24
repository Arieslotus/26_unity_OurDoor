using UnityEngine;

public class RemoveAnimator : MonoBehaviour
{
    public void RemoveAnimatorComponent()
    {
        Animator animator = GetComponent<Animator>();

        if (animator != null)
        {
            Destroy(animator);
        }
    }
}