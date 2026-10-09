using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Narrative
{
    [CreateAssetMenu(menuName = "Hortensia/Narrative/Chapter", fileName = "Chapter")]
    public sealed class ChapterDefinition : ScriptableObject
    {
        [SerializeField, Min(1)] private int index = 1;
        [SerializeField] private int year = 1899;
        [SerializeField] private int age = 19;
        [SerializeField] private string title;
        [SerializeField] private string closingCard;
        [SerializeField] private string baseScene;
        [SerializeField] private string baseSpawnPointId;
        [SerializeField] private string endingSpawnPointId;
        [SerializeReference] private List<NarrativeBeat> beats = new List<NarrativeBeat>();

        public int Index => index;
        public int Year => year;
        public int Age => age;
        public string Title => title;
        public string ClosingCard => closingCard;
        public string BaseScene => baseScene;
        public string BaseSpawnPointId => baseSpawnPointId;
        public string EndingSpawnPointId => endingSpawnPointId;
        public IReadOnlyList<NarrativeBeat> Beats => beats;
    }
}
