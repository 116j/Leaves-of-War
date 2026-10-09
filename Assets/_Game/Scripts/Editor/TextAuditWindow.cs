using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.EditorTools
{
    /// <summary>
    /// Finds player-visible text across the project, as a starting point
    /// for translation work.
    ///
    /// "Scan Assets &amp; Scenes" is precise: it walks every serialized
    /// string field on every ScriptableObject asset and every component in
    /// every Build Settings scene (LineContent, NameVariantLineContent,
    /// DocumentDefinition pages, FlagInteractable prompts, etc. - anything
    /// serialized, wherever it lives).
    ///
    /// "Scan Code (Candidates)" is best-effort: it searches every .cs file
    /// under Assets/Scripts for string literals. It cannot reliably tell
    /// player-facing text apart from internal strings (resource paths,
    /// Debug.Log messages, tooltips only you see in the Inspector, etc.),
    /// so it filters out the most obvious noise and produces a CANDIDATE
    /// list that still needs a human pass - not an authoritative one.
    /// </summary>
    public sealed class TextAuditWindow : EditorWindow
    {
        private readonly struct TextEntry
        {
            public TextEntry(string source, string location, string field, string text)
            {
                Source = source;
                Location = location;
                Field = field;
                Text = text;
            }

            public string Source { get; }   // Asset path, or "Scene: X"
            public string Location { get; } // Object name / hierarchy path
            public string Field { get; }    // Serialized field path
            public string Text { get; }
        }

        private readonly struct CodeCandidate
        {
            public CodeCandidate(string file, int line, string text, string lineContent)
            {
                File = file;
                Line = line;
                Text = text;
                LineContent = lineContent;
            }

            public string File { get; }
            public int Line { get; }
            public string Text { get; }
            public string LineContent { get; }
        }

        private const string ScriptsRootFolder = "Assets/Scripts";
        private const int MaxDisplayedResults = 200;

        /// <summary>
        /// Lists every .cs file under the given folder, sorted, excluding
        /// anything inside a folder literally named "Editor" - editor tool
        /// scripts (menu items, tooltips, debug GameObject names) never run
        /// in the shipped game and are never seen by the player.
        /// </summary>
        private static string[] GetScriptFiles(string absoluteRoot)
        {
            string[] files = Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories);
            var filtered = new List<string>(files.Length);
            foreach (string file in files)
            {
                string normalized = file.Replace('\\', '/');
                bool insideEditorFolder = normalized.Split('/')
                    .Any(segment => string.Equals(segment, "Editor", StringComparison.OrdinalIgnoreCase));
                if (!insideEditorFolder)
                    filtered.Add(file);
            }

            filtered.Sort(StringComparer.OrdinalIgnoreCase);
            return filtered.ToArray();
        }

        /// <summary>
        /// Constructors known to carry diegetic text at specific argument
        /// positions (0-indexed), found by actually reading the code -
        /// extend this list whenever another one turns up. Far more precise
        /// than the generic string-literal scan, since it only extracts
        /// text from arguments we've confirmed are headings/bodies/etc.
        /// </summary>
        private static readonly (string TypeName, int[] TextArgPositions)[] KnownTextConstructors =
        {
            // new SequencePresentation(kind, heading, body, backgroundColor, accentColor)
            ("SequencePresentation", new[] { 1, 2 }),
        };

        private List<TextEntry> assetSceneResults = new List<TextEntry>();
        private List<CodeCandidate> codeResults = new List<CodeCandidate>();
        private List<CodeCandidate> knownConstructorResults = new List<CodeCandidate>();
        private Vector2 assetSceneScroll;
        private Vector2 codeScroll;
        private Vector2 knownConstructorScroll;
        private string statusMessage = string.Empty;

        [MenuItem("Hortensia/Text Audit")]
        private static void Open()
        {
            var window = GetWindow<TextAuditWindow>("Text Audit");
            window.minSize = new Vector2(640f, 480f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Assets & Scenes (precise)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Walks every serialized string field on every ScriptableObject asset " +
                "and every component in every Build Settings scene. This will temporarily " +
                "open each Build Settings scene one at a time and restore your current " +
                "scene setup afterward - save your work first.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Assets && Scenes", GUILayout.Height(28f)))
                    RunAssetAndSceneScan();

                GUI.enabled = assetSceneResults.Count > 0;
                if (GUILayout.Button("Export CSV", GUILayout.Width(120f), GUILayout.Height(28f)))
                    ExportAssetSceneCsv();
                GUI.enabled = true;
            }

            if (!string.IsNullOrEmpty(statusMessage))
                EditorGUILayout.LabelField(statusMessage, EditorStyles.miniLabel);

            EditorGUILayout.LabelField($"Results: {assetSceneResults.Count} (showing first {MaxDisplayedResults})", EditorStyles.miniLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(assetSceneScroll, GUILayout.Height(200f)))
            {
                assetSceneScroll = scroll.scrollPosition;
                int shown = 0;
                foreach (TextEntry entry in assetSceneResults)
                {
                    if (shown++ >= MaxDisplayedResults)
                        break;
                    EditorGUILayout.LabelField($"{entry.Source} — {entry.Location} — {entry.Field}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(entry.Text, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.Space(4f);
                }
            }

            EditorGUILayout.Space(16f);
            EditorGUILayout.LabelField("Code (candidates - review manually)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Searches every .cs file under {ScriptsRootFolder} for string literals, " +
                "filtering out the most obvious noise (Debug.Log calls, Tooltip/Header " +
                "attributes, resource-path-looking strings). This CANNOT reliably tell " +
                "player-facing text apart from internal strings on its own - treat this as " +
                "a starting list to skim through, not a finished one.",
                MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Code (Candidates)", GUILayout.Height(28f)))
                    RunCodeScan();

                GUI.enabled = codeResults.Count > 0;
                if (GUILayout.Button("Export CSV", GUILayout.Width(120f), GUILayout.Height(28f)))
                    ExportCodeCsv();
                GUI.enabled = true;
            }

            EditorGUILayout.LabelField($"Candidates: {codeResults.Count} (showing first {MaxDisplayedResults}, grouped by file)", EditorStyles.miniLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(codeScroll, GUILayout.Height(200f)))
            {
                codeScroll = scroll.scrollPosition;
                int shown = 0;
                string currentFile = null;
                foreach (CodeCandidate entry in codeResults)
                {
                    if (shown++ >= MaxDisplayedResults)
                        break;

                    if (entry.File != currentFile)
                    {
                        currentFile = entry.File;
                        EditorGUILayout.Space(6f);
                        EditorGUILayout.LabelField(currentFile, EditorStyles.boldLabel);
                    }

                    EditorGUILayout.LabelField($"  Line {entry.Line}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(entry.Text, EditorStyles.wordWrappedLabel);
                }
            }

            EditorGUILayout.Space(16f);
            EditorGUILayout.LabelField("Known Text-Bearing Constructors (precise)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Looks for calls to constructors we've confirmed carry diegetic " +
                "text at specific argument positions (e.g. 'new SequencePresentation(kind, heading, body, ...)') " +
                "and extracts exactly those arguments - far more precise than the " +
                "generic scan above. Extend the KnownTextConstructors list in this " +
                "file whenever another one turns up.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Known Constructors", GUILayout.Height(28f)))
                    RunKnownConstructorScan();

                GUI.enabled = knownConstructorResults.Count > 0;
                if (GUILayout.Button("Export CSV", GUILayout.Width(120f), GUILayout.Height(28f)))
                    ExportKnownConstructorCsv();
                GUI.enabled = true;
            }

            EditorGUILayout.LabelField($"Results: {knownConstructorResults.Count} (grouped by file)", EditorStyles.miniLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(knownConstructorScroll, GUILayout.Height(200f)))
            {
                knownConstructorScroll = scroll.scrollPosition;
                string currentKnownFile = null;
                foreach (CodeCandidate entry in knownConstructorResults)
                {
                    if (entry.File != currentKnownFile)
                    {
                        currentKnownFile = entry.File;
                        EditorGUILayout.Space(6f);
                        EditorGUILayout.LabelField(currentKnownFile, EditorStyles.boldLabel);
                    }

                    EditorGUILayout.LabelField($"  Line {entry.Line} — {entry.LineContent}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(entry.Text, EditorStyles.wordWrappedLabel);
                }
            }
        }

        // ---------------------------------------------------------------
        // Assets & Scenes scan
        // ---------------------------------------------------------------

        private void RunAssetAndSceneScan()
        {
            assetSceneResults.Clear();
            statusMessage = "Scanning assets...";

            ScanScriptableObjectAssets();

            statusMessage = "Scanning Build Settings scenes...";
            ScanBuildSettingsScenes();

            statusMessage = $"Done - {assetSceneResults.Count} text entries found.";
            Repaint();
        }

        private void ScanScriptableObjectAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (i % 25 == 0)
                        EditorUtility.DisplayProgressBar("Text Audit", $"Assets: {path}", (float)i / guids.Length);

                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null)
                        continue;

                    try
                    {
                        var so = new SerializedObject(asset);
                        WalkStringProperties(so, path, asset.name, assetSceneResults);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Text Audit] Skipped asset '{path}' - {ex.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ScanBuildSettingsScenes()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            bool cancelled = false;

            try
            {
                EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
                for (int i = 0; i < buildScenes.Length; i++)
                {
                    if (!buildScenes[i].enabled)
                        continue;

                    string scenePath = buildScenes[i].path;
                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Text Audit", $"Opening scene: {scenePath}", (float)i / buildScenes.Length))
                    {
                        cancelled = true;
                        break;
                    }

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    GameObject[] roots = scene.GetRootGameObjects();
                    var progress = new ScanProgress { SceneLabel = scenePath, SceneIndex = i, SceneCount = buildScenes.Length };
                    for (int r = 0; r < roots.Length; r++)
                    {
                        if (!ScanGameObjectRecursive(roots[r], scenePath, progress))
                        {
                            cancelled = true;
                            break;
                        }
                    }

                    if (cancelled)
                        break;
                }
            }
            finally
            {
                // Guaranteed even if something above throws - a stuck progress
                // bar blocks the whole Editor and looks exactly like a hang.
                EditorUtility.ClearProgressBar();

                if (previousSetup != null && previousSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }

            if (cancelled)
                statusMessage = $"Cancelled - {assetSceneResults.Count} text entries found so far.";
        }

        private sealed class ScanProgress
        {
            public string SceneLabel;
            public int SceneIndex;
            public int SceneCount;
            public int ObjectsScanned;
        }

        /// <summary>Returns false if the user cancelled.</summary>
        private bool ScanGameObjectRecursive(GameObject go, string sceneLabel, ScanProgress progress)
        {
            progress.ObjectsScanned++;
            if (progress.ObjectsScanned % 40 == 0)
            {
                bool cancel = EditorUtility.DisplayCancelableProgressBar(
                    "Text Audit",
                    $"Scene {progress.SceneIndex + 1}/{progress.SceneCount}: {sceneLabel} ({progress.ObjectsScanned} objects scanned)",
                    (float)progress.SceneIndex / progress.SceneCount);
                if (cancel)
                    return false;
            }

            string hierarchyPath = GetHierarchyPath(go.transform);
            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue; // Missing script reference.

                // One malformed/unusual component throwing here must not
                // take down the whole scan - skip it and keep going.
                try
                {
                    var so = new SerializedObject(component);
                    WalkStringProperties(so, $"Scene: {sceneLabel}", hierarchyPath, assetSceneResults);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[Text Audit] Skipped a component on '{hierarchyPath}' ({component.GetType().Name}) - {ex.Message}");
                }
            }

            foreach (Transform child in go.transform)
            {
                if (!ScanGameObjectRecursive(child.gameObject, sceneLabel, progress))
                    return false;
            }

            return true;
        }

        private static string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            Transform current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static void WalkStringProperties(
            SerializedObject so,
            string source,
            string location,
            List<TextEntry> results)
        {
            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = true;

                // Skip descending into the script reference itself - not
                // player text, and can otherwise pull in unrelated noise.
                if (prop.name == "m_Script")
                {
                    enterChildren = false;
                    continue;
                }

                if (prop.propertyType == SerializedPropertyType.String)
                {
                    string value = prop.stringValue;
                    if (!string.IsNullOrWhiteSpace(value))
                        results.Add(new TextEntry(source, location, prop.propertyPath, value));
                }
            }
        }

        private void ExportAssetSceneCsv()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Assets & Scenes Text", Application.dataPath, "text-audit-assets-scenes", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            var sb = new StringBuilder();
            sb.AppendLine("Source,Location,Field,Text");
            foreach (TextEntry entry in assetSceneResults)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(entry.Source),
                    CsvEscape(entry.Location),
                    CsvEscape(entry.Field),
                    CsvEscape(entry.Text)));
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        // ---------------------------------------------------------------
        // Code scan (best-effort candidates)
        // ---------------------------------------------------------------

        private static readonly Regex StringLiteralPattern = new Regex(
            "\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

        private void RunCodeScan()
        {
            codeResults.Clear();
            statusMessage = "Scanning code...";

            string absoluteRoot = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                ScriptsRootFolder);

            if (!Directory.Exists(absoluteRoot))
            {
                statusMessage = $"Folder not found: {ScriptsRootFolder}";
                Repaint();
                return;
            }

            string[] files = GetScriptFiles(absoluteRoot);
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    if (i % 10 == 0)
                        EditorUtility.DisplayProgressBar("Text Audit", $"Code: {Path.GetFileName(files[i])}", (float)i / files.Length);

                    try
                    {
                        ScanCodeFile(files[i]);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Text Audit] Skipped file '{files[i]}' - {ex.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            statusMessage = $"Done - {codeResults.Count} candidate strings found (review needed).";
            Repaint();
        }

        private void ScanCodeFile(string absolutePath)
        {
            string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length).Replace('\\', '/');
            string[] lines = File.ReadAllLines(absolutePath);

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                string trimmed = line.TrimStart();

                // Obvious noise: skip whole lines that are clearly not
                // player-facing (logging, editor-only attributes, comments).
                if (trimmed.StartsWith("//"))
                    continue;
                if (trimmed.Contains("Debug.Log"))
                    continue;
                if (trimmed.StartsWith("[Tooltip") || trimmed.StartsWith("[Header"))
                    continue;

                MatchCollection matches = StringLiteralPattern.Matches(line);
                foreach (Match match in matches)
                {
                    string value = match.Groups[1].Value;
                    if (ShouldSkipCandidate(value))
                        continue;

                    codeResults.Add(new CodeCandidate(relativePath, lineIndex + 1, value, line.Trim()));
                }
            }
        }

        private static bool ShouldSkipCandidate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            // Too short to be meaningful player text (single chars, format
            // punctuation like "-", ":" used as separators in code).
            if (value.Trim().Length < 2)
                return true;

            // Looks like a resource/asset path ("Fonts/Gotfridus",
            // "Narrative/RetroUiTheme") rather than prose - no spaces, has
            // a slash.
            if (!value.Contains(" ") && value.Contains("/"))
                return true;

            // Shader property names and similar internal identifiers.
            if (value.StartsWith("_"))
                return true;

            // Pure format/placeholder strings with no letters at all.
            bool hasLetter = false;
            foreach (char c in value)
            {
                if (char.IsLetter(c))
                {
                    hasLetter = true;
                    break;
                }
            }
            if (!hasLetter)
                return true;

            return false;
        }

        private void ExportCodeCsv()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Code Candidates", Application.dataPath, "text-audit-code-candidates", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            var sb = new StringBuilder();
            sb.AppendLine("File,Line,Text,FullLine");
            foreach (CodeCandidate entry in codeResults)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(entry.File),
                    entry.Line.ToString(),
                    CsvEscape(entry.Text),
                    CsvEscape(entry.LineContent)));
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private static string CsvEscape(string value)
        {
            if (value == null)
                return string.Empty;

            bool needsQuotes = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
            string escaped = value.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{escaped}\"" : escaped;
        }

        // ---------------------------------------------------------------
        // Known text-bearing constructors scan (precise)
        // ---------------------------------------------------------------

        private void RunKnownConstructorScan()
        {
            knownConstructorResults.Clear();
            statusMessage = "Scanning known constructors...";

            string absoluteRoot = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                ScriptsRootFolder);

            if (!Directory.Exists(absoluteRoot))
            {
                statusMessage = $"Folder not found: {ScriptsRootFolder}";
                Repaint();
                return;
            }

            string[] files = GetScriptFiles(absoluteRoot);

            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    if (i % 10 == 0)
                        EditorUtility.DisplayProgressBar("Text Audit", $"Known constructors: {Path.GetFileName(files[i])}", (float)i / files.Length);

                    try
                    {
                        ScanFileForKnownConstructors(files[i]);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Text Audit] Skipped file '{files[i]}' - {ex.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            statusMessage = $"Done - {knownConstructorResults.Count} entries found from known constructors.";
            Repaint();
        }

        private void ScanFileForKnownConstructors(string absolutePath)
        {
            string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length).Replace('\\', '/');
            string content = File.ReadAllText(absolutePath);

            foreach ((string typeName, int[] positions) in KnownTextConstructors)
            {
                string marker = $"new {typeName}(";
                int searchStart = 0;
                while (true)
                {
                    int markerIndex = content.IndexOf(marker, searchStart, StringComparison.Ordinal);
                    if (markerIndex < 0)
                        break;

                    int openParenIndex = markerIndex + marker.Length - 1;
                    int closeParenIndex = FindMatchingParen(content, openParenIndex);
                    if (closeParenIndex < 0)
                    {
                        searchStart = openParenIndex + 1;
                        continue;
                    }

                    string argsText = content.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1);
                    List<string> args = SplitTopLevelArguments(argsText);
                    int lineNumber = 1 + CountNewlinesBefore(content, markerIndex);

                    foreach (int position in positions)
                    {
                        if (position < 0 || position >= args.Count)
                            continue;

                        if (TryExtractStringLiteral(args[position].Trim(), out string text) &&
                            !string.IsNullOrWhiteSpace(text))
                        {
                            knownConstructorResults.Add(new CodeCandidate(
                                relativePath, lineNumber, text, $"new {typeName}(...) arg {position}"));
                        }
                    }

                    searchStart = closeParenIndex + 1;
                }
            }
        }

        /// <summary>
        /// Given the index of an opening '(', returns the index of its
        /// matching ')' - correctly skipping over parens that appear inside
        /// string or char literals along the way.
        /// </summary>
        private static int FindMatchingParen(string text, int openParenIndex)
        {
            int depth = 0;
            bool inString = false;
            bool inChar = false;

            for (int i = openParenIndex; i < text.Length; i++)
            {
                char c = text[i];

                if (inString)
                {
                    if (c == '\\') i++; // Skip escaped character.
                    else if (c == '"') inString = false;
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\') i++;
                    else if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '\'') { inChar = true; continue; }
                if (c == '(') depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Splits an argument list on top-level commas only - commas inside
        /// nested parens/brackets/braces or inside string/char literals
        /// don't count as separators.
        /// </summary>
        private static List<string> SplitTopLevelArguments(string argsText)
        {
            var result = new List<string>();
            int depth = 0;
            bool inString = false;
            bool inChar = false;
            int segmentStart = 0;

            for (int i = 0; i < argsText.Length; i++)
            {
                char c = argsText[i];

                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\') i++;
                    else if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '\'') { inChar = true; continue; }
                if (c == '(' || c == '[' || c == '{') { depth++; continue; }
                if (c == ')' || c == ']' || c == '}') { depth--; continue; }

                if (c == ',' && depth == 0)
                {
                    result.Add(argsText.Substring(segmentStart, i - segmentStart));
                    segmentStart = i + 1;
                }
            }

            if (segmentStart <= argsText.Length)
                result.Add(argsText.Substring(segmentStart));

            return result;
        }

        /// <summary>
        /// Recognises a plain "..." or $"..." string literal argument and
        /// returns its content, unescaped. Returns false for anything else
        /// (a variable, an enum value, a nested call, etc.) - those aren't
        /// literal text we can extract.
        /// </summary>
        private static bool TryExtractStringLiteral(string arg, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(arg))
                return false;

            bool interpolated = arg.StartsWith("$\"", StringComparison.Ordinal);
            bool plain = !interpolated && arg.StartsWith("\"", StringComparison.Ordinal);
            if (!interpolated && !plain)
                return false;
            if (!arg.EndsWith("\"", StringComparison.Ordinal))
                return false;

            int contentStart = interpolated ? 2 : 1;
            int contentLength = arg.Length - contentStart - 1;
            if (contentLength < 0)
                return false;

            string raw = arg.Substring(contentStart, contentLength);
            value = raw
                .Replace("\\n", "\n")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
            return true;
        }

        private static int CountNewlinesBefore(string content, int index)
        {
            int count = 0;
            for (int i = 0; i < index && i < content.Length; i++)
            {
                if (content[i] == '\n')
                    count++;
            }
            return count;
        }

        private void ExportKnownConstructorCsv()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Known Constructor Text", Application.dataPath, "text-audit-known-constructors", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            var sb = new StringBuilder();
            sb.AppendLine("File,Line,Text,Pattern");
            foreach (CodeCandidate entry in knownConstructorResults)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(entry.File),
                    entry.Line.ToString(),
                    CsvEscape(entry.Text),
                    CsvEscape(entry.LineContent)));
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }
    }
}