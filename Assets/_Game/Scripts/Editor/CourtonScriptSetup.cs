using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using P = ParisScriptSetup;

public static class CourtonScriptSetup
{
    private const string Ungaretti = "ungaretti";
    private const string Lucien = "lucien";
    private const int PoemId = 6;
    private const string SoldiersFolder = "Assets/_Game/Art/3D/Generated/Courton";
    private const string Soldati = "Si sta come\nd'autunno\nsugli alberi\nle foglie";
    private static readonly int[] SoldierTrees = { 2, 5, 8, 12, 15 };

    private static readonly Dictionary<string, string> LucienLines = new Dictionary<string, string>
    {
        { "02", "Water, soldier? It's a warm night." },
        { "04", "Nobody should be here. But here we all are." },
        { "05", "Strange. They were green a moment ago." },
        { "07", "My son wrote that the wood is full of men you can't see." },
        { "09", "Then don't turn around. Look at what you're writing." },
        { "10", "Is it finished?" },
        { "12", "Then keep it safe. Some of these leaves won't see the autumn." },
    };

    private const int LucienIndex = 0, TreeA = 1, TreeB = 2, TreeC = 3, SoldiersIndex = 4;

    [MenuItem("Tools/Leaves of War/Setup Courton Script")]
    private static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform root = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Bosco di Courton");
        LevelFlow flow = Object.FindAnyObjectByType<LevelFlow>(FindObjectsInactive.Include);
        if (root == null || flow == null)
        {
            EditorUtility.DisplayDialog("Courton Script", "Open Level 6 - Courton (built with Build Level 6 - Courton) first.", "OK");
            return;
        }
        var log = new List<string>();
        UngarettiLines.Refresh();
        P.RelinkHeldItems(log);
        P.RelinkNotebookTexts(log);
        CarryLevelSettings.FromPreviousLevel(scene, log);

        Transform forest = Child(root, "Forest");
        Transform poemArea = Child(root, "Poem Area (centre)");
        Transform spot = Child(root, "Lucien Spot");
        if (forest == null || poemArea == null || spot == null)
        {
            EditorUtility.DisplayDialog("Courton Script", "The forest, Lucien's spot or the poem area are missing: rebuild with Build Level 6 - Courton.", "OK");
            return;
        }

        Transform[] leafTrees = new[] { "A", "B", "C" }.Select(l => Tree(forest, "Leaf Tree " + l)).ToArray();
        if (leafTrees.Any(t => t == null))
        {
            EditorUtility.DisplayDialog("Courton Script", "Leaf Tree A, B or C is missing in the Forest: rebuild with Build Level 6 - Courton.", "OK");
            return;
        }
        Material falling = MaterialAsset("Falling Leaf", AutumnTree.NewLeafMaterial);
        Material bare = MaterialAsset("Bare Branches", AutumnTree.BareMaterial);
        int trees = 0;
        foreach (Transform tree in forest.Cast<Transform>())
            if (tree.name.StartsWith("Forest Tree") || tree.name.StartsWith("Leaf Tree"))
            {
                var treeSo = new SerializedObject(P.Get<AutumnTree>(tree));
                treeSo.FindProperty("fallingLeaf").objectReferenceValue = falling;
                treeSo.FindProperty("bare").objectReferenceValue = bare;
                treeSo.ApplyModifiedPropertiesWithoutUndo();
                trees++;
            }
        foreach (Transform tree in leafTrees)
        {
            P.Get<Observable>(tree);
            foreach (ChangeBehaviour other in tree.GetComponents<ChangeBehaviour>())
                if (!(other is AutumnChange))
                    Object.DestroyImmediate(other);
            P.Get<AutumnChange>(tree);
        }
        log.Add(trees + " trees turn: each time you look away, the trees behind you change colour and drop some leaves. At the end, in one slow wave, every tree lets go.");

