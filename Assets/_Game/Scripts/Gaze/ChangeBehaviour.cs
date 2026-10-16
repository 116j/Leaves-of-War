using UnityEngine;

public abstract class ChangeBehaviour : MonoBehaviour
{
    public bool Changed => _changed;
    protected bool _changed;
    public abstract void OnChange();
    public virtual void Arm() => _changed = false;
}
