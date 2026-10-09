using System.Collections.Generic;

namespace Hortensia.Narrative
{
    public interface IReadableDocument
    {
        string Title { get; }
        IReadOnlyList<string> PagesFor(NarrativeState state, NarrativeCatalog catalog);
        ILineContent Narration { get; }
        FlagId SetWhenRead { get; }
    }
}