        Vector3 centre = poemArea.position;
        Vector3 away = Flat(spot.position - centre);
        Transform lucien = P.Figure(root, "Lucien", root.InverseTransformPoint(spot.position), Quaternion.LookRotation(-away).eulerAngles.y - root.eulerAngles.y, P.Mat("Tent Canvas"), P.Mat("Linen") ?? P.Mat("Bark"));
        DialogueSpeaker speaker = P.Get<DialogueSpeaker>(lucien);
        var speakerSo = new SerializedObject(speaker);
        speakerSo.FindProperty("speakerId").stringValue = Lucien;
        speakerSo.FindProperty("displayName").stringValue = "Lucien";
        speakerSo.ApplyModifiedPropertiesWithoutUndo();
        DialogueTrigger trigger = P.Get<DialogueTrigger>(lucien);
        P.Get<Observable>(lucien);
        foreach (ChangeBehaviour other in lucien.GetComponents<ChangeBehaviour>())
            if (!(other is VanishChange))
                Object.DestroyImmediate(other);
        P.Get<VanishChange>(lucien);
        Transform dark = Point(root, "Lucien Comes From (the dark)", spot.position + away * 9f);
        Jug(root, lucien, log);
        log.Add("Lucien comes out of the dark with a water jug and stops by his spot. When he is gone, the jug stays on the ground.");

        Transform soldiers = Soldiers(root, forest, centre, log);

        if (Object.FindAnyObjectByType<GazeManager>(FindObjectsInactive.Include) == null)
            new GameObject("GazeManager").AddComponent<GazeManager>();

        SetupPoem(poemArea, log);

        TranscribePoem transcribe = Object.FindAnyObjectByType<TranscribePoem>(FindObjectsInactive.Include);
        if (transcribe != null)
        {
            var tso = new SerializedObject(transcribe);
            tso.FindProperty("_firstPoemId").intValue = 1;
            CopyEarlierPoems(tso, log);
            tso.ApplyModifiedPropertiesWithoutUndo();
        }

        var so = new SerializedObject(flow);
        so.FindProperty("titleCard").stringValue = "BOSCO DI COURTON — LUGLIO 1918";
        so.FindProperty("nextScene").stringValue = "MainMenu";
        so.FindProperty("fallbackScene").stringValue = "MainMenu";
        so.FindProperty("character").objectReferenceValue = trigger;
        so.FindProperty("characterName").stringValue = "Lucien";
        so.FindProperty("lookAtCharacterOnStart").boolValue = false;
        so.FindProperty("followerLeafFromStart").boolValue = true;
        so.FindProperty("broom").objectReferenceValue = null;
        so.FindProperty("door").objectReferenceValue = null;
        so.FindProperty("characterOutside").objectReferenceValue = dark;
        so.FindProperty("characterExitPoint").objectReferenceValue = dark;
        so.FindProperty("forestFallSeconds").floatValue = 7f;
        AudioClip endingMusic = UngarettiLines.Clip("MUS_07_CREDITS");
        if (endingMusic != null)
            so.FindProperty("endingMusic").objectReferenceValue = endingMusic;
        log.Add(endingMusic != null ? "Ending music: MUS_07_CREDITS, from the last page of the notebook to the end of the credits." : "Ending music: MUS_07_CREDITS not found yet (Drive Audio).");
        SerializedProperty observables = so.FindProperty("observables");
        observables.arraySize = 5;
        observables.GetArrayElementAtIndex(LucienIndex).objectReferenceValue = lucien.GetComponent<Observable>();
        observables.GetArrayElementAtIndex(TreeA).objectReferenceValue = leafTrees[0].GetComponent<Observable>();
        observables.GetArrayElementAtIndex(TreeB).objectReferenceValue = leafTrees[1].GetComponent<Observable>();
        observables.GetArrayElementAtIndex(TreeC).objectReferenceValue = leafTrees[2].GetComponent<Observable>();
        observables.GetArrayElementAtIndex(SoldiersIndex).objectReferenceValue = soldiers.GetComponent<Observable>();

