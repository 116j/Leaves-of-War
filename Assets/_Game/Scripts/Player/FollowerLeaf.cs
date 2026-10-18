using UnityEngine;

public class FollowerLeaf : MonoBehaviour
{
    [SerializeField] private Texture2D texture;
    [SerializeField, Min(0.01f)] private float size = 0.09f;
    [SerializeField, Min(0.2f)] private float distance = 1.2f;
    [SerializeField] private Vector2 viewportX = new Vector2(0.15f, 0.85f);
    [SerializeField] private Vector2 viewportY = new Vector2(0.15f, 0.3f);
    [SerializeField, Min(0.05f)] private float smoothTime = 0.6f;
    [SerializeField, Min(0f)] private float flutter = 0.03f;
    [SerializeField] private Color tint = new Color(1f, 0.82f, 0.35f);
    [Tooltip("Shown only while the notebook is raised to transcribe; it starts again every time.")]
    [SerializeField] private bool onlyWhileWriting = true;

    private Camera view;
    private Transform leaf;
    private Vector2 slot;
    private Vector3 velocity;
    private float seed;
    private bool shown = true;
    private bool landing;
    private bool descending;
    private float landStart;
    private Vector3 landPoint;
    private Vector3 descendFrom;
    private Quaternion descendRotation;
    private float landYaw;

    public static AudioClip[] Sounds;
    public static float SoundVolume = 0.35f;

    public static FollowerLeaf Spawn(Texture2D texture = null)
    {
        FollowerLeaf existing = FindAnyObjectByType<FollowerLeaf>();
        if (existing != null)
            return existing;
        var go = new GameObject("Follower Leaf");
        FollowerLeaf follower = go.AddComponent<FollowerLeaf>();
        if (texture != null)
            follower.texture = texture;
        return follower;
    }

    private void Start()
    {
        view = Gaze.View;
        if (texture == null)
            texture = Resources.Load<Texture2D>("MainMenu/GoldenLeaf");
        seed = Random.value * 100f;
        slot = NewSlot();

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Golden Leaf";
        leaf = quad.transform;
        leaf.SetParent(transform, false);
        float aspect = texture != null && texture.width > 0 ? (float)texture.height / texture.width : 1f;
        leaf.localScale = new Vector3(size, size * aspect, 1f);
        int held = LayerMask.NameToLayer("HeldItems");
        if (held >= 0)
            quad.layer = held;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader != null ? shader : Shader.Find("Unlit/Transparent Cutout"));
        material.SetTexture("_BaseMap", texture);
        material.mainTexture = texture;
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.4f);
        material.SetFloat("_Cull", 0f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        quad.GetComponent<MeshRenderer>().sharedMaterial = material;
        quad.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (view != null)
            transform.position = Target();
    }

    private void PlaySound()
    {
        if (Sounds == null || Sounds.Length == 0)
            return;
        AudioClip clip = Sounds[Random.Range(0, Sounds.Length)];
        if (clip == null)
            return;
        var go = new GameObject("Follower Leaf Sound");
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.volume = SoundVolume;
        source.clip = clip;
        Hortensia.Runtime.AudioManager.Route(source, Hortensia.Runtime.AudioBus.SoundEffects);
        source.Play();
        Destroy(go, clip.length + 0.2f);
    }

    public static void LandOn(Vector3 point)
    {
        FollowerLeaf follower = Spawn();
        follower.landing = true;
        follower.descending = false;
        follower.landStart = Time.time;
        follower.landPoint = point;
        follower.landYaw = Random.Range(0f, 360f);
    }

    private void Land()
    {
        if (leaf != null && !leaf.gameObject.activeSelf)
        {
            leaf.gameObject.SetActive(true);
            shown = true;
            PlaySound();
        }
        float t = Time.time - landStart;
        const float Centre = 1.6f, Descend = 2.8f;
        if (t < Centre)
        {
            Vector3 middle = view.ViewportToWorldPoint(new Vector3(0.5f + (Mathf.PerlinNoise(t, 1f) - 0.5f) * flutter, 0.56f + (Mathf.PerlinNoise(2f, t) - 0.5f) * flutter, distance));
            transform.position = Vector3.SmoothDamp(transform.position, middle, ref velocity, smoothTime * 0.8f, Mathf.Infinity, Time.deltaTime);
            float s = Time.time + seed;
            transform.rotation = Quaternion.LookRotation(view.transform.forward, view.transform.up) * Quaternion.Euler(0f, 0f, Mathf.Sin(s * 1.3f) * 20f);
            return;
        }
        if (!descending)
        {
            descending = true;
            descendFrom = transform.position;
            descendRotation = transform.rotation;
            if (leaf != null)
                leaf.gameObject.layer = 0;
        }
        float k = Mathf.Clamp01((t - Centre) / Descend);
        float e = k * k * (3f - 2f * k);
        Vector3 sway = Vector3.Cross(Vector3.up, view.transform.forward).normalized * Mathf.Sin(k * Mathf.PI * 2.5f) * 0.12f * (1f - e);
        transform.position = Vector3.Lerp(descendFrom, landPoint, e) + sway;
        Quaternion flat = Quaternion.LookRotation(Vector3.down, Quaternion.Euler(0f, landYaw, 0f) * Vector3.forward);
        transform.rotation = Quaternion.Slerp(descendRotation, flat, e) * Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 3f) * 25f * (1f - e));
    }

    private Vector2 NewSlot() => new Vector2(Random.Range(viewportX.x, viewportX.y), Random.Range(viewportY.x, viewportY.y));

    private Vector3 Target()
    {
        float t = Time.time * 0.7f + seed;
        Vector3 local = new Vector3(slot.x + (Mathf.PerlinNoise(t, 0f) - 0.5f) * flutter, slot.y + (Mathf.PerlinNoise(0f, t) - 0.5f) * flutter, distance);
        return view.ViewportToWorldPoint(local);
    }

    private void LateUpdate()
    {
        if (view == null)
        {
            view = Gaze.View;
            if (view == null)
                return;
        }

        if (landing)
        {
            Land();
            return;
        }

        bool show = !onlyWhileWriting || DiaryInteraction.IsWriting;
        if (show != shown)
        {
            shown = show;
            if (leaf != null)
                leaf.gameObject.SetActive(show);
            if (show)
            {
                PlaySound();
                slot = NewSlot();
                transform.position = view.ViewportToWorldPoint(new Vector3(slot.x, -0.15f, distance));
                velocity = Vector3.zero;
            }
        }
        if (!show)
            return;

        Vector3 viewport = view.WorldToViewportPoint(transform.position);
        bool outside = viewport.z <= 0f || viewport.x < -0.05f || viewport.x > 1.05f || viewport.y < -0.3f || viewport.y > 1.05f;
        if (outside)
        {
            slot = NewSlot();
            transform.position = Target();
            velocity = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, Target(), ref velocity, smoothTime, Mathf.Infinity, Time.deltaTime);
        }

        float t = Time.time + seed;
        Quaternion face = Quaternion.LookRotation(view.transform.forward, view.transform.up);
        transform.rotation = face * Quaternion.Euler((Mathf.PerlinNoise(t, 3f) - 0.5f) * 50f, (Mathf.PerlinNoise(5f, t) - 0.5f) * 50f, Mathf.Sin(t * 1.3f) * 20f);
    }
}
