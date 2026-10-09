using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Declares how a location owns its visible and physical architecture.
    /// Final production art uses separate roots so imported render meshes can
    /// never silently become gameplay collision. Intentional greybox locations
    /// may keep renderers and primitive colliders beneath one shared root.
    /// </summary>
    public enum SceneGeometryOwnershipMode
    {
        SeparateVisualAndStructuralRoots = 0,
        SharedVisualAndStructuralRoot = 1
    }

    public enum SceneGeometrySolidProbeMode
    {
        VisibleFinalArt = 0,
        AuthoredCollisionEnvelope = 1
    }

    /// <summary>
    /// An authored route whose points describe the position of the player's
    /// feet. Geometry acceptance walks the route with a clone of the scene's
    /// production CharacterController. Keeping the route in scene data makes
    /// important paths explicit instead of inferring them from imported mesh
    /// names or object layout.
    /// </summary>
    [Serializable]
    public sealed class SceneGeometryTraversalProbe
    {
        [SerializeField] private string id;
        [SerializeField] private ChapterMask activeInChapters = ChapterMask.All;
        [SerializeField] private bool requireReverse = true;
        [SerializeField] private List<Transform> waypoints = new List<Transform>();

        public string Id => id;
        public ChapterMask ActiveInChapters => activeInChapters;
        public bool RequireReverse => requireReverse;
        public IReadOnlyList<Transform> Waypoints => waypoints;

        public bool IsActiveIn(int chapterIndex)
        {
            if (chapterIndex < 1 || chapterIndex > 7)
                return false;

            ChapterMask chapter = (ChapterMask)(1 << (chapterIndex - 1));
            return (activeInChapters & chapter) != 0;
        }
    }

    /// <summary>
    /// A deliberate attempt to walk the production player capsule from a
    /// reachable point through an architectural boundary. Acceptance succeeds
    /// only when structural collision stops the attempt. Optional expected
    /// colliders make wall and corner-seam ownership explicit; listing both
    /// adjoining walls is useful when either may receive the first contact.
    /// </summary>
    [Serializable]
    public sealed class SceneGeometryBarrierProbe
    {
        [SerializeField] private string id;
        [SerializeField] private ChapterMask activeInChapters = ChapterMask.All;
        [SerializeField] private Transform insidePoint;
        [SerializeField] private Transform outsidePoint;
        [SerializeField] private List<Collider> expectedBarriers =
            new List<Collider>();

        public string Id => id;
        public ChapterMask ActiveInChapters => activeInChapters;
        public Transform InsidePoint => insidePoint;
        public Transform OutsidePoint => outsidePoint;
        public IReadOnlyList<Collider> ExpectedBarriers => expectedBarriers;

        public bool IsActiveIn(int chapterIndex)
        {
            if (chapterIndex < 1 || chapterIndex > 7)
                return false;

            ChapterMask chapter = (ChapterMask)(1 << (chapterIndex - 1));
            return (activeInChapters & chapter) != 0;
        }
    }

    /// <summary>
    /// A visual assembly that must rest on production structural collision.
    /// Optional contact points are the most precise representation and should
    /// sit at the assembly's intended lowest contacts. Without them, editor
    /// acceptance samples the lowest referenced renderer bounds.
    /// </summary>
    [Serializable]
    public sealed class SceneGeometrySupportProbe
    {
        [SerializeField] private string id;
        [SerializeField] private ChapterMask activeInChapters = ChapterMask.All;
        [SerializeField] private List<Renderer> renderers = new List<Renderer>();
        [SerializeField] private List<Transform> contactPoints = new List<Transform>();
        [SerializeField, Min(0f)] private float maximumGap = 0.08f;
        [SerializeField, Min(0f)] private float maximumPenetration = 0.08f;
        [SerializeField, Min(0.1f)] private float probeDistance = 3f;

        public string Id => id;
        public ChapterMask ActiveInChapters => activeInChapters;
        public IReadOnlyList<Renderer> Renderers => renderers;
        public IReadOnlyList<Transform> ContactPoints => contactPoints;
        public float MaximumGap => maximumGap;
        public float MaximumPenetration => maximumPenetration;
        public float ProbeDistance => probeDistance;

        public bool IsActiveIn(int chapterIndex)
        {
            if (chapterIndex < 1 || chapterIndex > 7)
                return false;

            ChapterMask chapter = (ChapterMask)(1 << (chapterIndex - 1));
            return (activeInChapters & chapter) != 0;
        }
    }

    /// <summary>
    /// Binds a production renderer that players must not pass through to the
    /// invisible structural collider(s) that implement that boundary. This is
    /// deliberately explicit: imported render geometry remains visual-only,
    /// while acceptance can still prove that important final-art walls and
    /// props have matching physical ownership.
    /// </summary>
    [Serializable]
    public sealed class SceneGeometrySolidVisualProbe
    {
        [SerializeField] private string id;
        [SerializeField] private ChapterMask activeInChapters = ChapterMask.All;
        [SerializeField] private SceneGeometrySolidProbeMode probeMode =
            SceneGeometrySolidProbeMode.VisibleFinalArt;
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private List<Collider> structuralColliders =
            new List<Collider>();
        [SerializeField, Min(0f)] private float boundsTolerance = 0.08f;

        public string Id => id;
        public ChapterMask ActiveInChapters => activeInChapters;
        public SceneGeometrySolidProbeMode ProbeMode => probeMode;
        public Renderer VisualRenderer => visualRenderer;
        public IReadOnlyList<Collider> StructuralColliders => structuralColliders;
        public float BoundsTolerance => boundsTolerance;

        public bool IsActiveIn(int chapterIndex)
        {
            if (chapterIndex < 1 || chapterIndex > 7)
                return false;

            ChapterMask chapter = (ChapterMask)(1 << (chapterIndex - 1));
            return (activeInChapters & chapter) != 0;
        }
    }

    /// <summary>
    /// A visible floor, landing, or stair assembly whose entire walkable mesh
    /// must be backed by structural collision. The editor validator samples
    /// every upward-facing triangle on a dense lattice rather than trusting a
    /// few hand-authored points.
    /// </summary>
    [Serializable]
    public sealed class SceneGeometryFloorCoverageProbe
    {
        [SerializeField] private string id;
        [SerializeField] private ChapterMask activeInChapters = ChapterMask.All;
        [SerializeField] private Renderer visualSurface;
        [SerializeField, Range(0f, 1f)] private float minimumWalkableNormalY = 0.7f;
        [SerializeField, Min(0.05f)] private float sampleSpacing = 0.22f;
        [SerializeField, Min(0f)] private float maximumGap = 0.08f;
        [SerializeField, Min(0f)] private float maximumPenetration = 0.08f;

        public string Id => id;
        public ChapterMask ActiveInChapters => activeInChapters;
        public Renderer VisualSurface => visualSurface;
        public float MinimumWalkableNormalY => minimumWalkableNormalY;
        public float SampleSpacing => sampleSpacing;
        public float MaximumGap => maximumGap;
        public float MaximumPenetration => maximumPenetration;

        public bool IsActiveIn(int chapterIndex)
        {
            if (chapterIndex < 1 || chapterIndex > 7)
                return false;

            ChapterMask chapter = (ChapterMask)(1 << (chapterIndex - 1));
            return (activeInChapters & chapter) != 0;
        }
    }

    /// <summary>
    /// Binds a named spawn to the production visual surface it is intended to
    /// stand on. Structural support alone is insufficient: a stale lower
    /// collider can otherwise make an under-floor spawn look valid.
    /// </summary>
    [Serializable]
    public sealed class SceneGeometrySpawnProbe
    {
        [SerializeField] private string id;
        [SerializeField] private Renderer visualFloorRenderer;
        [SerializeField] private Transform visualFloorContactPoint;

        public string Id => id;
        public Renderer VisualFloorRenderer => visualFloorRenderer;
        public Transform VisualFloorContactPoint => visualFloorContactPoint;
    }

    /// <summary>
    /// Data-only scene contract consumed by editor geometry acceptance. It is
    /// intentionally free of runtime behavior: production roots and critical
    /// routes are authored once in the scene, while the editor validator owns
    /// all temporary physics simulation and diagnostics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneGeometryAcceptanceContract : MonoBehaviour
    {
        [Header("Validation context")]
        [SerializeField, Range(1, 7)] private int validationChapter = 1;
        [SerializeField] private ChapterMask validationChapters = ChapterMask.None;
        [SerializeField] private CharacterController playerController;
        [SerializeField] private LayerMask traversalMask = ~0;

        [Header("Geometry roles")]
        [SerializeField] private SceneGeometryOwnershipMode geometryOwnership =
            SceneGeometryOwnershipMode.SeparateVisualAndStructuralRoots;
        [SerializeField] private Transform productionVisualRoot;
        [SerializeField] private List<Transform> additionalProductionVisualRoots =
            new List<Transform>();
        [SerializeField] private Transform structuralCollisionRoot;
        [SerializeField] private List<Transform> architecturalGreyboxRoots =
            new List<Transform>();
        [SerializeField, Min(1)] private int maximumStructuralColliderCount = 128;

        [Header("Spawn contract")]
        [SerializeField] private List<string> expectedSpawnPointIds = new List<string>
        {
            "front_gate",
            "entry_hall",
            "bedroom",
            "consulting_room",
            "garden",
            "greenhouse"
        };
        [SerializeField] private List<SceneGeometrySpawnProbe> spawnProbes =
            new List<SceneGeometrySpawnProbe>();
        [SerializeField, Min(0f)] private float maximumSpawnGroundGap = 0.10f;
        [SerializeField, Min(0f)] private float maximumSpawnGroundPenetration = 0.08f;

        [Header("Traversal")]
        [SerializeField, Min(0.02f)] private float movementStep = 0.08f;
        [SerializeField, Min(0.01f)] private float endpointTolerance = 0.15f;
        [SerializeField, Min(0.02f)] private float downwardGroundSnap = 0.16f;
        [SerializeField] private List<SceneGeometryTraversalProbe> traversalProbes =
            new List<SceneGeometryTraversalProbe>();

        [Header("Containment")]
        [SerializeField] private List<SceneGeometryBarrierProbe> barrierProbes =
            new List<SceneGeometryBarrierProbe>();

        [Header("Visual grounding")]
        [SerializeField] private List<SceneGeometrySupportProbe> supportProbes =
            new List<SceneGeometrySupportProbe>();

        [Header("Final-art collision coverage")]
        [SerializeField] private List<SceneGeometrySolidVisualProbe> solidVisualProbes =
            new List<SceneGeometrySolidVisualProbe>();
        [SerializeField] private List<SceneGeometryFloorCoverageProbe> floorCoverageProbes =
            new List<SceneGeometryFloorCoverageProbe>();
        [SerializeField, Min(0)] private int minimumSolidVisualProbeCount;
        [SerializeField, Min(0)] private int minimumFloorCoverageProbeCount;

        public int ValidationChapter => validationChapter;
        /// <summary>
        /// Every authored dressing state this reused location must validate.
        /// Existing contracts with no mask retain their former single-chapter
        /// behavior through <see cref="ValidationChapter"/>.
        /// </summary>
        public ChapterMask ValidationChapters
        {
            get
            {
                if (validationChapters != ChapterMask.None)
                    return validationChapters & ChapterMask.All;
                if (validationChapter < 1 || validationChapter > 7)
                    return ChapterMask.None;
                return (ChapterMask)(1 << (validationChapter - 1));
            }
        }
        public ChapterMask AuthoredValidationChapters => validationChapters;
        public bool UsesLegacyValidationChapter =>
            validationChapters == ChapterMask.None;
        public CharacterController PlayerController => playerController;
        public int TraversalMask => traversalMask.value;
        public SceneGeometryOwnershipMode GeometryOwnership => geometryOwnership;
        public Transform ProductionVisualRoot => productionVisualRoot;
        public IReadOnlyList<Transform> AdditionalProductionVisualRoots =>
            additionalProductionVisualRoots;
        public Transform StructuralCollisionRoot => structuralCollisionRoot;
        public IReadOnlyList<Transform> ArchitecturalGreyboxRoots => architecturalGreyboxRoots;
        public int MaximumStructuralColliderCount => maximumStructuralColliderCount;
        public IReadOnlyList<string> ExpectedSpawnPointIds => expectedSpawnPointIds;
        public IReadOnlyList<SceneGeometrySpawnProbe> SpawnProbes => spawnProbes;
        public float MaximumSpawnGroundGap => maximumSpawnGroundGap;
        public float MaximumSpawnGroundPenetration => maximumSpawnGroundPenetration;
        public float MovementStep => movementStep;
        public float EndpointTolerance => endpointTolerance;
        public float DownwardGroundSnap => downwardGroundSnap;
        public IReadOnlyList<SceneGeometryTraversalProbe> TraversalProbes => traversalProbes;
        public IReadOnlyList<SceneGeometryBarrierProbe> BarrierProbes => barrierProbes;
        public IReadOnlyList<SceneGeometrySupportProbe> SupportProbes => supportProbes;
        public IReadOnlyList<SceneGeometrySolidVisualProbe> SolidVisualProbes =>
            solidVisualProbes;
        public IReadOnlyList<SceneGeometryFloorCoverageProbe> FloorCoverageProbes =>
            floorCoverageProbes;
        public int MinimumSolidVisualProbeCount => minimumSolidVisualProbeCount;
        public int MinimumFloorCoverageProbeCount => minimumFloorCoverageProbeCount;
    }
}
