using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Attach to a document/letter object. While the document is open, the
    /// background music pauses (keeping its position) and this component's own
    /// AudioSource plays; on close, the ambience stops and music resumes from
    /// where it left off.
    ///
    /// Detection is global: it reacts whenever ANY document is open, not only
    /// one specific letter, since only one document can be read at a time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DocumentAmbientAudio : MonoBehaviour
    {
        [Tooltip("Plays while a document is open; hidden/stopped when it closes.")]
        [SerializeField] private AudioSource documentAudioSource;

        private bool wasDocumentOpen;

        private void Reset()
        {
            documentAudioSource = GetComponentInChildren<AudioSource>();
        }

        private void Awake()
        {
            if (documentAudioSource != null)
                documentAudioSource.gameObject.SetActive(false);
        }

        private void Update()
        {
            bool isOpen = IsAnyDocumentOpen();
            if (isOpen == wasDocumentOpen)
                return;

            wasDocumentOpen = isOpen;
            if (isOpen)
                OnDocumentOpened();
            else
                OnDocumentClosed();
        }

        private void OnDocumentOpened()
        {
            AudioManager.Instance?.PauseMusic();
            foreach (PlayMusicOnce music in FindObjectsByType<PlayMusicOnce>(FindObjectsInactive.Include))
                music.Pause();

            if (documentAudioSource == null)
                return;

            documentAudioSource.gameObject.SetActive(true);
            documentAudioSource.Play();
        }

        private void OnDocumentClosed()
        {
            if (documentAudioSource != null)
            {
                documentAudioSource.Stop();
                documentAudioSource.gameObject.SetActive(false);
            }

            AudioManager.Instance?.ResumeMusic();
            foreach (PlayMusicOnce music in FindObjectsByType<PlayMusicOnce>(FindObjectsInactive.Include))
                music.Resume();
        }

        private static bool IsAnyDocumentOpen()
        {
            GameSession session = GameSession.Instance;
            if (session == null)
                return false;

            if (!session.SceneServices.TryGetPresenter(out INarrativePresenter presenter, out _))
                return false;

            return presenter is NarrativePresenter narrativePresenter && narrativePresenter.IsDocumentOpen;
        }
    }
}