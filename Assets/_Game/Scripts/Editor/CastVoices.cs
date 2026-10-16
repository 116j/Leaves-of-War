using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class CastVoices : EditorWindow
{
    public sealed class Line
    {
        public readonly string character;
        public readonly string id;
        public readonly string text;

        public Line(string character, string id, string text)
        {
            this.character = character;
            this.id = id;
            this.text = text;
        }
    }

    public static readonly Line[] All =
    {
        new Line("Carlo", "L1_02", "Professor! The window again. Half the trees of Milan are on my floor."),
        new Line("Carlo", "L1_04", "Then you sweep it. There's the broom."),
        new Line("Carlo", "L1_05", "And that notebook you carry everywhere. Do you ever let it go?"),
        new Line("Carlo", "L1_07", "Show me, then. What do you write in it?"),
        new Line("Carlo", "L1_10", "Of course it was. And now it's by the window. Things don't wait for you to look at them, Professor."),
        new Line("Carlo", "L1_12", "Is that yours? It looks like your hand."),
        new Line("Carlo", "L1_14", "Keep your eyes on it! Paper is like a cat, it runs as soon as you turn around."),
        new Line("Carlo", "L1_15", "I don't understand poetry, Professor. But that one sounded like you."),
        new Line("Carlo", "L1_17", "I'll leave you to your leaves. Don't turn around too fast, or you won't know where you are."),
        new Line("Apollinaire", "L2_02", "Giuseppe. I thought I'd find you here."),
        new Line("Apollinaire", "L2_04", "The landlady hasn't touched anything. His things are still here."),
        new Line("Apollinaire", "L2_06", "He never seemed to belong anywhere. Not in Egypt, not in Paris. Like most of us in this city."),
        new Line("Apollinaire", "L2_07", "You told me he always slept on the left. The light woke him on the other side."),
        new Line("Apollinaire", "L2_10", "The landlady gave it away. Someone needed it more than a memory did."),
        new Line("Apollinaire", "L2_11", "He wrote letters at that desk, at night. Did he ever send one?"),
        new Line("Apollinaire", "L2_13", "He liked the window. He said Paris looked better from above. Poets and exiles always choose the window."),
        new Line("Apollinaire", "L2_15", "I know. I'm sorry I wasn't there."),
        new Line("Apollinaire", "L2_18", "You wrote it for him."),
        new Line("Apollinaire", "L2_20", "Then go. And don't look back too much, my friend. Paris will wait for you."),
        new Line("Pietro", "L3_03", "Who's there? Are you hurt?"),
        new Line("Pietro", "L3_05", "Everyone is awake tonight. Nobody can sleep with the guns like this."),
        new Line("Pietro", "L3_06", "What are you looking for down there?"),
        new Line("Pietro", "L3_08", "Then you need light. Stay close, I'll come back this way."),
        new Line("Pietro", "L3_10", "I'm sorry. I can't do anything for him now. We'll take him down at first light."),
        new Line("Pietro", "L3_12", "Then I'll leave you the light for a while."),
        new Line("Pietro", "L3_13", "Don't lose it. Whatever you're writing, it matters tonight."),
        new Line("Pietro", "L3_14", "You wrote all night."),
        new Line("Pietro", "L3_16", "Keep writing, then. Some of us need to know someone is."),
        new Line("Rui", "L4_02", "You're sweeping again. The students keep asking for you, Giuseppe."),
        new Line("Rui", "L4_04", "Neither do you."),
        new Line("Rui", "L4_05", "Jeanne asked me to tell you: leave his corner. She doesn't want anything moved."),
        new Line("Rui", "L4_07", "His drawings... Did Jeanne take them down?"),
        new Line("Rui", "L4_09", "Then don't stop looking."),
        new Line("Rui", "L4_11", "What is that?"),
        new Line("Rui", "L4_13", "Read it. I'll listen."),
        new Line("Rui", "L4_14", "It's him. You kept him."),
        new Line("Rui", "L4_16", "The war is coming here too. Soon they'll send you home, to Italy."),
        new Line("Leone", "L5_02", "Professor, stay away from the window. There are soldiers at the corner again."),
        new Line("Leone", "L5_04", "You sweep when you're worried. You've swept this floor three times since I arrived."),
        new Line("Leone", "L5_05", "Do you hear them? They're shouting again."),
        new Line("Leone", "L5_07", "Then keep looking. Someone in this house should."),
        new Line("Leone", "L5_08", "It stopped. For now."),
        new Line("Leone", "L5_09", "They're taking someone away. Don't look too long, Professor."),
        new Line("Leone", "L5_10", "It's quiet. Too quiet. Write it down before it starts again."),
        new Line("Leone", "L5_11", "There's something under the leaves. By the door."),
        new Line("Leone", "L5_13", "Don't let them win, Professor. Finish it."),
        new Line("Leone", "L5_14", "You never talk about the other war. The first one."),
        new Line("Leone", "L5_16", "Then close them. I'll keep watch."),
        new Line("Lucien", "L6_02", "Water, soldier? It's a warm night."),
        new Line("Lucien", "L6_04", "Nobody should be here. But here we all are."),
        new Line("Lucien", "L6_05", "Strange. They were green a moment ago."),
        new Line("Lucien", "L6_07", "My son wrote that the wood is full of men you can't see."),
        new Line("Lucien", "L6_09", "Then don't turn around. Look at what you're writing."),
        new Line("Lucien", "L6_10", "Is it finished?"),
        new Line("Lucien", "L6_12", "Then keep it safe. Some of these leaves won't see the autumn."),
    };

    private static readonly (string character, string[] tokens)[] Names =
    {
        ("Carlo", new[] { "carlo", "car" }),
        ("Apollinaire", new[] { "apollinaire", "apo", "guillaume" }),
        ("Pietro", new[] { "pietro", "pie" }),
        ("Rui", new[] { "rui" }),
        ("Leone", new[] { "leone", "leo" }),
        ("Lucien", new[] { "lucien", "luc" }),
    };

    private const string VoiceRoot = "Assets/_Game/Audio/Voices";
    private static readonly string[] Characters = Names.Select(n => n.character).ToArray();

    private sealed class Plan
    {
        public string from;
        public string to;
        public Line line;
        public bool on = true;
    }

    private string source = "Assets/Audio/Import";
    private List<Plan> plans = new List<Plan>();
    private List<string> unknown = new List<string>();
    private Vector2 scroll;
    private string report = "";

    private AudioClip whole;
    private int wholeCharacter = 1;
    private List<Vector2> segments = new List<Vector2>();
    private float threshold = 0.02f;

    [MenuItem("Tools/Leaves of War/Cast Voices (import and split)")]
    private static void Open()
    {
        GetWindow<CastVoices>("Cast Voices").minSize = new Vector2(640f, 520f);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("1. Import the cast voices", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Files are recognised by the character's name (Carlo, Pietro, Rui, Leone, Lucien, Apollinaire, or CAR_, PIE_, LEO_...) and their number (track3, 03, - 3). " +
            "The Nth file of a character becomes his Nth line of the script, saved as " + VoiceRoot + "/<Character>/L1_02.wav etc. Check the list before applying.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            source = EditorGUILayout.TextField("Folder", source);
            if (GUILayout.Button("Scan", GUILayout.Width(80f)))
                Scan();
        }
        foreach (Plan plan in plans)
            using (new EditorGUILayout.HorizontalScope())
            {
                plan.on = EditorGUILayout.Toggle(plan.on, GUILayout.Width(18f));
                EditorGUILayout.LabelField(Path.GetFileName(plan.from), GUILayout.Width(230f));
                EditorGUILayout.LabelField("→ " + plan.line.id + "  " + plan.line.character + ": " + plan.line.text, EditorStyles.miniLabel);
            }
        if (unknown.Count > 0)
            EditorGUILayout.HelpBox("Not recognised (rename them or split them below):\n" + string.Join("\n", unknown.Select(Path.GetFileName)), MessageType.Warning);
        using (new EditorGUI.DisabledScope(!plans.Any(p => p.on)))
            if (GUILayout.Button("Import selected", GUILayout.Height(26f)))
                Import();

        EditorGUILayout.Space(14f);
        EditorGUILayout.LabelField("2. Split one recording into lines", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("For an actor who recorded all his lines in one file: the longest silences become the cuts, one per line of the character. Listen to each piece before saving.", MessageType.None);
        AudioClip picked = (AudioClip)EditorGUILayout.ObjectField("Recording", whole, typeof(AudioClip), false);
        int character = EditorGUILayout.Popup("Character", wholeCharacter, Characters);
        threshold = EditorGUILayout.Slider("Silence below", threshold, 0.002f, 0.1f);
        if (picked != whole || character != wholeCharacter)
        {
            whole = picked;
            wholeCharacter = character;
            segments.Clear();
        }
        Line[] lines = All.Where(l => l.character == Characters[wholeCharacter]).ToArray();
        using (new EditorGUI.DisabledScope(whole == null))
            if (GUILayout.Button("Find " + lines.Length + " lines", GUILayout.Height(24f)))
                segments = Detect(whole, lines.Length, threshold);
        for (int i = 0; i < segments.Count; i++)
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶", GUILayout.Width(26f)))
                    Preview(whole, segments[i].x);
                EditorGUILayout.LabelField(segments[i].x.ToString("0.0") + "–" + segments[i].y.ToString("0.0") + " s", GUILayout.Width(90f));
                string label = i < lines.Length ? lines[i].id + "  " + lines[i].text : "(extra)";
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("■ Stop"))
                StopPreview();
            using (new EditorGUI.DisabledScope(segments.Count == 0))
                if (GUILayout.Button("Save pieces", GUILayout.Height(24f)))
                    SaveSegments(lines);
        }
        if (!string.IsNullOrEmpty(report))
            EditorGUILayout.HelpBox(report, MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private void Scan()
    {
        plans.Clear();
        unknown.Clear();
        if (!AssetDatabase.IsValidFolder(source))
        {
            report = source + " does not exist.";
            return;
        }
        var found = new Dictionary<string, List<(int order, string path, Line direct)>>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { source }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            string character = CharacterOf(name);
            if (character == null)
            {
                unknown.Add(path);
                continue;
            }
            string id = LevelId(name);
            Line direct = All.FirstOrDefault(l => l.character == character && (l.id == id || Regex.IsMatch(name, "(^|[^0-9a-z])" + l.id.Replace("_", "[_ ]?") + "($|[^0-9])", RegexOptions.IgnoreCase)));
            if (direct == null && id != null)
            {
                unknown.Add(path + "  (" + id + " is not one of " + character + "'s lines)");
                continue;
            }
            int order = Number(name);
            if (direct == null && order < 0)
            {
                unknown.Add(path);
                continue;
            }
            if (!found.TryGetValue(character, out var list))
                found[character] = list = new List<(int, string, Line)>();
            list.Add((order, path, direct));
        }
        foreach (var pair in found)
        {
            Line[] lines = All.Where(l => l.character == pair.Key).ToArray();
            int index = 0;
            foreach (var file in pair.Value.OrderBy(f => f.direct != null ? Array.IndexOf(lines, f.direct) : f.order))
            {
                Line line = file.direct ?? (index < lines.Length ? lines[index] : null);
                index = line != null ? Array.IndexOf(lines, line) + 1 : index + 1;
                if (line == null)
                {
                    unknown.Add(file.path);
                    continue;
                }
                plans.Add(new Plan { from = file.path, line = line, to = VoiceRoot + "/" + line.character + "/" + line.id + Path.GetExtension(file.path) });
            }
        }
        report = plans.Count + " files recognised, " + unknown.Count + " not.";
    }

    private static string CharacterOf(string name)
    {
        string lower = name.ToLowerInvariant();
        if (lower.StartsWith("ung") || lower.Contains("ungaretti") || lower.Contains("poesia"))
            return null;
        foreach (var (character, tokens) in Names)
            foreach (string token in tokens)
                if (Regex.IsMatch(lower, "(^|[^a-z])" + token + "([^a-z]|$)") || token.Length > 3 && lower.Contains(token))
                    return character;
        return null;
    }

    private static string LevelId(string name)
    {
        Match m = Regex.Match(name, @"(?:lvl|level|liv|livello)[ _-]?(\d)[ _-]+0*(\d+)", RegexOptions.IgnoreCase);
        return m.Success ? "L" + m.Groups[1].Value + "_" + int.Parse(m.Groups[2].Value).ToString("00") : null;
    }

    private static int Number(string name)
    {
        Match track = Regex.Match(name, @"track\s*0*(\d+)", RegexOptions.IgnoreCase);
        if (track.Success)
            return int.Parse(track.Groups[1].Value);
        MatchCollection all = Regex.Matches(Regex.Replace(name, @"\bL\d[_ ]", " ", RegexOptions.IgnoreCase), @"\d+");
        return all.Count > 0 ? int.Parse(all[all.Count - 1].Value) : -1;
    }

    private void Import()
    {
        int moved = 0;
        foreach (Plan plan in plans.Where(p => p.on))
        {
            EnsureFolder(Path.GetDirectoryName(plan.to).Replace('\\', '/'));
            if (File.Exists(plan.to) && plan.to != plan.from)
                AssetDatabase.DeleteAsset(plan.to);
            string error = AssetDatabase.MoveAsset(plan.from, plan.to);
            if (!string.IsNullOrEmpty(error))
                continue;
            Configure(plan.to);
            moved++;
        }
        AssetDatabase.Refresh();
        plans.Clear();
        report = moved + " voices imported into " + VoiceRoot + ".\nNow run Setup Milano Script / Setup Paris Script again (and the other levels when they exist).";
    }

    private static void Configure(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
            return;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.75f;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.SaveAndReimport();
    }

    private static float[] Samples(AudioClip clip, out int channels, out int frequency)
    {
        channels = clip.channels;
        frequency = clip.frequency;
        string path = AssetDatabase.GetAssetPath(clip);
        if (AssetImporter.GetAtPath(path) is AudioImporter importer && importer.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        clip.LoadAudioData();
        var data = new float[clip.samples * clip.channels];
        return clip.GetData(data, 0) ? data : null;
    }

    private static List<Vector2> Detect(AudioClip clip, int count, float threshold)
    {
        var result = new List<Vector2>();
        float[] data = Samples(clip, out int channels, out int frequency);
        if (data == null || count < 1)
            return result;
        const float window = 0.02f;
        int frame = Mathf.Max(1, Mathf.RoundToInt(frequency * window)) * channels;
        int windows = (data.Length + frame - 1) / frame;
        var loud = new bool[windows];
        for (int w = 0; w < windows; w++)
        {
            float sum = 0f;
            int a = w * frame, b = Mathf.Min(data.Length, a + frame);
            for (int i = a; i < b; i++)
                sum += data[i] * data[i];
            loud[w] = Mathf.Sqrt(sum / Mathf.Max(1, b - a)) >= threshold;
        }
        int first = Array.IndexOf(loud, true), last = Array.LastIndexOf(loud, true);
        if (first < 0)
            return result;
        var gaps = new List<(int start, int end)>();
        int run = -1;
        for (int w = first; w <= last; w++)
        {
            if (!loud[w])
            {
                if (run < 0)
                    run = w;
            }
            else if (run >= 0)
            {
                gaps.Add((run, w));
                run = -1;
            }
        }
        var cuts = gaps.OrderByDescending(g => g.end - g.start).Take(count - 1).OrderBy(g => g.start).ToList();
        int start = first;
        foreach (var gap in cuts)
        {
            result.Add(new Vector2(Mathf.Max(0f, start * window - 0.05f), gap.start * window + 0.12f));
            start = gap.end;
        }
        result.Add(new Vector2(Mathf.Max(0f, start * window - 0.05f), Mathf.Min(clip.length, (last + 1) * window + 0.15f)));
        return result;
    }

    private void SaveSegments(Line[] lines)
    {
        float[] data = Samples(whole, out int channels, out int frequency);
        if (data == null)
            return;
        int saved = 0;
        for (int i = 0; i < segments.Count && i < lines.Length; i++)
        {
            string folder = VoiceRoot + "/" + lines[i].character;
            EnsureFolder(folder);
            string path = folder + "/" + lines[i].id + ".wav";
            int a = Mathf.Clamp(Mathf.RoundToInt(segments[i].x * frequency) * channels, 0, data.Length);
            int b = Mathf.Clamp(Mathf.RoundToInt(segments[i].y * frequency) * channels, a, data.Length);
            WriteWav(path, data, a, b - a, channels, frequency);
            saved++;
        }
        AssetDatabase.Refresh();
        for (int i = 0; i < saved; i++)
            Configure(VoiceRoot + "/" + lines[i].character + "/" + lines[i].id + ".wav");
        report = saved + " lines of " + Characters[wholeCharacter] + " saved in " + VoiceRoot + "/" + Characters[wholeCharacter] + ".\nNow run the level setup again (e.g. Setup Paris Script).";
    }

    private static void WriteWav(string path, float[] data, int offset, int count, int channels, int frequency)
    {
        using var stream = new FileStream(path, FileMode.Create);
        using var writer = new BinaryWriter(stream);
        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + count * 2);
        writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(frequency);
        writer.Write(frequency * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(count * 2);
        for (int i = 0; i < count; i++)
            writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(data[offset + i] * 32767f), -32768, 32767));
    }

    private static void Preview(AudioClip clip, float start)
    {
        StopPreview();
        Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        MethodInfo play = util?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        play?.Invoke(null, new object[] { clip, Mathf.RoundToInt(start * clip.frequency), false });
    }

    private static void StopPreview()
    {
        Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        util?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
