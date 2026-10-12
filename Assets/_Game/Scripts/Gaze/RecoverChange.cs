using Hortensia.Runtime;
using UnityEngine;

public class RecoverChange : ChangeBehaviour
{
    public override void OnChange()
    {
        if (TranscribePoem.AnyTranscribing && Time.timeScale > 0f && !DialogueRunner.LocksPlayer)
        {

        }
    }
}
