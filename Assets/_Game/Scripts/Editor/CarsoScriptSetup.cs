using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using P = ParisScriptSetup;

public static class CarsoScriptSetup
{
    private const string Ungaretti = "ungaretti";
    private const string Pietro = "pietro";
    private const int PoemId = 3;
    private const string Veglia =
        "Un’intera nottata\nbuttato vicino\na un compagno\nmassacrato\ncon la sua bocca\ndigrignata\nvolta al plenilunio\ncon la congestione\ndelle sue mani\npenetrata\nnel mio silenzio\nho scritto\nlettere piene d’amore\n\nNon sono mai stato\ntanto\nattaccato alla vita";

    private static readonly Dictionary<string, string> PietroLines = new Dictionary<string, string>
    {
        { "03", "Who's there? Are you hurt?" },
        { "05", "Everyone is awake tonight. Nobody can sleep with the guns like this." },
        { "06", "What are you looking for down there?" },
        { "08", "Then you need light. Stay close, I'll come back this way." },
        { "10", "I'm sorry. I can't do anything for him now. We'll take him down at first light." },
        { "12", "Then I'll leave you the light for a while." },
        { "13", "Don't lose it. Whatever you're writing, it matters tonight." },
        { "14", "You wrote all night." },
        { "16", "Keep writing, then. Some of us need to know someone is." },
    };

    private const int PietroIndex = 0, Companion = 1;

    [MenuItem("Tools/Leaves of War/Setup Carso Script")]
    private static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform root = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Carso Trench");
        LevelFlow flow = Object.FindAnyObjectByType<LevelFlow>(FindObjectsInactive.Include);
        if (root == null || flow == null)
        {
            EditorUtility.DisplayDialog("Carso Script", "Open Level 3 - Carso (built with Build Level 3 - Carso) first.", "OK");
            return;
        }
        var log = new List<string>();
        UngarettiLines.Refresh();
        P.RelinkHeldItems(log);
        P.RelinkNotebookTexts(log);

        Transform waypoints = Child(root, "Lantern Waypoints");
        Transform companion = Child(root, "Companion (seen only when unseen)");
        Transform poemArea = Child(root, "Poem Area (beside the companion)");
        Transform lantern = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Pietro's Lantern" && (t.parent == null || t.parent.name != "Lantern Post"));
        if (waypoints == null || companion == null || poemArea == null || lantern == null)
        {
            EditorUtility.DisplayDialog("Carso Script", "Waypoints, companion, poem area or lantern missing: rebuild with Build Level 3 - Carso.", "OK");
            return;
        }

