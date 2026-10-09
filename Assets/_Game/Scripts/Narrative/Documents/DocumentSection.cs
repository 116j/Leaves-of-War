using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hortensia.Narrative
{
    [Serializable]
    public abstract class DocumentSection
    {
        public abstract IEnumerable<string> PagesFor(
            NarrativeState state,
            NarrativeCatalog catalog);
    }

    [Serializable]
    public sealed class AuthoredProseSection : DocumentSection
    {
        [SerializeField] private List<string> pages = new List<string>();

        public IReadOnlyList<string> Pages => pages;

        public override IEnumerable<string> PagesFor(
            NarrativeState state,
            NarrativeCatalog catalog)
        {
            if (pages == null)
                yield break;

            for (int i = 0; i < pages.Count; i++)
                yield return pages[i] ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class DocumentsReadSection : DocumentSection
    {
        [SerializeField] private string heading;

        public string Heading => heading;

        public override IEnumerable<string> PagesFor(
            NarrativeState state,
            NarrativeCatalog catalog)
        {
            if (state == null || catalog == null)
                yield break;

            for (int documentIndex = 0; documentIndex < catalog.Documents.Count; documentIndex++)
            {
                DocumentDefinition document = catalog.Documents[documentIndex];
                if (document == null ||
                    document.SetWhenRead == null ||
                    !state.HasFlag(document.SetWhenRead))
                {
                    continue;
                }

                IReadOnlyList<string> pages = document.PagesFor(state, catalog);
                for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
                {
                    yield return ComposePage(heading, document.Title, pages[pageIndex]);
                }
            }
        }

        private static string ComposePage(string sectionHeading, string itemHeading, string body)
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(sectionHeading))
                parts.Add(sectionHeading);
            if (!string.IsNullOrWhiteSpace(itemHeading))
                parts.Add(itemHeading);
            if (!string.IsNullOrWhiteSpace(body))
                parts.Add(body);
            return string.Join("\n\n", parts);
        }
    }

    [Serializable]
    public sealed class PatientsHeardSection : DocumentSection
    {
        [SerializeField] private string heading;

        public string Heading => heading;

        public override IEnumerable<string> PagesFor(
            NarrativeState state,
            NarrativeCatalog catalog)
        {
            if (state == null || catalog == null)
                yield break;

            var included = new HashSet<PatientDefinition>();
            for (int chapterIndex = 0; chapterIndex < catalog.Chapters.Count; chapterIndex++)
            {
                ChapterDefinition chapter = catalog.Chapters[chapterIndex];
                if (chapter == null || chapter.Index > state.ChapterIndex)
                    continue;

                int completedBeatCount = chapter.Index < state.ChapterIndex
                    ? chapter.Beats.Count
                    : Math.Min(state.BeatIndex, chapter.Beats.Count);

                for (int beatIndex = 0; beatIndex < completedBeatCount; beatIndex++)
                {
                    if (!(chapter.Beats[beatIndex] is PatientSessionBeat session))
                        continue;

                    for (int patientIndex = 0; patientIndex < session.Roster.Count; patientIndex++)
                    {
                        PatientDefinition patient = session.Roster[patientIndex];
                        if (patient == null || !included.Add(patient))
                            continue;

                        yield return ComposePatientPage(patient, state.ChosenName);
                    }
                }
            }
        }

        private string ComposePatientPage(PatientDefinition patient, NameVariant chosenName)
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(heading))
                parts.Add(heading);
            if (!string.IsNullOrWhiteSpace(patient.DisplayName))
                parts.Add(patient.DisplayName);
            if (!string.IsNullOrWhiteSpace(patient.Content.TextFor(chosenName)))
                parts.Add(patient.Content.TextFor(chosenName));
            return string.Join("\n\n", parts);
        }
    }
}
