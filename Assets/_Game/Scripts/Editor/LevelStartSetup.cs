using System.Collections.Generic;
using System.Linq;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LevelStartSetup
{
    [MenuItem("Tools/Leaves of War/Setup Level Start")]
    private static void Setup()
    {
        var log = new List<string>();
        FirstPersonController player = Find<FirstPersonController>();
        DiaryInteraction diaryInteraction = Find<DiaryInteraction>();
        BroomSweeper sweeper = Find<BroomSweeper>();
        if (player == null || diaryInteraction == null || sweeper == null)
        {
            EditorUtility.DisplayDialog("Level Start", "Needs the Player, Diary Interaction and Broom Sweeper (run Setup Sweeping first).", "OK");
            return;
        }

        var diarySo = new SerializedObject(diaryInteraction);
        var heldDiary = diarySo.FindProperty("diary").objectReferenceValue as Transform;
        var sweeperSo = new SerializedObject(sweeper);
        var heldBroom = sweeperSo.FindProperty("broom").objectReferenceValue as Transform;
        if (heldBroom == null)
            heldBroom = diarySo.FindProperty("broom").objectReferenceValue as Transform;

        diarySo.FindProperty("startInHand").boolValue = true;
        diarySo.ApplyModifiedProperties();
        sweeperSo.FindProperty("startInHand").boolValue = false;
        sweeperSo.ApplyModifiedProperties();
        GameObject oldDiary = Roots().Select(t => t.gameObject).FirstOrDefault(g => g.name == "Diary (pickup)");
        if (oldDiary != null)
            Undo.DestroyObjectImmediate(oldDiary);
        log.Add("The notebook is always in hand; the broom starts against the wall.");

        Transform room = Roots().FirstOrDefault(t => t.name == "Milano Room");
        Vector3 broomPosition, diaryPosition;
        Quaternion broomRotation, diaryRotation;
        if (room != null)
        {
            broomPosition = room.TransformPoint(new Vector3(-1.85f, 0f, -0.45f));
            broomRotation = room.rotation * Quaternion.Euler(0f, 90f, 0f);
            diaryPosition = room.TransformPoint(new Vector3(1.45f, 0.76f, 1.85f));
            diaryRotation = room.rotation * Quaternion.Euler(0f, -15f, 0f);
        }
        else
        {
            Transform p = player.transform;
            broomPosition = p.position + p.forward * 1.5f + p.right;
            broomRotation = p.rotation;
            diaryPosition = p.position + p.forward * 1.5f - p.right + Vector3.up * 0.75f;
            diaryRotation = p.rotation;
        }

        PickupItem broomPickup = BuildBroom(heldBroom, broomPosition, broomRotation);
        log.Add("Broom leaning on the wall: move it where you like.");

        GameObject flowObject = Roots().Select(t => t.gameObject).FirstOrDefault(g => g.GetComponent<LevelFlow>() != null);
        if (flowObject == null)
        {
            flowObject = new GameObject("Level Flow");
            Undo.RegisterCreatedObjectUndo(flowObject, "Setup Level Start");
            flowObject.AddComponent<LevelFlow>();
        }
        DialogueTrigger character = Object.FindObjectsByType<DialogueTrigger>(FindObjectsInactive.Include).FirstOrDefault(d => !EditorUtility.IsPersistent(d));
        var flowSo = new SerializedObject(flowObject.GetComponent<LevelFlow>());
        flowSo.FindProperty("character").objectReferenceValue = character;
        if (character != null)
            flowSo.FindProperty("characterName").stringValue = character.transform.root.name.Trim();
        flowSo.FindProperty("broom").objectReferenceValue = broomPickup;
        flowSo.ApplyModifiedProperties();
        log.Add(character != null ? "Level starts looking at " + character.transform.root.name.Trim() + ": talk first, then the pickups unlock." : "WARNING: no DialogueTrigger found, pickups unlock at once.");

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Setup Level Start\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Level Start", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    internal static PickupItem BuildBroom(Transform held, Vector3 position, Quaternion rotation)
    {
        GameObject root = Pickup("Broom (pickup)", position, rotation, PickupItem.Item.Broom, "PICK UP THE BROOM");
        if (held != null)
        {
            GameObject model = Object.Instantiate(held.gameObject, root.transform);
            model.name = "Model";
            model.SetActive(true);
            Strip(model);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = held.localRotation * Quaternion.Euler(0f, 0f, 12f);
            model.transform.localScale = held.lossyScale;
            Bounds bounds = WorldBounds(model);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (size > 0.001f)
                model.transform.localScale *= 1.4f / size;
            bounds = WorldBounds(model);
            model.transform.position += Vector3.up * (position.y - bounds.min.y);
        }
        else
        {
            Box(root.transform, "Model", new Vector3(0f, 0.7f, 0f), new Vector3(0.08f, 1.4f, 0.08f));
        }
        FitCollider(root);
        return root.GetComponent<PickupItem>();
    }

    private static PickupItem BuildDiary(Transform held, Vector3 position, Quaternion rotation)
    {
        GameObject root = Pickup("Diary (pickup)", position, rotation, PickupItem.Item.Diary, "PICK UP THE DIARY");
        MeshFilter heldMesh = held != null ? held.GetComponent<MeshFilter>() : null;
        MeshRenderer heldRenderer = held != null ? held.GetComponent<MeshRenderer>() : null;
        var model = new GameObject("Model");
        model.transform.SetParent(root.transform, false);
        MeshFilter filter = model.AddComponent<MeshFilter>();
        MeshRenderer renderer = model.AddComponent<MeshRenderer>();
        filter.sharedMesh = heldMesh != null ? heldMesh.sharedMesh : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (heldRenderer != null)
            renderer.sharedMaterials = heldRenderer.sharedMaterials;
        Vector3 meshSize = filter.sharedMesh.bounds.size;
        model.transform.localScale = new Vector3(0.16f / Mathf.Max(0.001f, meshSize.x), 0.03f / Mathf.Max(0.001f, meshSize.y), 0.22f / Mathf.Max(0.001f, meshSize.z));
        model.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        FitCollider(root);
        return root.GetComponent<PickupItem>();
    }

    private static GameObject Pickup(string name, Vector3 position, Quaternion rotation, PickupItem.Item item, string prompt)
    {
        GameObject old = Roots().Select(t => t.gameObject).FirstOrDefault(g => g.name == name);
        if (old != null)
            Undo.DestroyObjectImmediate(old);
        var root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, "Setup Level Start");
        root.transform.SetPositionAndRotation(position, rotation);
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0)
            root.layer = layer;
        PickupItem pickup = root.AddComponent<PickupItem>();
        var so = new SerializedObject(pickup);
        so.FindProperty("item").enumValueIndex = (int)item;
        so.FindProperty("prompt").stringValue = prompt;
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static void Strip(GameObject model)
    {
        foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(behaviour);
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = 0;
    }

    private static void FitCollider(GameObject root)
    {
        Bounds world = WorldBounds(root);
        BoxCollider box = root.AddComponent<BoxCollider>();
        Vector3 center = root.transform.InverseTransformPoint(world.center);
        Vector3 size = root.transform.InverseTransformVector(world.size);
        box.center = center;
        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) + Vector3.one * 0.05f;
    }

    private static Bounds WorldBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one * 0.3f);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
            b.Encapsulate(r.bounds);
        return b;
    }

    private static void Box(Transform parent, string name, Vector3 position, Vector3 size)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(box.GetComponent<Collider>());
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
    }

    private static T Find<T>() where T : Component =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include).FirstOrDefault(c => !EditorUtility.IsPersistent(c));

    private static IEnumerable<Transform> Roots() =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.parent == null && !EditorUtility.IsPersistent(t));
}
