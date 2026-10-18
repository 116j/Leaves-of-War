using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using P = ParisScriptSetup;

public static class RomaScriptSetup
{
    private const string Ungaretti = "ungaretti";
    private const string Leone = "leone";
    private const int PoemId = 5;
    private const string NonGridate =
        "Cessate d'uccidere i morti,\nNon gridate più, non gridate\nSe li volete ancora udire,\nSe sperate di non perire.\n\n" +
        "Hanno l'impercettibile sussurro,\nNon fanno più rumore\nDel crescere dell'erba,\nLieta dove non passa l'uomo.";

    private static readonly Dictionary<string, string> LeoneLines = new Dictionary<string, string>
    {
        { "02", "Professor, stay away from the window. There are soldiers at the corner again." },
        { "04", "You sweep when you're worried. You've swept this floor three times since I arrived." },
        { "05", "Do you hear them? They're shouting again." },
        { "07", "Then keep looking. Someone in this house should." },
        { "08", "It stopped. For now." },
        { "09", "They're taking someone away. Don't look too long, Professor." },
        { "10", "It's quiet. Too quiet. Write it down before it starts again." },
        { "11", "There's something under the leaves. By the door." },
        { "13", "Don't let them win, Professor. Finish it." },
        { "14", "You never talk about the other war. The first one." },
        { "16", "Then close them. I'll keep watch." },
    };

    private const int LeoneIndex = 0, WindowIndex = 1;

    [MenuItem("Tools/Leaves of War/Setup Roma Script")]
    private static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform root = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Roma Study");
        LevelFlow flow = Object.FindAnyObjectByType<LevelFlow>(FindObjectsInactive.Include);
        if (root == null || flow == null)
        {
            EditorUtility.DisplayDialog("Roma Script", "Open Level 5 - Roma (built with Build Level 5 - Roma) first.", "OK");
            return;
        }
        var log = new List<string>();
        UngarettiLines.Refresh();
        P.RelinkHeldItems(log);
        P.RelinkNotebookTexts(log);
        CarryLevelSettings.FromPreviousLevel(scene, log);

        Transform props = Child(root, "Props");
        Transform poemArea = Child(root, "Poem Area (by the door)");
        Transform glass = Child(root, "Glass (interact)");
        Transform window = Child(root, "Window (shutters)");
        Transform voicesAnchor = Child(root, "Street Voices (source)");
        Transform leoneExit = Child(root, "Leone Exit (door)");
        Transform centre = Child(root, "Exit Anchor (room centre)");
        OpenableWindow shutters = window != null ? window.GetComponent<OpenableWindow>() : null;
        if (props == null || poemArea == null || glass == null || shutters == null || voicesAnchor == null)
        {
            EditorUtility.DisplayDialog("Roma Script", "The shutters, the street voices source or the poem area are missing: rebuild with Build Level 5 - Roma.", "OK");
            return;
        }

        Transform leone = P.Figure(props, "Leone", new Vector3(1.95f, 0f, -0.8f), 250f, P.Mat("Sofa Fabric"), P.Mat("Linen") ?? P.Mat("Paper Books"));
        DialogueSpeaker speaker = P.Get<DialogueSpeaker>(leone);
        var speakerSo = new SerializedObject(speaker);
        speakerSo.FindProperty("speakerId").stringValue = Leone;
        speakerSo.FindProperty("displayName").stringValue = "Leone";
        speakerSo.ApplyModifiedPropertiesWithoutUndo();
        DialogueTrigger trigger = P.Get<DialogueTrigger>(leone);
        P.Get<Observable>(leone);
        log.Add("Leone stands by the half-closed shutters.");

        Observable windowGaze = P.Get<Observable>(glass);
        var gazeSo = new SerializedObject(windowGaze);
        gazeSo.FindProperty("_changeDelay").floatValue = 2.5f;
        gazeSo.ApplyModifiedPropertiesWithoutUndo();
        foreach (ChangeBehaviour other in glass.GetComponents<ChangeBehaviour>())
            if (!(other is VoicesRiseChange))
                Object.DestroyImmediate(other);
        P.Get<VoicesRiseChange>(glass);

