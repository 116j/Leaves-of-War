using UnityEngine;

public abstract  class KeyboardControl :MonoBehaviour
{
    [SerializeField]
    protected KeyCode _interactionKey;

    protected virtual void Update()
    {
        if (Input.GetKeyDown(_interactionKey))
        {
            OnKeyPressed(_interactionKey);
        }
    }
    public abstract void OnKeyPressed(KeyCode _keyCode);


}
