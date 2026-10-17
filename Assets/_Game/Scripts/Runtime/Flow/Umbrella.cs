using UnityEngine;

namespace Hortensia.Runtime
{
    public sealed class Umbrella : MonoBehaviour
    {
        [SerializeField] private GameObject closed;
        [SerializeField] private GameObject open;
        [Tooltip("The umbrella opens when this character starts walking away.")]
        [SerializeField] private DialogueSpeaker speaker;
        [Tooltip("The character the umbrella stays with (it keeps its offset from him).")]
        [SerializeField] private Transform follow;
        [SerializeField] private AudioClip openClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private bool opened;
        private Vector3 localPosition;
        private Quaternion localRotation;

        private void Start()
        {
            if (closed != null)
                closed.SetActive(true);
            if (open != null)
                open.SetActive(false);
            if (follow != null)
            {
                localPosition = follow.InverseTransformPoint(transform.position);
                localRotation = Quaternion.Inverse(follow.rotation) * transform.rotation;
            }
        }

        private void LateUpdate()
        {
            if (follow == null)
                return;
            bool shown = follow.gameObject.activeInHierarchy;
            if (closed != null && closed.activeSelf && !shown)
                closed.SetActive(false);
            if (open != null && open.activeSelf && !shown)
                open.SetActive(false);
            if (shown)
                transform.SetPositionAndRotation(follow.TransformPoint(localPosition), follow.rotation * localRotation);
        }

        private void Update()
        {
            if (opened || speaker == null || !speaker.IsWalking)
                return;
            opened = true;
            if (closed != null)
                closed.SetActive(false);
            if (open != null)
                open.SetActive(true);
            if (openClip != null)
                AudioSource.PlayClipAtPoint(openClip, transform.position, volume);
        }
    }
}
