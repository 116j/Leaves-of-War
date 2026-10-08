using System.Collections;
using TMPro;
using UnityEngine;

namespace BitWave_Labs.AnimatedTextReveal
{
    /// <summary>
    /// This class animates the fade-in effect of a TextMeshProUGUI component, 
    /// smoothly revealing the text from left to right with adjustable speed and spread.
    /// </summary>
    public class AnimatedTextReveal : MonoBehaviour
    {
        // The TextMeshProUGUI component to animate.
        [SerializeField] private TextMeshProUGUI textMesh;

        // The speed at which the text fades in. Higher values result in faster fading.
        [SerializeField] private float fadeSpeed = 20.0f;

        // The number of characters affected at a time, creating a smoother transition effect.
        [SerializeField] private int characterSpread = 10;

        // Stores the running coroutine instance.
        private Coroutine _fadeCoroutine;

        // Tracks which text to display.
        private int _textCount;

        /// <summary>
        /// Gets the <see cref="TextMeshProUGUI"/> component that this script will animate.
        /// </summary>
        public TextMeshProUGUI TextMesh => textMesh;

        /// <summary>
        /// Gradually fades the text in or out by sweeping character transparency from left to right.
        /// </summary>
        /// <param name="fadeIn">Fade in or fade out the text
        /// </param>
        /// <returns> An <see cref="IEnumerator"/> that performs the fade animation over multiple frames.</returns>
        public IEnumerator FadeText(bool fadeIn)
        {
            textMesh.ForceMeshUpdate();
            int total = textMesh.textInfo.characterCount;

            float spread = Mathf.Max(1, characterSpread);
            float charsPerSecond = Mathf.Max(1f, fadeSpeed);
            float progress = 0f;
            float end = total + spread;

            while (progress < end)
            {
                progress += charsPerSecond * Time.deltaTime;
                ApplySweep(progress, spread, fadeIn);
                yield return null;
            }

            // Guarantee the final state, even if the mesh was rebuilt mid-fade.
            SetAllCharactersAlpha((byte)(fadeIn ? 255 : 0));
        }

        private void ApplySweep(float progress, float spread, bool fadeIn)
        {
            textMesh.ForceMeshUpdate();
            TMP_TextInfo textInfo = textMesh.textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo ci = textInfo.characterInfo[i];
                if (!ci.isVisible) continue;

                float t = Mathf.Clamp01((progress - i) / spread);
                byte a = (byte)(255f * (fadeIn ? t : 1f - t));

                Color32[] colors = textInfo.meshInfo[ci.materialReferenceIndex].colors32;
                int v = ci.vertexIndex;
                colors[v].a = colors[v + 1].a = colors[v + 2].a = colors[v + 3].a = a;
            }

            textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        public void SetAllCharactersAlpha(byte alpha)
        {
            ApplyFlat(alpha);
        }

        private void ApplyFlat(byte alpha)
        {
            textMesh.ForceMeshUpdate();
            TMP_TextInfo textInfo = textMesh.textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo ci = textInfo.characterInfo[i];
                if (!ci.isVisible) continue;

                Color32[] colors = textInfo.meshInfo[ci.materialReferenceIndex].colors32;
                int v = ci.vertexIndex;
                colors[v].a = colors[v + 1].a = colors[v + 2].a = colors[v + 3].a = alpha;
            }

            textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}