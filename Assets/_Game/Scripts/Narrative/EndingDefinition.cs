using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Narrative
{
    [CreateAssetMenu(menuName = "Hortensia/Narrative/Ending", fileName = "Ending")]
    public sealed class EndingDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [Tooltip("File name of this ending's video inside StreamingAssets when the protagonist is Laura (e.g. 'ending-sacrifice-laura.mp4'). Played automatically after this ending's beats finish, before the credits.")]
        [SerializeField] private string endingVideoFileNameLaura;
        [Tooltip("File name of this ending's video inside StreamingAssets when the protagonist is Everie.")]
        [SerializeField] private string endingVideoFileNameEverie;
        [SerializeField, Range(0f, 1f)] private float endingVideoVolume = 1f;
        [SerializeReference] private List<NarrativeBeat> beats = new List<NarrativeBeat>();

        public string Id => id;
        public string Title => title;
        public float EndingVideoVolume => endingVideoVolume;
        public IReadOnlyList<NarrativeBeat> Beats => beats;

        public string EndingVideoFileNameFor(NameVariant variant) =>
            variant == NameVariant.Laura ? endingVideoFileNameLaura : endingVideoFileNameEverie;
    }
}