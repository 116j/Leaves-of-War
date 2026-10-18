using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FlyingLetters : MonoBehaviour
{
    private const float MinFlightHeight = 0.08f;
    private const float BaseSize = 48f;

    private struct Spot
    {
        public int section;
        public int character;
        public float along;
    }

    private static Canvas sharedCanvas;

    private TextMeshPro floor;
    private TextMeshProUGUI text;
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
    private Vector3 pageCentre;

    public static FlyingLetters Create(TextMeshPro source, float flightSeconds, float flightSize, float spiralRadius, float spiralTurns)
    {
        var go = new GameObject(source.name + " (flying letters)", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(SharedCanvas().transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(20000f, 20000f);
        FlyingLetters letters = go.AddComponent<FlyingLetters>();
        TextMeshProUGUI copy = go.GetComponent<TextMeshProUGUI>();
        copy.font = source.font;
        copy.text = source.text;
        copy.fontSize = BaseSize;
        copy.enableAutoSizing = false;
        copy.alignment = TextAlignmentOptions.Center;
        copy.textWrappingMode = TextWrappingModes.NoWrap;
        copy.raycastTarget = false;
        copy.color = source.color;
        letters.floor = source;
        letters.text = copy;
        letters.flight = Mathf.Max(0.3f, flightSeconds);
        letters.size = Mathf.Max(1f, flightSize);
        letters.radius = spiralRadius;
        letters.turns = spiralTurns;
        letters.ink = source.color;
        letters.Prepare();
        return letters;
    }

    private static Canvas SharedCanvas()
    {
        if (sharedCanvas != null)
            return sharedCanvas;
        var go = new GameObject("Flying Letters", typeof(RectTransform), typeof(Canvas));
        sharedCanvas = go.GetComponent<Canvas>();
        sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        sharedCanvas.sortingOrder = 450;
        return sharedCanvas;
    }

    private void OnEnable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

    private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

    private void OnTextChanged(Object changed)
    {
        if (!ready || changed != text)
            return;
        Capture();
        Animate();
    }

    private void Capture()
    {
        TMP_TextInfo info = text.textInfo;
        if (info == null)
            return;
        if (baseVertices == null || baseVertices.Length != info.meshInfo.Length)
            baseVertices = new Vector3[info.meshInfo.Length][];
        for (int m = 0; m < info.meshInfo.Length; m++)
            baseVertices[m] = (Vector3[])info.meshInfo[m].vertices.Clone();
    }

    private void Prepare()
    {
        text.ForceMeshUpdate(true);
        floor.ForceMeshUpdate();
        Capture();
        int count = text.textInfo.characterCount;
        launched = new float[count];
        spots = new Spot[count];
        for (int i = 0; i < count; i++)
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
        if (launched[index] >= 0f)
            return;
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
        TMP_TextInfo floorInfo = floor != null ? floor.textInfo : null;
        if (info == null || info.characterCount == 0 || baseVertices == null)
            return;
        Camera cam = Camera.main;
        bool shown = cam != null && (DiaryInteraction.IsWriting || DiaryInteraction.IsRaised);
        float scale = sharedCanvas != null && sharedCanvas.scaleFactor > 0f ? sharedCanvas.scaleFactor : 1f;
        Vector2 screenCentre = new Vector2(Screen.width, Screen.height) * 0.5f;
        float pixelsPerMetre = cam != null ? Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)) : 1000f;
        Vector3 hubWorld = PageCentre() ? pageCentre : cam != null ? cam.transform.position + cam.transform.forward * 0.6f : Vector3.zero;
        Vector3 hubScreen = cam != null ? cam.WorldToScreenPoint(hubWorld) : Vector3.zero;
        float hubDepth = Mathf.Max(0.05f, hubScreen.z);
        float radiusPx = radius * pixelsPerMetre / hubDepth;
        float minPx = MinFlightHeight * pixelsPerMetre / hubDepth;
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
            bool landed = spots[i].section >= 0;
            byte alpha = 0;
            Vector3 centre = (source[v0] + source[v0 + 2]) * 0.5f;
            float baseHeight = Mathf.Max(0.001f, (source[v0 + 1] - source[v0]).magnitude);

            if (shown && p >= 0f && (p < 1f || landed) && FloorQuad(floorInfo, i, out Vector3 floorCentre, out float floorHeight))
            {
                float u = Mathf.Clamp01(p);
                Vector3 startScreen = cam.WorldToScreenPoint(floorCentre);
                if (startScreen.z <= 0.01f)
                    startScreen = new Vector3(startScreen.x, -Screen.height * 0.1f, 1f);
                float startPx = floorHeight * pixelsPerMetre / Mathf.Max(0.05f, startScreen.z);

                Vector2 goal = hubScreen;
                float goalPx = startPx * 0.4f;
                if (landed && Goal(spots[i], cam, out Vector2 spotScreen, out float spotPx))
                {
                    goal = spotScreen;
                    goalPx = spotPx;
                }

                float seed = Mathf.Sin(i * 12.9898f) * 43758.5453f;
                seed -= Mathf.Floor(seed);
                float rise = Smooth(0f, 0.4f, u);
                float close = Smooth(0.45f, 1f, u);
                float angle = (seed + u * turns) * Mathf.PI * 2f;
                float r = radiusPx * (1f - close) * Mathf.Lerp(0.6f, 1f, rise);
                Vector2 hub = Vector2.Lerp(hubScreen, goal, close);
                Vector2 orbit = hub + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                Vector2 lifted = (Vector2)startScreen + Vector2.up * Mathf.Sin(rise * Mathf.PI * 0.5f) * radiusPx * 0.6f;
                Vector2 screen = Vector2.Lerp(lifted, orbit, rise);

                float peakPx = Mathf.Max(startPx * size, minPx);
                float px = Mathf.Lerp(Mathf.Lerp(startPx, peakPx, rise), goalPx, close);
                float spin = Mathf.Sin(u * Mathf.PI) * (seed - 0.5f) * 120f * (1f - close);
                Quaternion turn = Quaternion.Euler(0f, 0f, spin);
                float k = px / baseHeight;
                Vector3 local = (Vector3)((screen - screenCentre) / scale);
                for (int c = 0; c < 4; c++)
                    vertices[v0 + c] = local + turn * ((source[v0 + c] - centre) * k / scale);
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
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    private bool FloorQuad(TMP_TextInfo floorInfo, int index, out Vector3 centre, out float height)
    {
        centre = default;
        height = 0f;
        if (floorInfo == null || index >= floorInfo.characterCount)
            return false;
        TMP_CharacterInfo character = floorInfo.characterInfo[index];
        if (character.materialReferenceIndex >= floorInfo.meshInfo.Length)
            return false;
        Vector3[] quad = floorInfo.meshInfo[character.materialReferenceIndex].vertices;
        if (quad == null || character.vertexIndex + 3 >= quad.Length)
            return false;
        Transform t = floor.transform;
        Vector3 bl = t.TransformPoint(quad[character.vertexIndex]);
        Vector3 tl = t.TransformPoint(quad[character.vertexIndex + 1]);
        Vector3 tr = t.TransformPoint(quad[character.vertexIndex + 2]);
        centre = (bl + tr) * 0.5f;
        height = (tl - bl).magnitude;
        return true;
    }

    private bool Goal(Spot spot, Camera cam, out Vector2 screen, out float px)
    {
        screen = default;
        px = 0f;
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
        Camera view = page.canvas != null && page.canvas.worldCamera != null ? page.canvas.worldCamera : cam;
        Vector3 a = view.WorldToScreenPoint(page.transform.TransformPoint(quad[character.vertexIndex]));
        Vector3 b = view.WorldToScreenPoint(page.transform.TransformPoint(quad[character.vertexIndex + 1]));
        Vector3 c = view.WorldToScreenPoint(page.transform.TransformPoint(quad[character.vertexIndex + 2]));
        if (a.z <= 0f)
            return false;
        screen = (a + c) * 0.5f;
        px = ((Vector2)b - (Vector2)a).magnitude;
        return true;
    }

    private bool PageCentre()
    {
        if (mappedSections == null)
            return false;
        Vector3 sum = Vector3.zero;
        int count = 0;
        var corners = new Vector3[4];
        foreach (TextMeshProUGUI section in mappedSections)
        {
            if (section == null || !section.gameObject.activeInHierarchy)
                continue;
            section.rectTransform.GetWorldCorners(corners);
            sum += (corners[0] + corners[2]) * 0.5f;
            count++;
        }
        if (count == 0)
            return false;
        pageCentre = sum / count;
        return true;
    }

    private static float Smooth(float from, float to, float value)
    {
        float t = Mathf.Clamp01((value - from) / (to - from));
        return t * t * (3f - 2f * t);
    }
}
