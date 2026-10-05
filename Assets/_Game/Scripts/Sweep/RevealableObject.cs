using UnityEngine;

public abstract class RevealableObject : MonoBehaviour
{
    [SerializeField]
    protected float _revealThreshold = 0.15f;
    [SerializeField]
    protected float _hideThreshold = 0.6f;

    /// <summary>
    /// Executes when an object reveals or hides
    /// </summary>
    /// <param name="revealed"></param>
    protected abstract void OnRevealChanged(bool revealed);
}
