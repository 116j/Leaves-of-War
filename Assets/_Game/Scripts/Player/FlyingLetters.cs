using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FlyingLetters : MonoBehaviour
{
    private struct Spot
    {
        public int section;
        public int character;
        public float along;
    }

    private TextMeshPro text;
    private Vector3[][] baseVertices;
    private float[] launched;
    private Spot[] spots;
    private List<TextMeshProUGUI> mappedSections;
    private float flight;
    private float size;
    private float radius;
    private float turns;
    private Color32 ink;
    private bool ready;
    private bool active;
    private float finish = -1f;
    private DiaryInteraction diary;

    public static FlyingLetters Create(TextMeshPro source, float flightSeconds, float flightSize, float spiralRadius, float spiralTurns)
    {
        var go = new GameObject(source.name + " (flying letters)", typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetParent(source.transform.parent, false);
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = source.transform.localScale;
        int held = LayerMask.NameToLayer("HeldItems");
        if (held >= 0)
            go.layer = held;
        FlyingLetters letters = go.AddComponent<FlyingLetters>();
        TextMeshPro copy = go.GetComponent<TextMeshPro>();
        copy.rectTransform.sizeDelta = source.rectTransform.sizeDelta;
        copy.font = source.font;
        copy.text = source.text;
        copy.alignment = source.alignment;
        copy.enableAutoSizing = source.enableAutoSizing;
        copy.fontSizeMin = source.fontSizeMin;
        copy.fontSizeMax = source.fontSizeMax;
        copy.fontSize = source.fontSize;
        copy.lineSpacing = source.lineSpacing;
        copy.textWrappingMode = source.textWrappingMode;
        copy.color = source.color;
        letters.text = copy;
        letters.flight = Mathf.Max(0.3f, flightSeconds);
        letters.size = Mathf.Max(1f, flightSize);
        letters.radius = spiralRadius;
        letters.turns = spiralTurns;
        letters.ink = source.color;
        letters.Prepare();
        return letters;
    }

    private void Prepare()
    {
        text.ForceMeshUpdate(true);
        TMP_TextInfo info = text.textInfo;
        baseVertices = new Vector3[info.meshInfo.Length][];
        for (int m = 0; m < info.meshInfo.Length; m++)
            baseVertices[m] = (Vector3[])info.meshInfo[m].vertices.Clone();
        launched = new float[info.characterCount];
        spots = new Spot[info.characterCount];
        for (int i = 0; i < launched.Length; i++)
        {
            launched[i] = -1f;
            spots[i].section = -1;
        }
        ready = true;
        Animate();
    }

    public bool IsLaunched(int index) => ready && index >= 0 && index < launched.Length && launched[index] >= 0f;

    public void SetActive(bool writing)
    {
        active = writing;
        if (writing && TranscribePoem.WritingSections != null && TranscribePoem.WritingSections != mappedSections)
            Map(TranscribePoem.WritingSections);
    }

    public void LaunchIfFree(int index)
    {
        if (ready && index >= 0 && index < launched.Length && spots[index].section < 0)
            Launch(index);
    }

    public void Finish()
    {
        if (finish < 0f)
            finish = Time.time;
    }

    private void Launch(int index)
    {
        if (launched[index] < 0f)
            launched[index] = Time.time;
    }

    private void Map(List<TextMeshProUGUI> sections)
    {
        mappedSections = sections;
        var page = new List<(int section, int character, char letter, float along)>();
        for (int s = 0; s < sections.Count; s++)
        {
            if (sections[s] == null)
                continue;
            sections[s].ForceMeshUpdate(true);
            TMP_TextInfo pageInfo = sections[s].textInfo;
            for (int c = 0; c < pageInfo.characterCount; c++)
                if (pageInfo.characterInfo[c].isVisible)
                    page.Add((s, c, char.ToLowerInvariant(pageInfo.characterInfo[c].character), (c + 0.5f) / Mathf.Max(1, pageInfo.characterCount)));
        }
        TMP_TextInfo info = text.textInfo;
        int pointer = 0;
        for (int i = 0; i < spots.Length; i++)
        {
            spots[i].section = -1;
            if (i >= info.characterCount || !info.characterInfo[i].isVisible)
                continue;
            char letter = char.ToLowerInvariant(info.characterInfo[i].character);
            for (int k = pointer; k < page.Count && k < pointer + 40; k++)
            {
                if (page[k].letter != letter)
                    continue;
                spots[i] = new Spot { section = page[k].section, character = page[k].character, along = page[k].along };
                pointer = k + 1;
                break;
            }
        }
    }

    private void LateUpdate()
    {
        if (!ready)
            return;
        if (active && mappedSections != null)
            LaunchOnTime();
        Animate();
    }

    private void LaunchOnTime()
    {
        int section = TranscribePoem.WritingSection;
        float progress = TranscribePoem.WritingProgress;
        float[] seconds = TranscribePoem.WritingSeconds;
        for (int i = 0; i < spots.Length; i++)
        {
            if (spots[i].section < 0 || launched[i] >= 0f)
                continue;
            float lead = seconds != null && spots[i].section < seconds.Length ? flight / Mathf.Max(0.1f, seconds[spots[i].section]) : 0f;
            if (section > spots[i].section || section == spots[i].section && progress >= spots[i].along - lead)
                Launch(i);
        }
    }

    private void Animate()
    {
        TMP_TextInfo info = text.textInfo;
        if (info == null || info.characterCount == 0)
            return;
        Camera cam = Camera.main;
        Transform view = cam != null ? cam.transform : transform;
        Transform notebook = Notebook();
        bool shown = notebook != null && DiaryInteraction.IsWriting;
        Vector3 middle = notebook != null ? notebook.position : view.position + view.forward * 0.6f;
        float ending = finish < 0f ? 1f : 1f - Mathf.Clamp01((Time.time - finish - flight) / 0.8f);
        for (int i = 0; i < info.characterCount && i < launched.Length; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible)
                continue;
            int m = character.materialReferenceIndex;
            int v0 = character.vertexIndex;
            if (m >= baseVertices.Length || v0 + 3 >= baseVertices[m].Length || v0 + 3 >= info.meshInfo[m].vertices.Length)
                continue;
            Vector3[] source = baseVertices[m];
            Vector3[] vertices = info.meshInfo[m].vertices;
            Color32[] colors = info.meshInfo[m].colors32;
            float p = launched[i] < 0f ? -1f : (Time.time - launched[i]) / flight;
            byte alpha = 0;
            bool landed = spots[i].section >= 0;
            if (shown && p >= 0f && (p < 1f || landed))
            {
                float u = Mathf.Clamp01(p);
                Vector3 centre = (source[v0] + source[v0 + 2]) * 0.5f;
                Vector3 start = transform.TransformPoint(centre);
                float startHeight = transform.TransformVector(source[v0 + 1] - source[v0]).magnitude;
                float seed = Mathf.Sin(i * 12.9898f) * 43758.5453f;
                seed -= Mathf.Floor(seed);

                Vector3 goal = middle;
                Quaternion goalRotation = view.rotation;
                float goalScale = 0.4f;
                if (landed && Goal(spots[i], out Vector3 spot, out Quaternion spotRotation, out float spotHeight))
                {
                    goal = spot;
                    goalRotation = spotRotation;
                    goalScale = startHeight > 0f ? spotHeight / startHeight : 1f;
                }

                float rise = Smooth(0f, 0.4f, u);
                float close = Smooth(0.45f, 1f, u);
                float angle = (seed + u * turns) * Mathf.PI * 2f;
                float r = radius * (1f - close) * Mathf.Lerp(0.6f, 1f, rise);
                Vector3 hub = Vector3.Lerp(middle, goal, close);
                Vector3 orbit = hub + (Mathf.Cos(angle) * view.right + Mathf.Sin(angle) * view.up) * r - view.forward * (0.03f + 0.08f * (1f - close));
                Vector3 lifted = start + Vector3.up * Mathf.Sin(rise * Mathf.PI * 0.5f) * 0.4f;
                Vector3 world = Vector3.Lerp(lifted, orbit, rise);

                Quaternion facing = Quaternion.Slerp(view.rotation, goalRotation, close);
                Quaternion turn = Quaternion.Slerp(transform.rotation, facing, Smooth(0f, 0.3f, u)) * Quaternion.Inverse(transform.rotation);
                Quaternion spin = Quaternion.AngleAxis(Mathf.Sin(u * Mathf.PI) * (seed - 0.5f) * 120f * (1f - close), view.forward);
                float big = Mathf.Lerp(1f, size, rise);
                float scale = Mathf.Lerp(big, goalScale, close);
                for (int c = 0; c < 4; c++)
                {
                    Vector3 offset = transform.TransformVector(source[v0 + c] - centre);
                    vertices[v0 + c] = transform.InverseTransformPoint(world + spin * (turn * offset) * scale);
                }
                float a = Mathf.Clamp01(u * 8f) * (landed ? ending : Mathf.Clamp01((1f - u) * 4f));
                alpha = (byte)Mathf.RoundToInt(255f * Mathf.Lerp(ink.a / 255f, 1f, rise) * a);
            }
            else
            {
                for (int c = 0; c < 4; c++)
                    vertices[v0 + c] = source[v0 + c];
            }
            for (int c = 0; c < 4; c++)
                colors[v0 + c] = new Color32(ink.r, ink.g, ink.b, alpha);
        }
        for (int m = 0; m < info.meshInfo.Length; m++)
        {
            info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
            info.meshInfo[m].mesh.colors32 = info.meshInfo[m].colors32;
            text.UpdateGeometry(info.meshInfo[m].mesh, m);
        }
    }

    private bool Goal(Spot spot, out Vector3 position, out Quaternion rotation, out float height)
    {
        position = default;
        rotation = Quaternion.identity;
        height = 0f;
        if (mappedSections == null || spot.section >= mappedSections.Count)
            return false;
        TextMeshProUGUI page = mappedSections[spot.section];
        if (page == null || !page.gameObject.activeInHierarchy)
            return false;
        TMP_TextInfo info = page.textInfo;
        if (info == null || spot.character >= info.characterCount)
            return false;
        TMP_CharacterInfo character = info.characterInfo[spot.character];
        if (character.materialReferenceIndex >= info.meshInfo.Length)
            return false;
        Vector3[] quad = info.meshInfo[character.materialReferenceIndex].vertices;
        if (quad == null || character.vertexIndex + 3 >= quad.Length)
            return false;
        Vector3 bottomLeft = page.transform.TransformPoint(quad[character.vertexIndex]);
        Vector3 topLeft = page.transform.TransformPoint(quad[character.vertexIndex + 1]);
        Vector3 topRight = page.transform.TransformPoint(quad[character.vertexIndex + 2]);
        position = (bottomLeft + topRight) * 0.5f;
        rotation = page.transform.rotation;
        height = (topLeft - bottomLeft).magnitude;
        Camera cam = Camera.main;
        if (cam != null)
            position += (cam.transform.position - position).normalized * 0.002f;
        return true;
    }

    private Transform Notebook()
    {
        if (diary == null)
            diary = FindAnyObjectByType<DiaryInteraction>();
        return diary != null && diary.Notebook != null && diary.Notebook.gameObject.activeInHierarchy ? diary.Notebook : null;
    }

    private static float Smooth(float from, float to, float value)
    {
        float t = Mathf.Clamp01((value - from) / (to - from));
        return t * t * (3f - 2f * t);
    }
}
