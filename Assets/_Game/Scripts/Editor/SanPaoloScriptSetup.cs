using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using P = ParisScriptSetup;

public static class SanPaoloScriptSetup
{
    private const string Ungaretti = "ungaretti";
    private const string Rui = "rui";
    private const int PoemId = 4;
    private const string Folder = "Assets/_Game/Art/3D/Generated/San Paolo";
    private const string GiornoPerGiorno =
        "\"Nessuno, mamma, ha mai sofferto tanto...\"\nE il volto già scomparso\nMa gli occhi ancora vivi\nDal guanciale volgeva alla finestra,\nE riempivano passeri la stanza\nVerso le briciole dal babbo sparse\nPer distrarre il suo bimbo...\n\n" +
        "Ora potrò baciare solo in sogno\nLe fiduciose mani...\nE discorro, lavoro,\nSono appena mutato, temo, fumo...\nCome si può ch'io regga a tanta notte?...\n\n" +
        "Mi porteranno gli anni\nChissà quali altri orrori,\nMa ti sentivo accanto,\nM'avresti consolato...\n\n" +
        "Mai, non saprete mai come m'illumina\nL'ombra che mi si pone a lato, timida,\nQuando non spero più...\n\n" +
        "In cielo cerco il tuo felice volto,\nEd i miei occhi in me null'altro vedano\nQuando anch'essi vorrà chiudere Iddio...\nE t'amo, t'amo, ed è continuo schianto!...";

    private static readonly Dictionary<string, string> RuiLines = new Dictionary<string, string>
    {
        { "02", "You're sweeping again. The students keep asking for you, Giuseppe." },
        { "04", "Neither do you." },
        { "05", "Jeanne asked me to tell you: leave his corner. She doesn't want anything moved." },
        { "07", "His drawings... Did Jeanne take them down?" },
        { "09", "Then don't stop looking." },
        { "11", "What is that?" },
        { "13", "Read it. I'll listen." },
        { "14", "It's him. You kept him." },
        { "16", "The war is coming here too. Soon they'll send you home, to Italy." },
    };

    private const int RuiIndex = 0, Drawings = 1, Toy = 2, Chair = 3;
    private static readonly string[] CornerObjects = { "1 - Drawings", "2 - Wooden Toy", "3 - Small Chair" };

