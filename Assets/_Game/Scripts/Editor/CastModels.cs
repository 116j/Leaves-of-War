using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CastModels
{
    private const string Folder = "Assets/_Game/Art/Characters/MakeHuman";
    private const string MaterialFolder = Folder + "/_Materials";
    private const string AnimatorFolder = "Assets/_Game/Art/Characters/Animators";
    private const string ModelChild = "CC0 Character";
    private const string Willowet = "willowet";
    private const string Raybrook = "raybrook";
    private const string Walk = "walk";
    private const string AnimationFolder = "Assets/_Game/Art/Characters/Animations";

    private sealed class Role
    {
        public readonly string name;
        public readonly string set;
        public readonly Color clothes;
        public readonly string[] aliases;

        public Role(string name, string set, Color clothes, params string[] aliases)
        {
            this.name = name;
            this.set = set;
            this.clothes = clothes;
            this.aliases = aliases;
        }
    }

    private static readonly Color Keep = Color.clear;

    private static readonly Role[] Casting =
    {
        new Role("Carlo", Raybrook, Keep),
        new Role("Apollinaire", Raybrook, Keep, "Guillaume"),
        new Role("Moammed", Raybrook, Keep, "Mohammed", "Mohamed", "Moamed", "Sceab"),
        new Role("Pietro", Raybrook, new Color(0.47f, 0.48f, 0.38f)),
        new Role("Rui", Raybrook, Keep),
        new Role("Leone", Raybrook, Keep),
        new Role("Lucien", Raybrook, Keep),
    };

    private static readonly string[] Cutout = { "eyebrow", "eyelash", "lash", "brow", "hair" };
    private static readonly string[] Body = { "skin", "body", "eye", "teeth", "tongue", "brow", "lash", "hair", "shoe", "boot" };

    [MenuItem("Tools/Leaves of War/Characters/Create MakeHuman folders")]
    private static void Folders()
    {
        foreach (Role role in Casting)
            EnsureFolder(Folder + "/" + role.name);
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(Folder + "/" + Casting[0].name);
    }

    [MenuItem("Tools/Leaves of War/Characters/Apply characters to open scene")]
    private static void ApplyMenu()
    {
        var log = new List<string>();
        log.AddRange(Setup());
        log.AddRange(Apply(SceneManager.GetActiveScene()));
        Debug.Log("Characters\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("Characters", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
    }

    private static string ModelPath(Role role)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            return null;
        var keys = new[] { role.name }.Concat(role.aliases).Select(k => k.ToLowerInvariant()).ToArray();
        return AssetDatabase.FindAssets("t:Model", new[] { Folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .Where(p => keys.Any(p.ToLowerInvariant().Contains))
            .OrderBy(p => p, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static readonly Dictionary<string, Avatar> Avatars = new Dictionary<string, Avatar>();
    private static readonly Dictionary<string, RuntimeAnimatorController> Controllers = new Dictionary<string, RuntimeAnimatorController>();

    private static List<string> Setup()
    {
        var log = new List<string>();
        Avatars.Clear();
        Controllers.Clear();
        EnsureFolder(AnimatorFolder);
        List<AnimationClip> own = FolderClips();
        AnimationClip ownIdle = own.FirstOrDefault(c => Named(c, "idle"));
        AnimationClip ownTalk = own.FirstOrDefault(c => Named(c, "talk") || Named(c, "speak"));
        (AnimationClip idle, AnimationClip talk) clips = (ownIdle, ownTalk);
        string controllerName = "Cast Characters";
        if (clips.idle == null)
        {
            log.Add("No Idle in " + AnimationFolder + ": Professor Raybrook's clips are used.");
            clips = Clips(Raybrook);
            controllerName = "Cast Raybrook";
        }
        else if (clips.talk == null)
        {
            clips.talk = Clips(Raybrook).talk;
        }
        if (clips.idle == null)
        {
            log.Add("Raybrook clips not found (ProfessorRaybrook@Idle / @Talking): Willowet is used instead.");
            clips = Clips(Willowet);
            controllerName = "Cast Willowet";
        }
        AnimationClip walk = own.FirstOrDefault(c => Named(c, Walk)) ?? WalkClip();
        log.Add("Clips: idle " + Describe(clips.idle) + ", talking " + Describe(clips.talk) + ", walking " + Describe(walk));
        if (walk == null)
            log.Add("No walk clip found (an FBX with \"walk\" in its name, e.g. Mixamo Walking): characters slide when they enter or leave.");

        foreach (Role role in Casting)
        {
            string path = ModelPath(role);
            if (path == null)
            {
                log.Add(role.name + ": no FBX in " + Folder + "/" + role.name);
                continue;
            }
            Avatar avatar = Humanoid(path);
            Avatars[role.name] = avatar;
            Controllers[role.name] = avatar != null && clips.idle != null ? Controller(controllerName, clips.idle, clips.talk ?? clips.idle, walk) : null;
            log.Add(role.name + ": " + System.IO.Path.GetFileName(path) + (avatar != null ? ", Humanoid, " + controllerName + (walk != null ? " + walk" : "") : ", NOT Humanoid (no animation): check the Game engine skeleton in MakeHuman"));
        }
        AssetDatabase.SaveAssets();
        return log;
    }

    private static (AnimationClip idle, AnimationClip talk) Clips(string key)
    {
        var all = new List<AnimationClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string lower = path.ToLowerInvariant();
            if (!lower.Contains(key))
                continue;
            Loop(path);
            all.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__") && c.humanMotion));
        }
        AnimationClip idle = all.FirstOrDefault(c => Named(c, "idle")) ?? all.FirstOrDefault(c => !Named(c, "talk"));
        AnimationClip talk = all.FirstOrDefault(c => Named(c, "talk") || Named(c, "speak") || Named(c, "parla"));
        return (idle, talk);
    }

    private static string Describe(AnimationClip clip) => clip == null ? "none" : System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(clip));

    private static List<AnimationClip> FolderClips()
    {
        var result = new List<AnimationClip>();
        if (!AssetDatabase.IsValidFolder(AnimationFolder))
            return result;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }
            Loop(path);
            result.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__") && c.humanMotion));
        }
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimationFolder }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null && clip.humanMotion && !result.Contains(clip))
                result.Add(clip);
        }
        return result;
    }

    private static AnimationClip WalkClip()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileName(path).ToLowerInvariant();
            if (!file.Contains(Walk) || path.StartsWith(Folder) || path.StartsWith("Packages/"))
                continue;
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }
            Loop(path);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") && c.humanMotion);
            if (clip != null)
                return clip;
        }
        return null;
    }

    private static bool Named(AnimationClip clip, string word) =>
        clip.name.ToLowerInvariant().Contains(word) || AssetDatabase.GetAssetPath(clip).ToLowerInvariant().Contains(word);

    private static void Loop(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            return;
        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        if (clips.Length == 0 || clips.All(c => c.loopTime && !c.lockRootPositionXZ && c.lockRootRotation && c.lockRootHeightY))
            return;
        foreach (ModelImporterClipAnimation c in clips)
        {
            c.loopTime = true;
            c.lockRootRotation = true;
            c.keepOriginalOrientation = true;
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = true;
            c.lockRootPositionXZ = false;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static RuntimeAnimatorController Controller(string name, AnimationClip idle, AnimationClip talk, AnimationClip walk)
    {
        string path = AnimatorFolder + "/" + name + ".controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        machine.defaultState = State(machine, "Idle", idle);
        State(machine, "Talking", talk);
        State(machine, "Waiting", idle);
        State(machine, "Walking", walk != null ? walk : idle);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion)
    {
        AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
        state.motion = motion;
        return state;
    }

    private static Avatar AvatarOf(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();

    private static bool Good(Avatar avatar) => avatar != null && avatar.isValid && avatar.isHuman;

    private static Avatar Humanoid(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer.animationType == ModelImporterAnimationType.Human && Good(AvatarOf(path)))
            return AvatarOf(path);

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.SaveAndReimport();
        if (Good(AvatarOf(path)))
            return AvatarOf(path);

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.SaveAndReimport();
        HumanDescription? mapped = Map(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        if (mapped != null)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = mapped.Value;
            importer.SaveAndReimport();
            if (Good(AvatarOf(path)))
                return AvatarOf(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.SaveAndReimport();
        }
        return null;
    }

    private static HumanDescription? Map(GameObject model)
    {
        if (model == null)
            return null;
        Transform root = model.transform;
        var renderers = new HashSet<Transform>(model.GetComponentsInChildren<Renderer>(true).Select(r => r.transform));
        List<Transform> bones = root.GetComponentsInChildren<Transform>(true).Where(t => t != root && !renderers.Contains(t)).ToList();

        Transform head = bones.FirstOrDefault(t => Is(t, "head") && !Is(t, "end", "top", "nub", "tip", "front"));
        List<Transform> feet = bones.Where(t => Is(t, "foot", "ankle") && !Is(t, "end", "nub", "tip", "toe", "ik", "target", "pole")).ToList();
        if (head == null || feet.Count < 2)
            return null;
        Transform footA = feet[0];
        Transform footB = feet.FirstOrDefault(f => !f.IsChildOf(footA) && !footA.IsChildOf(f) && Common(f, footA) != f.parent);
        if (footB == null)
            return null;
        Transform hips = Common(Common(footA, footB), head);
        if (hips == null || hips == root)
            return null;

        List<Transform> spinePath = Between(hips, head);
        if (spinePath.Count == 0)
            return null;
        var map = new Dictionary<string, Transform> { { "Hips", hips }, { "Head", head }, { "Spine", spinePath[0] } };
        int last = spinePath.Count - 1;
        if (spinePath.Count >= 2 && (Is(spinePath[last], "neck") || spinePath.Count >= 3))
            map["Neck"] = spinePath[last];
        else
            last++;
        if (last - 1 >= 1)
            map["Chest"] = spinePath[1];
        if (last - 1 >= 2)
            map["UpperChest"] = spinePath[2];

        var onPath = new HashSet<Transform>(spinePath) { hips, head };
        var arms = new List<List<Transform>>();
        foreach (Transform s in spinePath)
            foreach (Transform child in s)
                if (!onPath.Contains(child) && !renderers.Contains(child) && !head.IsChildOf(child))
                {
                    List<Transform> chain = Chain(child);
                    if (chain.Count >= 3)
                        arms.Add(chain);
                }
        var legs = new List<List<Transform>>();
        foreach (Transform child in hips)
            if (!onPath.Contains(child) && !renderers.Contains(child) && (footA.IsChildOf(child) || footB.IsChildOf(child)))
                legs.Add(Chain(child));
        if (arms.Count < 2 || legs.Count < 2)
            return null;

        List<List<Transform>> armPair = arms.OrderByDescending(a => a.Any(t => Is(t, "hand", "fist", "wrist", "palm"))).ThenByDescending(a => a.Count).Take(2).ToList();
        int[] armSides = SidePair(armPair, root);
        for (int i = 0; i < 2; i++)
        {
            List<Transform> arm = armPair[i];
            string side = armSides[i] < 0 ? "Left" : "Right";
            int k = arm.FindIndex(t => Is(t, "hand", "fist", "wrist", "palm"));
            if (k < 2)
                k = arm.Count >= 4 && Is(arm[0], "shoulder", "clavicle", "collar") ? 3 : 2;
            if (k >= arm.Count)
                return null;
            map[side + "Hand"] = arm[k];
            map[side + "LowerArm"] = arm[k - 1];
            map[side + "UpperArm"] = arm[k - 2];
            if (k >= 3)
                map[side + "Shoulder"] = arm[k - 3];
        }
        List<List<Transform>> legPair = legs.Take(2).ToList();
        int[] legSides = SidePair(legPair, root);
        for (int i = 0; i < 2; i++)
        {
            List<Transform> leg = legPair[i];
            string side = legSides[i] < 0 ? "Left" : "Right";
            int k = leg.FindIndex(t => Is(t, "foot", "ankle") && !Is(t, "toe"));
            if (k < 2)
                k = 2;
            if (k >= leg.Count)
                return null;
            map[side + "Foot"] = leg[k];
            map[side + "LowerLeg"] = leg[k - 1];
            map[side + "UpperLeg"] = leg[k - 2];
            if (k + 1 < leg.Count)
                map[side + "Toes"] = leg[k + 1];
        }
        if (!map.ContainsKey("LeftHand") || !map.ContainsKey("RightHand") || !map.ContainsKey("LeftFoot") || !map.ContainsKey("RightFoot"))
            return null;

        var human = map.Select(p => new HumanBone { boneName = p.Value.name, humanName = HumanName(p.Key), limit = new HumanLimit { useDefaultValues = true } }).ToArray();
        var skeleton = root.GetComponentsInChildren<Transform>(true)
            .Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
        return new HumanDescription
        {
            human = human,
            skeleton = skeleton,
            upperArmTwist = 0.5f,
            lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f,
            lowerLegTwist = 0.5f,
            armStretch = 0.05f,
            legStretch = 0.05f,
            feetSpacing = 0f,
            hasTranslationDoF = false,
        };
    }

    private static string HumanName(string key)
    {
        foreach (string name in HumanTrait.BoneName)
            if (name.Replace(" ", "") == key)
                return name;
        return key;
    }

    private static int[] SidePair(List<List<Transform>> pair, Transform root)
    {
        int a = SideOf(pair[0]), b = SideOf(pair[1]);
        if (a != 0 && b == 0)
            b = -a;
        else if (b != 0 && a == 0)
            a = -b;
        if (a == b)
        {
            float xa = root.InverseTransformPoint(pair[0][0].position).x;
            float xb = root.InverseTransformPoint(pair[1][0].position).x;
            a = xa < xb ? -1 : 1;
            b = -a;
        }
        return new[] { a, b };
    }

    private static int SideOf(List<Transform> chain)
    {
        foreach (Transform t in chain)
        {
            string n = t.name;
            string l = n.ToLowerInvariant();
            if (l.Contains("left"))
                return -1;
            if (l.Contains("right"))
                return 1;
            if (Regex.IsMatch(n, @"(^|[._\-: ])[Ll]($|[._\-: \d])"))
                return -1;
            if (Regex.IsMatch(n, @"(^|[._\-: ])[Rr]($|[._\-: \d])"))
                return 1;
        }
        return 0;
    }

    private static bool Is(Transform t, params string[] words)
    {
        string n = t.name.ToLowerInvariant();
        return words.Any(n.Contains);
    }

    private static Transform Common(Transform a, Transform b)
    {
        for (Transform p = a; p != null; p = p.parent)
            if (b.IsChildOf(p))
                return p;
        return null;
    }

    private static List<Transform> Between(Transform from, Transform to)
    {
        var list = new List<Transform>();
        for (Transform p = to.parent; p != null && p != from; p = p.parent)
            list.Insert(0, p);
        return list;
    }

    private static List<Transform> Chain(Transform start)
    {
        var chain = new List<Transform>();
        for (Transform t = start; t != null; t = t.Cast<Transform>().OrderByDescending(Depth).FirstOrDefault())
            chain.Add(t);
        return chain;
    }

    private static int Depth(Transform t) => t.childCount == 0 ? 0 : 1 + t.Cast<Transform>().Max(Depth);

    private static List<string> Apply(Scene scene)
    {
        var log = new List<string>();
        Physics.SyncTransforms();
        int placed = 0;
        foreach (Role cast in Casting)
        {
            List<Transform> targets = Targets(scene, cast);
            if (targets.Count == 0)
                continue;
            string path = ModelPath(cast);
            if (path == null)
            {
                log.Add(cast.name + ": model missing, export it from MakeHuman into " + Folder + "/" + cast.name + ".");
                continue;
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Transform target in targets)
            {
                Place(target, asset, cast);
                placed++;
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        log.Insert(0, placed + " character(s) placed in " + scene.name + ".");
        log.Add("If someone faces the wrong way, turn its \"" + ModelChild + "\" child by 180 on Y.");
        return log;
    }

    private static List<Transform> Targets(Scene scene, Role cast)
    {
        string id = cast.name.ToLowerInvariant();
        var result = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (Inside(t))
                    continue;
                DialogueSpeaker speaker = t.GetComponent<DialogueSpeaker>();
                if (t.name == cast.name || speaker != null && speaker.SpeakerId == id)
                    if (!result.Contains(t))
                        result.Add(t);
            }
        return result;
    }

    private static bool Inside(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.name == ModelChild)
                return true;
        return false;
    }

    private static void Place(Transform target, GameObject asset, Role cast)
    {
        Transform old = target.Find(ModelChild);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        List<MeshRenderer> stand = target.GetComponentsInChildren<MeshRenderer>(true).Where(r => !Inside(r.transform)).ToList();
        Bounds goal = new Bounds(target.position + Vector3.up * 0.875f, new Vector3(0.5f, 1.75f, 0.5f));
        if (stand.Count > 0)
        {
            goal = MeshBounds(stand[0]);
            foreach (MeshRenderer r in stand)
                goal.Encapsulate(MeshBounds(r));
        }
        float fallback = Mathf.Clamp(goal.size.y, 1.62f, 1.8f);
        float floor = Floor(target, goal);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, target);
        instance.name = ModelChild;
        Transform t = instance.transform;
        t.localPosition = Vector3.zero;

        Avatars.TryGetValue(cast.name, out Avatar avatar);
        Face(t, target, avatar);

        Measure(instance, out Bounds natural);
        if (natural.size.y < 1.4f || natural.size.y > 2.1f)
            t.localScale *= fallback / Mathf.Max(natural.size.y, 0.01f);
        Measure(instance, out Bounds b);
        t.position += new Vector3(target.position.x - b.center.x, floor - b.min.y, target.position.z - b.center.z);

        foreach (Animator other in target.GetComponentsInChildren<Animator>(true))
            if (!Inside(other.transform))
                Object.DestroyImmediate(other);

        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = instance.AddComponent<Animator>();
        if (avatar != null)
            animator.avatar = avatar;
        Controllers.TryGetValue(cast.name, out RuntimeAnimatorController controller);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = target.gameObject.layer;
        foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.updateWhenOffscreen = true;
        Materials(instance, cast);
        PeriodClothes.Dress(instance, cast.name);
        foreach (MeshRenderer r in stand)
            r.enabled = false;

        DialogueSpeaker speaker = target.GetComponent<DialogueSpeaker>();
        if (speaker != null)
        {
            var so = new SerializedObject(speaker);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("idleState").stringValue = "Idle";
            so.FindProperty("talkingState").stringValue = "Talking";
            so.FindProperty("waitingState").stringValue = "Waiting";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        LookAtPlayer look = target.GetComponent<LookAtPlayer>();
        if (look != null && avatar != null)
        {
            var so = new SerializedObject(look);
            so.FindProperty("head").objectReferenceValue = Bone(t, avatar, "Head");
            so.FindProperty("neck").objectReferenceValue = Bone(t, avatar, "Neck");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void Materials(GameObject instance, Role role)
    {
        EnsureFolder(MaterialFolder);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            return;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material original = mats[i];
                if (original == null)
                    continue;
                string name = original.name.Replace(" (Instance)", "");
                string lower = (name + " " + r.name).ToLowerInvariant();
                bool tinted = role.clothes.a > 0f && !Body.Any(lower.Contains);
                string file = (tinted ? role.name + " " : "") + Clean(name);
                string path = MaterialFolder + "/" + file + ".mat";
                Material made = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (made == null)
                {
                    made = new Material(lit) { name = file };
                    AssetDatabase.CreateAsset(made, path);
                }
                Texture texture = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : null;
                if (texture == null && original.HasProperty("_MainTex"))
                    texture = original.GetTexture("_MainTex");
                Color color = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.HasProperty("_Color") ? original.color : Color.white;
                color.a = 1f;
                if (texture != null || color.maxColorComponent < 0.02f)
                    color = Color.white;
                if (tinted)
                    color = role.clothes;
                made.SetTexture("_BaseMap", texture);
                made.SetColor("_BaseColor", color);
                made.SetFloat("_Smoothness", 0.12f);
                made.SetFloat("_Metallic", 0f);
                bool cut = Cutout.Any(lower.Contains);
                made.SetFloat("_AlphaClip", cut ? 1f : 0f);
                made.SetFloat("_Cutoff", 0.45f);
                made.SetFloat("_Cull", cut ? 0f : 2f);
                if (cut)
                    made.EnableKeyword("_ALPHATEST_ON");
                else
                    made.DisableKeyword("_ALPHATEST_ON");
                made.renderQueue = cut ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
                EditorUtility.SetDirty(made);
                mats[i] = made;
            }
            r.sharedMaterials = mats;
        }
    }

    private static string Clean(string name) => Regex.Replace(name, @"[^\w\- ]+", "_").Trim();

    private static void Face(Transform t, Transform target, Avatar avatar)
    {
        if (avatar == null)
            return;
        Transform left = Bone(t, avatar, "LeftUpperArm");
        Transform right = Bone(t, avatar, "RightUpperArm");
        if (left == null || right == null)
            return;
        Vector3 side = right.position - left.position;
        side.y = 0f;
        if (side.sqrMagnitude < 1e-6f)
            return;
        Vector3 forward = Vector3.Cross(side.normalized, Vector3.up);
        Vector3 want = target.forward;
        want.y = 0f;
        if (want.sqrMagnitude < 1e-6f)
            return;
        float yaw = Vector3.SignedAngle(forward, want.normalized, Vector3.up);
        t.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * t.rotation;
    }

    private static Transform Bone(Transform root, Avatar avatar, string human)
    {
        foreach (HumanBone bone in avatar.humanDescription.human)
            if (bone.humanName.Replace(" ", "") == human)
                return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == bone.boneName);
        return null;
    }

    private static float Floor(Transform target, Bounds goal)
    {
        Vector3 from = new Vector3(target.position.x, goal.center.y, target.position.z);
        float best = goal.min.y;
        float bestGap = 0.5f;
        foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, goal.extents.y + 1.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(target))
                continue;
            float gap = Mathf.Abs(hit.point.y - goal.min.y);
            if (gap < bestGap)
            {
                bestGap = gap;
                best = hit.point.y;
            }
        }
        return best;
    }

    private static Bounds MeshBounds(MeshRenderer r)
    {
        MeshFilter filter = r.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return r.bounds;
        Bounds local = filter.sharedMesh.bounds;
        Matrix4x4 m = r.transform.localToWorldMatrix;
        var result = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            result.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return result;
    }

    private static bool Measure(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Bounds b = r is SkinnedMeshRenderer skin ? SkinBounds(skin) : r.bounds;
            if (!any)
            {
                bounds = b;
                any = true;
            }
            else
            {
                bounds.Encapsulate(b);
            }
        }
        return any;
    }

    private static Bounds SkinBounds(SkinnedMeshRenderer skin)
    {
        if (skin.sharedMesh == null)
            return skin.bounds;
        var baked = new Mesh();
        skin.BakeMesh(baked, true);
        Vector3[] vertices = baked.vertices;
        Object.DestroyImmediate(baked);
        if (vertices.Length == 0)
            return skin.bounds;
        Matrix4x4 m = skin.transform.localToWorldMatrix;
        var result = new Bounds(m.MultiplyPoint3x4(vertices[0]), Vector3.zero);
        foreach (Vector3 v in vertices)
            result.Encapsulate(m.MultiplyPoint3x4(v));
        return result;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
