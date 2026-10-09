using UnityEngine;

namespace Hortensia.Narrative
{
    /// <summary>
    /// Defines an object the player can examine in 3D (rotate + read a
    /// description) without picking it up. Assign the Model prefab that
    /// should appear on the inspection stage - keep it separate from any
    /// scene instance, since ObjectInspector instantiates a fresh copy each
    /// time.
    /// </summary>
    [CreateAssetMenu(fileName = "NewInspectable", menuName = "Hortensia/Inspectable Item")]
    public sealed class InspectableItemDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private GameObject model;
        [SerializeField, TextArea(3, 8)] private string description;
        [Tooltip("Extra scale multiplier for the model on the inspection stage, in case its natural size makes auto-framing look wrong.")]
        [SerializeField] private float scaleMultiplier = 1f;
        [Tooltip("Corrective rotation (Euler degrees) applied before the player's own drag rotation. Use this if the model appears tilted/sideways instead of upright - some source assets bake in an orientation that isn't upright at identity rotation. Adjust by trial and error.")]
        [SerializeField] private Vector3 modelRotationOffset = Vector3.zero;
        [Tooltip("If Model is actually a whole multi-object scene (common with Sketchfab downloads), type the exact name of the ONE child object to isolate - everything else in the hierarchy is discarded at runtime. Leave empty if Model is already just the single object.")]
        [SerializeField] private string targetChildName;

        public string DisplayName => displayName;
        public GameObject Model => model;
        public string Description => description;
        public float ScaleMultiplier => scaleMultiplier;
        public Vector3 ModelRotationOffset => modelRotationOffset;
        public string TargetChildName => targetChildName;
    }
}