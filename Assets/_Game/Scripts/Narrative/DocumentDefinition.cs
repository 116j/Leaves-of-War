using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Narrative
{
    [CreateAssetMenu(menuName = "Hortensia/Narrative/Document", fileName = "Document")]
    public sealed class DocumentDefinition : ScriptableObject, IReadableDocument
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private List<string> pages = new List<string>();
        [SerializeField] private FlagId setWhenRead;
        [SerializeField, Min(0)] private int chapterIndex;
        [SerializeField] private SpeakerDefinition narrator;
        [SerializeField] private LineContent narration;
        [SerializeField] private bool usesNameVariantNarration;
        [SerializeField] private NameVariantLineContent nameVariantNarration;

        public string Id => id;
        public string Title => title;
        public IReadOnlyList<string> Pages => pages;
        public FlagId SetWhenRead => setWhenRead;
        public int ChapterIndex => chapterIndex;
        public SpeakerDefinition Narrator => narrator;
        public ILineContent Narration => usesNameVariantNarration ? nameVariantNarration : narration;
        public bool NarrationHasNameVariants => usesNameVariantNarration;
        public bool HasNarration => !string.IsNullOrWhiteSpace(Narration.LineId);

        public IReadOnlyList<string> PagesFor(NarrativeState state, NarrativeCatalog catalog) => pages;
    }
}
