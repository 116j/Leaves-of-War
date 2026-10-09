using UnityEngine;

namespace Hortensia.Runtime
{
    [CreateAssetMenu(menuName = "Hortensia/Presentation Settings", fileName = "PresentationSettings")]
    public sealed class PresentationSettings : ScriptableObject
    {
        [SerializeField, Min(1f)] private float revealCharactersPerSecond = 36f;
        [SerializeField, Min(0f)] private float minimumHoldSeconds = 0.25f;
        [SerializeField, Range(40, 240)] private int chunkCharacterLimit = 64;
        [SerializeField, Min(0f)] private float beatPauseSeconds = 0.6f;
        [SerializeField] private bool lockPlayerDuringLines;
        [SerializeField] private RetroUiTheme uiTheme;

        public float RevealCharactersPerSecond => revealCharactersPerSecond;
        public float MinimumHoldSeconds => minimumHoldSeconds;
        public int ChunkCharacterLimit => chunkCharacterLimit;
        public float BeatPauseSeconds => beatPauseSeconds;
        public bool LockPlayerDuringLines => lockPlayerDuringLines;
        public RetroUiTheme UiTheme => uiTheme;
    }
}
