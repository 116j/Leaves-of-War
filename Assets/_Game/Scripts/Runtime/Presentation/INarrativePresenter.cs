using System;
using System.Collections;
using System.Collections.Generic;
using Hortensia.Narrative;

namespace Hortensia.Runtime
{
    public interface INarrativePresenter
    {
        IEnumerator PresentLine(ResolvedLine line, string speakerName, float visionIntensity);
        IEnumerator PresentCard(string text);
        IEnumerator PresentChoice(IReadOnlyList<ChoiceOption> options, Action<EndingDefinition> selected);
        IEnumerator PresentDocument(IReadableDocument document, Action closed);
        void ShowStatus(string message);
        void ClearPresentation();
    }
}