        StreetVoices voices = P.Get<StreetVoices>(voicesAnchor);
        var voicesSo = new SerializedObject(voices);
        voicesSo.FindProperty("window").objectReferenceValue = windowGaze;
        voicesSo.FindProperty("shutters").objectReferenceValue = shutters;
        voicesSo.ApplyModifiedPropertiesWithoutUndo();
        var shutterSo = new SerializedObject(shutters);
        shutterSo.FindProperty("startOpen").boolValue = true;
        shutterSo.ApplyModifiedPropertiesWithoutUndo();
        log.Add("Street voices: they rise while you look away from the window (0 dB, up to +9 dB after 4 s) and fall silent at once when you look at it (−40 dB). Closed shutters muffle them (AMB_05_Voices_Outside / _Inside, wired by Drive Audio).");

        SetupPoem(poemArea, log);

        TranscribePoem transcribe = Object.FindAnyObjectByType<TranscribePoem>(FindObjectsInactive.Include);
        if (transcribe != null)
        {
            var tso = new SerializedObject(transcribe);
            tso.FindProperty("_firstPoemId").intValue = 1;
            CopyEarlierPoems(tso, log);
            tso.ApplyModifiedPropertiesWithoutUndo();
            log.Add("Looking away while transcribing: the voices come back and a gust from the shutters covers the paper.");
        }

        var so = new SerializedObject(flow);
        so.FindProperty("titleCard").stringValue = "ROMA — 1943–1944";
        so.FindProperty("nextScene").stringValue = "Level 6 - Courton";
        so.FindProperty("character").objectReferenceValue = trigger;
        so.FindProperty("characterName").stringValue = "Leone";
        so.FindProperty("lookAtCharacterOnStart").boolValue = false;
        so.FindProperty("followerLeafFromStart").boolValue = true;
        so.FindProperty("broom").objectReferenceValue = null;
        so.FindProperty("door").objectReferenceValue = null;
        so.FindProperty("characterExitPoint").objectReferenceValue = leoneExit;
        if (centre != null)
            so.FindProperty("exitAnchor").objectReferenceValue = centre;
        SerializedProperty observables = so.FindProperty("observables");
        observables.arraySize = 2;
        observables.GetArrayElementAtIndex(LeoneIndex).objectReferenceValue = leone.GetComponent<Observable>();
        observables.GetArrayElementAtIndex(WindowIndex).objectReferenceValue = windowGaze;

