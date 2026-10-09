using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Hortensia.Editor.Validation;
using Hortensia.Narrative;
using UnityEditor;
using UnityEngine;

public static class RecordingSheetExporter
{
    private const string MenuPath = "Tools/Hortensia/Narrative/Export Recording Sheets";
    private const string Header = "lineId,chapter,year,age,phase,text Laura,text Everie,two takes";

    public static string DefaultOutputDirectory => Path.GetFullPath(
        Path.Combine(Application.dataPath, "..", "..", "docs", "recording-sheets"));

    [MenuItem(MenuPath)]
    private static void ExportFromMenu()
    {
        if (!TryExport(DefaultOutputDirectory, out string error))
        {
            Debug.LogError(error);
            EditorUtility.DisplayDialog("Recording sheet export failed", error, "OK");
            return;
        }

        Debug.Log($"Recording sheets exported to '{DefaultOutputDirectory}'.");
    }

    public static bool TryExport(string outputDirectory, out string error)
    {
        if (!NarrativeCatalogLocator.TryLoadSingle(out NarrativeCatalog catalog, out error))
            return false;

        return TryExport(catalog, outputDirectory, out error);
    }

    public static bool TryExport(
        NarrativeCatalog catalog,
        string outputDirectory,
        out string error)
    {
        error = string.Empty;
        if (catalog == null)
        {
            error = "Recording sheet export requires a NarrativeCatalog.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            error = "Recording sheet export requires an output directory.";
            return false;
        }

        if (!NarrativeCatalogValidator.TryValidate(catalog, out error))
            return false;

        if (!TryCollectRows(catalog, out List<SpeakerSheet> speakerSheets, out List<Row> patientRows, out error))
            return false;

        if (!ValidateRows(speakerSheets, patientRows, out error))
            return false;

        try
        {
            // Validation happens before this point. A blank or duplicate line
            // ID therefore cannot create or alter any output file.
            Directory.CreateDirectory(outputDirectory);

            speakerSheets.Sort((left, right) =>
                string.Compare(left.FileStem, right.FileStem, StringComparison.Ordinal));
            for (int i = 0; i < speakerSheets.Count; i++)
            {
                SpeakerSheet sheet = speakerSheets[i];
                sheet.Rows.Sort((left, right) =>
                    string.Compare(left.LineId, right.LineId, StringComparison.Ordinal));
                WriteCsv(Path.Combine(outputDirectory, sheet.FileStem + ".csv"), sheet.Rows);
            }

            WriteCsv(Path.Combine(outputDirectory, "Patients.csv"), patientRows);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Recording sheet export failed while writing files: {exception.Message}";
            return false;
        }
    }

    private static bool TryCollectRows(
        NarrativeCatalog catalog,
        out List<SpeakerSheet> speakerSheets,
        out List<Row> patientRows,
        out string error)
    {
        speakerSheets = new List<SpeakerSheet>();
        patientRows = new List<Row>();
        error = string.Empty;

        var sheetsBySpeaker = new Dictionary<SpeakerDefinition, SpeakerSheet>();
        SpeakerSheet unknownSpeakerSheet = null;
        var chapters = new List<ChapterDefinition>();
        for (int i = 0; i < catalog.Chapters.Count; i++)
        {
            if (catalog.Chapters[i] != null)
                chapters.Add(catalog.Chapters[i]);
        }
        chapters.Sort((left, right) => left.Index.CompareTo(right.Index));

        if (chapters.Count == 0)
        {
            error = "The NarrativeCatalog has no chapters to export.";
            return false;
        }

        for (int i = 0; i < chapters.Count; i++)
        {
            ChapterDefinition chapter = chapters[i];
            ScanBeats(
                chapter.Beats,
                chapter,
                $"Chapter {chapter.Index}",
                sheetsBySpeaker,
                speakerSheets,
                ref unknownSpeakerSheet,
                patientRows);
        }

        ChapterDefinition finalChapter = chapters[chapters.Count - 1];
        for (int i = 0; i < catalog.Endings.Count; i++)
        {
            EndingDefinition ending = catalog.Endings[i];
            if (ending == null)
                continue;

            ScanBeats(
                ending.Beats,
                finalChapter,
                $"Ending '{ending.Title}'",
                sheetsBySpeaker,
                speakerSheets,
                ref unknownSpeakerSheet,
                patientRows);
        }

        if (!TryScanDocumentNarrations(
                catalog.Documents,
                chapters,
                sheetsBySpeaker,
                speakerSheets,
                ref unknownSpeakerSheet,
                out error))
        {
            return false;
        }

        if (speakerSheets.Count == 0 && patientRows.Count == 0)
        {
            error = "The NarrativeCatalog contains no narrative lines to export.";
            return false;
        }

        return true;
    }

