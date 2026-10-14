using System;
using UnityEngine;

public class PoemRelocateChange : RelocateChange
{
    Action<int> _relocateAction;
    public override void OnChange()
    {
        if (!_changed)
        {
            _relocateAction = (int _) => base.OnChange();
            TranscribePoem.Gusted+= _relocateAction;
            _changed = true;
        }
    }

    private void OnDestroy()
    {
        TranscribePoem.Gusted-= _relocateAction;
    }
}