        const LevelFlow.Condition W = LevelFlow.Condition.Wait;
        const LevelFlow.Condition Changed = LevelFlow.Condition.ObservableBehaviourChanged;
        const LevelFlow.Condition Look = LevelFlow.Condition.LookAtObservable;
        const LevelFlow.StepAction Move = LevelFlow.StepAction.MoveObjectWhenUnseen;
        const LevelFlow.StepAction None = LevelFlow.StepAction.None;
        var plan = new (string note, LevelFlow.Condition wait, float amount, int observable, string objective, string[] lines, LevelFlow.StepAction action)[]
        {
            ("Arrival", LevelFlow.Condition.None, 0f, 0, "", new[] { "U01" }, None),
            ("Leone", Look, 0f, LeoneIndex, "LOOK AT LEONE, BY THE SHUTTERS", new[] { "R02", "U02", "R04" }, None),
            ("Sweep the study", LevelFlow.Condition.SweepLeaves, 25f, WindowIndex, "SWEEP THE STUDY", new string[0], Move),
            ("The voices rise", Changed, 0f, WindowIndex, "", new[] { "R05", "U03", "R07" }, None),
            ("First silence", Look, 0f, WindowIndex, "LOOK AT THE WINDOW", new[] { "R08" }, Move),
            ("They rise again", Changed, 0f, WindowIndex, "", new string[0], None),
            ("Second silence", Look, 0f, WindowIndex, "LOOK AT THE WINDOW", new[] { "R09" }, Move),
            ("A third time", Changed, 0f, WindowIndex, "", new string[0], None),
            ("Third silence", Look, 0f, WindowIndex, "LOOK AT THE WINDOW", new[] { "R10" }, None),
            ("Words on the tiles", LevelFlow.Condition.PoemFound, 0f, 0, "SWEEP THE LEAVES BY THE DOOR", new[] { "R11", "U04" }, None),
            ("Non gridate più", LevelFlow.Condition.PoemTranscribed, 0f, 0, "KEEP YOUR EYES ON THE WORDS WHILE YOU TRANSCRIBE", new string[0], None),
            ("The other war", W, 2f, 0, "", new[] { "R14", "U05" }, None),
            ("Look at Leone", Look, 0f, LeoneIndex, "LOOK AT LEONE", new[] { "R16" }, None),
            ("Close the shutters", LevelFlow.Condition.ShuttersClosed, 0f, 0, "CLOSE THE SHUTTERS", new string[0], LevelFlow.StepAction.LoadNextLevel),
            ("Turn away from the room", LevelFlow.Condition.TurnAround, 0f, 0, "TURN AWAY FROM THE ROOM", new string[0], None),
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
        SerializedProperty gustLines = so.FindProperty("firstGustLines");
        gustLines.arraySize = 1;
        FillLine(gustLines.GetArrayElementAtIndex(0), "R13", ref voiced, ref silent);
        so.ApplyModifiedPropertiesWithoutUndo();

        log.Add("Level 5 follows the script: " + plan.Length + " steps, " + voiced + " voiced pieces, " + silent + " without voice yet.");
        log.Add("Three silences, then the words show through by the door. After the poem: the other war, Leone keeps watch, close the shutters and turn away from the room → Level 6 - Courton.");

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Setup Roma Script\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Roma Script", string.Join("\n\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
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
        AudioClip reading = UngarettiLines.Clip("UNG_poesia" + PoemId);
        var so = new SerializedObject(poem);
        so.FindProperty("_poemId").intValue = PoemId;
        so.FindProperty("_assignedPoemName").stringValue = "Non gridate più";
        so.FindProperty("_assignedDate").stringValue = "1943–1944";
        so.FindProperty("_assignedPlace").stringValue = "Roma occupata";
        so.FindProperty("_assignedPoemText").stringValue = NonGridate;
        so.FindProperty("_poemVoice").objectReferenceValue = reading;
        so.FindProperty("_fragment").intValue = 0;
        so.FindProperty("_fragmentCount").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("Poem 5, Non gridate più (Roma occupata, 1943–1944), on the tiles by the door" + (reading != null ? ", read by UNG_poesia5." : "; UNG_poesia5 not found yet (Drive Audio)."));
    }

    private static void CopyEarlierPoems(SerializedObject transcribe, List<string> log)
    {
        var found = new List<(int id, string name, string date, string place, string text, Object voice)>();
        Scene active = SceneManager.GetActiveScene();
        foreach (string level in new[] { "Level 1 - Milano", "Level 2 - Parigi", "Level 3 - Carso", "Level 4 - San Paolo" })
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
        log.Add(found.Count > 0 ? "The notebook already holds " + string.Join(", ", found.Select(f => "\"" + f.name + "\"")) + "." : "WARNING: no earlier poem found in Milano, Parigi, Carso or San Paolo.");
    }

    private static void FillLine(SerializedProperty line, string key, ref int voiced, ref int silent)
    {
        string number = key.Substring(1);
        if (key[0] == 'U')
        {
            UngarettiLines.Line script = UngarettiLines.Get("Ungaretti - Roma - " + number);
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
        AudioClip clip = LeoneClip("L5_" + number);
        line.FindPropertyRelative("speakerId").stringValue = Leone;
        SerializedProperty pieces = line.FindPropertyRelative("pieces");
        pieces.arraySize = 1;
        SerializedProperty only = pieces.GetArrayElementAtIndex(0);
        only.FindPropertyRelative("text").stringValue = LeoneLines[number];
        only.FindPropertyRelative("voice").objectReferenceValue = clip;
        only.FindPropertyRelative("volume").floatValue = 0.55f;
        if (clip != null)
            voiced++;
        else
            silent++;
    }

    private static AudioClip LeoneClip(string id)
    {
        string want = UngarettiLines.Clean(id);
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/_Trash") || path.IndexOf("/Leone/", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (UngarettiLines.FileKey(Path.GetFileNameWithoutExtension(path)) == want)
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }

    private static Transform Child(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
}
