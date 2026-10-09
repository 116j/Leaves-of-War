using System;
using System.Collections.Generic;
using System.Linq;
using Hortensia.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TherapyPatientModelTests
{
    private const string ScenePath = "Assets/Scenes/TherapyOffice.unity";
    private const string ModelRoot = "Assets/Art/Characters/Patients/Models/";
    private const string MaterialRoot = "Assets/Art/Characters/Patients/Materials/";
    private const string TextureRoot = "Assets/Art/Characters/Patients/Textures/";

    private static readonly IReadOnlyDictionary<string, string> ExpectedMappings =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Padgett"] = "GA_M5",
            ["Halstead"] = "GA_M1",
            ["Lockhart"] = "GA_M3",
            ["Hensleigh"] = "GA_F1",
            ["Norbury"] = "GA_M1",
            ["Grimsby"] = "GA_M8",
            ["Goodhew"] = "GA_F7",
            ["Ellingham"] = "GA_M2",
            ["Dorset"] = "GA_F3",
            ["Denby"] = "GA_M4",
            ["Bramley"] = "GA_M8",
            ["Blakeley"] = "GA_F2",
            ["Ashcombe"] = "GA_F5",
            ["Pemberton"] = "GA_F1",
            ["Otway"] = "GA_F6"
        };

    [Test]
    public void TherapyOffice_UsesApprovedPatientModelMappings()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool closeWhenDone = !scene.IsValid() || !scene.isLoaded;
        if (closeWhenDone)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            TherapyPatientPresenter presenter = scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<TherapyPatientPresenter>(true))
                .Single();
            Dictionary<string, string> actual = presenter.Bindings.ToDictionary(
                binding => binding.Patient.name,
                binding => binding.Model.name,
                StringComparer.Ordinal);

            Assert.That(actual, Is.EquivalentTo(ExpectedMappings));
            Assert.That(
                presenter.transform.position,
                Is.EqualTo(new Vector3(-1.3f, 0f, 0f)));
            Assert.That(presenter.PatientAnchor, Is.SameAs(presenter.transform));
        }
        finally
        {
            if (closeWhenDone && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void MappedPatientModels_UseRetroMaterialsAndProductionImportSettings()
    {
        foreach (string modelName in ExpectedMappings.Values.Distinct())
        {
            string path = ModelRoot + modelName + ".fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(model, Is.Not.Null, path);
            Assert.That(AssetDatabase.GetLabels(model), Does.Contain("HortensiaCharacter"));

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.importAnimation, Is.False, path);
            Assert.That(importer.importBlendShapes, Is.False, path);
            Assert.That(importer.importCameras, Is.False, path);
            Assert.That(importer.importLights, Is.False, path);

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(4), path);
            foreach (Material material in renderers.SelectMany(
                         renderer => renderer.sharedMaterials))
            {
                Assert.That(material, Is.Not.Null, path);
                Assert.That(material.shader.name, Is.EqualTo("Hortensia/Retro Lit"));
                Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith(MaterialRoot));
            }
        }
    }

    [Test]
    public void PatientTextures_ArePointFilteredAndDownsampledForRetroPresentation()
    {
        string[] textureGuids = AssetDatabase.FindAssets(
            "t:Texture2D",
            new[] { TextureRoot.TrimEnd('/') });
        Assert.That(textureGuids, Has.Length.EqualTo(16));

        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
            Assert.That(importer.maxTextureSize, Is.EqualTo(128), path);
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed),
                path);
        }
    }
}