    [MenuItem("Tools/Leaves of War/Setup San Paolo Script")]
    private static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform root = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "San Paolo House");
        LevelFlow flow = Object.FindAnyObjectByType<LevelFlow>(FindObjectsInactive.Include);
        if (root == null || flow == null)
        {
            EditorUtility.DisplayDialog("San Paolo Script", "Open Level 4 - San Paolo (built with Build Level 4 - San Paolo) first.", "OK");
            return;
        }
        var log = new List<string>();
        UngarettiLines.Refresh();
        P.RelinkHeldItems(log);
        P.RelinkNotebookTexts(log);
        EnsureFolder(Folder);

        Transform corner = Child(root, "Child's Corner (empties on look-away)");
        Transform poemArea = Child(root, "Poem Area (child's corner)");
        Transform props = Child(root, "Props");
        Transform garden = Child(root, "Garden");
        Transform ruiExit = Child(root, "Rui Exit (garden)");
        Transform frontExit = Child(root, "Exit Anchor (front door)");
        var corners = CornerObjects.Select(n => corner != null ? Child(corner, n) : null).ToArray();
        if (corner == null || poemArea == null || props == null || garden == null || corners.Any(c => c == null))
        {
            EditorUtility.DisplayDialog("San Paolo Script", "The child's corner, its three objects, the poem area or the garden are missing: rebuild with Build Level 4 - San Paolo.", "OK");
            return;
        }

        Transform rui = P.Figure(props, "Rui", new Vector3(-1.25f, 0f, 2.75f), 160f, P.Mat("Sofa Fabric"), P.Mat("Linen") ?? P.Mat("Paper"));
        DialogueSpeaker speaker = P.Get<DialogueSpeaker>(rui);
        var speakerSo = new SerializedObject(speaker);
        speakerSo.FindProperty("speakerId").stringValue = Rui;
        speakerSo.FindProperty("displayName").stringValue = "Rui";
        speakerSo.ApplyModifiedPropertiesWithoutUndo();
        DialogueTrigger trigger = P.Get<DialogueTrigger>(rui);
        P.Get<Observable>(rui);
        BuildUmbrella(props, rui, speaker);
        SetupGardenDoors(root, speaker, log);
        log.Add("Rui waits by the garden door with his wet umbrella; it opens when he walks out into the garden.");

        foreach (Transform item in corners)
        {
            P.Get<Observable>(item);
            if (!item.TryGetComponent(out ChangeBehaviour _))
                item.gameObject.AddComponent<VanishChange>();
        }
        P.Get<LookAwayCue>(corners[0]);
        log.Add("The child's corner empties one look-away at a time: drawings, toy, chair. The first look-away brings a far child's laugh, once (SFX_Child_Laugh, wired by Drive Audio).");

        ParticleSystem rain = BuildRain(garden, log);
        Weather weather = P.Get<Weather>(root);
        var weatherSo = new SerializedObject(weather);
        SerializedProperty rainList = weatherSo.FindProperty("rain");
        rainList.arraySize = 1;
        rainList.GetArrayElementAtIndex(0).objectReferenceValue = rain;
        weatherSo.ApplyModifiedPropertiesWithoutUndo();
        Transform backdrop = Child(garden, "Sky and Rain (backdrop)");
        if (backdrop != null && backdrop.TryGetComponent(out MeshRenderer backdropRenderer))
            backdropRenderer.enabled = false;

        SetupPoem(poemArea, log);

        TranscribePoem transcribe = Object.FindAnyObjectByType<TranscribePoem>(FindObjectsInactive.Include);
        if (transcribe != null)
        {
            var tso = new SerializedObject(transcribe);
            tso.FindProperty("_firstPoemId").intValue = 1;
            CopyEarlierPoems(tso, log);
            AudioClip[] wet = Enumerable.Range(1, 6).Select(i => UngarettiLines.Clip("SFX_Leaf_Wet_" + i)).Where(c => c != null).ToArray();
            if (wet.Length > 0)
            {
                SerializedProperty gusts = tso.FindProperty("_gustClips");
                gusts.arraySize = wet.Length;
                for (int i = 0; i < wet.Length; i++)
                    gusts.GetArrayElementAtIndex(i).objectReferenceValue = wet[i];
                log.Add("Looking away while transcribing: the rain covers the paper with wet leaves (wet-leaf sounds).");
            }
            tso.ApplyModifiedPropertiesWithoutUndo();
        }

        var so = new SerializedObject(flow);
        so.FindProperty("titleCard").stringValue = "SAN PAOLO DEL BRASILE — 1937–1942";
        so.FindProperty("nextScene").stringValue = "Level 5 - Roma";
        so.FindProperty("character").objectReferenceValue = trigger;
        so.FindProperty("characterName").stringValue = "Rui";
        so.FindProperty("lookAtCharacterOnStart").boolValue = false;
        so.FindProperty("followerLeafFromStart").boolValue = true;
        so.FindProperty("broom").objectReferenceValue = null;
        so.FindProperty("door").objectReferenceValue = null;
        so.FindProperty("characterExitPoint").objectReferenceValue = ruiExit;
        if (frontExit != null)
            so.FindProperty("exitAnchor").objectReferenceValue = frontExit;
        SerializedProperty observables = so.FindProperty("observables");
        observables.arraySize = 4;
        observables.GetArrayElementAtIndex(RuiIndex).objectReferenceValue = rui.GetComponent<Observable>();
        for (int i = 0; i < corners.Length; i++)
            observables.GetArrayElementAtIndex(Drawings + i).objectReferenceValue = corners[i].GetComponent<Observable>();

        const LevelFlow.Condition W = LevelFlow.Condition.Wait;
        const LevelFlow.Condition Changed = LevelFlow.Condition.ObservableBehaviourChanged;
        const LevelFlow.StepAction Move = LevelFlow.StepAction.MoveObjectWhenUnseen;
        const LevelFlow.StepAction None = LevelFlow.StepAction.None;
        var plan = new (string note, LevelFlow.Condition wait, float amount, int observable, string objective, string[] lines, LevelFlow.StepAction action)[]
        {
            ("Arrival", LevelFlow.Condition.None, 0f, 0, "", new[] { "U01" }, None),
            ("Rui", LevelFlow.Condition.LookAtObservable, 0f, RuiIndex, "LOOK AT RUI, BY THE GARDEN DOOR", new[] { "R02", "U02", "R04" }, None),
            ("Sweep the living room", LevelFlow.Condition.SweepLeaves, 30f, Drawings, "SWEEP THE LIVING ROOM", new[] { "R05", "U03" }, Move),
            ("The drawings are gone", Changed, 0f, Drawings, "LOOK AWAY FROM THE CHILD'S CORNER", new[] { "R07", "U04", "R09" }, None),
            ("The toy may go", W, 0.1f, Toy, "", new string[0], Move),
            ("The toy is gone", Changed, 0f, Toy, "LOOK AWAY FROM THE CORNER AGAIN", new[] { "U05" }, None),
            ("The chair may go", W, 0.1f, Chair, "", new string[0], Move),
            ("The chair is gone", Changed, 0f, Chair, "LOOK AWAY FROM THE CORNER AGAIN", new string[0], None),
            ("A sheet of paper", LevelFlow.Condition.PoemFound, 0f, 0, "SWEEP THE LEAVES IN THE EMPTY CORNER", new[] { "R11", "U06", "R13" }, None),
            ("Giorno per giorno", LevelFlow.Condition.PoemTranscribed, 0f, 0, "KEEP YOUR EYES ON THE WORDS WHILE YOU TRANSCRIBE", new string[0], None),
            ("Four seconds of rain", W, 4f, 0, "", new[] { "R14", "U07", "R16", "U08" }, LevelFlow.StepAction.CharacterLeaves),
            ("The rain stops", W, 5f, 0, "", new string[0], LevelFlow.StepAction.WeatherClears),
            ("Only to remember", W, 3f, 0, "", new[] { "U09" }, LevelFlow.StepAction.LoadNextLevel),
            ("Turn around", LevelFlow.Condition.TurnAround, 0f, 0, "TURN AWAY FROM THE FRONT DOOR", new string[0], None),
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

        log.Add("Level 4 follows the script: " + plan.Length + " steps, " + voiced + " voiced pieces, " + silent + " without voice yet (Rui: Cast Voices → Voices/Rui/L4_02…).");
        log.Add("After the poem: four seconds of rain, Rui's last lines, he opens the umbrella and walks out into the garden, seen; the rain stops, the sky clears, the ambience turns to the after-rain loops.");
        log.Add("Next Scene: Level 5 - Roma. Exit: the front door.");

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Setup San Paolo Script\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("San Paolo Script", string.Join("\n\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static void SetupGardenDoors(Transform root, DialogueSpeaker rui, List<string> log)
    {
        Transform shell = Child(root, "Shell") ?? root;
        Transform doors = Child(shell, "Garden Doors");
        if (doors == null)
        {
            doors = new GameObject("Garden Doors").transform;
            doors.SetParent(shell, false);
        }
        var hinges = new[] { "Garden Door Left (hinge)", "Garden Door Right (hinge)" }.Select(n => Child(shell, n)).ToArray();
        if (hinges.Any(h => h == null))
        {
            log.Add("WARNING: garden doors not found, rebuild with Build Level 4 - San Paolo.");
            return;
        }
        Vector3 middle = (hinges[0].position + hinges[1].position) * 0.5f;
        foreach (Transform hinge in hinges)
            hinge.SetParent(null, true);
        doors.position = middle;
        doors.rotation = shell.rotation;
        float[] angles = new float[hinges.Length];
        for (int i = 0; i < hinges.Length; i++)
        {
            hinges[i].SetParent(doors, true);
            float side = i == 0 ? -1f : 1f;
            hinges[i].localRotation = Quaternion.identity;
            angles[i] = side * 75f;
        }

        Transform old = Child(doors, "Garden Doors Interaction");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var interaction = new GameObject("Garden Doors Interaction");
        interaction.transform.SetParent(doors, false);
        interaction.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        interaction.AddComponent<BoxCollider>().size = new Vector3(1.8f, 2.5f, 0.12f);
        int interactable = LayerMask.NameToLayer("Interactable");
        if (interactable >= 0)
            interaction.layer = interactable;

        OpenableWindow window = P.Get<OpenableWindow>(doors);
        var so = new SerializedObject(window);
        SerializedProperty hingeList = so.FindProperty("hinges");
        SerializedProperty angleList = so.FindProperty("openAngles");
        hingeList.arraySize = hinges.Length;
        angleList.arraySize = hinges.Length;
        for (int i = 0; i < hinges.Length; i++)
        {
            hingeList.GetArrayElementAtIndex(i).objectReferenceValue = hinges[i];
            angleList.GetArrayElementAtIndex(i).floatValue = angles[i];
        }
        so.FindProperty("startOpen").boolValue = false;
        so.FindProperty("openWhenWalking").objectReferenceValue = rui;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("The garden doors start closed and open/close with the interact key (rain muffled when closed, loud when open). If they are closed when Rui leaves, he opens them himself.");
    }

    private static void BuildUmbrella(Transform props, Transform rui, DialogueSpeaker speaker)
    {
        Transform old = Child(props, "Rui's Umbrella");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        Material cloth = LitMat("Umbrella Cloth", new Color(0.06f, 0.06f, 0.07f), 0.55f);
        Material wood = P.Mat("Dark Wood") ?? LitMat("Umbrella Handle", new Color(0.2f, 0.13f, 0.08f), 0.3f);

        var umbrella = new GameObject("Rui's Umbrella").transform;
        umbrella.SetParent(props, false);
        umbrella.SetPositionAndRotation(rui.position, rui.rotation);

        var closed = new GameObject("Closed (wet, in hand)").transform;
        closed.SetParent(umbrella, false);
        Part(PrimitiveType.Cylinder, "Furled Canopy", closed, new Vector3(0.3f, 0.42f, 0.12f), new Vector3(0.06f, 0.26f, 0.06f), cloth);
        Part(PrimitiveType.Cylinder, "Tip", closed, new Vector3(0.3f, 0.1f, 0.12f), new Vector3(0.012f, 0.06f, 0.012f), wood);
        Part(PrimitiveType.Cylinder, "Shaft", closed, new Vector3(0.3f, 0.78f, 0.12f), new Vector3(0.014f, 0.1f, 0.014f), wood);
        Part(PrimitiveType.Cube, "Handle", closed, new Vector3(0.33f, 0.9f, 0.12f), new Vector3(0.08f, 0.025f, 0.025f), wood);

        var open = new GameObject("Open (walking out)").transform;
        open.SetParent(umbrella, false);
        Part(PrimitiveType.Cylinder, "Shaft", open, new Vector3(0.18f, 1.55f, 0.12f), new Vector3(0.016f, 0.5f, 0.016f), wood);
        var canopy = new GameObject("Canopy");
        canopy.transform.SetParent(open, false);
        canopy.transform.localPosition = new Vector3(0.18f, 2.0f, 0.12f);
        Mesh dome = Dome();
        string path = Folder + "/Umbrella Canopy.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(dome, path);
        canopy.AddComponent<MeshFilter>().sharedMesh = dome;
        canopy.AddComponent<MeshRenderer>().sharedMaterial = cloth;
        open.gameObject.SetActive(false);

        Umbrella component = umbrella.gameObject.AddComponent<Umbrella>();
        var so = new SerializedObject(component);
        so.FindProperty("closed").objectReferenceValue = closed.gameObject;
        so.FindProperty("open").objectReferenceValue = open.gameObject;
        so.FindProperty("speaker").objectReferenceValue = speaker;
        so.FindProperty("follow").objectReferenceValue = rui;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Mesh Dome()
    {
        const int Panels = 8, Rings = 4;
        const float Radius = 0.55f, Height = 0.24f;
        var vertices = new List<Vector3> { new Vector3(0f, Height, 0f) };
        for (int r = 1; r <= Rings; r++)
        {
            float k = r / (float)Rings;
            float y = Height * (1f - k * k) - (r == Rings ? 0.02f : 0f);
            for (int p = 0; p < Panels; p++)
            {
                float a = p / (float)Panels * Mathf.PI * 2f;
                float sag = r == Rings ? 0.92f : 1f;
                vertices.Add(new Vector3(Mathf.Cos(a) * Radius * k * sag, y, Mathf.Sin(a) * Radius * k * sag));
            }
        }
        var tris = new List<int>();
        for (int p = 0; p < Panels; p++)
            tris.AddRange(new[] { 0, 1 + (p + 1) % Panels, 1 + p });
        for (int r = 1; r < Rings; r++)
            for (int p = 0; p < Panels; p++)
            {
                int a = 1 + (r - 1) * Panels + p, b = 1 + (r - 1) * Panels + (p + 1) % Panels;
                int c = a + Panels, d = b + Panels;
                tris.AddRange(new[] { a, b, d, a, d, c });
            }
        var mesh = new Mesh { name = "Umbrella Canopy" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        if (mesh.normals[0].y < 0f)
        {
            for (int t = 0; t < tris.Count; t += 3)
            {
                int tmp = tris[t + 1];
                tris[t + 1] = tris[t + 2];
                tris[t + 2] = tmp;
            }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    private static ParticleSystem BuildRain(Transform garden, List<string> log)
    {
        Transform anchor = Child(garden, "Rain (particle system here)") ?? garden;
        Transform old = Child(anchor, "Rain");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("Rain");
        go.transform.SetParent(anchor, false);
        ParticleSystem rain = go.AddComponent<ParticleSystem>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = rain.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startLifetime = 0.9f;
        main.startSpeed = 9f;
        main.startSize = 0.025f;
        main.maxParticles = 5000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.78f, 0.82f, 0.88f, 0.32f);
        ParticleSystem.EmissionModule emission = rain.emission;
        emission.rateOverTime = 3200f;
        ParticleSystem.ShapeModule shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(9f, 0.1f, 9f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 6f;
        renderer.velocityScale = 0.04f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.sharedMaterial = RainMaterial();
        log.Add("Heavy rain falls over the garden (seen through the open garden doors) until it stops after Rui leaves.");
        return rain;
    }

    private static Material RainMaterial()
    {
        string path = Folder + "/Rain Drop.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_Surface"))
            m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend"))
            m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend"))
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend"))
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite"))
            m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", Color.white);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material LitMat(string name, Color color, float smoothness)
    {
        string path = Folder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness"))
            m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Cull"))
            m.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(m);
        return m;
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
        so.FindProperty("_assignedPoemName").stringValue = "Giorno per giorno";
        so.FindProperty("_assignedDate").stringValue = "1940–1946";
        so.FindProperty("_assignedPlace").stringValue = "San Paolo del Brasile";
        so.FindProperty("_assignedPoemText").stringValue = GiornoPerGiorno;
        so.FindProperty("_poemVoice").objectReferenceValue = reading;
        so.FindProperty("_fragment").intValue = 0;
        so.FindProperty("_fragmentCount").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("Poem 4, Giorno per giorno (San Paolo del Brasile, 1940–1946), on the floor of the emptied corner" + (reading != null ? ", read by UNG_poesia4." : "; UNG_poesia4 not found yet (Drive Audio)."));
    }

    private static void CopyEarlierPoems(SerializedObject transcribe, List<string> log)
    {
        var found = new List<(int id, string name, string date, string place, string text, Object voice)>();
        Scene active = SceneManager.GetActiveScene();
        foreach (string level in new[] { "Level 1 - Milano", "Level 2 - Parigi", "Level 3 - Carso" })
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
        log.Add(found.Count > 0 ? "The notebook already holds " + string.Join(", ", found.Select(f => "\"" + f.name + "\"")) + "." : "WARNING: no earlier poem found in Milano, Parigi or Carso.");
    }

    private static void FillLine(SerializedProperty line, string key, ref int voiced, ref int silent)
    {
        string number = key.Substring(1);
        if (key[0] == 'U')
        {
            UngarettiLines.Line script = UngarettiLines.Get("Ungaretti - San Paolo - " + number);
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
        AudioClip clip = RuiClip("L4_" + number);
        line.FindPropertyRelative("speakerId").stringValue = Rui;
        SerializedProperty pieces = line.FindPropertyRelative("pieces");
        pieces.arraySize = 1;
        SerializedProperty only = pieces.GetArrayElementAtIndex(0);
        only.FindPropertyRelative("text").stringValue = RuiLines[number];
        only.FindPropertyRelative("voice").objectReferenceValue = clip;
        only.FindPropertyRelative("volume").floatValue = 0.55f;
        if (clip != null)
            voiced++;
        else
            silent++;
    }

    private static AudioClip RuiClip(string id)
    {
        string want = UngarettiLines.Clean(id);
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/_Trash") || path.IndexOf("/Rui/", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (UngarettiLines.FileKey(Path.GetFileNameWithoutExtension(path)) == want)
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static Transform Child(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
}
