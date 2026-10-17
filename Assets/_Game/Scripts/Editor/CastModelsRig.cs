using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class CastModels
{
    private const string RigSuffix = " (Unity rig)";

    private sealed class RigBone
    {
        public readonly string human;
        public readonly string parent;
        public readonly string[] at;
        public readonly string[] skin;
        public Transform made;

        public RigBone(string human, string parent, string[] at, params string[] skin)
        {
            this.human = human;
            this.parent = parent;
            this.at = at;
            this.skin = skin;
        }
    }

    private static List<RigBone> ArpBones()
    {
        var bones = new List<RigBone>
        {
            new RigBone("Hips", null, new[] { "root.x" }, "c_root_bend.x", "root.x", "c_root.x", "c_root_master.x"),
            new RigBone("Spine", "Hips", new[] { "spine_01.x" }, "c_spine_01_bend.x", "spine_01.x"),
            new RigBone("Chest", "Spine", new[] { "spine_02.x" }, "c_spine_02_bend.x", "spine_02.x"),
            new RigBone("UpperChest", "Chest", new[] { "spine_03.x" }, "c_spine_03_bend.x", "spine_03.x"),
            new RigBone("Neck", "UpperChest", new[] { "neck.x" }, "neck.x", "c_neck.x", "neck_twist.x"),
            new RigBone("Head", "Neck", new[] { "head.x", "c_head.x" }, "head.x", "c_head.x"),
        };
        foreach (var (side, x) in new[] { ("Left", "l"), ("Right", "r") })
        {
            bones.Add(new RigBone(side + "Shoulder", "UpperChest", new[] { "shoulder." + x }, "shoulder." + x, "c_shoulder." + x));
            bones.Add(new RigBone(side + "UpperArm", side + "Shoulder", new[] { "arm_fk." + x, "arm." + x, "c_arm_stretch." + x },
                "c_arm_twist." + x, "c_arm_stretch." + x, "arm_twist." + x, "arm_stretch." + x, "arm." + x, "c_shoulder_bend." + x, "c_arm_bend." + x));
            bones.Add(new RigBone(side + "LowerArm", side + "UpperArm", new[] { "forearm_fk." + x, "forearm." + x, "c_forearm_stretch." + x },
                "c_forearm_twist." + x, "c_forearm_stretch." + x, "forearm_twist." + x, "forearm_stretch." + x, "c_forearm_bend." + x));
            bones.Add(new RigBone(side + "Hand", side + "LowerArm", new[] { "hand." + x }, "hand." + x, "c_wrist_bend." + x, "hand_rot_twist." + x));
            foreach (var (finger, arp) in new[] { ("Thumb", "thumb"), ("Index", "index"), ("Middle", "middle"), ("Ring", "ring"), ("Little", "pinky") })
            {
                bones.Add(new RigBone(side + finger + "Proximal", side + "Hand", new[] { arp + "1." + x, "c_" + arp + "1." + x }, arp + "1." + x, "c_" + arp + "1." + x));
                bones.Add(new RigBone(side + finger + "Intermediate", side + finger + "Proximal", new[] { "c_" + arp + "2." + x }, "c_" + arp + "2." + x));
                bones.Add(new RigBone(side + finger + "Distal", side + finger + "Intermediate", new[] { "c_" + arp + "3." + x }, "c_" + arp + "3." + x));
            }
            bones.Add(new RigBone(side + "UpperLeg", "Hips", new[] { "thigh_fk." + x, "thigh." + x, "c_thigh_stretch." + x },
                "c_thigh_twist." + x, "c_thigh_stretch." + x, "thigh_twist." + x, "thigh_stretch." + x, "c_thigh_b." + x, "c_thigh_bend_01." + x, "c_thigh_bend_02." + x));
            bones.Add(new RigBone(side + "LowerLeg", side + "UpperLeg", new[] { "leg_fk." + x, "leg." + x, "c_leg_stretch." + x },
                "c_leg_twist." + x, "c_leg_stretch." + x, "leg_twist." + x, "leg_stretch." + x, "c_leg_bend_01." + x, "c_leg_bend_02." + x));
            bones.Add(new RigBone(side + "Foot", side + "LowerLeg", new[] { "foot." + x, "foot_fk." + x }, "foot." + x));
            bones.Add(new RigBone(side + "Toes", side + "Foot", new[] { "toes_01." + x, "c_toes_fk." + x }, "toes_01." + x));
        }
        return bones;
    }

    private static string RebuildRig(string fbxPath, string role, List<string> log)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (asset == null || !asset.GetComponentsInChildren<Transform>(true).Any(t => t.name == "c_traj" || t.name == "c_root_master.x"))
            return null;

        var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
        if (importer.animationType != ModelImporterAnimationType.Generic || importer.importAnimation)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.SaveAndReimport();
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        }

        string folder = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
        string meshFolder = folder + "/_Rig";
        EnsureFolder(meshFolder);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = role + RigSuffix;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        try
        {
            Transform root = go.transform;
            var all = root.GetComponentsInChildren<Transform>(true);
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in all)
                if (!byName.ContainsKey(t.name))
                    byName[t.name] = t;
            SkinnedMeshRenderer[] skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0)
                return null;

            var bind = new Dictionary<Transform, Matrix4x4>();
            foreach (SkinnedMeshRenderer skin in skins)
            {
                Matrix4x4[] poses = skin.sharedMesh.bindposes;
                for (int i = 0; i < skin.bones.Length && i < poses.Length; i++)
                    if (skin.bones[i] != null && !bind.ContainsKey(skin.bones[i]))
                        bind[skin.bones[i]] = skin.transform.localToWorldMatrix * poses[i].inverse;
            }
            Vector3 BindPosition(Transform t) => bind.TryGetValue(t, out Matrix4x4 m) ? (Vector3)m.GetColumn(3) : t.position;

            List<RigBone> bones = ArpBones();
            var skeleton = new GameObject("Skeleton").transform;
            skeleton.SetParent(root, false);
            var made = new Dictionary<string, RigBone>();
            foreach (RigBone bone in bones)
            {
                Transform source = bone.at.Select(n => byName.TryGetValue(n, out Transform t) ? t : null).FirstOrDefault(t => t != null);
                if (source == null)
                    continue;
                var t2 = new GameObject(bone.human).transform;
                t2.SetParent(bone.parent != null && made.TryGetValue(bone.parent, out RigBone parent) ? parent.made : skeleton, false);
                t2.SetPositionAndRotation(BindPosition(source), root.rotation);
                bone.made = t2;
                made[bone.human] = bone;
            }
            string[] required = { "Hips", "Spine", "Head", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot" };
            string missing = string.Join(", ", required.Where(r => !made.ContainsKey(r)));
            if (missing.Length > 0)
            {
                log.Add(role + ": the rig of " + Path.GetFileName(fbxPath) + " is missing " + missing + ".");
                return null;
            }

            var target = new Dictionary<string, Transform>();
            foreach (RigBone bone in bones)
                if (bone.made != null)
                    foreach (string name in bone.skin.Concat(bone.at))
                        if (!target.ContainsKey(name))
                            target[name] = bone.made;
            Transform hips = made["Hips"].made;
            Transform Owner(Transform old)
            {
                for (Transform t = old; t != null; t = t.parent)
                    if (target.TryGetValue(t.name, out Transform owner))
                        return owner;
                return hips;
            }

            Transform[] newBones = bones.Where(b => b.made != null).Select(b => b.made).ToArray();
            var index = new Dictionary<Transform, int>();
            for (int i = 0; i < newBones.Length; i++)
                index[newBones[i]] = i;

            foreach (SkinnedMeshRenderer skin in skins)
            {
                if (skin.transform.IsChildOf(byName.TryGetValue("rig", out Transform rigNode) ? rigNode : skeleton) && skin.transform != root)
                    skin.transform.SetParent(root, true);
                Mesh source = skin.sharedMesh;
                Mesh mesh = Object.Instantiate(source);
                mesh.name = source.name;
                int[] remap = skin.bones.Select(b => b != null ? index[Owner(b)] : index[hips]).ToArray();
                NativeArray<byte> perVertex = source.GetBonesPerVertex();
                NativeArray<BoneWeight1> weights = source.GetAllBoneWeights();
                var counts = new List<byte>(perVertex.Length);
                var merged = new List<BoneWeight1>(weights.Length);
                int w = 0;
                var sum = new Dictionary<int, float>();
                for (int v = 0; v < perVertex.Length; v++)
                {
                    sum.Clear();
                    for (int k = 0; k < perVertex[v]; k++, w++)
                    {
                        int to = weights[w].boneIndex < remap.Length ? remap[weights[w].boneIndex] : index[hips];
                        sum[to] = (sum.TryGetValue(to, out float f) ? f : 0f) + weights[w].weight;
                    }
                    var top = sum.OrderByDescending(p => p.Value).Take(4).ToList();
                    if (top.Count == 0)
                        top.Add(new KeyValuePair<int, float>(index[hips], 1f));
                    float total = top.Sum(p => p.Value);
                    counts.Add((byte)top.Count);
                    foreach (var p in top)
                        merged.Add(new BoneWeight1 { boneIndex = p.Key, weight = total > 0f ? p.Value / total : 1f / top.Count });
                }
                using (var countArray = new NativeArray<byte>(counts.ToArray(), Allocator.Temp))
                using (var weightArray = new NativeArray<BoneWeight1>(merged.ToArray(), Allocator.Temp))
                    mesh.SetBoneWeights(countArray, weightArray);
                mesh.bindposes = newBones.Select(b => b.worldToLocalMatrix * skin.transform.localToWorldMatrix).ToArray();
                string meshPath = meshFolder + "/" + Clean(skin.name) + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
                    AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);
                skin.sharedMesh = mesh;
                skin.bones = newBones;
                skin.rootBone = hips;
                skin.updateWhenOffscreen = true;
            }

            foreach (Transform t in root.Cast<Transform>().ToList())
                if (t != skeleton && t.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
                    Object.DestroyImmediate(t.gameObject);

            Avatar avatar = BuildAvatar(root, bones);
            if (avatar == null)
            {
                log.Add(role + ": Unity could not build a Humanoid avatar from the rebuilt rig.");
                return null;
            }
            string avatarPath = meshFolder + "/" + role + " Avatar.asset";
            if (AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath) != null)
                AssetDatabase.DeleteAsset(avatarPath);
            AssetDatabase.CreateAsset(avatar, avatarPath);
            foreach (Animator old in root.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(old);
            Animator animator = go.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;

            FinalMaterials(go, fbxPath);
            string prefabPath = folder + "/" + role + RigSuffix + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            log.Add(role + ": " + Path.GetFileName(fbxPath) + " has a Blender control rig (Auto-Rig Pro): rebuilt as a clean Humanoid skeleton (" + newBones.Length + " bones) in " + Path.GetFileName(prefabPath) + ".");
            return prefabPath;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static Avatar BuildAvatar(Transform root, List<RigBone> bones)
    {
        var saved = new Dictionary<Transform, Quaternion>();
        Transform Made(string human) => bones.FirstOrDefault(b => b.human == human)?.made;
        Transform leftArm = Made("LeftUpperArm"), rightArm = Made("RightUpperArm");
        Vector3 side = (rightArm.position - leftArm.position);
        side.y = 0f;
        side.Normalize();
        foreach (var (upper, lower, hand, dir) in new[] { ("LeftUpperArm", "LeftLowerArm", "LeftHand", -side), ("RightUpperArm", "RightLowerArm", "RightHand", side) })
        {
            Transform u = Made(upper), l = Made(lower), h = Made(hand);
            foreach (Transform t in new[] { u, l })
                if (!saved.ContainsKey(t))
                    saved[t] = t.localRotation;
            u.rotation = Quaternion.FromToRotation(l.position - u.position, dir) * u.rotation;
            l.rotation = Quaternion.FromToRotation(h.position - l.position, dir) * l.rotation;
        }

        var human = bones.Where(b => b.made != null).Select(b => new HumanBone
        {
            boneName = b.made.name,
            humanName = HumanTraitName(b.human),
            limit = new HumanLimit { useDefaultValues = true }
        }).Where(h => h.humanName != null).ToArray();
        var skeleton = root.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
        {
            name = t.name,
            position = t.localPosition,
            rotation = t.localRotation,
            scale = t.localScale
        }).ToArray();
        foreach (var pair in saved)
            pair.Key.localRotation = pair.Value;

        var description = new HumanDescription
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
            hasTranslationDoF = false
        };
        Avatar avatar = AvatarBuilder.BuildHumanAvatar(root.gameObject, description);
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            return null;
        avatar.name = root.name + " Avatar";
        return avatar;
    }

    private static string HumanTraitName(string compact)
    {
        foreach (string name in HumanTrait.BoneName)
            if (name.Replace(" ", "") == compact)
                return name;
        return null;
    }

    private static Avatar PrefabAvatar(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Animator animator = prefab != null ? prefab.GetComponent<Animator>() : null;
        return animator != null && Good(animator.avatar) ? animator.avatar : null;
    }
}
