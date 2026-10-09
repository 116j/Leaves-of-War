using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    public enum ManorRev5SurfaceRole
    {
        Walkable = 0,
        Barrier = 1,
        Ceiling = 2,
        SmoothStairEnvelope = 3
    }

    /// <summary>
    /// Binds one architectural renderer from the locked Rev5 import to the
    /// separate collider geometry that implements it. Decoration is covered by
    /// the model inventory hash; only surfaces with a physical role are listed.
    /// </summary>
    [Serializable]
    public sealed class ManorRev5SurfaceBinding
    {
        [SerializeField] private string id;
        [SerializeField] private ManorRev5SurfaceRole role;
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private List<Collider> structuralColliders =
            new List<Collider>();

        public string Id => id;
        public ManorRev5SurfaceRole Role => role;
        public Renderer VisualRenderer => visualRenderer;
        public IReadOnlyList<Collider> StructuralColliders => structuralColliders;
    }

    /// <summary>
    /// Scene-owned evidence for the Manor revision. This component has no
    /// runtime behavior; editor acceptance uses it to reject model drift and
    /// prove that every physical architectural surface has explicit ownership.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManorRev5GeometryManifest : MonoBehaviour
    {
        [Header("Locked source import")]
        [SerializeField] private Transform sourceModelRoot;
        [SerializeField] private string sourceAssetGuid;
        [SerializeField] private string sourceFbxSha256;
        [SerializeField] private string rendererInventorySha256;
        [SerializeField, Min(1)] private int expectedRendererCount;
        [SerializeField, Min(1)] private int expectedMeshFilterCount;
        [SerializeField, Min(1)] private int expectedMaterialSlotCount;
        [SerializeField, Min(1)] private int expectedTriangleCount;

        [Header("Physical ownership")]
        [SerializeField] private Transform generatedCollisionRoot;
        [SerializeField] private List<ManorRev5SurfaceBinding> surfaceBindings =
            new List<ManorRev5SurfaceBinding>();

        [Header("Required player route")]
        [SerializeField] private Transform routeStart;
        [SerializeField] private Transform stairBottom;
        [SerializeField] private Transform stairTop;
        [SerializeField] private Transform upperHall;
        [SerializeField] private Transform bedroom;

        public Transform SourceModelRoot => sourceModelRoot;
        public string SourceAssetGuid => sourceAssetGuid;
        public string SourceFbxSha256 => sourceFbxSha256;
        public string RendererInventorySha256 => rendererInventorySha256;
        public int ExpectedRendererCount => expectedRendererCount;
        public int ExpectedMeshFilterCount => expectedMeshFilterCount;
        public int ExpectedMaterialSlotCount => expectedMaterialSlotCount;
        public int ExpectedTriangleCount => expectedTriangleCount;
        public Transform GeneratedCollisionRoot => generatedCollisionRoot;
        public IReadOnlyList<ManorRev5SurfaceBinding> SurfaceBindings =>
            surfaceBindings;
        public Transform RouteStart => routeStart;
        public Transform StairBottom => stairBottom;
        public Transform StairTop => stairTop;
        public Transform UpperHall => upperHall;
        public Transform Bedroom => bedroom;
    }
}