    private static bool TryScanDocumentNarrations(
        IReadOnlyList<DocumentDefinition> documents,
        IReadOnlyList<ChapterDefinition> chapters,
        Dictionary<SpeakerDefinition, SpeakerSheet> sheetsBySpeaker,
        List<SpeakerSheet> speakerSheets,
        ref SpeakerSheet unknownSpeakerSheet,
        out string error)
    {
        error = string.Empty;
        for (int i = 0; i < documents.Count; i++)
        {
            DocumentDefinition document = documents[i];
            if (document == null || !document.HasNarration)
                continue;

            ChapterDefinition chapter = null;
            for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
            {
                if (chapters[chapterIndex].Index == document.ChapterIndex)
                {
                    chapter = chapters[chapterIndex];
                    break;
                }
            }

            if (chapter == null)
            {
                error = $"Narrated document '{document.Title}' references missing chapter {document.ChapterIndex}.";
                return false;
            }

            ILineContent content = document.Narration;
            SpeakerDefinition speaker = document.Narrator;
            string lauraText = content.TextFor(NameVariant.Laura);
            string everieText = content.TextFor(NameVariant.Everie);
            if (document.NarrationHasNameVariants)
            {
                lauraText = NameVariants.Substitute(lauraText, NameVariant.Laura);
                everieText = NameVariants.Substitute(everieText, NameVariant.Everie);
            }

            var row = new Row(
                content.LineId,
                chapter.Index,
                chapter.Year,
                chapter.Age,
                PhaseFor(speaker, chapter.Index, content.LineId),
                lauraText,
                everieText,
                document.NarrationHasNameVariants,
                $"Document '{document.Title}'");

            SpeakerSheet sheet;
            if (speaker == null)
            {
                if (unknownSpeakerSheet == null)
                {
                    unknownSpeakerSheet = new SpeakerSheet("UnknownSpeaker");
                    speakerSheets.Add(unknownSpeakerSheet);
                }
                sheet = unknownSpeakerSheet;
            }
            else if (!sheetsBySpeaker.TryGetValue(speaker, out sheet))
            {
                sheet = new SpeakerSheet(FileStemFor(speaker));
                sheetsBySpeaker.Add(speaker, sheet);
                speakerSheets.Add(sheet);
            }

            sheet.Rows.Add(row);
        }

        return true;
    }

    private static void ScanBeats(
        IReadOnlyList<NarrativeBeat> beats,
        ChapterDefinition chapter,
        string source,
        Dictionary<SpeakerDefinition, SpeakerSheet> sheetsBySpeaker,
        List<SpeakerSheet> speakerSheets,
        ref SpeakerSheet unknownSpeakerSheet,
        List<Row> patientRows)
    {
        for (int beatIndex = 0; beatIndex < beats.Count; beatIndex++)
        {
            NarrativeBeat beat = beats[beatIndex];
            if (beat is LineBeat lineBeat)
            {
                ResolvedLine laura = lineBeat.Resolve(NameVariant.Laura);
                ResolvedLine everie = lineBeat.Resolve(NameVariant.Everie);
                SpeakerDefinition speaker = lineBeat.Speaker;
                string phase = PhaseFor(speaker, chapter.Index, laura.LineId);
                var row = new Row(
                    laura.LineId,
                    chapter.Index,
                    chapter.Year,
                    chapter.Age,
                    phase,
                    laura.Text,
                    everie.Text,
                    lineBeat.HasNameVariants,
                    $"{source}, beat {beatIndex + 1}");

                SpeakerSheet sheet;
                if (speaker == null)
                {
                    if (unknownSpeakerSheet == null)
                    {
                        unknownSpeakerSheet = new SpeakerSheet("UnknownSpeaker");
                        speakerSheets.Add(unknownSpeakerSheet);
                    }
                    sheet = unknownSpeakerSheet;
                }
                else if (!sheetsBySpeaker.TryGetValue(speaker, out sheet))
                {
                    sheet = new SpeakerSheet(FileStemFor(speaker));
                    sheetsBySpeaker.Add(speaker, sheet);
                    speakerSheets.Add(sheet);
                }

                sheet.Rows.Add(row);
            }
            else if (beat is PatientSessionBeat patientSession)
            {
                for (int patientIndex = 0; patientIndex < patientSession.Roster.Count; patientIndex++)
                {
                    PatientDefinition patient = patientSession.Roster[patientIndex];
                    if (patient == null)
                        continue;

                    LineContent content = patient.Content;
                    patientRows.Add(new Row(
                        content.LineId,
                        chapter.Index,
                        chapter.Year,
                        chapter.Age,
                        string.Empty,
                        content.TextFor(NameVariant.Laura),
                        content.TextFor(NameVariant.Everie),
                        false,
                        $"{source}, patient {patient.DisplayName}"));
                }
            }
        }
    }

