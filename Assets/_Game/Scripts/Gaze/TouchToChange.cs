using System;
using Hortensia.Runtime;
using UnityEngine;

public sealed class TouchToChange : MonoBehaviour, IInteractable
{
    private ChangeBehaviour change;
    private Action touched;
    private BoxCollider added;
    private bool done;

    public string Prompt => done ? string.Empty : "TOUCH";

    public static TouchToChange Attach(Observable target, ChangeBehaviour change, Action touched)
    {
        var touch = target.gameObject.AddComponent<TouchToChange>();
        touch.change = change;
        touch.touched = touched;
        if (target.GetComponentInChildren<Collider>() == null)
        {
            Bounds bounds = default;
            bool any = false;
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                    bounds = r.bounds;
                else
                    bounds.Encapsulate(r.bounds);
                any = true;
            }
            if (any)
            {
                touch.added = target.gameObject.AddComponent<BoxCollider>();
                touch.added.center = target.transform.InverseTransformPoint(bounds.center);
                Vector3 scale = target.transform.lossyScale;
                touch.added.size = new Vector3(
                    bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                    bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                    bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
            }
        }
        return touch;
    }

    public void Interact()
    {
        if (done || change == null)
            return;
        done = true;
        touched?.Invoke();
        change.OnChange();
    }

    private void OnDestroy()
    {
        if (added != null)
            Destroy(added);
    }
}
