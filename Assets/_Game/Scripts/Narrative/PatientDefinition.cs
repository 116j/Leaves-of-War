using System;
using UnityEngine;

namespace Hortensia.Narrative
{
    public enum DreamMotif
    {
        Greenhouse,
        KneelingGardener,
        WitheringFace,
        NotAgeing,
        ToldItALie,
        KnewMyName,
        NearlyReady
    }

    [CreateAssetMenu(menuName = "Hortensia/Narrative/Patient", fileName = "Patient")]
    public sealed class PatientDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private LineContent content;
        [SerializeField] private DreamMotif[] motifs = Array.Empty<DreamMotif>();

        public string DisplayName => displayName;
        public LineContent Content => content;
        public DreamMotif[] Motifs => motifs;
    }
}
