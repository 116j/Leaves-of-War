using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    public enum SequencePlaybackStatus
    {
        Pending,
        Running,
        Completed,
        Cancelled,
        Failed
    }

    [Flags]
    public enum SequenceLockFlags
    {
        None = 0,
        PlayerMovement = 1 << 0,
        NarrativeInput = 1 << 1
    }

    /// <summary>
    /// Owns one set-piece's cancellation signal, runtime dependencies, lock
    /// leases, cleanup callbacks, and terminal status. Cleanup and lock release
    /// are idempotent and do not depend on Unity resuming an iterator's finally
    /// block after its parent coroutine is stopped.
    /// </summary>
    public sealed class SequencePlaybackHandle : IDisposable
    {
        private readonly GameSession session;
        private readonly List<Action> cleanup = new List<Action>();
        private SequenceLockFlags heldLocks;
        private bool cleanupRan;

        public SequencePlaybackHandle(
            GameSession session,
            string sequenceId,
            string endingId,
            SequenceLockFlags requestedLocks,
            ISequenceClock clock,
            ISequenceInput input)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            SequenceId = sequenceId ?? string.Empty;
            EndingId = endingId;
            RequestedLocks = requestedLocks;
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public string SequenceId { get; }
        public string EndingId { get; }
        public SequenceLockFlags RequestedLocks { get; }
        public SequencePlaybackStatus Status { get; private set; } =
            SequencePlaybackStatus.Pending;
        public string Error { get; private set; } = string.Empty;
        public bool IsCancellationRequested => Status == SequencePlaybackStatus.Cancelled;
        public bool IsTerminal => Status == SequencePlaybackStatus.Completed ||
            Status == SequencePlaybackStatus.Cancelled ||
            Status == SequencePlaybackStatus.Failed;
        public ISequenceClock Clock { get; }
        public ISequenceInput Input { get; }
        public SceneServiceRegistry SceneServices => session.SceneServices;

        public void Begin()
        {
            if (Status != SequencePlaybackStatus.Pending)
                throw new InvalidOperationException("Sequence playback can begin only once.");

            Status = SequencePlaybackStatus.Running;
            if ((RequestedLocks & SequenceLockFlags.PlayerMovement) != 0)
            {
                session.PushPlayerLock();
                heldLocks |= SequenceLockFlags.PlayerMovement;
            }

            if ((RequestedLocks & SequenceLockFlags.NarrativeInput) != 0)
            {
                session.PushNarrativeInputLock();
                heldLocks |= SequenceLockFlags.NarrativeInput;
            }
        }

        public void RegisterCleanup(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            if (cleanupRan)
            {
                callback();
                return;
            }

            cleanup.Add(callback);
        }

        /// <summary>
        /// Releases this playback's narrative-input lease early. Sacrifice uses
        /// this after combat so the authored thorn interaction can receive focus.
        /// Calling it more than once is harmless.
        /// </summary>
        public void ReleaseNarrativeInput()
        {
            if ((heldLocks & SequenceLockFlags.NarrativeInput) == 0)
                return;

            session.PopNarrativeInputLock();
            heldLocks &= ~SequenceLockFlags.NarrativeInput;
        }

        public void Cancel()
        {
            if (IsTerminal)
                return;

            Finish(SequencePlaybackStatus.Cancelled, string.Empty);
        }

        public void Complete()
        {
            if (IsTerminal)
                return;

            Finish(SequencePlaybackStatus.Completed, string.Empty);
        }

        public void Fail(string error)
        {
            if (IsTerminal)
                return;

            Finish(
                SequencePlaybackStatus.Failed,
                string.IsNullOrWhiteSpace(error) ? "Sequence playback failed." : error);
        }

        public void Dispose()
        {
            if (!IsTerminal)
                Cancel();
        }

        private void Finish(SequencePlaybackStatus status, string error)
        {
            Status = status;
            Error = error ?? string.Empty;

            Exception cleanupFailure = RunCleanup();
            ReleaseLocks();
            if (cleanupFailure != null && Status == SequencePlaybackStatus.Completed)
            {
                Status = SequencePlaybackStatus.Failed;
                Error = $"Sequence cleanup failed: {cleanupFailure.Message}";
            }
        }

        private Exception RunCleanup()
        {
            if (cleanupRan)
                return null;

            cleanupRan = true;
            Exception firstFailure = null;
            for (int i = cleanup.Count - 1; i >= 0; i--)
            {
                try
                {
                    cleanup[i]();
                }
                catch (Exception exception)
                {
                    firstFailure ??= exception;
                    Debug.LogException(exception);
                }
            }
            cleanup.Clear();
            return firstFailure;
        }

        private void ReleaseLocks()
        {
            if ((heldLocks & SequenceLockFlags.NarrativeInput) != 0)
                session.PopNarrativeInputLock();
            if ((heldLocks & SequenceLockFlags.PlayerMovement) != 0)
                session.PopPlayerLock();

            heldLocks = SequenceLockFlags.None;
        }
    }
}
