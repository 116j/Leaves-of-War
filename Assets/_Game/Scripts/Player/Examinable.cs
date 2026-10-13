using Hortensia.Runtime;
using UnityEngine;

[DisallowMultipleComponent]
public class Examinable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "EXAMINE";
    [SerializeField] private string displayName;
    [SerializeField, TextArea(3, 8)] private string caption;
    [Tooltip("Optional model shown in the inspection view. Empty: a copy of this object.")]
    [SerializeField] private GameObject model;
    [Tooltip("Extra scale for the model on the inspection stage.")]
    [SerializeField] private float scaleMultiplier = 1f;
    [Tooltip("Extra rotation on the inspection stage. Flat objects (papers, drawings) already turn to face the camera.")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;
    [Tooltip("Optional: Ungaretti's voice reading the description.")]
    [SerializeField] private AudioClip voice;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.55f;

    public static bool IsOpen => ObjectInspector.Instance != null && ObjectInspector.Instance.IsOpen;

    public string Prompt => IsOpen ? string.Empty : (prompt + " " + DisplayName.ToUpperInvariant()).Trim();
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName : CleanName(name);
    public string Description => caption;
    public GameObject Model => model != null ? model : gameObject;
    public bool UsesSelf => model == null;
    public float ScaleMultiplier => scaleMultiplier;
    public Vector3 RotationOffset => rotationOffset;
    public AudioClip Voice => voice;
    public float VoiceVolume => voiceVolume;

    private void Awake()
    {
        if (GetComponent<Collider>() == null)
            gameObject.AddComponent<BoxCollider>();
    }

    private static string CleanName(string raw)
    {
        int bracket = raw.IndexOf('(');
        if (bracket > 0)
            raw = raw.Substring(0, bracket);
        return raw.Replace("Placeholder", string.Empty).Trim();
    }

    public void Interact()
    {
        if (!IsOpen)
            ObjectInspector.EnsureInstance().Inspect(this);
    }
}
