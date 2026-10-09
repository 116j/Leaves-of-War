using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Runtime
{
    [Flags]
    public enum ChapterMask
    {
        None = 0,
        ChapterI = 1 << 0,
        ChapterII = 1 << 1,
        ChapterIII = 1 << 2,
        ChapterIV = 1 << 3,
        ChapterV = 1 << 4,
        ChapterVI = 1 << 5,
        ChapterVII = 1 << 6,
        All = ChapterI | ChapterII | ChapterIII | ChapterIV | ChapterV | ChapterVI | ChapterVII
    }

    [Serializable]
    public sealed class ChapterDressingGroup
    {
        [SerializeField] private string label;
        [SerializeField] private GameObject root;
        [SerializeField] private ChapterMask activeInChapters = ChapterMask.All;

        public string Label => label;
        public GameObject Root => root;
        public ChapterMask ActiveInChapters => activeInChapters;

        public void Apply(int chapterIndex)
        {
            if (root == null)
                return;

            root.SetActive(IsActiveIn(chapterIndex));
        }

        public bool IsActiveIn(int chapterIndex)
        {
            if (chapterIndex < 1 || chapterIndex > 7)
                return false;

            ChapterMask chapter = (ChapterMask)(1 << (chapterIndex - 1));
            return (activeInChapters & chapter) != 0;
        }
    }

    [DisallowMultipleComponent]
    public sealed class ChapterDressing : MonoBehaviour
    {
        [SerializeField] private List<ChapterDressingGroup> groups = new List<ChapterDressingGroup>();

        public IReadOnlyList<ChapterDressingGroup> Groups => groups;
        public int AppliedChapterIndex { get; private set; }

        private void Start()
        {
            ApplyCurrentChapter();
        }

        public bool ApplyCurrentChapter()
        {
            GameSession session = GameSession.Instance;
            if (session == null || session.CurrentChapter == null)
                return false;

            ApplyChapter(session.CurrentChapter.Index);
            return true;
        }

        // ChapterDefinition indices are authored as one-based values (I–VII).
        public void ApplyChapter(int chapterIndex)
        {
            AppliedChapterIndex = chapterIndex;

            if (groups == null)
                return;

            for (int i = 0; i < groups.Count; i++)
                groups[i]?.Apply(chapterIndex);
        }
    }
}
