using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ParisScriptSetup
{
    private const string Ungaretti = "ungaretti";
    private const string Apollinaire = "apollinaire";
    private const int PoemId = 2;
    private const string GreyboxFolder = "Assets/_Game/Materials/Greybox/";
    private const string FragmentPlaceholder = "[In memoria — paste the lines of fragment {0} here]";

    private static readonly string[] Fragments =
    {
        "Si chiamava\nMoammed Sceab\nDiscendente\ndi emiri di nomadi\nsuicida\nperché non aveva più\nPatria\nAmò la Francia\ne mutò nome\nFu Marcel",
        "ma non era Francese\ne non sapeva più\nvivere\nnella tenda dei suoi\ndove si ascolta la cantilena\ndel Corano\ngustando un caffè\nE non sapeva\nsciogliere\nil canto\ndel suo abbandono",
        "L’ho accompagnato\ninsieme alla padrona dell’albergo\ndove abitavamo\na Parigi\ndal numero 5 della rue des Carmes\nappassito vicolo in discesa.\nRiposa\nnel camposanto d’Ivry\nsobborgo che pare\nsempre\nin una giornata\ndi una\ndecomposta fiera\nE forse io solo\nso ancora\nche visse",
    };

    private static readonly Dictionary<string, string> ScriptIds = new Dictionary<string, string>
    {
        { "A01", "L2_02" }, { "A02", "L2_04" }, { "A03", "L2_06" }, { "A04", "L2_07" }, { "A05", "L2_10" },
        { "A06", "L2_11" }, { "A07", "L2_13" }, { "A08", "L2_15" }, { "A09", "L2_18" }, { "A10", "L2_20" },
    };

    private static readonly Dictionary<string, string> ApollinaireLines = new Dictionary<string, string>
    {
        { "01", "Giuseppe. I thought I'd find you here." },
        { "02", "The landlady hasn't touched anything. His things are still here." },
        { "03", "He never seemed to belong anywhere. Not in Egypt, not in Paris. Like most of us in this city." },
        { "04", "You told me he always slept on the left. The light woke him on the other side." },
        { "05", "The landlady gave it away. Someone needed it more than a memory did." },
        { "06", "He wrote letters at that desk, at night. Did he ever send one?" },
        { "07", "He liked the window. He said Paris looked better from above. Poets and exiles always choose the window." },
        { "08", "I know. I'm sorry I wasn't there." },
        { "09", "You wrote it for him." },
        { "10", "Then go. And don't look back too much, my friend. Paris will wait for you." },
    };

    private const int Coat = 0, Books = 1, Moammed = 2, Guillaume = 3;

    [MenuItem("Tools/Leaves of War/Setup Paris Script")]
    private static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform room = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Parigi Room");
        LevelFlow flow = Object.FindAnyObjectByType<LevelFlow>(FindObjectsInactive.Include);
        if (room == null || flow == null)
        {
            EditorUtility.DisplayDialog("Paris Script", "Open Level 2 - Parigi (built with Build Level 2 - Parigi) first.", "OK");
            return;
        }
        var log = new List<string>();
        UngarettiLines.Refresh();

        Transform coat = Child(room, "Moammed's Coat (vanishes)");
        Transform books = Child(room, "Books (vanish as a group)");
        Transform moammed = Figure(room, "Moammed", new Vector3(0.5f, 0f, 2.1f), 160f, Mat("Dark Coat"), Mat("Linen"));
        Transform guillaume = Figure(room, "Apollinaire", new Vector3(-0.45f, 0f, -2.1f), 30f, Mat("Grey Blanket"), Mat("Linen"));
        PickupItem broom = SetupBroom(room, log);
        RelinkHeldItems(log);
        PlaceEntry(room, log);
        if (coat == null || books == null)
        {
            EditorUtility.DisplayDialog("Paris Script", "Moammed's coat or the books are missing: rebuild the room with Build Level 2 - Parigi.", "OK");
            return;
        }

        DialogueSpeaker speaker = Get<DialogueSpeaker>(guillaume);
        var speakerSo = new SerializedObject(speaker);
        speakerSo.FindProperty("speakerId").stringValue = Apollinaire;
        speakerSo.FindProperty("displayName").stringValue = "Guillaume Apollinaire";
        speakerSo.ApplyModifiedPropertiesWithoutUndo();
        DialogueTrigger trigger = Get<DialogueTrigger>(guillaume);

        var observed = new[] { coat, books, moammed, guillaume };
        foreach (Transform t in observed)
        {
            Get<Observable>(t);
            if (!t.TryGetComponent(out ChangeBehaviour _))
                t.gameObject.AddComponent<VanishChange>();
        }
        if (Object.FindAnyObjectByType<GazeManager>(FindObjectsInactive.Include) == null)
            new GameObject("GazeManager").AddComponent<GazeManager>();
        log.Add("Observables (Vanish Change): 0 coat, 1 books, 2 Moammed, 3 Apollinaire.");

        int texts = SetupFragments(room, log);

        TranscribePoem transcribe = Object.FindAnyObjectByType<TranscribePoem>(FindObjectsInactive.Include);
        if (transcribe != null)
        {
            var tso = new SerializedObject(transcribe);
            tso.FindProperty("_firstPoemId").intValue = 1;
            CopyEarlierPoems(tso, log);
            tso.ApplyModifiedPropertiesWithoutUndo();
        }

        Transform exit = ExitAnchor(room);

        var so = new SerializedObject(flow);
        so.FindProperty("titleCard").stringValue = "PARIGI — 5 RUE DES CARMES — 1912–1914";
        so.FindProperty("nextScene").stringValue = "Level 3 - Carso";
        so.FindProperty("character").objectReferenceValue = trigger;
        so.FindProperty("characterName").stringValue = "Apollinaire";
        so.FindProperty("exitAnchor").objectReferenceValue = exit;
        so.FindProperty("followerLeafFromStart").boolValue = true;
        so.FindProperty("lookAtCharacterOnStart").boolValue = false;
        so.FindProperty("broom").objectReferenceValue = broom;
        SerializedProperty list = so.FindProperty("observables");
        list.arraySize = observed.Length;
        for (int i = 0; i < observed.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = observed[i].GetComponent<Observable>();

        const LevelFlow.Condition W = LevelFlow.Condition.Wait;
        var plan = new (string note, LevelFlow.Condition wait, float amount, int observable, int fragment, string objective, string[] lines, LevelFlow.StepAction action)[]
        {
            ("Arrival", LevelFlow.Condition.None, 0f, 0, 0, "", new[] { "U01" }, LevelFlow.StepAction.None),
            ("Apollinaire", W, 1f, 0, 0, "", new[] { "A01", "U02", "A02" }, LevelFlow.StepAction.None),
            ("Moammed at the window", LevelFlow.Condition.LookAtObservable, 0f, Moammed, 0, "LOOK AT THE MAN BY THE WINDOW", new[] { "U03", "A03" }, LevelFlow.StepAction.None),
            ("Broom", LevelFlow.Condition.PickUpBroom, 0f, 0, 0, "PICK UP THE BROOM", new string[0], LevelFlow.StepAction.None),
            ("Sweep between the beds", LevelFlow.Condition.SweepLeaves, 40f, 0, 0, "SWEEP BETWEEN THE TWO BEDS", new[] { "A04" }, LevelFlow.StepAction.None),
            ("Fragment 1", LevelFlow.Condition.FragmentFound, 0f, 0, 1, "SWEEP BETWEEN THE TWO BEDS", new[] { "U04" }, LevelFlow.StepAction.None),
            ("Transcribe 1", LevelFlow.Condition.FragmentTranscribed, 0f, Coat, 1, "KEEP YOUR EYES ON THE WORDS WHILE YOU TRANSCRIBE", new string[0], LevelFlow.StepAction.MoveObjectWhenUnseen),
            ("The coat is gone", LevelFlow.Condition.ObservableBehaviourChanged, 0f, Coat, 0, "LOOK AT THE COAT ON THE CHAIR, THEN LOOK AWAY", new[] { "U05", "A05" }, LevelFlow.StepAction.None),
            ("Fragment 2", LevelFlow.Condition.FragmentFound, 0f, 0, 2, "SWEEP UNDER THE DESK", new[] { "A06" }, LevelFlow.StepAction.None),
            ("Transcribe 2", LevelFlow.Condition.FragmentTranscribed, 0f, Books, 2, "KEEP YOUR EYES ON THE WORDS WHILE YOU TRANSCRIBE", new string[0], LevelFlow.StepAction.MoveObjectWhenUnseen),
            ("The shelf is empty", LevelFlow.Condition.ObservableBehaviourChanged, 0f, Books, 0, "LOOK AT THE BOOKS ON THE SHELF, THEN LOOK AWAY", new[] { "U06" }, LevelFlow.StepAction.None),
            ("Fragment 3", LevelFlow.Condition.FragmentFound, 0f, 0, 3, "SWEEP UNDER THE WINDOW", new[] { "A07", "U07", "A08", "U08" }, LevelFlow.StepAction.None),
            ("Transcribe 3", LevelFlow.Condition.FragmentTranscribed, 0f, Moammed, 3, "KEEP YOUR EYES ON THE WORDS WHILE YOU TRANSCRIBE", new string[0], LevelFlow.StepAction.MoveObjectWhenUnseen),
            ("Moammed is gone", LevelFlow.Condition.ObservableBehaviourChanged, 0f, Moammed, 0, "LOOK AT MOAMMED, THEN LOOK AWAY", new[] { "U09" }, LevelFlow.StepAction.JoinFragments),
            ("In memoria", LevelFlow.Condition.PoemTranscribed, 0f, 0, 0, "", new[] { "A09", "U10" }, LevelFlow.StepAction.None),
            ("Look at Apollinaire", LevelFlow.Condition.LookAtObservable, 0f, Guillaume, 0, "LOOK AT {character}", new[] { "A10" }, LevelFlow.StepAction.MoveObjectWhenUnseen),
            ("Apollinaire is gone", LevelFlow.Condition.ObservableBehaviourChanged, 0f, Guillaume, 0, "LOOK AWAY FROM {character}", new string[0], LevelFlow.StepAction.LoadNextLevel),
            ("Turn around", LevelFlow.Condition.TurnAround, 0f, 0, 0, "TURN AROUND", new string[0], LevelFlow.StepAction.None),
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
            step.FindPropertyRelative("fragment").intValue = plan[i].fragment;
            step.FindPropertyRelative("objective").stringValue = plan[i].objective;
            step.FindPropertyRelative("action").enumValueIndex = (int)plan[i].action;
            SerializedProperty lines = step.FindPropertyRelative("lines");
            lines.arraySize = plan[i].lines.Length;
            for (int l = 0; l < plan[i].lines.Length; l++)
                FillLine(lines.GetArrayElementAtIndex(l), plan[i].lines[l], ref voiced, ref silent);
        }
        so.FindProperty("firstGustLines").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.Add("Level 2 follows the script: " + plan.Length + " steps, " + voiced + " voiced pieces, " + silent + " still without voice (Apollinaire: name the files L2_02, L2_04... as in the script, and run this again).");
        log.Add(texts == 3 ? "The three fragments have their text." : "Paste the lines of In memoria into Fragment 1, 2 and 3 → Assign Poem → Fragment Text (" + (3 - texts) + " still empty).");
        log.Add("Next Scene: Level 3 - Carso. Exit: the door.");
        log.Add("Optional: Level Flow → Exit Cue = distant artillery; Window View material = Paris rooftops.");

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Setup Paris Script\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Paris Script", string.Join("\n\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static int SetupFragments(Transform room, List<string> log)
    {
        PoemInfo info = AssetDatabase.FindAssets("t:PoemInfo").Select(g => AssetDatabase.LoadAssetAtPath<PoemInfo>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        AssignPoem existing = Object.FindObjectsByType<AssignPoem>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p => p.poemInfo != null);
        if (existing != null)
            info = existing.poemInfo;
        Material invisible = InvisibleMaterial();
        AudioClip reading = UngarettiLines.Clip("UNG_poesia2");
        int filled = 0;
        for (int i = 1; i <= 3; i++)
        {
            Transform area = room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Fragment " + i + " "));
            if (area == null)
                continue;
            area.localRotation = Quaternion.Euler(90f, 0f, 0f);
            area.localScale = new Vector3(0.8f, 0.8f, 1f);
            area.localPosition = new Vector3(area.localPosition.x, 0.01f, area.localPosition.z);
            MeshFilter filter = Get<MeshFilter>(area);
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = Get<MeshRenderer>(area);
            renderer.sharedMaterial = invisible;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            AssignPoem poem = Get<AssignPoem>(area);
            poem.poemInfo = info;
            var so = new SerializedObject(poem);
            so.FindProperty("_poemId").intValue = PoemId;
            so.FindProperty("_assignedPoemName").stringValue = "In memoria";
            so.FindProperty("_assignedDate").stringValue = "30 settembre 1916";
            so.FindProperty("_assignedPlace").stringValue = "Locvizza";
            so.FindProperty("_poemVoice").objectReferenceValue = reading;
            so.FindProperty("_fragment").intValue = i;
            so.FindProperty("_fragmentCount").intValue = 3;
            SerializedProperty text = so.FindProperty("_fragmentText");
            if (string.IsNullOrWhiteSpace(text.stringValue) || text.stringValue.StartsWith("[In memoria"))
                text.stringValue = Fragments[i - 1];
            if (i == 1)
            {
                SerializedProperty whole = so.FindProperty("_assignedPoemText");
                if (string.IsNullOrWhiteSpace(whole.stringValue))
                    whole.stringValue = string.Join("\n", Fragments);
            }
            if (!text.stringValue.StartsWith("[In memoria"))
                filled++;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        log.Add("Fragments 1-3 of In memoria (poem 2)" + (reading != null ? ", read by UNG_poesia2 when they join." : "; UNG_poesia2 not found yet (Drive Audio)."));
        return filled;
    }

    private static void CopyEarlierPoems(SerializedObject transcribe, List<string> log)
    {
        const string milanoPath = "Assets/_Game/Scenes/Level 1 - Milano.unity";
        if (!File.Exists(milanoPath))
            return;
        Scene active = SceneManager.GetActiveScene();
        Scene milano = EditorSceneManager.OpenScene(milanoPath, OpenSceneMode.Additive);
        var poems = milano.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AssignPoem>(true))
            .Where(p => p.PoemId > 0 && p.PoemId < PoemId && !p.IsFragment)
            .GroupBy(p => p.PoemId).Select(g => g.FirstOrDefault(p => p.gameObject.activeInHierarchy) ?? g.First())
            .OrderBy(p => p.PoemId).ToList();
        SerializedProperty list = transcribe.FindProperty("_earlierPoems");
        list.arraySize = poems.Count;
        for (int i = 0; i < poems.Count; i++)
        {
            var source = new SerializedObject(poems[i]);
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("poemId").intValue = poems[i].PoemId;
            entry.FindPropertyRelative("poemName").stringValue = source.FindProperty("_assignedPoemName").stringValue;
            entry.FindPropertyRelative("poemDate").stringValue = source.FindProperty("_assignedDate").stringValue;
            entry.FindPropertyRelative("poemPlace").stringValue = source.FindProperty("_assignedPlace").stringValue;
            entry.FindPropertyRelative("poemText").stringValue = source.FindProperty("_assignedPoemText").stringValue;
            entry.FindPropertyRelative("poemVoice").objectReferenceValue = source.FindProperty("_poemVoice").objectReferenceValue;
        }
        transcribe.ApplyModifiedPropertiesWithoutUndo();
        string names = string.Join(", ", poems.Select(p => "\"" + new SerializedObject(p).FindProperty("_assignedPoemName").stringValue + "\""));
        EditorSceneManager.CloseScene(milano, true);
        SceneManager.SetActiveScene(active);
        log.Add(poems.Count > 0 ? "The notebook already holds " + names + " from Level 1." : "WARNING: no poem found in Level 1 - Milano.");
    }

    private static void FillLine(SerializedProperty line, string key, ref int voiced, ref int silent)
    {
        bool ungaretti = key[0] == 'U';
        string number = key.Substring(1);
        string text;
        AudioClip clip;
        if (ungaretti)
        {
            UngarettiLines.Line script = UngarettiLines.Get("Ungaretti - Parigi - " + number);
            text = script != null ? script.English : key;
            clip = script != null ? UngarettiLines.Clip(script.pieces[0].file) : null;
        }
        else
        {
            text = ApollinaireLines[number];
            clip = ClipNamed(ScriptIds[key], "Apollinaire - Parigi - " + number, "APO_L2_track" + int.Parse(number));
        }
        line.FindPropertyRelative("speakerId").stringValue = ungaretti ? Ungaretti : Apollinaire;
        SerializedProperty pieces = line.FindPropertyRelative("pieces");
        pieces.arraySize = 1;
        SerializedProperty piece = pieces.GetArrayElementAtIndex(0);
        piece.FindPropertyRelative("text").stringValue = text;
        piece.FindPropertyRelative("voice").objectReferenceValue = clip;
        piece.FindPropertyRelative("volume").floatValue = 0.55f;
        if (clip != null)
            voiced++;
        else
            silent++;
    }

    private static Transform Figure(Transform room, string name, Vector3 local, float yaw, Material clothes, Material skin)
    {
        Transform figure = room.Find(name);
        if (figure == null)
        {
            figure = new GameObject(name).transform;
            figure.SetParent(room, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(figure, false);
            body.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            body.transform.localScale = new Vector3(0.44f, 0.82f, 0.32f);
            body.GetComponent<Renderer>().sharedMaterial = clothes;
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(figure, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            head.transform.localScale = Vector3.one * 0.22f;
            Object.DestroyImmediate(head.GetComponent<Collider>());
            Object.DestroyImmediate(body.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = skin;
        }
        figure.localPosition = local;
        figure.localRotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (Collider c in figure.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
        return figure;
    }

    private static PickupItem SetupBroom(Transform room, List<string> log)
    {
        BroomSweeper sweeper = Object.FindAnyObjectByType<BroomSweeper>(FindObjectsInactive.Include);
        DiaryInteraction diary = Object.FindAnyObjectByType<DiaryInteraction>(FindObjectsInactive.Include);
        Vector3 position = room.TransformPoint(new Vector3(-1.35f, 0f, -1.0f));
        Quaternion rotation = room.rotation * Quaternion.Euler(0f, 90f, 0f);
        PickupItem broom = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p => p.Kind == PickupItem.Item.Broom);
        if (broom == null)
        {
            Transform held = null;
            if (sweeper != null)
                held = new SerializedObject(sweeper).FindProperty("broom").objectReferenceValue as Transform;
            if (held == null && diary != null)
                held = new SerializedObject(diary).FindProperty("broom").objectReferenceValue as Transform;
            broom = LevelStartSetup.BuildBroom(held, position, rotation);
        }
        else
        {
            broom.transform.SetPositionAndRotation(position, rotation);
            broom.gameObject.SetActive(true);
        }
        if (sweeper != null)
        {
            var so = new SerializedObject(sweeper);
            so.FindProperty("startInHand").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        log.Add("Broom leaning on the west wall, by the washstand: pick it up before sweeping.");
        return broom;
    }

    private static void RelinkHeldItems(List<string> log)
    {
        DiaryInteraction diary = Object.FindAnyObjectByType<DiaryInteraction>(FindObjectsInactive.Include);
        BroomSweeper sweeper = Object.FindAnyObjectByType<BroomSweeper>(FindObjectsInactive.Include);
        FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        if (diary == null || player == null)
            return;
        Transform[] held = player.GetComponentsInChildren<Transform>(true)
            .Concat(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)).ToArray();
        Transform book = held.FirstOrDefault(t => t.name == "Diary_Demo") ?? held.FirstOrDefault(t => t.name.StartsWith("Diary"));
        Transform stick = held.FirstOrDefault(t => t.name == "Broom_Stick");
        if (stick == null && sweeper != null)
            stick = new SerializedObject(sweeper).FindProperty("broom").objectReferenceValue as Transform;
        var so = new SerializedObject(diary);
        if (so.FindProperty("diary").objectReferenceValue == null && book != null)
            so.FindProperty("diary").objectReferenceValue = book;
        if (so.FindProperty("broom").objectReferenceValue == null && stick != null)
            so.FindProperty("broom").objectReferenceValue = stick;
        so.FindProperty("startInHand").boolValue = true;
        so.FindProperty("startClosed").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (sweeper != null && stick != null)
        {
            var sso = new SerializedObject(sweeper);
            if (sso.FindProperty("broom").objectReferenceValue == null)
            {
                sso.FindProperty("broom").objectReferenceValue = stick;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        log.Add(book != null ? "Notebook relinked to the player (" + book.name + "), closed in hand." : "WARNING: no Diary_Demo under the Player.");
    }

    private static void PlaceEntry(Transform room, List<string> log)
    {
        Transform entry = Child(room, "Entry Anchor (door)");
        if (entry == null)
            return;
        entry.localPosition = new Vector3(0.15f, 0f, -1.75f);
        FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        if (player != null)
            player.transform.position = new Vector3(entry.position.x, player.transform.position.y, entry.position.z);
        log.Add("Start point moved away from the desk and the chair.");
    }

    private static Transform ExitAnchor(Transform room)
    {
        Transform hinge = room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Door (hinge)");
        Transform exit = room.Find("Exit Anchor (door)");
        if (exit == null)
        {
            exit = new GameObject("Exit Anchor (door)").transform;
            exit.SetParent(room, false);
        }
        Vector3 doorCenter = hinge != null ? hinge.TransformPoint(new Vector3(0.425f, 0f, 0f)) : room.TransformPoint(new Vector3(0.9f, 0f, -2.5f));
        exit.position = new Vector3(doorCenter.x, room.position.y, doorCenter.z) + room.forward * 0.3f;
        exit.rotation = Quaternion.Euler(0f, room.eulerAngles.y + 180f, 0f);
        return exit;
    }

    private static AudioClip ClipNamed(params string[] names)
    {
        var wanted = new HashSet<string>(names.Select(UngarettiLines.Clean));
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/_Trash"))
                continue;
            string file = Path.GetFileNameWithoutExtension(path);
            int dash = file.IndexOf(" - ");
            if (wanted.Contains(UngarettiLines.Clean(file)) || dash > 0 && wanted.Contains(UngarettiLines.Clean(file.Substring(0, dash))) && file.StartsWith("APO_"))
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }

    private static T Get<T>(Transform t) where T : Component =>
        t.TryGetComponent(out T found) ? found : t.gameObject.AddComponent<T>();

    private static Transform Child(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(GreyboxFolder + name + ".mat");

    private static Material InvisibleMaterial()
    {
        foreach (string candidate in new[] { "Transparent", "TransporantMaterial" })
            foreach (string guid in AssetDatabase.FindAssets(candidate + " t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == candidate && !path.StartsWith("Assets/_Trash"))
                    return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
        AssignPoem milano = Object.FindObjectsByType<AssignPoem>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p => p.GetComponent<Renderer>() != null);
        return milano != null ? milano.GetComponent<Renderer>().sharedMaterial : null;
    }
}
