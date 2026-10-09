using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.Editor.Diagnostics
{
    public sealed class ZFightingCandidateAuditorTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private Scene scene;

        [SetUp]
        public void SetUp()
        {
            // SceneManager.CreateScene is play-mode only in Unity 6.  These are
            // EditMode tests, so create the disposable test scene through the
            // editor API instead.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                    Object.DestroyImmediate(createdObjects[index]);
            }

            createdObjects.Clear();
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void AuditScene_FlagsOffsetCoplanarRenderers()
        {
            CreateRenderer("Floor A", CreateHorizontalQuad(), Vector3.zero);
            CreateRenderer("Floor B", CreateHorizontalQuad(), new Vector3(0f, 0.005f, 0f));

            ZFightingCandidateAuditor.SceneAudit audit =
                ZFightingCandidateAuditor.AuditScene(scene);

            Assert.That(audit.Candidates, Has.Count.EqualTo(1));
            Assert.That(audit.Candidates[0].ExactDuplicate, Is.False);
            Assert.That(audit.Candidates[0].Internal, Is.False);
            Assert.That(audit.Candidates[0].Evidence.HasWitnesses, Is.True);
        }

        [Test]
        public void AuditScene_FlagsExactDuplicateMeshAndTransform()
        {
            Mesh mesh = CreateHorizontalQuad();
            CreateRenderer("First copy", mesh, Vector3.zero);
            CreateRenderer("Second copy", mesh, Vector3.zero);

            ZFightingCandidateAuditor.SceneAudit audit =
                ZFightingCandidateAuditor.AuditScene(scene);

            Assert.That(audit.Candidates, Has.Count.EqualTo(1));
            Assert.That(audit.Candidates[0].ExactDuplicate, Is.True);
        }

        [Test]
        public void AuditScene_DoesNotFlagPerpendicularTrianglesThatOnlyShareBounds()
        {
            CreateRenderer("Floor", CreateHorizontalQuad(), Vector3.zero);
            CreateRenderer("Wall", CreateVerticalQuad(), Vector3.zero);

            ZFightingCandidateAuditor.SceneAudit audit =
                ZFightingCandidateAuditor.AuditScene(scene);

            Assert.That(audit.Candidates, Is.Empty);
        }

        [Test]
        public void AuditScene_OmitsOpposingSingleSidedFacesAcrossAGap()
        {
            CreateRenderer("Floor top", CreateHorizontalQuad(), Vector3.zero);
            CreateRenderer(
                "Prop underside",
                CreateHorizontalQuad(reversed: true),
                new Vector3(0f, 0.005f, 0f));

            ZFightingCandidateAuditor.SceneAudit audit =
                ZFightingCandidateAuditor.AuditScene(scene);

            Assert.That(audit.Candidates, Is.Empty);
        }

        private void CreateRenderer(string name, Mesh mesh, Vector3 position)
        {
            var gameObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            gameObject.transform.position = position;
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            createdObjects.Add(gameObject);
        }

        private Mesh CreateHorizontalQuad(bool reversed = false)
        {
            var mesh = new Mesh
            {
                name = "Test horizontal quad",
                vertices = new[]
                {
                    new Vector3(-1f, 0f, -1f),
                    new Vector3(1f, 0f, -1f),
                    new Vector3(1f, 0f, 1f),
                    new Vector3(-1f, 0f, 1f)
                },
                triangles = reversed
                    ? new[] { 2, 1, 0, 3, 2, 0 }
                    : new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.RecalculateBounds();
            createdObjects.Add(mesh);
            return mesh;
        }

        private Mesh CreateVerticalQuad()
        {
            var mesh = new Mesh
            {
                name = "Test vertical quad",
                vertices = new[]
                {
                    new Vector3(0f, -1f, -1f),
                    new Vector3(0f, 1f, -1f),
                    new Vector3(0f, 1f, 1f),
                    new Vector3(0f, -1f, 1f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.RecalculateBounds();
            createdObjects.Add(mesh);
            return mesh;
        }
    }
}