        const LevelFlow.Condition W = LevelFlow.Condition.Wait;
        const LevelFlow.Condition Changed = LevelFlow.Condition.ObservableBehaviourChanged;
        const LevelFlow.Condition Look = LevelFlow.Condition.LookAtObservable;
        const LevelFlow.StepAction Move = LevelFlow.StepAction.MoveObjectWhenUnseen;
        const LevelFlow.StepAction None = LevelFlow.StepAction.None;
        var plan = new (string note, LevelFlow.Condition wait, float amount, int observable, string objective, string[] lines, LevelFlow.StepAction action)[]
        {
            ("Arrival", LevelFlow.Condition.None, 0f, 0, "", new[] { "U01" }, LevelFlow.StepAction.CharacterEnters),
            ("Lucien", Look, 0f, LucienIndex, "LOOK AT LUCIEN", new[] { "C02", "U02", "C04" }, None),
            ("The trees may turn", W, 0.1f, TreeB, "", new string[0], Move),
            ("The trees may turn", W, 0.1f, TreeC, "", new string[0], Move),
            ("Sweep the clearing", LevelFlow.Condition.SweepLeaves, 20f, TreeA, "SWEEP THE CLEARING", new string[0], Move),
            ("Green a moment ago", Changed, 0f, SoldiersIndex, "KEEP SWEEPING THE CLEARING", new[] { "C05", "U03" }, Move),
            ("Men you can't see", W, 6f, 0, "", new[] { "C07", "U04", "C09" }, None),
            ("The paper", LevelFlow.Condition.PoemFound, 0f, 0, "SWEEP THE LEAVES IN THE MIDDLE OF THE CLEARING", new string[0], None),
            ("Soldati", LevelFlow.Condition.PoemTranscribed, 0f, 0, "", new string[0], None),
            ("Silence", W, 4f, 0, "", new[] { "C10", "U05" }, None),
            ("Look at Lucien", Look, 0f, LucienIndex, "LOOK AT LUCIEN", new[] { "C12" }, Move),
            ("Lucien is gone", Changed, 0f, LucienIndex, "LOOK AWAY FROM LUCIEN", new string[0], LevelFlow.StepAction.ForestFalls),
            ("Every leaf falls", W, 7f, 0, "", new[] { "U06" }, LevelFlow.StepAction.CharacterCardsThenNextLevel),
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
        so.FindProperty("firstGustLines").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.Add("Level 6 follows the script: " + plan.Length + " steps, " + voiced + " voiced pieces, " + silent + " without voice yet.");
        log.Add("Ending: notebook on its last page (the six poems with place and date), the character cards, Ungaretti's card, credits with falling leaves (each name appears as a leaf drifts past it), then the title screen with a small golden leaf on the paper.");

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Setup Courton Script\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Courton Script", string.Join("\n\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
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
        so.FindProperty("_assignedPoemName").stringValue = "Soldati";
        so.FindProperty("_assignedDate").stringValue = "luglio 1918";
        so.FindProperty("_assignedPlace").stringValue = "Bosco di Courton";
        so.FindProperty("_assignedPoemText").stringValue = Soldati;
        so.FindProperty("_poemVoice").objectReferenceValue = reading;
        so.FindProperty("_fragment").intValue = 0;
        so.FindProperty("_fragmentCount").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        P.Get<PoemFinale>(area);
        log.Add("Poem 6, Soldati (Bosco di Courton, luglio 1918), in the middle of the clearing" + (reading != null ? ", read by UNG_poesia6." : "; UNG_poesia6 not found yet (Drive Audio)."));
        log.Add("Once the paper is uncovered the camera settles on it and will not turn away; the golden leaf drifts to the centre of the view and lands on the paper; the camera is released 4 seconds after the poem.");
    }

