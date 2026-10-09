using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class DevelopmentBuildCommandTests
{
    [Test]
    public void CheckedInBuildSettings_HaveCanonicalEnabledSceneOrder()
    {
        Assert.That(
            DevelopmentBuildCommand.TryGetCanonicalEnabledScenes(
                out string[] scenes,
                out string error),
            Is.True,
            error);
        Assert.That(scenes, Has.Length.EqualTo(8));
        Assert.That(scenes[0], Is.EqualTo("Assets/Scenes/Boot.unity"));
        Assert.That(scenes[5], Is.EqualTo("Assets/Scenes/TherapyOffice.unity"));
        Assert.That(scenes[7], Is.EqualTo("Assets/Scenes/DreamGreenhouse.unity"));
    }

    [Test]
    public void NormalizeBuildPath_ResolvesRelativePathInsideUnityProject()
    {
        string expected = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Builds", "Test-WebGL"));

        string actual = DevelopmentBuildCommand.NormalizeBuildPath("Builds/Test-WebGL");

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void ProjectSerializationSnapshot_RestoresExistingAndRemovesNewFiles()
    {
        string root = CreateTemporaryDirectory();
        const string existingRelativePath = "ProjectSettings/ProjectSettings.asset";
        const string newRelativePath = "Assets/Settings/PC_RPAsset.asset";
        string existingPath = Path.Combine(root, existingRelativePath);
        string newPath = Path.Combine(root, newRelativePath);
        byte[] original = { 0, 1, 2, 3, 255 };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(existingPath));
            File.WriteAllBytes(existingPath, original);
            var snapshot = new DevelopmentBuildCommand.ProjectSerializationSnapshot(
                root,
                new[] { existingRelativePath, newRelativePath });

            File.WriteAllText(existingPath, "serialized incidentally");
            Directory.CreateDirectory(Path.GetDirectoryName(newPath));
            File.WriteAllText(newPath, "new incidental file");

            snapshot.Restore();

            Assert.That(File.ReadAllBytes(existingPath), Is.EqualTo(original));
            Assert.That(File.Exists(newPath), Is.False);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void RemoveNewDataDirectory_OnlyRemovesDirectoryAbsentBeforeBuild()
    {
        string root = CreateTemporaryDirectory();
        string dataPath = Path.Combine(root, "Data");
        try
        {
            Directory.CreateDirectory(dataPath);
            File.WriteAllText(Path.Combine(dataPath, "incidental.txt"), "generated");

            DevelopmentBuildCommand.RemoveNewDataDirectory(root, false);

            Assert.That(Directory.Exists(dataPath), Is.False);

            Directory.CreateDirectory(dataPath);
            File.WriteAllText(Path.Combine(dataPath, "owned.txt"), "preserve");

            DevelopmentBuildCommand.RemoveNewDataDirectory(root, true);

            Assert.That(Directory.Exists(dataPath), Is.True);
            Assert.That(File.Exists(Path.Combine(dataPath, "owned.txt")), Is.True);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "hortensia-development-build-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
