using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public abstract class LineBeat : NarrativeBeat
    {
        [SerializeField] private SpeakerDefinition speaker;

        public SpeakerDefinition Speaker => speaker;
        public virtual bool HasNameVariants => false;

        protected abstract ILineContent Content { get; }

        public ResolvedLine Resolve(NameVariant variant)
        {
            ILineContent content = Content;
            string text = content.TextFor(variant);
            if (HasNameVariants)
                text = NameVariants.Substitute(text, variant);

            return new ResolvedLine(
                content.LineId,
                text,
                content.ClipFor(variant),
                content.SegmentsFor(variant),
                content.VolumeFor(variant));
        }
    }

    [Serializable]
    public sealed class SpokenLineBeat : LineBeat
    {
        [SerializeField] private LineContent content;

        public LineContent SharedContent => content;
        protected override ILineContent Content => content;
    }

    [Serializable]
    public sealed class NameVariantLineBeat : LineBeat
    {
        [SerializeField] private NameVariantLineContent content;

        public override bool HasNameVariants => true;

        protected override ILineContent Content => content;
    }

    [Serializable]
    public sealed class DocumentSequenceBeat : LineBeat
    {
        [SerializeField] private DocumentDefinition document;
        [SerializeField] private NameVariantLineContent content;

        public DocumentDefinition Document => document;
        public override bool HasNameVariants => true;

        protected override ILineContent Content => content;
    }
}