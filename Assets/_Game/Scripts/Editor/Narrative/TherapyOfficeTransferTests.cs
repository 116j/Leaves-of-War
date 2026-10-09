using System;
using System.IO;
using System.Linq;
using System.Text;
using Hortensia.Narrative;
using Hortensia.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TherapyOfficeTransferTests
{
    private const string ChapterFourPath =
        "Assets/Resources/Narrative/Chapters/Chapter04.asset";
    private const string ChapterFivePath =
        "Assets/Resources/Narrative/Chapters/Chapter05.asset";
    private const string ModelPath =
        "Assets/Art/Environment/TherapyOffice/Models/TherapyOffice.fbx";
    private const string ScenePath = "Assets/Scenes/TherapyOffice.unity";

    private static readonly string[] ChapterFivePropNames =
    {
        "Cube.044",
        "Cylinder.019",
        "Cube.095",
        "Cube.073",
        "Collision — Cube.044",
        "Collision — Cylinder.019",
        "Collision — Cube.095",
        "Collision — Cube.073"
    };

    [Test]
    public void PatientChapters_BeginInTherapyOfficeAndReturnToManorAfterSessions()
    {
        ChapterDefinition chapterFour =
            AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterFourPath);
        ChapterDefinition chapterFive =
            AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterFivePath);

        AssertTransferredChapter(chapterFour, expectedTravelOffset: 2);
        AssertTransferredChapter(chapterFive, expectedTravelOffset: 1);
    }

    [Test]
    public void TherapyOfficeScene_IsInCanonicalBuildOrder()
    {
        string[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        Assert.That(enabledScenes, Has.Length.EqualTo(8));
        Assert.That(enabledScenes[4], Is.EqualTo("Assets/Scenes/Merridew.unity"));
        Assert.That(enabledScenes[5], Is.EqualTo("Assets/Scenes/TherapyOffice.unity"));
        Assert.That(enabledScenes[6], Is.EqualTo("Assets/Scenes/Lynwarre.unity"));
    }

    [Test]
    public void TherapyOfficeModel_IsVisualOnlyAndContainsNoWorkstationPaths()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.GetComponentsInChildren<MeshFilter>(true), Has.Length.EqualTo(88));
        Assert.That(AssetDatabase.GetLabels(model), Does.Contain("HortensiaVisualOnly"));

        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.importAnimation, Is.False);
        Assert.That(importer.importCameras, Is.False);
        Assert.That(importer.importLights, Is.False);

        string absolutePath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", ModelPath));
        string payload = Encoding.ASCII.GetString(File.ReadAllBytes(absolutePath));
        Assert.That(payload, Does.Not.Contain("/Users/"));
        Assert.That(payload, Does.Not.Contain("\\Users\\"));
        Assert.That(payload, Does.Not.Contain("Downloads"));
        Assert.That(payload, Does.Contain("TherapyOfficeSource.fbx"));
    }

    [Test]
    public void TherapyOffice_1915PropsAndColliders_AreChapterFiveOnly()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool closeWhenDone = !scene.IsValid() || !scene.isLoaded;
        if (closeWhenDone)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            ChapterDressing dressing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ChapterDressing>(true))
                .Single();

            Assert.That(dressing.Groups, Has.Count.EqualTo(ChapterFivePropNames.Length));
            foreach (string objectName in ChapterFivePropNames)
            {
                ChapterDressingGroup group = dressing.Groups.Single(candidate =>
                    candidate.Root != null && candidate.Root.name == objectName);
                Assert.That(group.Label, Is.EqualTo("1915 props"));
                Assert.That(group.ActiveInChapters, Is.EqualTo(ChapterMask.ChapterV));
                Assert.That(group.IsActiveIn(4), Is.False, objectName);
                Assert.That(group.IsActiveIn(5), Is.True, objectName);
            }
        }
        finally
        {
            if (closeWhenDone && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AssertTransferredChapter(
        ChapterDefinition chapter,
        int expectedTravelOffset)
    {
        Assert.That(chapter, Is.Not.Null);
        Assert.That(chapter.BaseScene, Is.EqualTo("TherapyOffice"));
        Assert.That(chapter.BaseSpawnPointId, Is.EqualTo("therapy_chair"));

        int patientIndex = Enumerable.Range(0, chapter.Beats.Count)
            .Single(index => chapter.Beats[index] is PatientSessionBeat);
        Assert.That(
            chapter.Beats[patientIndex + expectedTravelOffset],
            Is.TypeOf<TravelBeat>());
        var travel = (TravelBeat)chapter.Beats[patientIndex + expectedTravelOffset];
        Assert.That(travel.TargetScene, Is.EqualTo("Manor"));
        Assert.That(travel.SpawnPointId, Is.EqualTo("consulting_room"));
    }
}
