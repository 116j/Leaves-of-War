using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace BitWave_Labs.AnimatedTextReveal
{
    public class AnimateText : MonoBehaviour
    {
        [System.Serializable]
        private class AnimatedTextSettings
        {
            [Header("Text")]
            public AnimatedTextReveal animatedTextReveal;

            [Header("Fade")]
            public FadeMode fadeMode = FadeMode.FadeInAndOut;

            [Header("Timing")]
            [Tooltip("Time to wait before starting this text.")]
            public float delayBeforeFadeIn = 0f;

            [Tooltip("Time to wait after fade-in before fade-out.")]
            public float delayBeforeFadeOut = 1f;

            [Tooltip("Time to wait after fade-out before next text.")]
            public float delayAfterFadeOut = 1f;
        }

        private enum FadeMode
        {
            FadeIn,
            FadeOut,
            FadeInAndOut
        }

        [Header("Animated Text Sequence")]
        [SerializeField]
        private List<AnimatedTextSettings> animatedTexts = new();




        [Header("Play Limit")]
        [Tooltip("How many times the sequence can play. 0 = unlimited.")]
        [SerializeField] private int maxPlays = 1;

        private int _playCount;
        private Coroutine _cycleCoroutine;

        public int PlayCount => _playCount;


        /// <summary>
        /// Hook this to a Button OnClick, EventTrigger, or any UnityEvent in the Inspector.
        /// </summary>
        public void StartSequence()
        {
            if (_cycleCoroutine != null)
                return;

            if (maxPlays > 0 && _playCount >= maxPlays)
            {
                Debug.Log($"'{gameObject.name}' reached max plays ({maxPlays}).");
                return;
            }

            _playCount++;
            _cycleCoroutine = StartCoroutine(CycleThroughTexts());
        }


        /// <summary>
        /// Optional: stop the sequence from an Inspector event.
        /// </summary>
        public void StopSequence()
        {
            if (_cycleCoroutine == null)
                return;
            Debug.Log($"Stopping text sequence on '{gameObject.name}'");
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }


        private void OnDisable()
        {
            _cycleCoroutine = null;
        }


        private IEnumerator CycleThroughTexts()
        {
          

            for (int i = 0; i < animatedTexts.Count; i++)
            {
                AnimatedTextSettings settings = animatedTexts[i];

                if (settings == null ||
                    settings.animatedTextReveal == null)
                {
                    continue;
                }

                AnimatedTextReveal textReveal = settings.animatedTextReveal;

                if (textReveal.TextMesh == null)
                {
                    Debug.LogError(
                        $"Text Mesh is not assigned on '{textReveal.name}' (list element {i}).",
                        textReveal
                    );
                    continue;
                }


                // WAIT BEFORE THIS TEXT
                if (settings.delayBeforeFadeIn > 0f)
                    yield return new WaitForSeconds(settings.delayBeforeFadeIn);


                // INITIAL STATE
                if (settings.fadeMode == FadeMode.FadeIn ||
                    settings.fadeMode == FadeMode.FadeInAndOut)
                {
                    textReveal.SetAllCharactersAlpha(0);
                }
                else
                {
                    textReveal.SetAllCharactersAlpha(255);
                }

             
             


                // FADE IN
                if (settings.fadeMode == FadeMode.FadeIn ||
                    settings.fadeMode == FadeMode.FadeInAndOut)
                {
                    yield return textReveal.FadeText(true);
                }


                // WAIT BEFORE FADE OUT
                if (settings.fadeMode == FadeMode.FadeInAndOut &&
                    settings.delayBeforeFadeOut > 0f)
                {
                    yield return new WaitForSeconds(settings.delayBeforeFadeOut);
                }


                // FADE OUT
                if (settings.fadeMode == FadeMode.FadeOut ||
                    settings.fadeMode == FadeMode.FadeInAndOut)
                {
                    yield return textReveal.FadeText(false);
                }


                // WAIT BEFORE NEXT TEXT
                if (settings.delayAfterFadeOut > 0f)
                    yield return new WaitForSeconds(settings.delayAfterFadeOut);
            }

            _cycleCoroutine = null;
           
        }
    }
}