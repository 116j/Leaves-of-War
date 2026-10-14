using UnityEngine;

public abstract class ChangeBehaviour : MonoBehaviour
{
    protected bool _changed;
    public abstract void OnChange();
}