    private static Transform Soldiers(Transform root, Transform forest, Vector3 centre, List<string> log)
    {
        Transform group = Child(root, "Soldiers (edge of sight)");
        if (group == null)
        {
            group = new GameObject("Soldiers (edge of sight)").transform;
            group.SetParent(root, false);
        }
        foreach (Transform old in group.Cast<Transform>().ToList())
            Object.DestroyImmediate(old.gameObject);

        Mesh body = SeatedBody(out Material[] materials, out string source);
        Transform[] ring = forest.Cast<Transform>().Where(t => t.name.StartsWith("Forest Tree")).OrderBy(t => t.name).ToArray();
        int placed = 0;
        foreach (int index in SoldierTrees)
        {
            if (index >= ring.Length)
                continue;
            Transform tree = ring[index];
            Vector3 inward = Flat(centre - tree.position);
            var soldier = new GameObject("Soldier " + (placed + 1)).transform;
            soldier.SetParent(group, false);
            soldier.position = new Vector3(tree.position.x, root.position.y, tree.position.z) + inward * 0.85f;
            soldier.rotation = Quaternion.LookRotation(inward, Vector3.up) * Quaternion.Euler(0f, (placed % 2 == 0 ? 12f : -9f), 0f);
            if (body != null)
            {
                soldier.gameObject.AddComponent<MeshFilter>().sharedMesh = body;
                soldier.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
            }
            else
            {
                MilanoRoomBuilder.Box("Body", soldier, new Vector3(0f, 0.42f, -0.05f), new Vector3(0.42f, 0.7f, 0.3f), P.Mat("Tent Canvas"), false);
                MilanoRoomBuilder.Box("Legs", soldier, new Vector3(0f, 0.09f, 0.4f), new Vector3(0.34f, 0.16f, 0.8f), P.Mat("Tent Canvas"), false);
                MilanoRoomBuilder.Box("Head", soldier, new Vector3(0f, 0.9f, -0.05f), new Vector3(0.2f, 0.22f, 0.2f), P.Mat("Linen") ?? P.Mat("Bark"), false);
            }
            foreach (Collider c in soldier.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            PeripheralFigure figure = soldier.gameObject.AddComponent<PeripheralFigure>();
            var figureSo = new SerializedObject(figure);
            figureSo.FindProperty("_hiddenUntilRevealed").boolValue = true;
            figureSo.FindProperty("_focusAngle").floatValue = 22f;
            figureSo.FindProperty("_maxDistance").floatValue = 40f;
            figureSo.ApplyModifiedPropertiesWithoutUndo();
            placed++;
        }
        P.Get<Observable>(group);
        foreach (ChangeBehaviour other in group.GetComponents<ChangeBehaviour>())
            if (!(other is RevealChange))
                Object.DestroyImmediate(other);
        P.Get<RevealChange>(group);
        log.Add(placed + " soldiers sit in silence at the foot of the trunks, " + (body != null ? "(" + source + "'s body in grigioverde)" : "(greybox shapes: no MakeHuman model found)") +
            ": they appear after Lucien's \"green a moment ago\", only at the edge of sight, and are gone when looked at.");
        return group;
    }

    private static Mesh SeatedBody(out Material[] materials, out string source)
    {
        materials = new Material[0];
        source = null;
        GameObject model = CompanionBuilder.Models.Select(CompanionBuilder.Model).FirstOrDefault(m => m != null);
        if (model == null)
            return null;
        source = model.name;
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
        temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        PeriodClothes.Dress(temp, "Companion");
        Vector3 forward = Sit(temp.transform);
        var space = new GameObject("Seated Space").transform;
        Mesh body = CompanionBuilder.Bake(temp, space, out materials);
        Object.DestroyImmediate(space.gameObject);
        Object.DestroyImmediate(temp);
        if (body == null)
            return null;

        Quaternion turn = Quaternion.FromToRotation(forward, Vector3.forward);
        Vector3[] vertices = body.vertices;
        Vector3[] normals = body.normals;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = turn * vertices[i];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = turn * normals[i];
        body.vertices = vertices;
        body.normals = normals;
        body.RecalculateBounds();
        Bounds b = body.bounds;
        Vector3 shift = new Vector3(-b.center.x, 0.01f - b.min.y, -b.center.z);
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] += shift;
        body.vertices = vertices;
        body.RecalculateBounds();
        body.name = "Soldier Seated";
        CompanionBuilder.EnsureFolder(SoldiersFolder);
        return CompanionBuilder.Save(body, SoldiersFolder + "/Soldier Seated.asset");
    }

    private static Vector3 Sit(Transform root)
    {
        Transform shoulderL = CompanionBuilder.Bone(root, "upperarm", "_l");
        Transform shoulderR = CompanionBuilder.Bone(root, "upperarm", "_r");
        Vector3 right = shoulderL != null && shoulderR != null ? Flat(shoulderR.position - shoulderL.position) : Vector3.right;
        Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
        Vector3 down = Vector3.down;

        Transform spine = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.ToLowerInvariant().Contains("spine_01") || t.name.ToLowerInvariant() == "spine01");
        if (spine != null)
            spine.rotation = Quaternion.AngleAxis(-12f, right) * spine.rotation;

