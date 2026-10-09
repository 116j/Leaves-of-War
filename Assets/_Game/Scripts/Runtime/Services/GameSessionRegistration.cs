using System;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Keeps a scene component registered with whichever persistent
    /// <see cref="GameSession"/> currently owns runtime services. Components
    /// may enable before the session is created, so registration cannot rely
    /// on a one-time lookup from OnEnable.
    /// </summary>
    internal sealed class GameSessionRegistration : IDisposable
    {
        private readonly Action<GameSession> register;
        private readonly Action<GameSession> unregister;
        private GameSession registeredSession;
        private bool observing;

        public GameSessionRegistration(
            Action<GameSession> register,
            Action<GameSession> unregister)
        {
            this.register = register ?? throw new ArgumentNullException(nameof(register));
            this.unregister = unregister ?? throw new ArgumentNullException(nameof(unregister));
        }

        public void Enable()
        {
            if (observing)
                return;

            observing = true;
            GameSession.InstanceChanged += HandleInstanceChanged;
            HandleInstanceChanged(GameSession.Instance);
        }

        public void Disable()
        {
            if (!observing)
                return;

            observing = false;
            GameSession.InstanceChanged -= HandleInstanceChanged;
            ChangeSession(null);
        }

        public void Dispose() => Disable();

        private void HandleInstanceChanged(GameSession session)
        {
            if (observing)
                ChangeSession(session);
        }

        private void ChangeSession(GameSession session)
        {
            if (ReferenceEquals(registeredSession, session))
                return;

            GameSession previous = registeredSession;
            registeredSession = null;
            if (previous != null)
                unregister(previous);

            registeredSession = session;
            if (registeredSession != null)
                register(registeredSession);
        }
    }
}
