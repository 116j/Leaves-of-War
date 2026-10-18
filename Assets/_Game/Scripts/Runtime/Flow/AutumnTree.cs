using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AutumnTree : MonoBehaviour
{
    private static readonly List<AutumnTree> All = new List<AutumnTree>();
    private static readonly string[] FoliageWords = { "leaf", "leaves", "foliage", "canopy", "green", "needle", "crown", "bush" };
    private static readonly Color[] Season =
    {
        new Color(0.2f, 0.38f, 0.16f), new Color(0.55f, 0.55f, 0.16f), new Color(0.78f, 0.47f, 0.12f),
        new Color(0.6f, 0.27f, 0.08f), new Color(0.36f, 0.21f, 0.11f)
    };
    private static Material invisible;
    private static Material leafMaterial;
    private static ParticleSystem clearing;

    public static bool Falling { get; private set; }

    [Tooltip("Empty: found by name (Canopy, Leaves, Foliage...).")]
    [SerializeField] private Renderer[] foliage = new Renderer[0];
    [SerializeField, Range(0.05f, 1f)] private float stepPerTurn = 0.22f;
    [SerializeField, Range(0f, 1f)] private float maxBeforeFall = 0.8f;
    [SerializeField, Min(0.1f)] private float colourSeconds = 1.2f;
    [SerializeField, Min(0)] private int leavesPerTurn = 45;
    [SerializeField, Min(0f)] private float leavesPerSecondOnFall = 70f;
    [SerializeField, Min(0.5f)] private float fallSeconds = 6f;
    [SerializeField] private Vector2 leafSize = new Vector2(0.12f, 0.22f);
    [SerializeField, Range(0.2f, 1f)] private float nightShade = 0.6f;
    [Tooltip("Material of the falling leaves. Empty: made at runtime.")]
    [SerializeField] private Material fallingLeaf;
    [Tooltip("Swapped onto the foliage once the tree is bare. Empty: made at runtime.")]
    [SerializeField] private Material bare;

    private readonly List<(Renderer renderer, int index, Color colour)> slots = new List<(Renderer, int, Color)>();
    private MaterialPropertyBlock block;
    private ParticleSystem leaves;
    private Bounds canopy;
    private float stage;
    private float shown;
    private bool fallen;

    private void OnEnable() => All.Add(this);

    private void OnDisable() => All.Remove(this);

    private void OnDestroy()
    {
        if (All.Count == 0)
            Falling = false;
    }

    private void Start()
    {
        Falling = false;
        block = new MaterialPropertyBlock();
        bool chosen = foliage != null && foliage.Length > 0;
        Renderer[] found = chosen ? foliage : FindFoliage(out chosen);
        bool any = false;
        foreach (Renderer r in found)
        {
            if (r == null)
                continue;
            Material[] materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !chosen && !IsFoliage(r, materials[i]) && !IsFoliageObject(r))
                    continue;
                slots.Add((r, i, BaseColour(materials[i])));
            }
            canopy = any ? Encapsulate(canopy, r.bounds) : r.bounds;
            any = true;
        }
        if (!any)
            canopy = new Bounds(transform.position + Vector3.up * 6f, new Vector3(3f, 3f, 3f));
        if (fallingLeaf != null)
            leafMaterial = fallingLeaf;
        if (bare != null)
            invisible = bare;
        leaves = BuildLeaves(transform, canopy, Mathf.Max(400, leavesPerTurn * 6 + Mathf.RoundToInt(leavesPerSecondOnFall * fallSeconds)), leafSize);
        leaves.Play();
    }

    private static Bounds Encapsulate(Bounds a, Bounds b)
    {
        a.Encapsulate(b);
        return a;
    }

    private Renderer[] FindFoliage(out bool fallback)
    {
        fallback = false;
        var result = new List<Renderer>();
        Renderer highest = null;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer)
                continue;
            if (highest == null || r.bounds.center.y > highest.bounds.center.y)
                highest = r;
            if (IsFoliageObject(r))
            {
                result.Add(r);
                continue;
            }
            foreach (Material m in r.sharedMaterials)
                if (m != null && IsFoliage(r, m))
                {
                    result.Add(r);
                    break;
                }
        }
        if (result.Count == 0 && highest != null)
        {
            result.Add(highest);
            fallback = true;
        }
        return result.ToArray();
    }

    private static bool IsFoliageObject(Renderer r)
    {
        string name = r.name.ToLowerInvariant();
        foreach (string word in FoliageWords)
            if (name.Contains(word))
                return true;
        return false;
    }

    private static bool IsFoliage(Renderer r, Material m)
    {
        string name = m.name.ToLowerInvariant();
        foreach (string word in FoliageWords)
            if (name.Contains(word))
                return true;
        Color c = BaseColour(m);
        return r.sharedMaterials.Length == 1 && c.g > c.r * 1.15f && c.g > c.b * 1.15f;
    }

    private static Color BaseColour(Material m)
    {
        if (m.HasProperty("_BaseColor"))
            return m.GetColor("_BaseColor");
        return m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
    }

    public static void TurnUnseen()
    {
        if (Falling)
            return;
        foreach (AutumnTree tree in All)
            if (tree != null && !Gaze.IsSeen(tree.canopy, 80f))
                tree.Advance();
    }

    public static void FallAll()
    {
        if (Falling)
            return;
        Falling = true;
        Vector3 from = Gaze.View != null ? Gaze.View.transform.position : Vector3.zero;
        Bounds area = default;
        bool any = false;
        foreach (AutumnTree tree in All)
        {
            if (tree == null)
                continue;
            float delay = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(tree.transform.position.x, 0f, tree.transform.position.z)) * 0.22f;
            tree.StartCoroutine(tree.Fall(delay));
            area = any ? Encapsulate(area, new Bounds(tree.transform.position, Vector3.one)) : new Bounds(tree.transform.position, Vector3.one);
            any = true;
        }
        if (!any)
            return;
        AutumnTree first = All[0];
        Vector3 centre = new Vector3(area.center.x, area.min.y + 10f, area.center.z);
        float side = Mathf.Max(area.size.x, area.size.z) * 0.8f;
        clearing = BuildLeaves(null, new Bounds(centre, new Vector3(side, 1f, side)), 5000, first.leafSize);
        clearing.name = "Clearing Leaves";
        ParticleSystem.EmissionModule emission = clearing.emission;
        emission.rateOverTime = 220f;
        ParticleSystem.MainModule main = clearing.main;
        main.duration = first.fallSeconds + 2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
        main.startColor = Colours(first.nightShade, 0.6f, 1f);
        clearing.Play();
    }

    private void Advance()
    {
        if (fallen)
            return;
        float before = stage;
        stage = Mathf.Min(maxBeforeFall, stage + stepPerTurn);
        if (stage > before && leaves != null)
        {
            ParticleSystem.MainModule main = leaves.main;
            main.startColor = Colours(nightShade, Mathf.Max(0f, stage - 0.2f), stage);
            leaves.Emit(leavesPerTurn);
        }
    }

    private IEnumerator Fall(float delay)
    {
        yield return new WaitForSeconds(delay);
        stage = 1f;
        if (leaves != null)
        {
            ParticleSystem.MainModule main = leaves.main;
            main.startColor = Colours(nightShade, 0.6f, 1f);
            ParticleSystem.EmissionModule emission = leaves.emission;
            emission.rateOverTime = leavesPerSecondOnFall;
            if (!leaves.isPlaying)
                leaves.Play();
        }
        yield return new WaitForSeconds(fallSeconds * 0.75f);
        fallen = true;
        Bare();
        yield return new WaitForSeconds(fallSeconds * 0.25f);
        if (leaves != null)
        {
            ParticleSystem.EmissionModule emission = leaves.emission;
            emission.rateOverTime = 0f;
        }
    }

    private void Bare()
    {
        if (invisible == null)
            invisible = BareMaterial();
        var empty = new MaterialPropertyBlock();
        foreach (var slot in slots)
        {
            if (slot.renderer == null)
                continue;
            Material[] materials = slot.renderer.sharedMaterials;
            if (slot.index < materials.Length)
                materials[slot.index] = invisible;
            slot.renderer.sharedMaterials = materials;
            slot.renderer.SetPropertyBlock(empty, slot.index);
        }
    }

    private void Update()
    {
        if (fallen || Mathf.Approximately(shown, stage))
            return;
        shown = Mathf.MoveTowards(shown, stage, Time.deltaTime / colourSeconds);
        Color season = Sample(shown);
        foreach (var slot in slots)
        {
            if (slot.renderer == null)
                continue;
            slot.renderer.GetPropertyBlock(block, slot.index);
            Color c = Color.Lerp(slot.colour, Tint(slot.colour, season), Mathf.Clamp01(shown * 1.6f));
            c.a = slot.colour.a;
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            slot.renderer.SetPropertyBlock(block, slot.index);
        }
    }

    private static Color Tint(Color original, Color season)
    {
        float brightness = Mathf.Max(0.05f, original.maxColorComponent);
        float target = Mathf.Max(0.05f, season.maxColorComponent);
        return original.maxColorComponent < 0.7f ? season : season * (brightness / target);
    }

    private static Color Sample(float t)
    {
        float scaled = Mathf.Clamp01(t) * (Season.Length - 1);
        int i = Mathf.Min(Season.Length - 2, Mathf.FloorToInt(scaled));
        return Color.Lerp(Season[i], Season[i + 1], scaled - i);
    }

    private static ParticleSystem.MinMaxGradient Colours(float shade, float from, float to)
    {
        Color a = Sample(from) * shade;
        Color b = Sample(to) * shade;
        a.a = b.a = 1f;
        return new ParticleSystem.MinMaxGradient(a, b);
    }

    private static ParticleSystem BuildLeaves(Transform parent, Bounds box, int max, Vector2 size)
    {
        var go = new GameObject("Falling Leaves");
        if (parent != null)
            go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(box.center, Quaternion.identity);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.045f;
        main.startColor = Colours(0.6f, 0f, 0.2f);

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(0.5f, box.size.x * 0.9f), Mathf.Max(0.3f, box.size.y * 0.6f), Mathf.Max(0.5f, box.size.z * 0.9f));

        ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 1.1f;
        limit.dampen = 0.3f;

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.7f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.2f;

        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);

        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.04f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = LeafMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    private static Material LeafMaterial()
    {
        if (leafMaterial == null)
            leafMaterial = NewLeafMaterial();
        return leafMaterial;
    }

    public static Material NewLeafMaterial()
    {
        Texture2D texture = Resources.Load<Texture2D>("MainMenu/GoldenLeaf");
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = new Material(shader != null ? shader : Shader.Find("Particles/Standard Unlit")) { name = "Falling Leaf" };
        m.SetTexture("_BaseMap", texture);
        m.mainTexture = texture;
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.35f);
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.EnableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    public static Material BareMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(shader != null ? shader : Shader.Find("Unlit/Transparent")) { name = "Bare Branches" };
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_ZWrite", 0f);
        m.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }
}