        foreach (string side in new[] { "_l", "_r" })
        {
            Transform thigh = CompanionBuilder.Bone(root, "thigh", side);
            Transform calf = CompanionBuilder.Bone(root, "calf", side);
            Transform foot = CompanionBuilder.Bone(root, "foot", side);
            float splay = side == "_l" ? -1f : 1f;
            if (thigh != null && calf != null)
                thigh.rotation = Quaternion.FromToRotation(calf.position - thigh.position, (forward + right * splay * 0.08f + down * 0.05f).normalized) * thigh.rotation;
            if (calf != null && foot != null)
                calf.rotation = Quaternion.FromToRotation(foot.position - calf.position, (forward + down * 0.32f + right * splay * 0.05f).normalized) * calf.rotation;

            Transform upper = CompanionBuilder.Bone(root, "upperarm", side);
            Transform lower = CompanionBuilder.Bone(root, "lowerarm", side);
            Transform hand = CompanionBuilder.Bone(root, "hand", side);
            if (upper != null && lower != null)
                upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, (down * 0.8f + forward * 0.45f + right * splay * 0.12f).normalized) * upper.rotation;
            if (lower != null && hand != null)
                lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, (forward * 0.9f + down * 0.35f - right * splay * 0.1f).normalized) * lower.rotation;
        }
        return forward;
    }

    private static void Jug(Transform root, Transform lucien, List<string> log)
    {
        Transform jug = Child(root, "Lucien's Water Jug");
        bool created = jug == null;
        if (created)
        {
            GameObject asset = AssetDatabase.FindAssets("jug_01").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith("Assets/_Trash") && (p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) || p.EndsWith(".prefab") || p.EndsWith(".gltf") || p.EndsWith(".glb")))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).FirstOrDefault(g => g != null);
            if (asset != null)
            {
                jug = ((GameObject)PrefabUtility.InstantiatePrefab(asset, root)).transform;
                jug.name = "Lucien's Water Jug";
                Bounds b = Measure(jug);
                if (b.size.y > 0.01f)
                    jug.localScale *= 0.3f / b.size.y;
            }
            else
            {
                jug = new GameObject("Lucien's Water Jug").transform;
                jug.SetParent(root, false);
                MilanoRoomBuilder.Box("Jug", jug, new Vector3(0f, 0.14f, 0f), new Vector3(0.16f, 0.28f, 0.16f), P.Mat("Limestone"), false);
            }
        }
        foreach (Collider c in jug.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
        CarriedProp carried = P.Get<CarriedProp>(jug);
        var so = new SerializedObject(carried);
        so.FindProperty("carrier").objectReferenceValue = lucien;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (!created)
            return;
        jug.position = lucien.TransformPoint(new Vector3(0.3f, 0.55f, 0.18f));
        jug.rotation = lucien.rotation;
    }

    private static Material MaterialAsset(string name, System.Func<Material> make)
    {
        CompanionBuilder.EnsureFolder(SoldiersFolder);
        string path = SoldiersFolder + "/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;
        Material m = make();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static Bounds Measure(Transform t)
    {
        Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(t.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
            b.Encapsulate(r.bounds);
        return b;
    }

    private static void CopyEarlierPoems(SerializedObject transcribe, List<string> log)
    {
        var found = new List<(int id, string name, string date, string place, string text, Object voice)>();
        Scene active = SceneManager.GetActiveScene();
        foreach (string level in new[] { "Level 1 - Milano", "Level 2 - Parigi", "Level 3 - Carso", "Level 4 - San Paolo", "Level 5 - Roma" })
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
        log.Add(found.Count > 0 ? "The notebook already holds " + string.Join(", ", found.Select(f => "\"" + f.name + "\"")) + "." : "WARNING: no earlier poem found in the first five levels.");
    }

    private static void FillLine(SerializedProperty line, string key, ref int voiced, ref int silent)
    {
        string number = key.Substring(1);
        if (key[0] == 'U')
        {
            UngarettiLines.Line script = UngarettiLines.Get("Ungaretti - Courton - " + number);
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
        AudioClip clip = LucienClip("L6_" + number);
        line.FindPropertyRelative("speakerId").stringValue = Lucien;
        SerializedProperty pieces = line.FindPropertyRelative("pieces");
        pieces.arraySize = 1;
        SerializedProperty only = pieces.GetArrayElementAtIndex(0);
        only.FindPropertyRelative("text").stringValue = LucienLines[number];
        only.FindPropertyRelative("voice").objectReferenceValue = clip;
        only.FindPropertyRelative("volume").floatValue = 0.55f;
        if (clip != null)
            voiced++;
        else
            silent++;
    }

    private static AudioClip LucienClip(string id)
    {
        string want = UngarettiLines.Clean(id);
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/_Trash") || path.IndexOf("/Lucien/", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (UngarettiLines.FileKey(Path.GetFileNameWithoutExtension(path)) == want)
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }

    private static Transform Tree(Transform forest, string prefix) =>
        forest.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith(prefix));

    private static Transform Point(Transform parent, string name, Vector3 world)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        t.position = new Vector3(world.x, parent.position.y, world.z);
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