        Transform[] wp = Enumerable.Range(1, 5).Select(i => waypoints.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("WP " + i))).ToArray();
        Transform kneel = Point(waypoints, "Pietro kneels (by the companion)", new Vector3(0.4f, 0f, 7.7f), Vector3.zero);
        kneel.rotation = Quaternion.LookRotation(Flat(companion.position - kneel.position), Vector3.up);
        Transform waits = Point(waypoints, "Pietro waits (trench end)", new Vector3(0.2f, 0f, 9.0f), new Vector3(0f, 180f, 0f));

        Transform pietro = P.Figure(root, "Pietro", new Vector3(0f, 0f, 0f), 0f, P.Mat("Trench Earth"), P.Mat("Linen"));
        pietro.SetPositionAndRotation(wp[0].position, wp[0].rotation);
        DialogueSpeaker speaker = P.Get<DialogueSpeaker>(pietro);
        var speakerSo = new SerializedObject(speaker);
        speakerSo.FindProperty("speakerId").stringValue = Pietro;
        speakerSo.FindProperty("displayName").stringValue = "Pietro";
        speakerSo.ApplyModifiedPropertiesWithoutUndo();
        DialogueTrigger trigger = P.Get<DialogueTrigger>(pietro);

        lantern.SetParent(pietro, true);
        lantern.localPosition = new Vector3(0.28f, 0.75f, 0.22f);
        lantern.localRotation = Quaternion.identity;
        P.Get<LanternGust>(lantern);
        Transform hung = HungLantern(root, lantern);
        P.Get<LanternGust>(hung);

        var path = P.Get<WaypointChange>(pietro);
        foreach (ChangeBehaviour other in pietro.GetComponents<ChangeBehaviour>().Where(c => c != path).ToList())
            Object.DestroyImmediate(other);
        var pathSo = new SerializedObject(path);
        pathSo.FindProperty("_carriedLantern").objectReferenceValue = lantern.gameObject;
        pathSo.FindProperty("_hungLantern").objectReferenceValue = hung.gameObject;
        var points = new (Transform target, bool stop, bool carry, bool hide)[]
        {
            (wp[1], false, true, false), (wp[2], false, true, false), (wp[3], false, true, false), (kneel, true, true, false),
            (waits, true, false, false),
            (kneel, true, true, false),
            (null, true, true, true),
        };
        SerializedProperty list = pathSo.FindProperty("_points");
        list.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++)
        {
            SerializedProperty p = list.GetArrayElementAtIndex(i);
            p.FindPropertyRelative("target").objectReferenceValue = points[i].target;
            p.FindPropertyRelative("stopHere").boolValue = points[i].stop;
            p.FindPropertyRelative("carryLantern").boolValue = points[i].carry;
            p.FindPropertyRelative("hide").boolValue = points[i].hide;
        }
        pathSo.ApplyModifiedPropertiesWithoutUndo();
        P.Get<Observable>(pietro);

        P.Get<PeripheralFigure>(companion);
        P.Get<Observable>(companion);
        if (!companion.TryGetComponent(out ChangeBehaviour _))
            companion.gameObject.AddComponent<VanishChange>();
        foreach (Collider c in companion.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);

        if (Object.FindAnyObjectByType<GazeManager>(FindObjectsInactive.Include) == null)
            new GameObject("GazeManager").AddComponent<GazeManager>();
        log.Add("Pietro carries the lantern along WP 1-5 one look-away at a time, kneels by the companion, hangs the light on the post, comes back for it at dawn and is gone.");
        log.Add("The companion shows only at the edge of sight, and vanishes for good at the end.");

        SetupPoem(poemArea, log);
        DawnAfterPoem dawn = P.Get<DawnAfterPoem>(root);
        var dawnSo = new SerializedObject(dawn);
        dawnSo.FindProperty("poemId").intValue = PoemId;
        dawnSo.ApplyModifiedPropertiesWithoutUndo();
        log.Add("Dawn: 3 seconds after Veglia the sky slowly greys (25 s).");

        TranscribePoem transcribe = Object.FindAnyObjectByType<TranscribePoem>(FindObjectsInactive.Include);
        if (transcribe != null)
        {
            var tso = new SerializedObject(transcribe);
            tso.FindProperty("_firstPoemId").intValue = 1;
            CopyEarlierPoems(tso, log);
            tso.ApplyModifiedPropertiesWithoutUndo();
        }

        var so = new SerializedObject(flow);
        so.FindProperty("titleCard").stringValue = "IL CARSO — CIMA QUATTRO — 23 DICEMBRE 1915";
        so.FindProperty("nextScene").stringValue = "Level 4 - San Paolo";
        so.FindProperty("character").objectReferenceValue = trigger;
        so.FindProperty("characterName").stringValue = "Pietro";
        so.FindProperty("lookAtCharacterOnStart").boolValue = false;
        so.FindProperty("followerLeafFromStart").boolValue = true;
        so.FindProperty("broom").objectReferenceValue = null;
        Transform exit = Child(root, "Exit Anchor (trench end)");
        if (exit != null)
            so.FindProperty("exitAnchor").objectReferenceValue = exit;
        SerializedProperty observables = so.FindProperty("observables");
        observables.arraySize = 2;
        observables.GetArrayElementAtIndex(PietroIndex).objectReferenceValue = pietro.GetComponent<Observable>();
        observables.GetArrayElementAtIndex(Companion).objectReferenceValue = companion.GetComponent<Observable>();

        const LevelFlow.Condition W = LevelFlow.Condition.Wait;
        const LevelFlow.Condition Changed = LevelFlow.Condition.ObservableBehaviourChanged;
        const LevelFlow.StepAction Move = LevelFlow.StepAction.MoveObjectWhenUnseen;
        var plan = new (string note, LevelFlow.Condition wait, float amount, int observable, string objective, string[] lines, LevelFlow.StepAction action)[]
        {
            ("Arrival", LevelFlow.Condition.None, 0f, 0, "", new[] { "U01", "U02" }, LevelFlow.StepAction.None),
            ("Pietro's lantern", LevelFlow.Condition.LookAtObservable, 0f, PietroIndex, "LOOK AT THE LANTERN AT THE END OF THE TRENCH", new[] { "P03", "U03", "P05" }, LevelFlow.StepAction.None),
            ("Sweep in the dark", LevelFlow.Condition.SweepLeaves, 30f, 0, "SWEEP THE TRENCH FLOOR", new[] { "P06", "U04", "P08" }, Move),
            ("Follow the lantern", Changed, 0f, PietroIndex, "FOLLOW THE LANTERN: EACH TIME YOU LOOK AWAY, PIETRO MOVES ON", new string[0], LevelFlow.StepAction.None),
            ("The companion", LevelFlow.Condition.LookAtObservable, 0f, Companion, "LOOK AT THE PLACE BESIDE YOU, UNDER THE BLANKET", new[] { "U05", "P10", "U06", "P12" }, LevelFlow.StepAction.None),
            ("Pietro leaves the light", W, 0.1f, PietroIndex, "", new string[0], Move),
            ("The postcard", LevelFlow.Condition.PoemFound, 0f, 0, "SWEEP BESIDE YOUR COMPANION, IN THE LANTERN LIGHT", new string[0], LevelFlow.StepAction.None),
            ("Veglia", LevelFlow.Condition.PoemTranscribed, 0f, 0, "KEEP YOUR EYES ON THE WORDS WHILE YOU TRANSCRIBE", new string[0], LevelFlow.StepAction.None),
            ("Silence", W, 3f, 0, "", new string[0], Move),
            ("Pietro comes back", Changed, 0f, PietroIndex, "LOOK AWAY: PIETRO IS COMING BACK FOR THE LANTERN", new[] { "P14", "U07", "P16" }, Move),
            ("Pietro is gone", Changed, 0f, Companion, "LOOK AWAY FROM PIETRO", new string[0], Move),
            ("The place is empty", Changed, 0f, Companion, "LOOK AT YOUR COMPANION, THEN LOOK AWAY", new[] { "U08" }, LevelFlow.StepAction.LoadNextLevel),
            ("Turn around", LevelFlow.Condition.TurnAround, 0f, 0, "TURN AWAY FROM THE END OF THE TRENCH", new string[0], LevelFlow.StepAction.None),
        };

        int voiced = 0, silent = 0;
        SerializedProperty steps = so.FindProperty("steps");
        steps.arraySize = plan.Length;
        for (int i = 0; i < plan.Length; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);
            step.FindPropertyRelative("note").stringValue = plan[i].note;
            step.FindPropertyRelative("waitFor").intValue = (int)plan[i].wait;
            step.FindPropertyRelative("amount").floatValue = plan[i].amount;
            step.FindPropertyRelative("observableIndex").intValue = plan[i].observable;
            step.FindPropertyRelative("fragment").intValue = 0;
            step.FindPropertyRelative("objective").stringValue = plan[i].objective;
            step.FindPropertyRelative("action").enumValueIndex = (int)plan[i].action;
            SerializedProperty lines = step.FindPropertyRelative("lines");
            lines.arraySize = plan[i].lines.Length;
            for (int l = 0; l < plan[i].lines.Length; l++)
                FillLine(lines.GetArrayElementAtIndex(l), plan[i].lines[l], ref voiced, ref silent);
        }
        SerializedProperty gust = so.FindProperty("firstGustLines");
        gust.arraySize = 1;
        FillLine(gust.GetArrayElementAtIndex(0), "P13", ref voiced, ref silent);
        so.ApplyModifiedPropertiesWithoutUndo();

        log.Add("Level 3 follows the script: " + plan.Length + " steps, " + voiced + " voiced pieces, " + silent + " without voice yet (Pietro: Cast Voices → Voices/Pietro/L3_03…).");
        log.Add("First gust during the transcription: Pietro, \"Don't lose it…\" and the lantern flickers.");
        log.Add("Next Scene: Level 4 - San Paolo. Exit: the end of the trench.");

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Setup Carso Script\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Carso Script", string.Join("\n\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static void SetupPoem(Transform area, List<string> log)
    {
        area.localRotation = Quaternion.Euler(90f, 0f, 0f);
        area.localScale = new Vector3(0.8f, 0.8f, 1f);
        area.localPosition = new Vector3(area.localPosition.x, 0.01f, area.localPosition.z);
        P.Get<MeshFilter>(area).sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        MeshRenderer renderer = P.Get<MeshRenderer>(area);
        renderer.sharedMaterial = P.InvisibleMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        PoemInfo info = AssetDatabase.FindAssets("t:PoemInfo").Select(g => AssetDatabase.LoadAssetAtPath<PoemInfo>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        AssignPoem poem = P.Get<AssignPoem>(area);
        if (poem.poemInfo == null)
            poem.poemInfo = info;
        AudioClip reading = UngarettiLines.Clip("UNG_poesia3");
        var so = new SerializedObject(poem);
        so.FindProperty("_poemId").intValue = PoemId;
        so.FindProperty("_assignedPoemName").stringValue = "Veglia";
        so.FindProperty("_assignedDate").stringValue = "23 dicembre 1915";
        so.FindProperty("_assignedPlace").stringValue = "Cima Quattro";
        so.FindProperty("_assignedPoemText").stringValue = Veglia;
        so.FindProperty("_poemVoice").objectReferenceValue = reading;
        so.FindProperty("_fragment").intValue = 0;
        so.FindProperty("_fragmentCount").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("Poem 3, Veglia (Cima Quattro, 23 dicembre 1915) on the floor beside the companion" + (reading != null ? ", read by UNG_poesia3." : "; UNG_poesia3 not found yet (Drive Audio)."));
    }

    private static void CopyEarlierPoems(SerializedObject transcribe, List<string> log)
    {
        var found = new List<(int id, string name, string date, string place, string text, Object voice)>();
        Scene active = SceneManager.GetActiveScene();
        foreach (string level in new[] { "Level 1 - Milano", "Level 2 - Parigi" })
        {
            string path = "Assets/_Game/Scenes/" + level + ".unity";
            if (!File.Exists(path))
                continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (AssignPoem p in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AssignPoem>(true))
                .Where(p => p.PoemId > 0 && p.PoemId < PoemId && p.Fragment <= 1).OrderBy(p => p.Fragment == 1 ? 0 : 1))
            {
                if (found.Any(f => f.id == p.PoemId))
                    continue;
                var s = new SerializedObject(p);
                string text = s.FindProperty("_assignedPoemText").stringValue;
                if (string.IsNullOrWhiteSpace(text))
                    text = s.FindProperty("_fragmentText").stringValue;
                found.Add((p.PoemId, s.FindProperty("_assignedPoemName").stringValue, s.FindProperty("_assignedDate").stringValue,
                    s.FindProperty("_assignedPlace").stringValue, text, s.FindProperty("_poemVoice").objectReferenceValue));
            }
            EditorSceneManager.CloseScene(scene, true);
        }
        SceneManager.SetActiveScene(active);
        found = found.OrderBy(f => f.id).ToList();
        SerializedProperty list = transcribe.FindProperty("_earlierPoems");
        list.arraySize = found.Count;
        for (int i = 0; i < found.Count; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("poemId").intValue = found[i].id;
            entry.FindPropertyRelative("poemName").stringValue = found[i].name;
            entry.FindPropertyRelative("poemDate").stringValue = found[i].date;
            entry.FindPropertyRelative("poemPlace").stringValue = found[i].place;
            entry.FindPropertyRelative("poemText").stringValue = found[i].text;
            entry.FindPropertyRelative("poemVoice").objectReferenceValue = found[i].voice;
        }
        log.Add(found.Count > 0 ? "The notebook already holds " + string.Join(", ", found.Select(f => "\"" + f.name + "\"")) + "." : "WARNING: no earlier poem found in Milano or Parigi.");
    }

    private static void FillLine(SerializedProperty line, string key, ref int voiced, ref int silent)
    {
        bool ungaretti = key[0] == 'U';
        string number = key.Substring(1);
        string text;
        AudioClip clip;
        if (ungaretti)
        {
            UngarettiLines.Line script = UngarettiLines.Get("Ungaretti - Carso - " + number);
            text = script != null ? script.English : key;
            SerializedProperty pieces0 = line.FindPropertyRelative("pieces");
            int count = script != null ? script.pieces.Length : 1;
            pieces0.arraySize = count;
            line.FindPropertyRelative("speakerId").stringValue = Ungaretti;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty piece = pieces0.GetArrayElementAtIndex(i);
                piece.FindPropertyRelative("text").stringValue = script != null ? script.pieces[i].english : key;
                AudioClip c = script != null ? UngarettiLines.Clip(script.pieces[i].file) : null;
                piece.FindPropertyRelative("voice").objectReferenceValue = c;
                piece.FindPropertyRelative("volume").floatValue = 0.55f;
                if (c != null)
                    voiced++;
                else
                    silent++;
            }
            return;
        }
        text = PietroLines[number];
        clip = PietroClip("L3_" + number);
        line.FindPropertyRelative("speakerId").stringValue = Pietro;
        SerializedProperty pieces = line.FindPropertyRelative("pieces");
        pieces.arraySize = 1;
        SerializedProperty only = pieces.GetArrayElementAtIndex(0);
        only.FindPropertyRelative("text").stringValue = text;
        only.FindPropertyRelative("voice").objectReferenceValue = clip;
        only.FindPropertyRelative("volume").floatValue = 0.55f;
        if (clip != null)
            voiced++;
        else
            silent++;
    }

    private static AudioClip PietroClip(string id)
    {
        string want = UngarettiLines.Clean(id);
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/_Trash") || path.IndexOf("pietro", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (UngarettiLines.FileKey(Path.GetFileNameWithoutExtension(path)) == want)
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }

    private static Transform HungLantern(Transform root, Transform carried)
    {
        Transform post = Child(root, "Lantern Post");
        if (post == null)
        {
            post = new GameObject("Lantern Post").transform;
            post.SetParent(root, false);
            post.localPosition = new Vector3(-0.66f, 0f, 5.4f);
            MilanoRoomBuilder.Box("Timber Post", post, new Vector3(0f, 0.8f, 0f), new Vector3(0.1f, 1.6f, 0.1f), P.Mat("Dark Wood"), false);
            MilanoRoomBuilder.Box("Hook", post, new Vector3(0.09f, 1.5f, 0f), new Vector3(0.12f, 0.02f, 0.02f), P.Mat("Painted Iron"), false);
        }
        Transform old = post.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Pietro's Lantern");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        GameObject copy = Object.Instantiate(carried.gameObject, post);
        copy.name = "Pietro's Lantern";
        copy.transform.localPosition = new Vector3(0.14f, 1.3f, 0f);
        copy.transform.localRotation = Quaternion.identity;
        copy.SetActive(false);
        return copy.transform;
    }

    private static Transform Point(Transform parent, string name, Vector3 local, Vector3 euler)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        t.localPosition = local;
        t.localRotation = Quaternion.Euler(euler);
        return t;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private static Transform Child(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
}
