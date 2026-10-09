using System;
using System.Collections.Generic;
using Hortensia.Narrative;
using Hortensia.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.Editor.Validation
{
    public sealed class ValidationInfrastructureTests
    {
        [Test]
        public void ValidationReport_PreservesInsertionOrderAndFormatting()
        {
            var report = new ValidationReport();
            report.Add("first");
            report.WithPrefix("Catalog: ").Add("second");

            bool valid = report.TryFormat("Failed:", true, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo("Failed:\n- first\n- Catalog: second"));
            Assert.That(report.Diagnostics[0].Order, Is.EqualTo(0));
            Assert.That(report.Diagnostics[1].Order, Is.EqualTo(1));
        }

        [Test]
        public void BuildSceneIndex_ReportsDuplicateEnabledNamesDeterministically()
        {
            var report = new ValidationReport();
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Manor.unity", true),
                new EditorBuildSettingsScene("Assets/Alternate/Manor.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Disabled.unity", false)
            };

            BuildSceneIndex index = BuildSceneIndex.Create(scenes, report);

            Assert.That(index.EnabledScenePaths.Count, Is.EqualTo(2));
            Assert.That(index.TryGetPath("Manor", out string path), Is.True);
            Assert.That(path, Is.EqualTo("Assets/Scenes/Manor.unity"));
            Assert.That(report.Count, Is.EqualTo(1));
            Assert.That(
                report,
                Does.Contain("Build Settings contains more than one enabled scene named 'Manor'."));
        }

        [Test]
        public void SceneInspectionCache_ReusesSnapshotAndReportsFailureOnce()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var index = BuildSceneIndex.CreateForTests(
                    new[]
                    {
                        new KeyValuePair<string, string>("TestScene", "Assets/TestScene.unity")
                    });
                int openCount = 0;
                var report = new ValidationReport();
                using (var cache = new SceneInspectionCache(
                           index,
                           _ =>
                           {
                               openCount++;
                               return SceneSnapshot.Capture(scene, false);
                           }))
                {
                    Assert.That(
                        cache.TryGet(
                            "TestScene",
                            SceneDiagnosticKind.Catalog,
                            report,
                            out SceneSnapshot first),
                        Is.True);
                    Assert.That(
                        cache.TryGet(
                            "TestScene",
                            SceneDiagnosticKind.Gameplay,
                            report,
                            out SceneSnapshot second),
                        Is.True);
                    Assert.That(second, Is.SameAs(first));

                    Assert.That(
                        cache.TryGet(
                            "Missing",
                            SceneDiagnosticKind.Catalog,
                            report,
                            out _),
                        Is.False);
                    Assert.That(
                        cache.TryGet(
                            "Missing",
                            SceneDiagnosticKind.Gameplay,
                            report,
                            out _),
                        Is.False);
                }

                Assert.That(openCount, Is.EqualTo(1));
                Assert.That(report.Count, Is.EqualTo(1));
                Assert.That(
                    report,
                    Does.Contain("Scene 'Missing' is not an enabled Build Settings scene."));
                Assert.That(scene.isLoaded, Is.True, "A preloaded scene must not be closed.");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void SceneInspectionCache_ClosesOnlyOwnedSnapshots()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var index = BuildSceneIndex.CreateForTests(
                new[]
                {
                    new KeyValuePair<string, string>("Owned", "Assets/Owned.unity")
                });
            var cache = new SceneInspectionCache(
                index,
                _ => SceneSnapshot.Capture(scene, true));
            try
            {
                Assert.That(
                    cache.TryGet(
                        "Owned",
                        SceneDiagnosticKind.Catalog,
                        new ValidationReport(),
                        out _),
                    Is.True);

                cache.Dispose();

                Assert.That(scene.isLoaded, Is.False);
            }
            finally
            {
                cache.Dispose();
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void SceneSnapshot_CentralizesDressingReachabilityAndColliderEligibility()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var dressingOwner = new GameObject("Dressing");
                SceneManager.MoveGameObjectToScene(dressingOwner, scene);
                ChapterDressing dressing = dressingOwner.AddComponent<ChapterDressing>();
                var chapterRoot = new GameObject("Chapter II Root");
                SceneManager.MoveGameObjectToScene(chapterRoot, scene);
                chapterRoot.SetActive(false);

                var serialized = new SerializedObject(dressing);
                SerializedProperty groups = serialized.FindProperty("groups");
                groups.arraySize = 1;
                SerializedProperty group = groups.GetArrayElementAtIndex(0);
                group.FindPropertyRelative("label").stringValue = "Chapter II";
                group.FindPropertyRelative("root").objectReferenceValue = chapterRoot;
                group.FindPropertyRelative("activeInChapters").intValue =
                    (int)ChapterMask.ChapterII;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var child = new GameObject("Reachable Child");
                child.transform.SetParent(chapterRoot.transform, false);
                SpawnPoint chapterSpawn = child.AddComponent<SpawnPoint>();

                var inactiveRoot = new GameObject("Inactive Non-Dressing Root");
                SceneManager.MoveGameObjectToScene(inactiveRoot, scene);
                SpawnPoint inactiveSpawn = inactiveRoot.AddComponent<SpawnPoint>();
                inactiveRoot.SetActive(false);

                var disabledDressingOwner = new GameObject("Disabled Dressing");
                SceneManager.MoveGameObjectToScene(disabledDressingOwner, scene);
                ChapterDressing disabledDressing =
                    disabledDressingOwner.AddComponent<ChapterDressing>();
                var disabledDressingRoot = new GameObject("Disabled Dressing Root");
                SceneManager.MoveGameObjectToScene(disabledDressingRoot, scene);
                disabledDressingRoot.SetActive(false);
                var disabledDressingChild = new GameObject("Disabled Dressing Child");
                disabledDressingChild.transform.SetParent(
                    disabledDressingRoot.transform,
                    false);
                SpawnPoint disabledDressingSpawn =
                    disabledDressingChild.AddComponent<SpawnPoint>();
                ConfigureDressingGroup(disabledDressing, disabledDressingRoot);
                disabledDressing.enabled = false;

                var inactiveControllerParent = new GameObject("Inactive Controller Parent");
                SceneManager.MoveGameObjectToScene(inactiveControllerParent, scene);
                inactiveControllerParent.SetActive(false);
                var inactiveDressingOwner = new GameObject("Inactive Dressing Controller");
                inactiveDressingOwner.transform.SetParent(
                    inactiveControllerParent.transform,
                    false);
                ChapterDressing inactiveDressing =
                    inactiveDressingOwner.AddComponent<ChapterDressing>();
                var inactiveDressingRoot = new GameObject("Inactive Dressing Root");
                SceneManager.MoveGameObjectToScene(inactiveDressingRoot, scene);
                inactiveDressingRoot.SetActive(false);
                var inactiveDressingChild = new GameObject("Inactive Dressing Child");
                inactiveDressingChild.transform.SetParent(
                    inactiveDressingRoot.transform,
                    false);
                SpawnPoint inactiveDressingSpawn =
                    inactiveDressingChild.AddComponent<SpawnPoint>();
                ConfigureDressingGroup(inactiveDressing, inactiveDressingRoot);

                var interactableObject = new GameObject("Document");
                SceneManager.MoveGameObjectToScene(interactableObject, scene);
                DocumentInteractable interactable =
                    interactableObject.AddComponent<DocumentInteractable>();
                BoxCollider collider = interactableObject.AddComponent<BoxCollider>();
                DocumentDefinition document =
                    ScriptableObject.CreateInstance<DocumentDefinition>();
                var interactableSerialized = new SerializedObject(interactable);
                interactableSerialized.FindProperty("document").objectReferenceValue = document;
                interactableSerialized.ApplyModifiedPropertiesWithoutUndo();

                try
                {
                    using (SceneSnapshot snapshot = SceneSnapshot.Capture(scene, false))
                    {
                        Assert.That(snapshot.IsReachableInChapter(child.transform, 1), Is.False);
                        Assert.That(snapshot.IsReachableInChapter(child.transform, 2), Is.True);
                        Assert.That(
                            snapshot.WouldRegisterInChapter(inactiveSpawn, 2),
                            Is.False,
                            "An inactive non-dressing service would never receive OnEnable.");
                        Assert.That(
                            snapshot.WouldRegisterInChapter(chapterSpawn, 2),
                            Is.True,
                            "A dressing root checked in inactive must register in its authored chapter.");
                        Assert.That(
                            snapshot.WouldRegisterInChapter(chapterSpawn, 1),
                            Is.False,
                            "A dressing-owned service must not register in a mismatched chapter.");
                        Assert.That(
                            snapshot.WouldRegisterInChapter(disabledDressingSpawn, 2),
                            Is.False,
                            "A disabled ChapterDressing cannot activate its authored root.");
                        Assert.That(
                            snapshot.WouldRegisterInChapter(inactiveDressingSpawn, 2),
                            Is.False,
                            "A ChapterDressing beneath inactive hierarchy never reaches Start.");
                        Assert.That(snapshot.CountSpawns("default", 2), Is.EqualTo(1));
                        Assert.That(snapshot.CountSpawns("default", 1), Is.Zero);
                        Assert.That(
                            snapshot.HasUsableInteractionCollider(interactable, 1),
                            Is.True);
                        Assert.That(snapshot.HasReachableDocument(document, 1), Is.True);

                        interactable.enabled = false;
                        Assert.That(
                            snapshot.HasReachableDocument(document, 1),
                            Is.False,
                            "A disabled DocumentInteractable never registers from OnEnable.");
                        interactable.enabled = true;

                        collider.isTrigger = true;
                        Assert.That(
                            snapshot.HasUsableInteractionCollider(interactable, 1),
                            Is.False);
                        Assert.That(snapshot.HasReachableDocument(document, 1), Is.False);
                        Assert.That(
                            SceneSnapshot.HierarchyPath(child.transform),
                            Is.EqualTo("Chapter II Root/Reachable Child"));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(document);
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ConfigureDressingGroup(
            ChapterDressing dressing,
            GameObject root)
        {
            var serialized = new SerializedObject(dressing);
            SerializedProperty groups = serialized.FindProperty("groups");
            groups.arraySize = 1;
            SerializedProperty group = groups.GetArrayElementAtIndex(0);
            group.FindPropertyRelative("label").stringValue = "Chapter II";
            group.FindPropertyRelative("root").objectReferenceValue = root;
            group.FindPropertyRelative("activeInChapters").intValue =
                (int)ChapterMask.ChapterII;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
