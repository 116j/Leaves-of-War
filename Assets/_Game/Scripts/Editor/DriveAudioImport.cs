using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class DriveAudioImport : EditorWindow
{
    private const string Trash = "Assets/_Trash";
    private const string VoiceFolder = "Assets/_Game/Audio/Voices/Ungaretti";
    private const string PoemFolder = "Assets/_Game/Audio/Voices/Poems";
    private const string AmbienceFolder = "Assets/_Game/Audio/Ambience";
    private const string MusicFolder = "Assets/_Game/Audio/Music";
    private const string SfxFolder = "Assets/_Game/Audio/SFX";

    private static readonly (string surface, FootstepSurfaceType type)[] StepSets =
    {
        ("Wood", FootstepSurfaceType.Wood), ("Stone", FootstepSurfaceType.Stone),
        ("Tile", FootstepSurfaceType.Tile), ("Earth", FootstepSurfaceType.Earth)
    };

    private static readonly (string scene, FootstepSurfaceType surface)[] GroundByLevel =
    {
        ("Milano", FootstepSurfaceType.Wood), ("Parigi", FootstepSurfaceType.Wood), ("Carso", FootstepSurfaceType.Earth),
        ("San Paolo", FootstepSurfaceType.Tile), ("Roma", FootstepSurfaceType.Wood), ("Courton", FootstepSurfaceType.Earth)
    };
    private const string Stinger = "STG_PoemFound";
    private const string TitleMusic = "MUS_00_Title";
    private const string TitleAmbience = "AMB_00_Title";

    private sealed class Amb
    {
        public readonly string[] files;
        public readonly LevelAudio.Mix mix;
        public readonly float volume;
        public bool afterClear;
        public bool open;

        public Amb(LevelAudio.Mix mix, float volume, params string[] files)
        {
            this.files = files;
            this.mix = mix;
            this.volume = volume;
        }
    }

    private sealed class LevelSound
    {
        public readonly string scene;
        public readonly int poem;
        public readonly string music;
        public readonly Amb[] ambience;

        public LevelSound(string scene, int poem, string music, params Amb[] ambience)
        {
            this.scene = scene;
            this.poem = poem;
            this.music = music;
            this.ambience = ambience;
        }
    }

    private static readonly LevelSound[] Levels =
    {
        new LevelSound("Milano", 1, "MUS_P1",
            new Amb(LevelAudio.Mix.WindowClosed, 0.47f, "AMB_01_Milano_Inside", "LVL1_Atmos_Inside"),
            new Amb(LevelAudio.Mix.WindowOpen, 0.41f, "AMB_01_Milano_Outside", "LVL1_Atmos_Outside")),
        new LevelSound("Parigi", 2, "MUS_P2",
            new Amb(LevelAudio.Mix.WindowClosed, 0.47f, "AMB_02_Parigi_Inside"),
            new Amb(LevelAudio.Mix.WindowOpen, 0.41f, "AMB_02_Parigi_Outside")),
        new LevelSound("Carso", 3, "MUS_P3",
            new Amb(LevelAudio.Mix.Always, 0.6f, "AMB_03_Carso")),
        new LevelSound("San Paolo", 4, "MUS_P4",
            new Amb(LevelAudio.Mix.WindowClosed, 0.47f, "AMB_04_SanPaolo_Inside") { open = true },
            new Amb(LevelAudio.Mix.WindowOpen, 0.41f, "AMB_04_SanPaolo_Outside") { open = true },
            new Amb(LevelAudio.Mix.WindowClosed, 0.47f, "AMB_04_SanPaolo_Transition_Inside", "AMB_04_SanPaolo_Transition:Inside") { open = true, afterClear = true },
            new Amb(LevelAudio.Mix.WindowOpen, 0.41f, "AMB_04_SanPaolo_Transition_Outside", "AMB_04_SanPaolo_Transition:Outside") { open = true, afterClear = true }),
        new LevelSound("Roma", 5, "MUS_P5"),
        new LevelSound("Courton", 6, "MUS_06_FINALE"),
    };

    private Vector2 scroll;
    private string report = "";

    [MenuItem("Tools/Leaves of War/Drive Audio (voices, ambience, music)")]
    private static void Open()
    {
        GetWindow<DriveAudioImport>("Drive Audio").minSize = new Vector2(560f, 420f);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. From the Leaves of War Drive, download: voice acting/definitives/ungaretti, Sound design (AMB_ folders and SFX) and music/Lullaby/MP3s.\n" +
            "2. Unzip them anywhere inside Assets (e.g. Assets/_Game/Audio/Import).\n" +
            "3. Organize & Import, then Apply to all scenes.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1. Organize & Import", GUILayout.Height(28f)))
                Organize();
            if (GUILayout.Button("2. Apply to all scenes", GUILayout.Height(28f)))
                ApplyAll();
        }

        UngarettiLines.Refresh();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (!string.IsNullOrEmpty(report))
            EditorGUILayout.HelpBox(report, MessageType.Info);
        EditorGUILayout.LabelField("Files", EditorStyles.boldLabel);
        foreach (string file in Expected())
            using (new EditorGUILayout.HorizontalScope())
            {
                AudioClip clip = UngarettiLines.Clip(file);
                EditorGUILayout.LabelField(clip != null ? "✓" : "–", GUILayout.Width(16f));
                EditorGUILayout.LabelField(file, GUILayout.Width(220f));
                EditorGUILayout.LabelField(clip != null ? AssetDatabase.GetAssetPath(clip) : "missing", EditorStyles.miniLabel);
            }
        EditorGUILayout.EndScrollView();
    }

    private static IEnumerable<string> Expected()
    {
        foreach (UngarettiLines.Line line in UngarettiLines.All)
            foreach (UngarettiLines.Piece piece in line.pieces)
                yield return piece.file;
        for (int i = 1; i <= 6; i++)
            yield return "UNG_poesia" + i;
        yield return TitleMusic;
        yield return TitleAmbience;
        yield return Stinger;
        foreach (LevelSound level in Levels)
        {
            yield return level.music;
            foreach (Amb amb in level.ambience)
                yield return amb.files[0];
        }
        yield return "MUS_07_CREDITS";
        foreach (var set in StepSets)
            for (int i = 1; i <= 6; i++)
                yield return "SFX_Footsteps_" + set.surface + "_" + i;
        for (int i = 1; i <= 7; i++)
            yield return "SFX_Page_Turn_" + i;
        yield return "SFX_Old_Door_Open";
        yield return "SFX_Old_Door_Close";
        yield return "SFX_Window_Open";
        yield return "SFX_Window_Close";
        yield return "SFX_Window_Shutters_Open";
        yield return "SFX_Window_Shutters_Close";
        yield return "SFX_Lantern_Flicker_Loop";
        yield return "SFX_Lantern_Hang_1";
        yield return "SFX_Lantern_Hang_2";
        foreach (string surface in new[] { "Wood", "Stone", "Tile" })
        {
            yield return "SFX_Broom_" + surface + "_Slow";
            yield return "SFX_Broom_" + surface + "_Fast";
        }
        for (int i = 1; i <= 6; i++)
            yield return "SFX_Leaf_Rustle_" + i;
        for (int i = 1; i <= 6; i++)
            yield return "SFX_Leaf_Wet_" + i;
        for (int i = 1; i <= 5; i++)
            yield return "SFX_Wind_Gust_" + i;
        for (int i = 1; i <= 3; i++)
            yield return "SFX_Follower_Leaf_" + i;
        for (int i = 1; i <= 3; i++)
            yield return "SFX_Paper_Reveal_" + i;
        yield return "SFX_Pen_Write";
        yield return "SFX_Notebook_Open_1";
        yield return "SFX_Notebook_Open_2";
        yield return "SFX_Notebook_Close_1";
        yield return "SFX_Notebook_Close_2";
        yield return "SFX_Chair_Move";
        yield return "SFX_Child_Laugh";
        yield return "SFX_Umbrella_Open";
        foreach (string ui in MenuSounds)
            yield return ui;
    }

    private static readonly string[] MenuSounds = { "UI_Menu_Gust1", "UI_Menu_Gust2", "UI_Hover_1", "UI_Select_2", "UI_Back_8", "UI_Back_6", "UI_StartGame" };

    private static string Destination(string name)
    {
        string upper = name.ToUpperInvariant();
        if (upper.StartsWith("UNG_POESIA"))
            return PoemFolder;
        if (upper.StartsWith("UNG_"))
            return VoiceFolder;
        if (upper.StartsWith("AMB_") || upper.StartsWith("LVL") && upper.Contains("ATMOS"))
            return AmbienceFolder;
        if (upper.StartsWith("MUS_") || upper.StartsWith("00_MUS_") || upper.StartsWith("STG_"))
            return MusicFolder;
        if (upper.StartsWith("UI_"))
            return SfxFolder + "/UI";
        if (upper.StartsWith("SFX_FOOTSTEPS"))
            return SfxFolder + "/Footsteps";
        if (upper.StartsWith("SFX_PAGE_TURN"))
            return SfxFolder + "/Page Turn";
        if (upper.StartsWith("SFX_LANTERN"))
            return SfxFolder + "/Lantern";
        if (upper.StartsWith("SFX_WINDOW"))
            return SfxFolder + "/Window";
        if (upper.StartsWith("SFX_OLD_DOOR"))
            return SfxFolder + "/Door";
        if (upper.StartsWith("SFX_BROOM"))
            return SfxFolder + "/Broom";
        if (upper.StartsWith("SFX_LEAF"))
            return SfxFolder + "/Leaves";
        if (upper.StartsWith("SFX_WIND"))
            return SfxFolder + "/Wind";
        if (upper.StartsWith("SFX_FOLLOWER"))
            return SfxFolder + "/Follower Leaf";
        if (upper.StartsWith("SFX_PAPER") || upper.StartsWith("SFX_PEN") || upper.StartsWith("SFX_NOTEBOOK"))
            return SfxFolder + "/Notebook";
        if (upper.StartsWith("SFX_CHAIR"))
            return SfxFolder + "/Furniture";
        if (upper.StartsWith("SFX_"))
            return SfxFolder;
        return null;
    }

    private void Organize()
    {
        var moved = new List<string>();
        var duplicates = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(Trash) || path.StartsWith(AmbienceMono.Folder))
                continue;
            string name = Path.GetFileName(path);
            string folder = Destination(Path.GetFileNameWithoutExtension(path));
            if (folder == null)
                continue;
            string target = folder + "/" + name;
            if (Path.GetDirectoryName(path).Replace('\\', '/') != folder)
            {
                EnsureFolder(folder);
                if (File.Exists(target))
                {
                    string trash = Trash + "/Audio Duplicates/" + name;
                    EnsureFolder(Trash + "/Audio Duplicates");
                    AssetDatabase.MoveAsset(path, AssetDatabase.GenerateUniqueAssetPath(trash));
                    duplicates.Add(name);
                    path = target;
                }
                else
                {
                    string error = AssetDatabase.MoveAsset(path, target);
                    if (string.IsNullOrEmpty(error))
                    {
                        moved.Add(name);
                        path = target;
                    }
                }
            }
            Configure(path, folder);
        }
        RemoveEmptyFolders("Assets/_Game/Audio");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        UngarettiLines.Refresh();
        report = "Moved " + moved.Count + " files into Assets/_Game/Audio." +
            (duplicates.Count > 0 ? "\n" + duplicates.Count + " copies already present went to " + Trash + "/Audio Duplicates." : "") +
            "\nMissing: " + Expected().Count(f => UngarettiLines.Clip(f) == null) + " (see the list).";
    }

    private static void Configure(string path, string folder)
    {
        if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
            return;
        bool voice = folder == VoiceFolder || folder == PoemFolder;
        bool sfx = folder.StartsWith(SfxFolder);
        bool loop = sfx && Path.GetFileName(path).ToUpperInvariant().Contains("LOOP");
        bool longClip = folder == AmbienceFolder || folder == MusicFolder && !Path.GetFileName(path).StartsWith("STG_");
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = voice || sfx && !loop ? AudioClipLoadType.DecompressOnLoad : longClip && folder == MusicFolder ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = voice ? 0.75f : sfx ? 0.7f : 0.6f;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = voice || folder == SfxFolder + "/Footsteps" || folder == SfxFolder + "/Page Turn" || folder == SfxFolder + "/Leaves" || folder == SfxFolder + "/Notebook";
        importer.loadInBackground = longClip;
        importer.SaveAndReimport();
    }

    private void ApplyAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        UngarettiLines.Refresh();
        string active = SceneManager.GetActiveScene().path;
        var lines = new List<string>();
        var paths = EditorBuildSettings.scenes.Select(s => s.path).Where(File.Exists).ToList();
        foreach (string path in Directory.GetFiles("Assets/_Game/Scenes", "Level *.unity").Select(p => p.Replace('\\', '/')))
            if (!paths.Contains(path))
                paths.Add(path);

        foreach (string path in paths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string name = Path.GetFileNameWithoutExtension(path);
            var done = new List<string>();
            LevelSound level = Levels.FirstOrDefault(l => name.Contains(l.scene));
            if (level != null)
            {
                done.Add(ApplyLevelAudio(scene, level));
                done.Add(ApplyPoemVoice(scene, level.poem));
            }
            MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (menu != null)
                done.Add(ApplyMenu(menu));
            done.Add(ApplySfx(name));
            int relinked = Relink(scene, out int voiced);
            if (relinked > 0)
                done.Add(relinked + " Ungaretti lines in English, " + voiced + " with Drive voice");
            done.RemoveAll(string.IsNullOrEmpty);
            if (done.Count == 0)
                continue;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            lines.Add(name + ": " + string.Join("; ", done) + ".");
        }
        if (!string.IsNullOrEmpty(active) && File.Exists(active))
            EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
        report = lines.Count == 0 ? "Nothing to apply." : string.Join("\n", lines);
        Debug.Log("Drive Audio\n" + report);
    }

    private static AudioClip First(params string[] files)
    {
        foreach (string file in files)
        {
            AudioClip clip = UngarettiLines.Clip(file);
            if (clip != null)
                return clip;
        }
        return null;
    }

    private static string ApplyLevelAudio(Scene scene, LevelSound level)
    {
        LevelAudio audio = Object.FindAnyObjectByType<LevelAudio>(FindObjectsInactive.Include);
        if (audio == null)
        {
            var go = new GameObject("Level Audio");
            SceneManager.MoveGameObjectToScene(go, scene);
            audio = go.AddComponent<LevelAudio>();
        }
        OpenableWindow window = Object.FindAnyObjectByType<OpenableWindow>(FindObjectsInactive.Include);
        var so = new SerializedObject(audio);
        var done = new List<string>();

        AudioClip music = First(level.music);
        if (music != null)
        {
            so.FindProperty("music").objectReferenceValue = music;
            so.FindProperty("musicMode").enumValueIndex = (int)LevelAudio.MusicMode.DuringTranscription;
            done.Add("music " + music.name);
        }
        AudioClip stinger = First(Stinger);
        if (stinger != null)
            so.FindProperty("poemFoundStinger").objectReferenceValue = stinger;

        var clips = level.ambience.Select(a => (amb: a, clip: First(a.files))).Where(a => a.clip != null).ToList();
        if (clips.Count > 0 || level.ambience.Length == 0)
        {
            SerializedProperty list = so.FindProperty("ambience");
            list.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                LevelAudio.Mix mix = window == null ? LevelAudio.Mix.Always : clips[i].amb.mix;
                float volume = window == null && clips[i].amb.mix == LevelAudio.Mix.WindowOpen && !clips[i].amb.open ? 0.2f : clips[i].amb.volume;
                entry.FindPropertyRelative("clip").objectReferenceValue = clips[i].clip;
                entry.FindPropertyRelative("monoClip").objectReferenceValue = AmbienceMono.Copy(clips[i].clip);
                entry.FindPropertyRelative("mix").enumValueIndex = (int)mix;
                entry.FindPropertyRelative("volume").floatValue = volume;
                entry.FindPropertyRelative("source").objectReferenceValue = null;
                entry.FindPropertyRelative("maxDistance").floatValue = 12f;
                entry.FindPropertyRelative("afterWeatherClears").boolValue = clips[i].amb.afterClear;
            }
            done.Add(clips.Count == 0 ? "no ambience yet" : "ambience " + string.Join(" + ", clips.Select(c => c.clip.name)));
        }
        so.FindProperty("window").objectReferenceValue = window;
        so.ApplyModifiedPropertiesWithoutUndo();
        return string.Join(", ", done);
    }

    private static string ApplyPoemVoice(Scene scene, int poem)
    {
        AudioClip clip = First("UNG_poesia" + poem);
        if (clip == null)
            return null;
        int count = 0;
        foreach (AssignPoem assign in Object.FindObjectsByType<AssignPoem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(assign);
            so.FindProperty("_poemVoice").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }
        return count > 0 ? "poem reading " + clip.name : null;
    }

    private static AudioClip[] Clips(string prefix, int count)
    {
        var list = new List<AudioClip>();
        for (int i = 1; i <= count; i++)
        {
            AudioClip clip = First(prefix + i);
            if (clip != null)
                list.Add(clip);
        }
        return list.ToArray();
    }

    private static void SetArray(SerializedProperty property, AudioClip[] clips)
    {
        property.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }

    private static void SetFootsteps(GameObject target, FootstepSurfaceType ground, bool spatial, float volume)
    {
        Footsteps steps = target.GetComponent<Footsteps>();
        if (steps == null)
            steps = target.AddComponent<Footsteps>();
        var so = new SerializedObject(steps);
        so.FindProperty("defaultSurface").enumValueIndex = (int)ground;
        so.FindProperty("spatial").boolValue = spatial;
        so.FindProperty("volume").floatValue = volume;
        SerializedProperty list = so.FindProperty("surfaces");
        var sets = StepSets.Select(s => (s.type, clips: Clips("SFX_Footsteps_" + s.surface + "_", 6))).Where(s => s.clips.Length > 0).ToList();
        list.arraySize = sets.Count;
        for (int i = 0; i < sets.Count; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("surface").enumValueIndex = (int)sets[i].type;
            SetArray(entry.FindPropertyRelative("clips"), sets[i].clips);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string ApplySfx(string sceneName)
    {
        var done = new List<string>();
        FootstepSurfaceType ground = GroundByLevel.FirstOrDefault(g => sceneName.Contains(g.scene)).surface;
        bool hasSteps = First("SFX_Footsteps_Wood_1", "SFX_Footsteps_Stone_1", "SFX_Footsteps_Tile_1", "SFX_Footsteps_Earth_1") != null;

        FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        if (player != null && hasSteps)
        {
            SetFootsteps(player.gameObject, ground, false, 0.35f);
            done.Add("player footsteps (" + ground + ")");
        }

        LevelFlow flow = Object.FindAnyObjectByType<LevelFlow>(FindObjectsInactive.Include);
        if (flow != null)
        {
            var so = new SerializedObject(flow);
            AudioClip open = First("SFX_Old_Door_Open");
            AudioClip close = First("SFX_Old_Door_Close");
            if (open != null)
                so.FindProperty("doorOpenClip").objectReferenceValue = open;
            if (close != null)
                so.FindProperty("doorCloseClip").objectReferenceValue = close;
            DialogueTrigger character = so.FindProperty("character").objectReferenceValue as DialogueTrigger;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (open != null || close != null)
                done.Add("door");
            if (character != null && hasSteps)
            {
                SetFootsteps(character.transform.root.gameObject, ground, true, 0.5f);
                done.Add(character.transform.root.name + " footsteps");
            }
        }

        int windows = 0;
        foreach (OpenableWindow window in Object.FindObjectsByType<OpenableWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool shutters = window.name.ToLowerInvariant().Contains("shutter");
            AudioClip open = First(shutters ? "SFX_Window_Shutters_Open" : "SFX_Window_Open");
            AudioClip close = First(shutters ? "SFX_Window_Shutters_Close" : "SFX_Window_Close");
            if (open == null && close == null)
                continue;
            var so = new SerializedObject(window);
            so.FindProperty("openClip").objectReferenceValue = open;
            so.FindProperty("closeClip").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
            windows++;
        }
        if (windows > 0)
            done.Add(windows + " window(s)");

        AudioClip[] pages = Clips("SFX_Page_Turn_", 7);
        TranscribePoem transcribe = Object.FindAnyObjectByType<TranscribePoem>(FindObjectsInactive.Include);
        if (transcribe != null && pages.Length > 0)
        {
            var so = new SerializedObject(transcribe);
            SetArray(so.FindProperty("_pageTurnClips"), pages);
            so.ApplyModifiedPropertiesWithoutUndo();
            done.Add("page turns");
        }

        AudioClip flicker = First("SFX_Lantern_Flicker_Loop");
        AudioClip[] hangs = Clips("SFX_Lantern_Hang_", 2);
        int lanterns = 0;
        if (flicker != null || hangs.Length > 0)
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.Contains("Lantern") || t.parent != null && t.parent.GetComponentsInParent<Transform>(true).Any(p => p.name.Contains("Lantern")))
                    continue;
                LanternSfx sfx = t.GetComponent<LanternSfx>();
                if (sfx == null)
                    sfx = t.gameObject.AddComponent<LanternSfx>();
                var so = new SerializedObject(sfx);
                so.FindProperty("flickerLoop").objectReferenceValue = flicker;
                SetArray(so.FindProperty("hangClips"), hangs);
                so.ApplyModifiedPropertiesWithoutUndo();
                lanterns++;
            }
        if (lanterns > 0)
            done.Add(lanterns + " lantern(s)");

        string broomSurface = ground == FootstepSurfaceType.Wood ? "Wood" : ground == FootstepSurfaceType.Tile ? "Tile" : "Stone";
        bool wet = ground == FootstepSurfaceType.Earth;
        BroomSweeper sweeper = Object.FindAnyObjectByType<BroomSweeper>(FindObjectsInactive.Include);
        AudioClip slow = First("SFX_Broom_" + broomSurface + "_Slow");
        AudioClip fast = First("SFX_Broom_" + broomSurface + "_Fast");
        AudioClip[] leaves = Clips(wet ? "SFX_Leaf_Wet_" : "SFX_Leaf_Rustle_", 6);
        if (leaves.Length == 0)
            leaves = Clips(wet ? "SFX_Leaf_Rustle_" : "SFX_Leaf_Wet_", 6);
        if (sweeper != null && (slow != null || fast != null || leaves.Length > 0))
        {
            var so = new SerializedObject(sweeper);
            if (slow != null)
                so.FindProperty("sweepClip").objectReferenceValue = slow;
            if (fast != null)
                so.FindProperty("fastClip").objectReferenceValue = fast;
            SetArray(so.FindProperty("leafClips"), leaves);
            so.ApplyModifiedPropertiesWithoutUndo();
            done.Add("broom (" + broomSurface + (leaves.Length > 0 ? ", " + (wet ? "wet" : "dry") + " leaves" : "") + ")");
        }

        AudioClip pen = First("SFX_Pen_Write");
        AudioClip[] gusts = sceneName.Contains("San Paolo") ? Clips("SFX_Leaf_Wet_", 6) : Clips("SFX_Wind_Gust_", 5);
        if (gusts.Length == 0)
            gusts = Clips("SFX_Wind_Gust_", 5);
        AudioClip laugh = First("SFX_Child_Laugh", "SFX_Child_Laugh_1");
        foreach (LookAwayCue cue in Object.FindObjectsByType<LookAwayCue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (laugh != null)
            {
                var cueSo = new SerializedObject(cue);
                cueSo.FindProperty("clip").objectReferenceValue = laugh;
                cueSo.ApplyModifiedPropertiesWithoutUndo();
                done.Add("child's laugh");
            }
        AudioClip umbrellaOpen = First("SFX_Umbrella_Open");
        foreach (Umbrella umbrella in Object.FindObjectsByType<Umbrella>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (umbrellaOpen != null)
            {
                var umbrellaSo = new SerializedObject(umbrella);
                umbrellaSo.FindProperty("openClip").objectReferenceValue = umbrellaOpen;
                umbrellaSo.ApplyModifiedPropertiesWithoutUndo();
                done.Add("umbrella");
            }
        if (transcribe != null && (pen != null || gusts.Length > 0))
        {
            var so = new SerializedObject(transcribe);
            if (pen != null)
                so.FindProperty("_writingClip").objectReferenceValue = pen;
            if (gusts.Length > 0)
            {
                SetArray(so.FindProperty("_gustClips"), gusts);
                so.FindProperty("_gustClip").objectReferenceValue = gusts[0];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            done.Add("pen and gusts");
        }

        DiaryInteraction diary = Object.FindAnyObjectByType<DiaryInteraction>(FindObjectsInactive.Include);
        AudioClip notebookOpen = First("SFX_Notebook_Open_1", "SFX_Notebook_Open_2");
        AudioClip notebookClose = First("SFX_Notebook_Close_1", "SFX_Notebook_Close_2");
        if (diary != null && (notebookOpen != null || notebookClose != null))
        {
            var so = new SerializedObject(diary);
            if (notebookOpen != null)
                so.FindProperty("openClip").objectReferenceValue = notebookOpen;
            if (notebookClose != null)
                so.FindProperty("closeClip").objectReferenceValue = notebookClose;
            so.ApplyModifiedPropertiesWithoutUndo();
            done.Add("notebook open/close");
        }

        AudioClip[] reveal = Clips("SFX_Paper_Reveal_", 3);
        AudioClip[] follower = Clips("SFX_Follower_Leaf_", 3);
        LevelAudio levelAudio = Object.FindAnyObjectByType<LevelAudio>(FindObjectsInactive.Include);
        if (levelAudio != null && (reveal.Length > 0 || follower.Length > 0))
        {
            var so = new SerializedObject(levelAudio);
            SetArray(so.FindProperty("paperRevealClips"), reveal);
            SetArray(so.FindProperty("followerLeafClips"), follower);
            so.ApplyModifiedPropertiesWithoutUndo();
            done.Add("paper reveal and golden leaf");
        }

        AudioClip chairMove = First("SFX_Chair_Move");
        if (chairMove != null)
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name.Contains("(moves)")))
            {
                MoveSfx move = t.GetComponent<MoveSfx>();
                if (move == null)
                    move = t.gameObject.AddComponent<MoveSfx>();
                var so = new SerializedObject(move);
                so.FindProperty("clip").objectReferenceValue = chairMove;
                so.ApplyModifiedPropertiesWithoutUndo();
                done.Add(t.name + " sound");
            }
        return string.Join(", ", done);
    }

    private static string ApplyMenu(MainMenuController menu)
    {
        var so = new SerializedObject(menu);
        var done = new List<string>();
        AudioClip music = First(TitleMusic);
        AudioClip wind = First(TitleAmbience);
        if (music != null)
        {
            so.FindProperty("menuMusic").objectReferenceValue = music;
            done.Add("menu music " + music.name);
        }
        if (wind != null)
        {
            so.FindProperty("windAmbience").objectReferenceValue = wind;
            done.Add("menu ambience " + wind.name);
        }
        AudioClip menuGust = First("UI_Menu_Gust1", "SFX_Wind_Gust_1");
        if (menuGust != null)
        {
            so.FindProperty("gustClip").objectReferenceValue = menuGust;
            done.Add("menu gust " + menuGust.name);
        }
        SetClips(so.FindProperty("gustClips"), "UI_Menu_Gust1", "UI_Menu_Gust2");
        SetClips(so.FindProperty("entryBackSounds"), "UI_Back_8", "UI_Back_6");
        SetClips(so.FindProperty("entryPaperSounds"), "SFX_Paper_Reveal_1", "SFX_Paper_Reveal_2", "SFX_Paper_Reveal_3");
        var menuSounds = new[] { ("entryEnterSound", "UI_Hover_1"), ("entryClickSound", "UI_Select_2"), ("startGameSound", "UI_StartGame") };
        foreach ((string property, string file) in menuSounds)
        {
            AudioClip clip = First(file.Split('|'));
            SerializedProperty field = so.FindProperty(property);
            if (clip != null && field != null)
            {
                field.objectReferenceValue = clip;
                done.Add(clip.name);
            }
        }
        AudioClip menuSweep = First("SFX_Broom_Wood_Slow");
        if (menuSweep != null)
        {
            so.FindProperty("sweepLoop").objectReferenceValue = menuSweep;
            done.Add("menu sweep");
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return string.Join(", ", done);
    }

    private static void SetClips(SerializedProperty list, params string[] files)
    {
        if (list == null)
            return;
        var clips = files.Select(f => First(f)).Where(c => c != null).ToList();
        if (clips.Count == 0)
            return;
        list.arraySize = clips.Count;
        for (int i = 0; i < clips.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }

    private static int Relink(Scene scene, out int voiced)
    {
        int lines = 0;
        voiced = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;
                var so = new SerializedObject(behaviour);
                var paths = new List<string>();
                SerializedProperty it = so.GetIterator();
                while (it.Next(true))
                    if (it.name == "pieces" && it.isArray && it.propertyType == SerializedPropertyType.Generic)
                        paths.Add(it.propertyPath);
                bool changed = false;
                foreach (string path in paths)
                {
                    SerializedProperty pieces = so.FindProperty(path);
                    if (pieces == null || pieces.arraySize == 0)
                        continue;
                    if (pieces.arraySize == 1)
                    {
                        UngarettiLines.Line whole = UngarettiLines.FindLine(pieces.GetArrayElementAtIndex(0).FindPropertyRelative("text").stringValue);
                        if (whole != null && whole.pieces.Length > 1)
                        {
                            float volume = pieces.GetArrayElementAtIndex(0).FindPropertyRelative("volume").floatValue;
                            pieces.arraySize = whole.pieces.Length;
                            for (int i = 0; i < whole.pieces.Length; i++)
                            {
                                SerializedProperty piece = pieces.GetArrayElementAtIndex(i);
                                piece.FindPropertyRelative("text").stringValue = whole.pieces[i].italian;
                                piece.FindPropertyRelative("volume").floatValue = volume;
                            }
                        }
                    }
                    bool any = false;
                    for (int i = 0; i < pieces.arraySize; i++)
                    {
                        SerializedProperty piece = pieces.GetArrayElementAtIndex(i);
                        SerializedProperty text = piece.FindPropertyRelative("text");
                        SerializedProperty voice = piece.FindPropertyRelative("voice");
                        if (text == null || voice == null)
                            continue;
                        UngarettiLines.Piece match = UngarettiLines.FindPiece(text.stringValue);
                        if (match == null)
                            continue;
                        text.stringValue = match.english;
                        AudioClip clip = UngarettiLines.Clip(match.file);
                        if (clip != null)
                        {
                            voice.objectReferenceValue = clip;
                            voiced++;
                        }
                        any = true;
                    }
                    if (any)
                    {
                        lines++;
                        changed = true;
                        string parent = path.Substring(0, path.Length - "pieces".Length);
                        SerializedProperty speaker = so.FindProperty(parent + "speakerId");
                        if (speaker != null && string.IsNullOrEmpty(speaker.stringValue))
                            speaker.stringValue = "ungaretti";
                    }
                }
                if (changed)
                    so.ApplyModifiedPropertiesWithoutUndo();
            }
        return lines;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void RemoveEmptyFolders(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return;
        foreach (string sub in AssetDatabase.GetSubFolders(folder))
            RemoveEmptyFolders(sub);
        if (folder != "Assets/_Game/Audio" && AssetDatabase.GetSubFolders(folder).Length == 0 && !Directory.GetFiles(folder).Any(f => !f.EndsWith(".meta")))
            AssetDatabase.DeleteAsset(folder);
    }
}
