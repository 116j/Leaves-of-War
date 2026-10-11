using UnityEngine;

public abstract class RevealableObject : MonoBehaviour
{
    [SerializeField]
    protected float _revealThreshold = 0.05f;
    [SerializeField]
    protected float _hideThreshold = 0.6f;

    public bool IsRevealed { get; private set; }
    public float Coverage { get; private set; } = 1f;

    public virtual bool HasFootprint => false;
    public virtual Bounds Footprint => default;

    public void SetCoverage(float coverage)
    {
        Coverage = coverage;
        if (!IsRevealed && coverage <= _revealThreshold)
        {
            IsRevealed = true;
            OnRevealChanged(true);
        }
        else if (IsRevealed && coverage >= _hideThreshold)
        {
            IsRevealed = false;
            OnRevealChanged(false);
        }
    }

    protected abstract void OnRevealChanged(bool revealed);
}
