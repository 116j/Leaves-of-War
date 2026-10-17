using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public class LeafField : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Leaf material with GPU instancing and alpha clipping. Created from the leaf texture when empty.")]
    [SerializeField] private Material leafMaterial;
    [SerializeField] private Texture2D leafTexture;
    [SerializeField] private Color[] tints = { new Color(1f, 0.92f, 0.7f), new Color(1f, 0.7f, 0.4f), new Color(0.75f, 0.5f, 0.3f) };
    [SerializeField] private Vector2 leafSize = new Vector2(0.12f, 0.24f);
    [SerializeField, Range(0f, 40f)] private float tilt = 12f;

    [Header("Amount")]
    [Tooltip("Cover a box around every poem on the floor. Off: use this object's position and Area Size.")]
    [SerializeField] private bool autoArea = true;
    [SerializeField] private Vector2 areaSize = new Vector2(20f, 10f);
    [SerializeField, Min(0f)] private float areaMargin = 3f;
    [SerializeField, Min(1f)] private float leavesPerSquareMeter = 45f;
    [Tooltip("Extra leaves piled on top of each poem.")]
    [SerializeField, Min(0f)] private float poemPileMultiplier = 3f;
    [SerializeField, Min(100)] private int maxLeaves = 20000;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Motion")]
    [SerializeField, Min(0.1f)] private float friction = 4.5f;
    [SerializeField, Min(0.1f)] private float gravity = 3f;
    [SerializeField] private Vector2 pushSpeed = new Vector2(0.8f, 3.5f);
    [SerializeField, Min(0.1f)] private float fullSweepSpeed = 3f;
    [SerializeField, Range(0f, 1f)] private float forwardBias = 0.55f;
    [Tooltip("Seconds a swept leaf keeps flying before it starts to vanish.")]
    [SerializeField, Min(0f)] private float vanishDelay = 0.6f;
    [SerializeField, Min(0.05f)] private float vanishDuration = 0.4f;

    [Header("Look Away")]
    [Tooltip("Swept leaves come back to their place where the player is not looking, like in the main menu.")]
    [SerializeField] private bool returnWhenUnseen = true;
    [Tooltip("Seconds the floor must stay out of view before the swept leaves come back.")]
    [SerializeField, Min(0f)] private float floorUnseenDelay = 3f;
    [Tooltip("How much of the screen edge does not count when checking if the floor is in view.")]
    [SerializeField, Range(0f, 0.4f)] private float floorViewMargin = 0.1f;
    [Tooltip("How many leaves per second come back.")]
    [SerializeField, Min(1f)] private float returnPerSecond = 60f;

    public static LeafField Instance { get; private set; }
    public Bounds Area => area;

    private struct Spot
    {
        public RevealableObject Target;
        public Rect Rect;
        public int Initial;
    }

    private Vector3[] position;
    private Vector2[] velocity;
    private float[] lift;
    private float[] liftVelocity;
    private float[] yaw;
    private float[] spin;
    private Quaternion[] lean;
    private Vector3[] scale;
    private const float Alive = -1f;
    private const float Dead = -2f;
    private bool[] moving;
    private float[] life;
    private Vector3[] origin;
    private float[] originYaw;
    private float[] deadSince;
    private readonly List<int> dead = new List<int>();
    private float returnBudget;
    private float floorUnseen;
    private bool returnStopped;
    private Matrix4x4[] matrices;
    private readonly List<int> active = new List<int>();
    private readonly List<Spot> spots = new List<Spot>();
    private Material[] materials;
    private Mesh quad;
    private Bounds area;
    private int count;
    private float nextCoverage;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        foreach (RevealableObject target in FindObjectsByType<RevealableObject>())
        {
            if (!target.HasFootprint)
                continue;
            Bounds b = target.Footprint;
            spots.Add(new Spot { Target = target, Rect = new Rect(b.min.x, b.min.z, b.size.x, b.size.z) });
        }

        area = BuildArea();
        quad = BuildQuad();
        materials = BuildMaterials();
        Spawn();
        UpdateCoverage();
    }

    private Bounds BuildArea()
    {
        if (!autoArea || spots.Count == 0)
            return new Bounds(transform.position, new Vector3(areaSize.x, 2f, areaSize.y));

        Rect r = spots[0].Rect;
        foreach (Spot s in spots)
            r = Rect.MinMaxRect(Mathf.Min(r.xMin, s.Rect.xMin), Mathf.Min(r.yMin, s.Rect.yMin), Mathf.Max(r.xMax, s.Rect.xMax), Mathf.Max(r.yMax, s.Rect.yMax));
        float y = spots.Average(s => s.Target.Footprint.min.y);
        return new Bounds(new Vector3(r.center.x, y, r.center.y), new Vector3(r.width + areaMargin * 2f, 2f, r.height + areaMargin * 2f));
    }

    private void Spawn()
    {
        float areaCount = area.size.x * area.size.z * leavesPerSquareMeter;
        float pileCount = spots.Sum(s => s.Rect.width * s.Rect.height) * leavesPerSquareMeter * poemPileMultiplier;
        count = Mathf.Min(maxLeaves, Mathf.RoundToInt(areaCount + pileCount));
        int pileLeaves = areaCount + pileCount > 0f ? Mathf.RoundToInt(count * pileCount / (areaCount + pileCount)) : 0;

        position = new Vector3[count];
        velocity = new Vector2[count];
        lift = new float[count];
        liftVelocity = new float[count];
        yaw = new float[count];
        spin = new float[count];
        lean = new Quaternion[count];
        scale = new Vector3[count];
        moving = new bool[count];
        life = new float[count];
        origin = new Vector3[count];
        originYaw = new float[count];
        deadSince = new float[count];
        matrices = new Matrix4x4[count];

        var random = new System.Random(1914);
        for (int i = 0; i < count; i++)
        {
            Vector2 p;
            if (i < pileLeaves && spots.Count > 0)
            {
                Rect r = spots[i % spots.Count].Rect;
                p = new Vector2(Mathf.Lerp(r.xMin - 0.1f, r.xMax + 0.1f, (float)random.NextDouble()), Mathf.Lerp(r.yMin - 0.1f, r.yMax + 0.1f, (float)random.NextDouble()));
            }
            else
            {
                p = new Vector2(Mathf.Lerp(area.min.x, area.max.x, (float)random.NextDouble()), Mathf.Lerp(area.min.z, area.max.z, (float)random.NextDouble()));
            }

            float layer = 0.006f + (float)random.NextDouble() * 0.02f;
            position[i] = new Vector3(p.x, Ground(p) + layer, p.y);
            yaw[i] = (float)random.NextDouble() * 360f;
            lean[i] = Quaternion.Euler(((float)random.NextDouble() * 2f - 1f) * tilt, 0f, ((float)random.NextDouble() * 2f - 1f) * tilt);
            float size = Mathf.Lerp(leafSize.x, leafSize.y, (float)random.NextDouble());
            scale[i] = new Vector3(size, 1f, size);
            life[i] = Alive;
        }

        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            Swap(i, j);
        }
        for (int i = 0; i < count; i++)
        {
            origin[i] = position[i];
            originYaw[i] = yaw[i];
            UpdateMatrix(i);
        }
    }

    private void Swap(int a, int b)
    {
        (position[a], position[b]) = (position[b], position[a]);
        (yaw[a], yaw[b]) = (yaw[b], yaw[a]);
        (lean[a], lean[b]) = (lean[b], lean[a]);
        (scale[a], scale[b]) = (scale[b], scale[a]);
    }

    private float Ground(Vector2 p)
    {
        var origin = new Vector3(p.x, area.max.y + 10f, p.y);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 40f, groundMask, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<CharacterController>() != null || hit.point.y > area.center.y + 1f)
                continue;
            best = Mathf.Max(best, hit.point.y);
        }
        return best > float.MinValue ? best : area.min.y + 1f;
    }

    private Mesh BuildQuad()
    {
        float aspect = leafTexture != null && leafTexture.height > 0 ? (float)leafTexture.height / leafTexture.width : 1f;
        var mesh = new Mesh { name = "Leaf" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f * aspect), new Vector3(0.5f, 0f, -0.5f * aspect),
            new Vector3(-0.5f, 0f, 0.5f * aspect), new Vector3(0.5f, 0f, 0.5f * aspect)
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private Material[] BuildMaterials()
    {
        if (leafTexture == null)
            leafTexture = Resources.Load<Texture2D>("MainMenu/GoldenLeaf");

        Material baseMaterial = leafMaterial;
        if (baseMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            baseMaterial = new Material(shader);
            baseMaterial.SetTexture("_BaseMap", leafTexture);
            baseMaterial.SetFloat("_AlphaClip", 1f);
            baseMaterial.SetFloat("_Cutoff", 0.4f);
            baseMaterial.SetFloat("_Cull", 0f);
            baseMaterial.SetFloat("_Smoothness", 0.1f);
            baseMaterial.EnableKeyword("_ALPHATEST_ON");
        }

        Color[] colors = tints != null && tints.Length > 0 ? tints : new[] { Color.white };
        var result = new Material[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            result[i] = new Material(baseMaterial) { enableInstancing = true };
            if (result[i].HasProperty("_BaseColor"))
                result[i].SetColor("_BaseColor", colors[i]);
        }
        return result;
    }

    private void UpdateMatrix(int i)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw[i], 0f) * lean[i];
        float shrink = life[i] < 0f ? (life[i] == Dead ? 0f : 1f) : Mathf.Clamp01(life[i] / vanishDuration);
        matrices[i] = Matrix4x4.TRS(position[i] + Vector3.up * lift[i], rotation, scale[i] * shrink);
    }

    public int Push(Vector3 from, Vector3 to, float radius, Vector3 forward, float speed)
    {
        if (position == null)
            return 0;

        Vector2 a = new Vector2(from.x, from.z);
        Vector2 b = new Vector2(to.x, to.z);
        Vector2 segment = b - a;
        float length = segment.magnitude;
        if (length < 0.0005f)
            return 0;

        Vector2 along = segment / length;
        Vector2 ahead = new Vector2(forward.x, forward.z).normalized;
        Vector2 direction = (along * (1f - forwardBias) + ahead * forwardBias).normalized;
        float strength = Mathf.Lerp(pushSpeed.x, pushSpeed.y, Mathf.Clamp01(speed / fullSweepSpeed));
        float radiusSqr = radius * radius;
        Rect reach = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - radius, Mathf.Min(a.y, b.y) - radius, Mathf.Max(a.x, b.x) + radius, Mathf.Max(a.y, b.y) + radius);

        int pushed = 0;
        for (int i = 0; i < count; i++)
        {
            Vector2 p = new Vector2(position[i].x, position[i].z);
            if (!reach.Contains(p))
                continue;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, along) / length);
            if ((a + along * (t * length) - p).sqrMagnitude > radiusSqr)
                continue;

            float jitter = Random.Range(0.7f, 1.3f);
            Vector2 side = new Vector2(-direction.y, direction.x) * Random.Range(-0.35f, 0.35f);
            velocity[i] = (direction + side) * (strength * jitter);
            liftVelocity[i] = Mathf.Max(liftVelocity[i], Random.Range(0.2f, 0.6f) * strength);
            spin[i] = Random.Range(-540f, 540f);
            if (life[i] == Alive)
                life[i] = vanishDelay + vanishDuration + Random.Range(0f, 0.4f);
            if (!moving[i])
            {
                moving[i] = true;
                active.Add(i);
            }
            pushed++;
        }
        return pushed;
    }

    public void Cover(RevealableObject target, float coverage)
    {
        if (position == null)
            return;
        int s = spots.FindIndex(spot => spot.Target == target);
        if (s < 0)
            return;
        Spot spot = spots[s];
        int inside = 0;
        for (int i = 0; i < count; i++)
            if (spot.Rect.Contains(new Vector2(position[i].x, position[i].z)))
                inside++;
        int needed = Mathf.CeilToInt(spot.Initial * coverage) - inside;
        for (int i = 0; i < count && needed > 0; i++)
        {
            if (life[i] != Dead)
                continue;
            var p = new Vector2(Random.Range(spot.Rect.xMin, spot.Rect.xMax), Random.Range(spot.Rect.yMin, spot.Rect.yMax));
            position[i] = new Vector3(p.x, Ground(p) + 0.006f + Random.Range(0f, 0.02f), p.y);
            life[i] = Alive;
            lift[i] = Random.Range(0.4f, 1.4f);
            liftVelocity[i] = 0f;
            velocity[i] = Random.insideUnitCircle * 0.6f;
            spin[i] = Random.Range(-360f, 360f);
            if (!moving[i])
            {
                moving[i] = true;
                active.Add(i);
            }
            UpdateMatrix(i);
            needed--;
        }
        UpdateCoverage();
    }

    public void ClearRemaining(float spread = 0.8f) => ClearRemainingExcept(null, spread);

    public void ClearRemainingExcept(ICollection<RevealableObject> keep, float spread = 0.8f)
    {
        if (position == null)
            return;
        var kept = keep == null ? new List<Rect>() : spots.Where(s => keep.Contains(s.Target)).Select(s => s.Rect).ToList();
        for (int i = 0; i < count; i++)
        {
            if (life[i] != Alive)
                continue;
            Vector2 flat = new Vector2(position[i].x, position[i].z);
            if (kept.Any(r => r.Contains(flat)))
                continue;
            life[i] = vanishDuration + Random.Range(0f, spread);
            velocity[i] = Vector2.zero;
            liftVelocity[i] = Random.Range(0.05f, 0.25f);
            spin[i] = Random.Range(-180f, 180f);
            if (!moving[i])
            {
                moving[i] = true;
                active.Add(i);
            }
        }
    }

    private void Update()
    {
        if (position == null)
            return;

        float dt = Time.deltaTime;
        if (dt > 0f)
            Simulate(dt);

        ReturnLeaves(dt);

        if (Time.time >= nextCoverage)
        {
            nextCoverage = Time.time + 0.2f;
            UpdateCoverage();
        }

        Draw();
    }

    private void OnEnable() => TranscribePoem.Transcribed += OnTranscribed;

    private void OnDisable() => TranscribePoem.Transcribed -= OnTranscribed;

    private void OnTranscribed(int poemId)
    {
        returnStopped = true;
        ClearRemaining();
    }

    private void ReturnLeaves(float dt)
    {
        if (!returnWhenUnseen || returnStopped || TranscribePoem.AnyTranscribing || DiaryInteraction.IsWriting)
        {
            floorUnseen = 0f;
            returnBudget = 0f;
            return;
        }

        floorUnseen = FloorSeen() ? 0f : floorUnseen + dt;
        if (floorUnseen < floorUnseenDelay || dead.Count == 0)
        {
            returnBudget = 0f;
            return;
        }

        returnBudget = Mathf.Min(returnBudget + returnPerSecond * dt, returnPerSecond);
        for (int n = dead.Count - 1; n >= 0 && returnBudget >= 1f; n--)
        {
            int i = dead[n];
            dead.RemoveAt(n);
            if (life[i] != Dead)
                continue;
            position[i] = origin[i];
            yaw[i] = originYaw[i];
            lift[i] = 0f;
            liftVelocity[i] = 0f;
            velocity[i] = Vector2.zero;
            life[i] = Alive;
            UpdateMatrix(i);
            returnBudget -= 1f;
        }
    }

    private bool FloorSeen()
    {
        if (DiaryInteraction.IsReading)
            return false;
        Camera view = Gaze.View;
        if (view == null)
            return true;
        const int grid = 8;
        for (int x = 0; x <= grid; x++)
        {
            for (int z = 0; z <= grid; z++)
            {
                var point = new Vector3(Mathf.Lerp(area.min.x, area.max.x, x / (float)grid), area.center.y, Mathf.Lerp(area.min.z, area.max.z, z / (float)grid));
                Vector3 v = view.WorldToViewportPoint(point);
                if (v.z > 0f && v.x > floorViewMargin && v.x < 1f - floorViewMargin && v.y > floorViewMargin && v.y < 1f - floorViewMargin)
                    return true;
            }
        }
        return false;
    }

    private void Simulate(float dt)
    {
        float damping = Mathf.Exp(-friction * dt);
        for (int n = active.Count - 1; n >= 0; n--)
        {
            int i = active[n];
            position[i].x += velocity[i].x * dt;
            position[i].z += velocity[i].y * dt;
            velocity[i] *= damping;
            liftVelocity[i] -= gravity * dt;
            lift[i] = Mathf.Max(0f, lift[i] + liftVelocity[i] * dt);
            if (lift[i] <= 0f)
                liftVelocity[i] = Mathf.Max(0f, liftVelocity[i]);
            yaw[i] += spin[i] * dt;
            spin[i] *= damping;
            bool vanishing = life[i] >= 0f;
            if (vanishing)
                life[i] = Mathf.Max(0f, life[i] - dt);
            UpdateMatrix(i);

            bool dead = vanishing && life[i] <= 0f;
            bool settled = !vanishing && velocity[i].sqrMagnitude < 0.0004f && lift[i] <= 0f;
            if (dead)
            {
                life[i] = Dead;
                deadSince[i] = Time.time;
                this.dead.Add(i);
                position[i] = new Vector3(1e6f, position[i].y, 1e6f);
                matrices[i] = Matrix4x4.TRS(position[i], Quaternion.identity, Vector3.zero);
            }
            if (dead || settled)
            {
                moving[i] = false;
                active[n] = active[active.Count - 1];
                active.RemoveAt(active.Count - 1);
            }
        }
    }

    private void UpdateCoverage()
    {
        for (int s = 0; s < spots.Count; s++)
        {
            Spot spot = spots[s];
            if (spot.Target == null)
                continue;
            int inside = 0;
            for (int i = 0; i < count; i++)
                if (spot.Rect.Contains(new Vector2(position[i].x, position[i].z)))
                    inside++;
            if (spot.Initial == 0)
            {
                spot.Initial = Mathf.Max(1, inside);
                spots[s] = spot;
            }
            spot.Target.SetCoverage((float)inside / spot.Initial);
        }
    }

    private void Draw()
    {
        if (materials == null || materials.Length == 0 || count == 0)
            return;

        var bounds = new Bounds(area.center, area.size + new Vector3(30f, 4f, 30f));
        int groups = materials.Length;
        for (int g = 0; g < groups; g++)
        {
            int start = count * g / groups;
            int end = count * (g + 1) / groups;
            var parameters = new RenderParams(materials[g])
            {
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer
            };
            for (int s = start; s < end; s += 1023)
                Graphics.RenderMeshInstanced(parameters, quad, 0, matrices, Mathf.Min(1023, end - s), s);
        }
    }
}
