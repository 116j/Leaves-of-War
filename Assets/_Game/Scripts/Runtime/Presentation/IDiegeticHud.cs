namespace Hortensia.Runtime
{
    /// <summary>
    /// Narrow scene service for reusable, point-filtered gameplay feedback.
    /// Narrative playback remains behind <see cref="INarrativePresenter"/>.
    /// </summary>
    public interface IDiegeticHud
    {
        void ShowStatus(string message);
        void SetGameplayHud(string message);
        void ClearGameplayHud();
    }
}