    private static string PhaseFor(SpeakerDefinition speaker, int chapterIndex, string lineId)
    {
        // Sacrifice is the Gardener's one release phase. It is an ending
        // branch rather than another numbered chapter, so representing it in
        // phasesByChapter would also relabel his other Chapter VII delivery.
        if (speaker != null &&
            string.Equals(speaker.Id, "gardener", StringComparison.Ordinal) &&
            string.Equals(lineId, "end03_002_gardener", StringComparison.Ordinal))
        {
            return "release";
        }

        if (speaker != null && speaker.TryGetPhase(chapterIndex, out ArcPhase phase))
            return phase.DirectionWord ?? string.Empty;
        return string.Empty;
    }

    private static bool ValidateRows(
        List<SpeakerSheet> speakerSheets,
        List<Row> patientRows,
        out string error)
    {
        var rows = new List<Row>(patientRows.Count + 64);
        var fileStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < speakerSheets.Count; i++)
        {
            SpeakerSheet sheet = speakerSheets[i];
            if (!fileStems.Add(sheet.FileStem))
            {
                error = $"Two speakers would export to the same file '{sheet.FileStem}.csv'.";
                return false;
            }
            rows.AddRange(sheet.Rows);
        }
        rows.AddRange(patientRows);

        // IDs become filenames on the case-insensitive filesystems used by
        // the project's primary editor/build targets.
        var byLineId = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (string.IsNullOrWhiteSpace(row.LineId))
            {
                problems.Add($"Blank lineId at {row.Source}.");
                continue;
            }

            if (byLineId.TryGetValue(row.LineId, out Row first))
            {
                problems.Add(
                    $"Duplicate lineId '{row.LineId}' at {first.Source} and {row.Source}.");
                continue;
            }

            byLineId.Add(row.LineId, row);
        }

        if (problems.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        error = "Recording sheet export refused:\n" + string.Join("\n", problems);
        return false;
    }

    private static void WriteCsv(string path, List<Row> rows)
    {
        var builder = new StringBuilder(Header.Length + rows.Count * 256);
        builder.Append(Header).Append("\r\n");
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            AppendCsvField(builder, row.LineId);
            builder.Append(',');
            AppendCsvField(builder, row.Chapter.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            AppendCsvField(builder, row.Year.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            AppendCsvField(builder, row.Age.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            AppendCsvField(builder, row.Phase);
            builder.Append(',');
            AppendCsvField(builder, row.LauraText);
            builder.Append(',');
            AppendCsvField(builder, row.EverieText);
            builder.Append(',');
            AppendCsvField(builder, row.TwoTakes ? "yes" : "no");
            builder.Append("\r\n");
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    private static void AppendCsvField(StringBuilder builder, string value)
    {
        value ??= string.Empty;
        bool quoted = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (!quoted)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '"')
                builder.Append("\"\"");
            else
                builder.Append(value[i]);
        }
        builder.Append('"');
    }

    private static string FileStemFor(SpeakerDefinition speaker)
    {
        string value = !string.IsNullOrWhiteSpace(speaker.Id)
            ? speaker.Id
            : !string.IsNullOrWhiteSpace(speaker.DisplayName)
                ? speaker.DisplayName
                : speaker.name;

        var builder = new StringBuilder(value.Length);
        bool separatorPending = false;
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && builder.Length > 0)
                    builder.Append('_');
                builder.Append(character);
                separatorPending = false;
            }
            else
            {
                separatorPending = builder.Length > 0;
            }
        }

        return builder.Length > 0 ? builder.ToString() : "UnknownSpeaker";
    }

    private sealed class SpeakerSheet
    {
        public SpeakerSheet(string fileStem)
        {
            FileStem = fileStem;
        }

        public string FileStem { get; }
        public List<Row> Rows { get; } = new List<Row>();
    }

    private sealed class Row
    {
        public Row(
            string lineId,
            int chapter,
            int year,
            int age,
            string phase,
            string lauraText,
            string everieText,
            bool twoTakes,
            string source)
        {
            LineId = lineId;
            Chapter = chapter;
            Year = year;
            Age = age;
            Phase = phase;
            LauraText = lauraText;
            EverieText = everieText;
            TwoTakes = twoTakes;
            Source = source;
        }

        public string LineId { get; }
        public int Chapter { get; }
        public int Year { get; }
        public int Age { get; }
        public string Phase { get; }
        public string LauraText { get; }
        public string EverieText { get; }
        public bool TwoTakes { get; }
        public string Source { get; }
    }
}
