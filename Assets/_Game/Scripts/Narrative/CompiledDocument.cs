using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Narrative
{
    [CreateAssetMenu(menuName = "Hortensia/Narrative/Compiled Document", fileName = "CompiledDocument")]
    public sealed class CompiledDocument : ScriptableObject, IReadableDocument
    {
        [SerializeField] private string title;
        [SerializeReference] private List<DocumentSection> sections = new List<DocumentSection>();

        public string Title => title;
        public IReadOnlyList<DocumentSection> Sections => sections;
        public ILineContent Narration => null;
        public FlagId SetWhenRead => null;

        public IReadOnlyList<string> PagesFor(NarrativeState state, NarrativeCatalog catalog)
        {
            var pages = new List<string>();
            if (sections == null)
                return pages;

            for (int i = 0; i < sections.Count; i++)
            {
                DocumentSection section = sections[i];
                if (section == null)
                    continue;

                IEnumerable<string> sectionPages = section.PagesFor(state, catalog);
                if (sectionPages == null)
                    continue;

                foreach (string page in sectionPages)
                    pages.Add(page ?? string.Empty);
            }

            return pages;
        }
    }
}
