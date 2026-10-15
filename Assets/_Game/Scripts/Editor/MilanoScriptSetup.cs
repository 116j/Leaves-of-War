using System.Collections.Generic;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MilanoScriptSetup
{
    private const string Ungaretti = "ungaretti";
    private const string Carlo = "carlo";

    private static readonly Dictionary<string, (string speaker, string text)> Lines = new Dictionary<string, (string, string)>
    {
        { "L1_01", (Ungaretti, "Milan. I teach French to the boys, and in the evening the wind comes in through this window.") },
        { "L1_02", (Carlo, "Professor! The window again. Half the trees of Milan are on my floor.") },
        { "L1_03", (Ungaretti, "Forgive me, Signor Carlo. I like to feel the air from the street.") },
        { "L1_04", (Carlo, "Then you sweep it. There's the broom.") },
        { "L1_05", (Carlo, "And that notebook you carry everywhere. Do you ever let it go?") },
        { "L1_06", (Ungaretti, "Never. The broom in one hand, the notebook in the other.") },
        { "L1_07", (Carlo, "Show me, then. What do you write in it?") },
        { "L1_08", (Ungaretti, "Nothing yet. Words come when they please.") },
        { "L1_09", (Ungaretti, "The chair... it was at the table.") },
        { "L1_10", (Carlo, "Of course it was. And now it's by the window. Things don't wait for you to look at them, Professor.") },
        { "L1_11", (Ungaretti, "There's something underneath. Words... as if written on the floor.") },
        { "L1_12", (Carlo, "Words? I only see floorboards, Professor. You look like you've seen a ghost.") },
        { "L1_13", (Ungaretti, "I don't remember writing them. And yet it's my handwriting.") },
        { "L1_14", (Carlo, "Whatever you're looking at, keep your eyes on it. Things like that run as soon as you turn around.") },
        { "L1_15", (Carlo, "I don't understand poetry, Professor. But that one sounded like you.") },
        { "L1_16", (Ungaretti, "Thank you, Signor Carlo. That's the finest compliment you could pay me.") },
        { "L1_17", (Carlo, "I'll leave you to your leaves. Don't turn around too fast, or you won't know where you are.") },
        { "L1_18", (Ungaretti, "I can feel the room changing behind me. Like a page turning by itself.") }
    };

    [MenuItem("Tools/Leaves of War/Setup Milano Script")]
    private static void Setup()
    {
        var log = new List<string>();
        Transform room = Roots().FirstOrDefault(t => t.name == "Milano Room");
        LevelFlow flow = Find<LevelFlow>();
        PickupItem broom = Find<PickupItem>();
        if (room == null || flow == null || broom == null)
        {
            EditorUtility.DisplayDialog("Milano Script", "Run Build Milano Room, Setup Sweeping and Setup Level Start first.", "OK");
            return;
        }

        Transform carloExit = Anchor(room, "Carlo Exit", new Vector3(1.2f, 0f, -2.3f), 180f);
        Transform exit = Anchor(room, "Exit Anchor (door)", new Vector3(1.2f, 0f, -2.0f), 180f);
        Transform outside = Anchor(room, "Carlo Outside", new Vector3(1.2f, 0f, -3.3f), 0f);
        Transform door = room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Door (hinge)");

        var so = new SerializedObject(flow);
        so.FindProperty("titleCard").stringValue = "MILANO — 1914–1915";
        so.FindProperty("nextScene").stringValue = "Level 2 - Parigi";
        so.FindProperty("characterName").stringValue = "Carlo";
        so.FindProperty("exitAnchor").objectReferenceValue = exit;
        so.FindProperty("followerLeafFromStart").boolValue = false;
        so.FindProperty("broom").objectReferenceValue = broom;
        so.FindProperty("characterExitPoint").objectReferenceValue = carloExit;
        so.FindProperty("characterOutside").objectReferenceValue = outside;
        so.FindProperty("door").objectReferenceValue = door;
        so.FindProperty("doorOpenAngle").floatValue = -90f;
        so.FindProperty("lookAtCharacterOnStart").boolValue = false;
        so.FindProperty("doorVolume").floatValue = 0.55f;

        int missingVoices = 0;
        SerializedProperty steps = so.FindProperty("steps");
        Kept.Clear();
        Keep(so.FindProperty("firstGustLines"));
        for (int i = 0; i < steps.arraySize; i++)
            Keep(steps.GetArrayElementAtIndex(i).FindPropertyRelative("lines"));
        var plan = new (string note, LevelFlow.Condition wait, float amount, string objective, string[] lines, LevelFlow.StepAction action)[]
        {
            ("Arrival", LevelFlow.Condition.None, 0f, "", new[] { "L1_01" }, LevelFlow.StepAction.None),
            ("Carlo comes in", LevelFlow.Condition.Wait, 10f, "", new string[0], LevelFlow.StepAction.CharacterEnters),
            ("Carlo complains", LevelFlow.Condition.TalkToCharacter, 0f, "TALK TO {character}", new[] { "L1_02", "L1_03", "L1_04" }, LevelFlow.StepAction.None),
            ("Broom", LevelFlow.Condition.PickUpBroom, 0f, "PICK UP THE BROOM", new string[0], LevelFlow.StepAction.None),
            ("Sweep by the bed", LevelFlow.Condition.SweepLeaves, 120f, "HOLD {sweepKey} AND MOVE THE MOUSE TO SWEEP", new[] { "L1_05", "L1_06", "L1_07" }, LevelFlow.StepAction.None),
            ("Notebook", LevelFlow.Condition.OpenNotebook, 0f, "PRESS {diaryKey} TO OPEN THE NOTEBOOK", new[] { "L1_08" }, LevelFlow.StepAction.MoveObjectWhenUnseen),
            ("The chair", LevelFlow.Condition.CloseNotebook | LevelFlow.Condition.ObservableBehaviourChanged, 0f, "", new[] { "L1_09", "L1_10" }, LevelFlow.StepAction.ShowFollowerLeaf),
            ("The big pile", LevelFlow.Condition.PoemFound | LevelFlow.Condition.Wait, 0f, "SWEEP THE BIG PILE IN THE MIDDLE OF THE ROOM", new[] { "L1_11", "L1_12", "L1_13" }, LevelFlow.StepAction.None),
            ("Transcription", LevelFlow.Condition.PoemTranscribed, 0f, "KEEP YOUR EYES ON THE POEM WHILE YOU TRANSCRIBE", new string[0], LevelFlow.StepAction.None),
            ("Silence, Carlo leaves", LevelFlow.Condition.Wait, 2.5f, "", new[] { "L1_15", "L1_16", "L1_17" }, LevelFlow.StepAction.CharacterLeaves),
            ("Alone", LevelFlow.Condition.Wait, 1.5f, "", new[] { "L1_18" }, LevelFlow.StepAction.LoadNextLevel),
            ("Turn around", LevelFlow.Condition.TurnAround, 0f, "TURN AROUND", new string[0], LevelFlow.StepAction.None)
        };
        steps.arraySize = plan.Length;
        for (int i = 0; i < plan.Length; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);
            step.FindPropertyRelative("note").stringValue = plan[i].note;
            step.FindPropertyRelative("waitFor").intValue = (int)plan[i].wait;
            step.FindPropertyRelative("amount").floatValue = plan[i].amount;
            step.FindPropertyRelative("objective").stringValue = plan[i].objective;
            step.FindPropertyRelative("action").enumValueIndex = (int)plan[i].action;
            FillLines(step.FindPropertyRelative("lines"), plan[i].lines, ref missingVoices);
        }
        FillLines(so.FindProperty("firstGustLines"), new[] { "L1_14" }, ref missingVoices);
        so.ApplyModifiedProperties();
        log.Add("Level 1 follows the script: L1_01 to L1_18, " + plan.Length + " steps.");
        log.Add(missingVoices == 0 ? "All voice clips found." : missingVoices + " voice clips not found yet (Ungaretti: UNG_L1_track1... from the Drive; Carlo: Carlo - Milano - 01... or L1_02 ...; then run this again).");

        OpenableWindow window = Find<OpenableWindow>();
        if (window != null)
        {
            var windowSo = new SerializedObject(window);
            windowSo.FindProperty("startOpen").boolValue = true;
            windowSo.ApplyModifiedProperties();
            log.Add("The window starts open, as in the script.");
        }

        DiaryInteraction diary = Find<DiaryInteraction>();
        if (diary != null)
        {
            var diarySo = new SerializedObject(diary);
            diarySo.FindProperty("startInHand").boolValue = true;
            diarySo.ApplyModifiedProperties();
        }
        GameObject diaryPickup = Roots().Select(t => t.gameObject).FirstOrDefault(g => g.name == "Diary (pickup)");
        if (diaryPickup != null)
            Undo.DestroyObjectImmediate(diaryPickup);
        log.Add("The notebook is always in hand.");

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Setup Milano Script\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Milano Script", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static void FillLines(SerializedProperty list, string[] ids, ref int missing)
    {
        list.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++)
        {
            (string speaker, string text) = Lines[ids[i]];
            SerializedProperty line = list.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("speakerId").stringValue = speaker;
            SerializedProperty pieces = line.FindPropertyRelative("pieces");
            pieces.arraySize = 1;
            SerializedProperty piece = pieces.GetArrayElementAtIndex(0);
            piece.FindPropertyRelative("text").stringValue = text;
            piece.FindPropertyRelative("volume").floatValue = 0.55f;
            AudioClip clip = Voice(ids[i]);
            if (clip == null)
                Kept.TryGetValue(text, out clip);
            piece.FindPropertyRelative("voice").objectReferenceValue = clip;
            if (clip == null)
                missing++;
        }
    }

    private static readonly Dictionary<string, AudioClip> Kept = new Dictionary<string, AudioClip>();

    private static void Keep(SerializedProperty lines)
    {
        for (int i = 0; i < lines.arraySize; i++)
        {
            SerializedProperty pieces = lines.GetArrayElementAtIndex(i).FindPropertyRelative("pieces");
            for (int j = 0; j < pieces.arraySize; j++)
            {
                SerializedProperty piece = pieces.GetArrayElementAtIndex(j);
                string text = piece.FindPropertyRelative("text").stringValue;
                if (piece.FindPropertyRelative("voice").objectReferenceValue is AudioClip clip && !string.IsNullOrEmpty(text))
                    Kept[text] = clip;
            }
        }
    }

    private static AudioClip Voice(string id)
    {
        string speaker = Lines[id].speaker;
        int order = Lines.Keys.Where(k => Lines[k].speaker == speaker).OrderBy(k => k, System.StringComparer.Ordinal).ToList().IndexOf(id) + 1;
        string name = char.ToUpperInvariant(speaker[0]) + speaker.Substring(1);
        UngarettiLines.Line script = speaker == Ungaretti ? UngarettiLines.Get("Ungaretti - Milano - " + order.ToString("00")) : null;
        if (script != null)
        {
            UngarettiLines.Refresh();
            AudioClip drive = UngarettiLines.Clip(script.pieces[0].file);
            if (drive != null)
                return drive;
        }
        string[] wanted =
        {
            Clean(id),
            Clean(name + " - Milano - " + order.ToString("00")),
            Clean(name + " - Milano - " + order)
        };

        AudioClip fallback = null;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Clean(System.IO.Path.GetFileNameWithoutExtension(path));
            if (file == wanted[0])
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (fallback == null && (file == wanted[1] || file == wanted[2]))
                fallback = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return fallback;
    }

    private static string Clean(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static Transform Anchor(Transform room, string name, Vector3 local, float yaw)
    {
        Transform anchor = room.Find(name);
        if (anchor == null)
        {
            anchor = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Setup Milano Script");
            anchor.SetParent(room, false);
        }
        anchor.localPosition = local;
        anchor.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return anchor;
    }

    private static T Find<T>() where T : Component =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include).FirstOrDefault(c => !EditorUtility.IsPersistent(c));

    private static IEnumerable<Transform> Roots() =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.parent == null && !EditorUtility.IsPersistent(t));
}
