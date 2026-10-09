using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Typed references to the runtime-created narrative and HUD hierarchy.
    /// Presentation behavior does not need to know how this hierarchy is built.
    /// </summary>
    internal sealed class NarrativeUiView : IDisposable
    {
        public DiegeticUiCompositor DiegeticCompositor { get; set; }
        public GameObject SubtitlePanel { get; set; }
        public TMP_Text SpeakerText { get; set; }
        public TMP_Text SubtitleText { get; set; }
        public TMP_Text InteractionPromptText { get; set; }
        public TMP_Text CarryStatusText { get; set; }
        public TMP_Text StatusText { get; set; }
        public TMP_Text GameplayHudText { get; set; }
        internal ObjectiveDirectionArrowGraphic ObjectiveDirectionArrow { get; set; }
        public GameObject DocumentPanel { get; set; }
        public TMP_Text DocumentTitleText { get; set; }
        public TMP_Text DocumentBodyText { get; set; }
        public TMP_Text DocumentPageText { get; set; }
        public GameObject AuthorialPanel { get; set; }
        public TMP_Text AuthorialText { get; set; }
        public GameObject ChoicePanel { get; set; }
        public List<Button> ChoiceButtons { get; } = new List<Button>();
        public List<TMP_Text> ChoiceLabels { get; } = new List<TMP_Text>();

        public void SetVisible(bool visible)
        {
            DiegeticCompositor?.SetVisible(visible);
        }

        public void HideNarrativePanels()
        {
            SetActive(SubtitlePanel, false);
            SetActive(DocumentPanel, false);
            SetActive(AuthorialPanel, false);
            SetActive(ChoicePanel, false);
        }

        public void Dispose()
        {
            DiegeticCompositor?.Dispose();
            DiegeticCompositor = null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
