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
        quad.GetComponent<MeshRenderer>().sharedMaterial = material;
        quad.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (view != null)
            transform.position = Target();
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

        bool show = !onlyWhileWriting || DiaryInteraction.IsWriting;
        if (show != shown)
        {
            shown = show;
            if (leaf != null)
                leaf.gameObject.SetActive(show);
            if (show)
            {
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
