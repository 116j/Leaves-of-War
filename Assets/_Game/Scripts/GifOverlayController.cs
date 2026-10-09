using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Drives the looping GIF overlay(s) shown during patient sessions,
    /// alongside <see cref="VisionBleedController"/>'s colour tint. Attach to
    /// the SAME GameObject as VisionBleedController so overlays share its
    /// exact screen area. Two gif sources are authored: Gif 1 alone plays in
    /// Chapter 4 (1910); Gif 1 then Gif 2, in that order, cycle in Chapter 5
    /// (1915). During a patient swap, extra scattered copies appear via
    /// <see cref="SpawnScatteredDuplicates"/> / <see cref="ClearScatteredDuplicates"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GifOverlayController : MonoBehaviour
    {
        [Header("Gif Sources")]
        [Tooltip("Rename your .gif files to .bytes so Unity imports them as TextAsset instead of a static image. Used alone in Chapter 4, and together with Gif 2 (in order: 1 then 2) in Chapter 5.")]
        [SerializeField] private TextAsset gif1;
        [Tooltip("Only used in Chapter 5, cycling after Gif 1.")]
        [SerializeField] private TextAsset gif2;

        [Header("Layout")]
        [Tooltip("RectTransform the overlays are positioned within - use the same one VisionBleedController tints.")]
        [SerializeField] private RectTransform overlayParent;
        [SerializeField] private Vector2 overlaySize = new Vector2(160f, 160f);
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.5f;

        [Header("Transition Duplicates")]
        [Tooltip("Extra scattered copies shown during a patient swap. 1 = two total on screen, 2 = three total ('triplicano'), etc.")]
        [SerializeField, Min(0)] private int extraDuplicatesOnTransition = 2;

        private GameSessionRegistration sessionRegistration;

        private List<GifDecoder.GifFrame> decodedGif1;
        private List<GifDecoder.GifFrame> decodedGif2;
        private List<GifDecoder.GifFrame> currentSequence;

        private RawImage mainImage;
        private Coroutine mainCycleRoutine;
        private readonly List<RawImage> duplicateImages = new List<RawImage>();
        private readonly List<Coroutine> duplicateRoutines = new List<Coroutine>();

        private void Awake()
        {
            sessionRegistration = new GameSessionRegistration(
                session => session.SceneServices.RegisterGifOverlay(this),
                session => session.SceneServices.UnregisterGifOverlay(this));
        }

        private void OnEnable() => sessionRegistration?.Enable();

        private void OnDisable()
        {
            sessionRegistration?.Disable();
            StopLoop();
        }

        private void OnDestroy() => sessionRegistration?.Dispose();

        /// <summary>
        /// Starts the continuous loop for the given chapter's gif set. Safe
        /// to call even if a loop is already running (restarts cleanly).
        /// Decoding is cached per gif, so repeated calls across a session
        /// don't re-decode the same file.
        /// </summary>
        public void StartForChapter(int chapterIndex)
        {
            StopLoop();

            currentSequence = BuildSequenceForChapter(chapterIndex);
            if (currentSequence == null || currentSequence.Count == 0)
                return;

            mainImage = CreateOverlayImage("Gif Overlay - Main", Vector2.zero, stretch: true);
            mainCycleRoutine = StartCoroutine(CycleRoutine(mainImage, currentSequence));
        }

        /// <summary>Stops the continuous loop and clears any duplicates.</summary>
        public void StopLoop()
        {
            ClearScatteredDuplicates();

            if (mainCycleRoutine != null)
            {
                StopCoroutine(mainCycleRoutine);
                mainCycleRoutine = null;
            }

            if (mainImage != null)
            {
                Destroy(mainImage.gameObject);
                mainImage = null;
            }

            currentSequence = null;
        }

        /// <summary>
        /// Spawns the configured number of extra copies at randomised
        /// positions within the overlay area, each independently cycling
        /// the same gif sequence. Call <see cref="ClearScatteredDuplicates"/>
        /// once the transition is over.
        /// </summary>
        public void SpawnScatteredDuplicates()
        {
            ClearScatteredDuplicates();

            if (currentSequence == null || currentSequence.Count == 0 || overlayParent == null)
                return;

            for (int i = 0; i < extraDuplicatesOnTransition; i++)
            {
                Vector2 randomOffset = new Vector2(
                    Random.Range(-overlayParent.rect.width * 0.35f, overlayParent.rect.width * 0.35f),
                    Random.Range(-overlayParent.rect.height * 0.35f, overlayParent.rect.height * 0.35f));

                RawImage duplicate = CreateOverlayImage($"Gif Overlay - Duplicate {i}", randomOffset, stretch: false);
                duplicateImages.Add(duplicate);
                duplicateRoutines.Add(StartCoroutine(CycleRoutine(duplicate, currentSequence)));
            }
        }

        public void ClearScatteredDuplicates()
        {
            for (int i = 0; i < duplicateRoutines.Count; i++)
            {
                if (duplicateRoutines[i] != null)
                    StopCoroutine(duplicateRoutines[i]);
            }
            duplicateRoutines.Clear();

            for (int i = 0; i < duplicateImages.Count; i++)
            {
                if (duplicateImages[i] != null)
                    Destroy(duplicateImages[i].gameObject);
            }
            duplicateImages.Clear();
        }

        private List<GifDecoder.GifFrame> BuildSequenceForChapter(int chapterIndex)
        {
            EnsureDecoded();

            var sequence = new List<GifDecoder.GifFrame>();
            if (decodedGif1 != null)
                sequence.AddRange(decodedGif1);

            // Chapter 5 (1915) also cycles Gif 2, after Gif 1. Every other
            // chapter (e.g. 4 / 1910) plays Gif 1 alone.
            if (chapterIndex == 5 && decodedGif2 != null)
                sequence.AddRange(decodedGif2);

            return sequence;
        }

        private void EnsureDecoded()
        {
            if (decodedGif1 == null && gif1 != null)
                decodedGif1 = GifDecoder.Decode(gif1.bytes);

            if (decodedGif2 == null && gif2 != null)
                decodedGif2 = GifDecoder.Decode(gif2.bytes);
        }

        private RawImage CreateOverlayImage(string name, Vector2 anchoredPosition, bool stretch)
        {
            Transform parent = overlayParent != null ? (Transform)overlayParent : transform;
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)imageObject.transform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = overlaySize;
                rect.anchoredPosition = anchoredPosition;
            }

            RawImage image = imageObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, overlayAlpha);
            return image;
        }

        private IEnumerator CycleRoutine(RawImage image, List<GifDecoder.GifFrame> frames)
        {
            int index = 0;
            while (image != null && frames.Count > 0)
            {
                GifDecoder.GifFrame frame = frames[index];
                image.texture = frame.Texture;
                yield return new WaitForSecondsRealtime(frame.DelaySeconds);
                index = (index + 1) % frames.Count;
            }
        }
    }
}