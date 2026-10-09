using UnityEngine;
using UnityEngine.InputSystem;

namespace Hortensia.Runtime
{
    public sealed class TestRespawn : MonoBehaviour
    {
        [Tooltip("Object to bring back (e.g. Carlo). Put this script on a DIFFERENT object, which stays active.")]
        [SerializeField] private GameObject target;
        [SerializeField] private Key key = Key.R;

        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Start()
        {
            if (target == null)
                return;
            startPosition = target.transform.position;
            startRotation = target.transform.rotation;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (target == null || keyboard == null || !keyboard[key].wasPressedThisFrame || DialogueRunner.IsPlaying)
                return;

            target.transform.SetPositionAndRotation(startPosition, startRotation);
            target.SetActive(true);
            foreach (DialogueTrigger trigger in target.GetComponentsInChildren<DialogueTrigger>(true))
                trigger.ResetPlayed();
        }
    }
}
