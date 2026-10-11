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
        { "L1_01", (Ungaretti, "Milano. Insegno il francese ai ragazzi, e la sera il vento entra da questa finestra.") },
        { "L1_02", (Carlo, "Professor! The window again. Half the trees of Milan are on my floor.") },
        { "L1_03", (Ungaretti, "Mi scusi, signor Carlo. Mi piace sentire l'aria della strada.") },
        { "L1_04", (Carlo, "Then you sweep it. There's the broom.") },
        { "L1_05", (Carlo, "And that notebook you carry everywhere. Do you ever let it go?") },
        { "L1_06", (Ungaretti, "Mai. La scopa in una mano, il quaderno nell'altra.") },
        { "L1_07", (Carlo, "Show me, then. What do you write in it?") },
        { "L1_08", (Ungaretti, "Ancora niente. Le parole arrivano quando vogliono loro.") },
        { "L1_09", (Ungaretti, "La sedia... era al tavolo.") },
        { "L1_10", (Carlo, "Of course it was. And now it's by the window. Things don't wait for you to look at them, Professor.") },
        { "L1_11", (Ungaretti, "C'è qualcosa, sotto. Una pagina scritta.") },
        { "L1_12", (Carlo, "Is that yours? It looks like your hand.") },
        { "L1_13", (Ungaretti, "Non ricordo di averla scritta. Eppure è la mia scrittura.") },
        { "L1_14", (Carlo, "Keep your eyes on it! Paper is like a cat, it runs as soon as you turn around.") },
        { "L1_15", (Carlo, "I don't understand poetry, Professor. But that one sounded like you.") },
        { "L1_16", (Ungaretti, "Grazie, signor Carlo. È il complimento più bello che potesse farmi.") },
        { "L1_17", (Carlo, "I'll leave you to your leaves. Don't turn around too fast, or you won't know where you are.") },
        { "L1_18", (Ungaretti, "Sento la stanza cambiare alle mie spalle. Come una pagina che si volta da sola.") }
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

        Transform chair = room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Bentwood Chair"));
        Transform chairTarget = Anchor(room, "Chair By Window", new Vector3(-0.55f, 0f, 1.95f), 200f);
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
        so.FindProperty("movable").objectReferenceValue = chair;
        so.FindProperty("movableTarget").objectReferenceValue = chairTarget;
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
            ("The chair", LevelFlow.Condition.CloseNotebook, 0f, "", new[] { "L1_09", "L1_10" }, LevelFlow.StepAction.ShowFollowerLeaf),
            ("The big pile", LevelFlow.Condition.PoemFound, 0f, "SWEEP THE BIG PILE IN THE MIDDLE OF THE ROOM", new[] { "L1_11", "L1_12", "L1_13" }, LevelFlow.StepAction.None),
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
            step.FindPropertyRelative("waitFor").enumValueIndex = (int)plan[i].wait;
            step.FindPropertyRelative("amount").floatValue = plan[i].amount;
            step.FindPropertyRelative("objective").stringValue = plan[i].objective;
            step.FindPropertyRelative("action").enumValueIndex = (int)plan[i].action;
            FillLines(step.FindPropertyRelative("lines"), plan[i].lines, ref missingVoices);
        }
        FillLines(so.FindProperty("firstGustLines"), new[] { "L1_14" }, ref missingVoices);
        so.ApplyModifiedProperties();
        log.Add("Level 1 follows the script: L1_01 to L1_18, " + plan.Length + " steps.");
        log.Add(missingVoices == 0 ? "All voice clips found." : missingVoices + " voice clips not found yet (name the files L1_01 ... L1_18 and run this again).");

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
        foreach (string guid in AssetDatabase.FindAssets(id + " t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path).Equals(id, System.StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }

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
