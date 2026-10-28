using TMPro;
using UnityEngine;

public sealed class JoinWriting : MonoBehaviour
{
    private const float Distance = 1.7f;
    private const float Width = 1.7f;
    private const float Depth = 1.5f;
    private const float AppearSeconds = 1.2f;
    private const float FadeSeconds = 2.5f;

    private TextMeshPro floorText;
    private FlyingLetters flying;
    private Color color;
    private float lifted;
    private float appear;
    private float fade = 1f;
    private bool fading;

    public static JoinWriting Create(string text, TMP_FontAsset font, Color color)
    {
        Camera cam = Camera.main;
        if (cam == null || string.IsNullOrWhiteSpace(text))
            return null;
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();
        Vector3 spot = cam.transform.position + forward * Distance;
        float ground = cam.transform.position.y - 1.6f;
        if (Physics.Raycast(spot + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            ground = hit.point.y;

        var go = new GameObject("Joined Poem Floor Writing", typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetPositionAndRotation(new Vector3(spot.x, ground + 0.004f, spot.z), Quaternion.LookRotation(Vector3.down, forward));
        JoinWriting writing = go.AddComponent<JoinWriting>();
        writing.color = color;
        TextMeshPro floor = go.GetComponent<TextMeshPro>();
        writing.floorText = floor;
        floor.rectTransform.sizeDelta = new Vector2(Width, Depth);
        if (font != null)
            floor.font = font;
        floor.text = text;
        floor.alignment = TextAlignmentOptions.Center;
        floor.enableAutoSizing = true;
        floor.fontSizeMin = 0.05f;
        floor.fontSizeMax = 4f;
        floor.lineSpacing = 5f;
        floor.textWrappingMode = TextWrappingModes.Normal;
        floor.color = new Color(color.r, color.g, color.b, 1f);
        floor.ForceMeshUpdate();

        FlyingLettersLook look = FlyingLettersLook.Current;
        if (look == null || look.lettersFly)
        {
            floor.color = color;
            writing.flying = look != null
                ? FlyingLetters.Create(floor, look.flightSeconds, look.flightSize, look.spiralRadius, look.spiralTurns)
                : FlyingLetters.Create(floor, 2.4f, 2.6f, 0.24f, 1.5f);
            floor.color = new Color(color.r, color.g, color.b, 1f);
        }
        return writing;
    }

    public void SetLifted(float fraction)
    {
        lifted = Mathf.Clamp01(fraction);
        if (flying != null)
            flying.SetActive(TranscribePoem.AnyTranscribing);
    }

    public void Finish()
    {
        if (fading)
            return;
        fading = true;
        lifted = 1f;
        if (flying != null)
            flying.Finish();
    }

    private void LateUpdate()
    {
        if (floorText == null)
            return;
        appear = Mathf.MoveTowards(appear, 1f, Time.deltaTime / AppearSeconds);
        if (fading)
        {
            fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime / FadeSeconds);
            if (fade <= 0f)
            {
                if (flying != null)
                    Destroy(flying.gameObject, 2f);
                Destroy(gameObject, 2.1f);
                floorText.gameObject.GetComponent<MeshRenderer>().enabled = false;
                enabled = false;
                return;
            }
        }

        TMP_TextInfo info = floorText.textInfo;
        if (info == null || info.characterCount == 0)
            return;
        float alpha = color.a * appear * fade;
        float liftedLines = lifted * info.lineCount;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible)
                continue;
            float line = 1f - Mathf.Clamp01(liftedLines - character.lineNumber);
            if (flying != null)
            {
                TMP_LineInfo row = info.lineInfo[character.lineNumber];
                float along = row.characterCount > 0 ? (i - row.firstCharacterIndex + 0.5f) / row.characterCount : 0f;
                if (liftedLines - character.lineNumber > along)
                    flying.LaunchIfFree(i);
                line = flying.IsLaunched(i) ? 0f : 1f;
            }
            byte a = (byte)Mathf.RoundToInt(255f * alpha * line);
            Color32[] colors = info.meshInfo[character.materialReferenceIndex].colors32;
            for (int v = 0; v < 4; v++)
                colors[character.vertexIndex + v].a = a;
        }
        floorText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}
