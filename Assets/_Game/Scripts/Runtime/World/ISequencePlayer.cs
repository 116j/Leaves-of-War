using System.Collections;

namespace Hortensia.Runtime
{
    public interface ISequencePlayer
    {
        string SequenceId { get; }
        SequenceLockFlags RequiredLocks { get; }
        IEnumerator Play(SequencePlaybackHandle playback);
    }
}
