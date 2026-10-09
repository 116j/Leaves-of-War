using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Hortensia.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.Editor.Diagnostics
{
    /// <summary>
    /// Finds renderers with competing, nearly coplanar triangles before a
    /// playtest. This is a triage tool: a candidate is geometric evidence, not
    /// proof that the production camera visibly flickers.
    /// </summary>
    public static class ZFightingCandidateAuditor
    {
        private const string MenuPath = "Tools/Hortensia/Diagnostics/Write Z-Fighting Candidate Report";
        private const float BroadPhaseMargin = 0.015f;
        private const float PlaneDistanceTolerance = 0.0125f;
        private const float ParallelNormalThreshold = 0.995f;
        private const float MinimumTriangleArea = 0.000001f;
        private const float MinimumProjectedOverlapArea = 0.000025f;
        private const int TriangleLeafSize = 12;
        private const int MaximumTrianglePairTests = 250000;
        private const int MaximumWitnessesPerCandidate = 6;
        private const int MaximumMarkdownCandidateDetailsPerScene = 20;

        [MenuItem(MenuPath)]
        private static void RunFromMenu()
        {
            try
            {
                string reportPath = RunAndWriteReport();
                EditorUtility.DisplayDialog(
                    "Z-fighting candidate report written",
                    "The scene audit completed. Inspect the ranked candidates before " +
                    "confirming them in the production Game view.\n\n" + reportPath,
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Z-fighting candidate audit failed",
                    exception.Message,
                    "OK");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRunFromMenu() =>
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling;

        /// <summary>
        /// Command-line entry point. For example:
        /// Unity -batchmode -quit -projectPath ... -executeMethod
        /// Hortensia.Editor.Diagnostics.ZFightingCandidateAuditor.Execute
        /// </summary>
        public static void Execute()
        {
            string reportPath = RunAndWriteReport();
            Debug.Log("Z_FIGHTING_CANDIDATE_REPORT_WRITTEN " + reportPath);
        }

        public static string RunAndWriteReport()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "The z-fighting candidate audit must run outside Play Mode.");
            }

            AuditRun audit = AuditEnabledBuildScenes();
            string reportPath = GetReportPath();
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, BuildMarkdown(audit), new UTF8Encoding(false));
            File.WriteAllText(GetCsvReportPath(), BuildCsv(audit), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            return reportPath;
        }

        internal static AuditRun AuditEnabledBuildScenes()
        {
            var run = new AuditRun();
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (buildScene == null || !buildScene.enabled)
                    continue;

                if (string.IsNullOrWhiteSpace(buildScene.path) ||
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path) == null)
                {
                    run.Failures.Add(
                        "Enabled Build Settings scene could not be loaded: '" +
                        (buildScene == null ? "<null>" : buildScene.path) + "'.");
                    continue;
                }

                Scene scene = SceneManager.GetSceneByPath(buildScene.path);
                bool closeWhenDone = !scene.IsValid() || !scene.isLoaded;
                try
                {
                    if (closeWhenDone)
                    {
                        scene = EditorSceneManager.OpenScene(
                            buildScene.path,
                            OpenSceneMode.Additive);
                    }

                    run.Scenes.Add(AuditScene(scene));
                }
                catch (Exception exception)
                {
                    run.Failures.Add(
                        "Scene '" + buildScene.path + "' could not be audited: " +
                        exception.Message);
                }
                finally
                {
                    if (closeWhenDone && scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }

            return run;
        }

        internal static SceneAudit AuditScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException(
                    "The z-fighting candidate audit requires a loaded scene.",
                    nameof(scene));
            }

            var result = new SceneAudit(scene.name, scene.path);
            var dressingContext = new DressingContext(scene);
            List<RendererRecord> renderers = CollectMeshRenderers(
                scene,
                dressingContext,
                result);

            for (int i = 0; i < renderers.Count; i++)
            {
                RendererRecord renderer = renderers[i];
                if (!TryGetTriangleIndex(renderer, result, out TriangleIndex index))
                    continue;

                PairEvidence internalEvidence = FindCoplanarOverlap(
                    index,
                    index,
                    true,
                    renderer.MayRenderBackfaces);
                if (internalEvidence.HasWitnesses)
                {
                    result.Candidates.Add(new Candidate(
                        renderer,
                        renderer,
                        true,
                        false,
                        internalEvidence));
                }
                else if (internalEvidence.LimitReached)
                {
                    result.UnresolvedContacts.Add(new UnresolvedContact(
                        renderer,
                        renderer,
                        "The internal triangle comparison reached its safety limit " +
                        "before finding an overlap."));
                }
            }

            renderers.Sort(RendererRecord.CompareByMinimumX);
            for (int firstIndex = 0; firstIndex < renderers.Count; firstIndex++)
            {
                RendererRecord first = renderers[firstIndex];
                float maximumX = first.WorldBounds.max.x + BroadPhaseMargin;
                for (int secondIndex = firstIndex + 1;
                    secondIndex < renderers.Count &&
                    renderers[secondIndex].WorldBounds.min.x <= maximumX;
                    secondIndex++)
                {
                    RendererRecord second = renderers[secondIndex];
                    if (!CanCompete(first, second))
                        continue;

                    if (ReferenceEquals(first.Mesh, second.Mesh) &&
                        MatricesApproximatelyEqual(
                            first.LocalToWorld,
                            second.LocalToWorld))
                    {
                        result.Candidates.Add(new Candidate(
                            first,
                            second,
                            false,
                            true,
                            PairEvidence.ExactDuplicate));
                        continue;
                    }

                    if (!TryGetTriangleIndex(first, result, out TriangleIndex firstIndexData) ||
                        !TryGetTriangleIndex(second, result, out TriangleIndex secondIndexData))
                    {
                        result.UnresolvedContacts.Add(new UnresolvedContact(
                            first,
                            second,
                            "At least one mesh could not be read through Unity's " +
                            "read-only mesh API."));
                        continue;
                    }

                    PairEvidence evidence = FindCoplanarOverlap(
                        firstIndexData,
                        secondIndexData,
                        false,
                        first.MayRenderBackfaces || second.MayRenderBackfaces);
                    if (evidence.HasWitnesses)
                    {
                        result.Candidates.Add(new Candidate(
                            first,
                            second,
                            false,
                            false,
                            evidence));
                    }
                    else if (evidence.LimitReached)
                    {
                        result.UnresolvedContacts.Add(new UnresolvedContact(
                            first,
                            second,
                            "The triangle comparison reached its safety limit " +
                            "before finding an overlap."));
                    }
                }
            }

            result.Candidates.Sort(Candidate.CompareByRisk);
            result.UnresolvedContacts.Sort(UnresolvedContact.CompareByPaths);
            return result;
        }

        private static List<RendererRecord> CollectMeshRenderers(
            Scene scene,
            DressingContext dressingContext,
            SceneAudit result)
        {
            var records = new List<RendererRecord>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Renderer[] renderers = roots[rootIndex].GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null || !renderer.enabled)
                        continue;

                    if (!TryGetMesh(renderer, out Mesh mesh))
                        continue;

                    ChapterMask chapters = dressingContext.PotentialChaptersFor(
                        renderer.transform);
                    if (chapters == ChapterMask.None)
                        continue;

                    try
                    {
                        records.Add(new RendererRecord(
                            renderer,
                            mesh,
                            chapters,
                            CalculateWorldBounds(mesh.bounds, renderer.localToWorldMatrix)));
                    }
                    catch (Exception exception)
                    {
                        result.ReadFailures.Add(
                            "Renderer '" + GetHierarchyPath(renderer.transform) +
                            "' could not be prepared: " + exception.Message);
                    }
                }
            }

            result.RendererCount = records.Count;
            return records;
        }

        private static bool TryGetMesh(Renderer renderer, out Mesh mesh)
        {
            mesh = null;
            if (renderer is SkinnedMeshRenderer skinned)
                mesh = skinned.sharedMesh;
            else if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                mesh = filter == null ? null : filter.sharedMesh;
            }

            return mesh != null && mesh.vertexCount > 0;
        }

        private static bool CanCompete(RendererRecord first, RendererRecord second)
        {
            return (first.Chapters & second.Chapters) != ChapterMask.None &&
                BoundsAreNear(first.WorldBounds, second.WorldBounds, BroadPhaseMargin);
        }

        private static bool TryGetTriangleIndex(
            RendererRecord renderer,
            SceneAudit result,
            out TriangleIndex index)
        {
            if (renderer.TriangleIndex != null)
            {
                index = renderer.TriangleIndex;
                return true;
            }

            if (renderer.GeometryReadAttempted)
            {
                index = null;
                return false;
            }

            renderer.GeometryReadAttempted = true;
            try
            {
                Vector3[] vertices = EditorMeshReadUtility.GetVertices(renderer.Mesh);
                int[] triangles = EditorMeshReadUtility.GetTriangles(renderer.Mesh);
                var worldTriangles = new List<TriangleData>(triangles.Length / 3);
                for (int triangleIndex = 0;
                    triangleIndex + 2 < triangles.Length;
                    triangleIndex += 3)
                {
                    int first = triangles[triangleIndex];
                    int second = triangles[triangleIndex + 1];
                    int third = triangles[triangleIndex + 2];
                    if (first < 0 || second < 0 || third < 0 ||
                        first >= vertices.Length || second >= vertices.Length ||
                        third >= vertices.Length)
                    {
                        continue;
                    }

                    Vector3 a = renderer.LocalToWorld.MultiplyPoint3x4(vertices[first]);
                    Vector3 b = renderer.LocalToWorld.MultiplyPoint3x4(vertices[second]);
                    Vector3 c = renderer.LocalToWorld.MultiplyPoint3x4(vertices[third]);
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    float doubledArea = cross.magnitude;
                    if (doubledArea * 0.5f < MinimumTriangleArea)
                        continue;

                    worldTriangles.Add(new TriangleData(
                        a,
                        b,
                        c,
                        cross / doubledArea));
                }

                if (worldTriangles.Count == 0)
                {
                    result.ReadFailures.Add(
                        "Mesh '" + renderer.Mesh.name + "' on '" +
                        renderer.Path + "' contains no usable triangles.");
                    index = null;
                    return false;
                }

                renderer.TriangleIndex = new TriangleIndex(worldTriangles);
                index = renderer.TriangleIndex;
                return true;
            }
            catch (Exception exception)
            {
                result.ReadFailures.Add(
                    "Mesh '" + renderer.Mesh.name + "' on '" + renderer.Path +
                    "' could not be read: " + exception.Message);
                index = null;
                return false;
            }
        }

        private static PairEvidence FindCoplanarOverlap(
            TriangleIndex first,
            TriangleIndex second,
            bool selfComparison,
            bool includeOpposingFaces)
        {
            var evidence = new PairEvidence();
            if (selfComparison)
                SearchSelf(first, first.Root, includeOpposingFaces, evidence);
            else
            {
                SearchAcross(
                    first,
                    first.Root,
                    second,
                    second.Root,
                    includeOpposingFaces,
                    evidence);
            }
            return evidence;
        }

        private static void SearchSelf(
            TriangleIndex index,
            TriangleNode node,
            bool includeOpposingFaces,
            PairEvidence evidence)
        {
            if (evidence.ShouldStop)
                return;

            if (node.IsLeaf)
            {
                int end = node.Start + node.Count;
                for (int first = node.Start; first < end; first++)
                {
                    for (int second = first + 1; second < end; second++)
                    {
                        CompareTriangles(
                            index.Triangles[first],
                            index.Triangles[second],
                            includeOpposingFaces,
                            evidence);
                        if (evidence.ShouldStop)
                            return;
                    }
                }

                return;
            }

            SearchSelf(index, node.Left, includeOpposingFaces, evidence);
            SearchAcross(
                index,
                node.Left,
                index,
                node.Right,
                includeOpposingFaces,
                evidence);
            SearchSelf(index, node.Right, includeOpposingFaces, evidence);
        }

        private static void SearchAcross(
            TriangleIndex firstIndex,
            TriangleNode first,
            TriangleIndex secondIndex,
            TriangleNode second,
            bool includeOpposingFaces,
            PairEvidence evidence)
        {
            if (evidence.ShouldStop ||
                !BoundsAreNear(first.Bounds, second.Bounds, PlaneDistanceTolerance))
            {
                return;
            }

            if (first.IsLeaf && second.IsLeaf)
            {
                int firstEnd = first.Start + first.Count;
                int secondEnd = second.Start + second.Count;
                for (int firstTriangle = first.Start;
                    firstTriangle < firstEnd;
                    firstTriangle++)
                {
                    for (int secondTriangle = second.Start;
                        secondTriangle < secondEnd;
                        secondTriangle++)
                    {
                        CompareTriangles(
                            firstIndex.Triangles[firstTriangle],
                            secondIndex.Triangles[secondTriangle],
                            includeOpposingFaces,
                            evidence);
                        if (evidence.ShouldStop)
                            return;
                    }
                }

                return;
            }

            if (second.IsLeaf || (!first.IsLeaf && first.Volume >= second.Volume))
            {
                SearchAcross(
                    firstIndex,
                    first.Left,
                    secondIndex,
                    second,
                    includeOpposingFaces,
                    evidence);
                SearchAcross(
                    firstIndex,
                    first.Right,
                    secondIndex,
                    second,
                    includeOpposingFaces,
                    evidence);
            }
            else
            {
                SearchAcross(
                    firstIndex,
                    first,
                    secondIndex,
                    second.Left,
                    includeOpposingFaces,
                    evidence);
                SearchAcross(
                    firstIndex,
                    first,
                    secondIndex,
                    second.Right,
                    includeOpposingFaces,
                    evidence);
            }
        }

        private static void CompareTriangles(
            TriangleData first,
            TriangleData second,
            bool includeOpposingFaces,
            PairEvidence evidence)
        {
            evidence.TrianglePairTests++;
            if (evidence.TrianglePairTests > MaximumTrianglePairTests)
            {
                evidence.LimitReached = true;
                return;
            }

            float normalDot = Vector3.Dot(first.Normal, second.Normal);
            if (!BoundsAreNear(first.Bounds, second.Bounds, PlaneDistanceTolerance) ||
                (includeOpposingFaces
                    ? Mathf.Abs(normalDot) < ParallelNormalThreshold
                    : normalDot < ParallelNormalThreshold))
            {
                return;
            }

            float firstToSecond = MaximumDistanceToPlane(first, second);
            float secondToFirst = MaximumDistanceToPlane(second, first);
            float planeSeparation = Mathf.Max(firstToSecond, secondToFirst);
            if (planeSeparation > PlaneDistanceTolerance)
                return;

            float overlapArea = ProjectedOverlapArea(first, second);
            if (overlapArea < MinimumProjectedOverlapArea)
                return;

            evidence.Witnesses.Add(new TriangleWitness(
                planeSeparation,
                overlapArea,
                (first.Center + second.Center) * 0.5f));
        }

        private static float MaximumDistanceToPlane(
            TriangleData source,
            TriangleData plane)
        {
            return Mathf.Max(
                Mathf.Abs(Vector3.Dot(source.A - plane.A, plane.Normal)),
                Mathf.Abs(Vector3.Dot(source.B - plane.A, plane.Normal)),
                Mathf.Abs(Vector3.Dot(source.C - plane.A, plane.Normal)));
        }

        private static float ProjectedOverlapArea(TriangleData first, TriangleData second)
        {
            int droppedAxis = DominantAxis(first.Normal);
            var subject = new List<Vector2>(3)
            {
                Project(first.A, droppedAxis),
                Project(first.B, droppedAxis),
                Project(first.C, droppedAxis)
            };
            Vector2[] clip =
            {
                Project(second.A, droppedAxis),
                Project(second.B, droppedAxis),
                Project(second.C, droppedAxis)
            };

            bool clipIsCounterClockwise = SignedArea(clip) >= 0f;
            for (int edgeIndex = 0; edgeIndex < clip.Length; edgeIndex++)
            {
                Vector2 edgeStart = clip[edgeIndex];
                Vector2 edgeEnd = clip[(edgeIndex + 1) % clip.Length];
                subject = ClipAgainstEdge(
                    subject,
                    edgeStart,
                    edgeEnd,
                    clipIsCounterClockwise);
                if (subject.Count == 0)
                    return 0f;
            }

            return Mathf.Abs(SignedArea(subject));
        }

        private static List<Vector2> ClipAgainstEdge(
            List<Vector2> subject,
            Vector2 edgeStart,
            Vector2 edgeEnd,
            bool clipIsCounterClockwise)
        {
            var result = new List<Vector2>(subject.Count + 1);
            if (subject.Count == 0)
                return result;

            Vector2 previous = subject[subject.Count - 1];
            bool previousInside = IsInside(
                previous,
                edgeStart,
                edgeEnd,
                clipIsCounterClockwise);
            for (int index = 0; index < subject.Count; index++)
            {
                Vector2 current = subject[index];
                bool currentInside = IsInside(
                    current,
                    edgeStart,
                    edgeEnd,
                    clipIsCounterClockwise);
                if (currentInside != previousInside)
                {
                    result.Add(LineIntersection(
                        previous,
                        current,
                        edgeStart,
                        edgeEnd));
                }

                if (currentInside)
                    result.Add(current);

                previous = current;
                previousInside = currentInside;
            }

            return result;
        }

        private static bool IsInside(
            Vector2 point,
            Vector2 edgeStart,
            Vector2 edgeEnd,
            bool clipIsCounterClockwise)
        {
            float cross = Cross(edgeEnd - edgeStart, point - edgeStart);
            return clipIsCounterClockwise ? cross >= -0.000001f : cross <= 0.000001f;
        }

        private static Vector2 LineIntersection(
            Vector2 lineStart,
            Vector2 lineEnd,
            Vector2 edgeStart,
            Vector2 edgeEnd)
        {
            Vector2 lineDirection = lineEnd - lineStart;
            Vector2 edgeDirection = edgeEnd - edgeStart;
            float denominator = Cross(lineDirection, edgeDirection);
            if (Mathf.Abs(denominator) < 0.0000001f)
                return (lineStart + lineEnd) * 0.5f;

            float factor = Cross(edgeStart - lineStart, edgeDirection) / denominator;
            return lineStart + lineDirection * factor;
        }

        private static int DominantAxis(Vector3 normal)
        {
            Vector3 absolute = new Vector3(
                Mathf.Abs(normal.x),
                Mathf.Abs(normal.y),
                Mathf.Abs(normal.z));
            if (absolute.x >= absolute.y && absolute.x >= absolute.z)
                return 0;
            return absolute.y >= absolute.z ? 1 : 2;
        }

        private static Vector2 Project(Vector3 value, int droppedAxis)
        {
            switch (droppedAxis)
            {
                case 0:
                    return new Vector2(value.y, value.z);
                case 1:
                    return new Vector2(value.x, value.z);
                default:
                    return new Vector2(value.x, value.y);
            }
        }

        private static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            float doubleArea = 0f;
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 current = polygon[index];
                Vector2 next = polygon[(index + 1) % polygon.Count];
                doubleArea += current.x * next.y - next.x * current.y;
            }

            return doubleArea * 0.5f;
        }

        private static float Cross(Vector2 first, Vector2 second) =>
            first.x * second.y - first.y * second.x;

        private static Bounds CalculateWorldBounds(Bounds localBounds, Matrix4x4 localToWorld)
        {
            Vector3 center = localBounds.center;
            Vector3 extents = localBounds.extents;
            Bounds result = new Bounds(
                localToWorld.MultiplyPoint3x4(
                    center + new Vector3(-extents.x, -extents.y, -extents.z)),
                Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        result.Encapsulate(localToWorld.MultiplyPoint3x4(
                            center + Vector3.Scale(
                                extents,
                                new Vector3(x, y, z))));
                    }
                }
            }

            return result;
        }

        private static bool BoundsAreNear(Bounds first, Bounds second, float margin)
        {
            return first.min.x <= second.max.x + margin &&
                first.max.x + margin >= second.min.x &&
                first.min.y <= second.max.y + margin &&
                first.max.y + margin >= second.min.y &&
                first.min.z <= second.max.z + margin &&
                first.max.z + margin >= second.min.z;
        }

        private static bool MatricesApproximatelyEqual(
            Matrix4x4 first,
            Matrix4x4 second)
        {
            for (int index = 0; index < 16; index++)
            {
                if (Mathf.Abs(first[index] - second[index]) > 0.00001f)
                    return false;
            }

            return true;
        }

        private static string GetReportPath()
        {
            string repositoryRoot = GetRepositoryRoot();
            return Path.Combine(
                repositoryRoot,
                "docs",
                "reviews",
                "z-fighting-candidates.md");
        }

        private static string GetCsvReportPath()
        {
            return Path.Combine(
                GetRepositoryRoot(),
                "docs",
                "reviews",
                "z-fighting-candidates.csv");
        }

        private static string GetRepositoryRoot()
        {
            string unityProjectPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            return Directory.GetParent(unityProjectPath).FullName;
        }

        private static string BuildMarkdown(AuditRun audit)
        {
            int candidateCount = audit.Scenes.Sum(scene => scene.Candidates.Count);
            int unresolvedCount = audit.Scenes.Sum(scene => scene.UnresolvedContacts.Count);
            int rendererCount = audit.Scenes.Sum(scene => scene.RendererCount);
            var report = new StringBuilder();
            report.AppendLine("# Z-Fighting Candidate Audit");
            report.AppendLine();
            report.AppendLine(
                "Generated " + DateTime.UtcNow.ToString("u") +
                " from the enabled Build Settings scenes.");
            report.AppendLine();
            report.AppendLine(
                "This is a geometric triage report, not visual proof. A listed pair " +
                "has overlapping, near-coplanar triangles (or identical geometry), " +
                "but must still be confirmed by a controlled production Game-view A/B " +
                "test. The RetroLit clip-space snapping can make a small legitimate " +
                "depth separation unstable, so prioritize the high-risk results first. " +
                "Opposing single-sided faces are omitted because they cannot compete " +
                "from one camera direction; they are retained only when a material " +
                "explicitly disables culling.");
            report.AppendLine();
            report.AppendLine("## Summary");
            report.AppendLine();
            report.AppendLine("| Scene | Mesh renderers scanned | Geometric candidates | Unresolved broad-phase contacts |");
            report.AppendLine("| --- | ---: | ---: | ---: |");
            foreach (SceneAudit scene in audit.Scenes)
            {
                report.AppendLine(
                    "| " + EscapeTable(scene.Name) + " | " + scene.RendererCount + " | " +
                    scene.Candidates.Count + " | " + scene.UnresolvedContacts.Count + " |");
            }

            report.AppendLine();
            report.AppendLine("- Total mesh renderers scanned: " + rendererCount);
            report.AppendLine("- Total geometric candidates: " + candidateCount);
            report.AppendLine("- Unresolved broad-phase contacts: " + unresolvedCount);
            report.AppendLine();
            report.AppendLine(
                "The complete per-candidate index is `docs/reviews/z-fighting-candidates.csv`. " +
                "This Markdown report groups every finding by source and expands only " +
                "the highest-risk individual candidates per scene.");
            report.AppendLine();
            report.AppendLine("## How to use this report");
            report.AppendLine();
            report.AppendLine(
                "For each candidate, reproduce the normal gameplay route at the named " +
                "chapter/spawn, then disable one renderer temporarily in Play Mode. If " +
                "the surface stops swapping during a small yaw/pitch motion, inspect " +
                "which visual owner should remain before making a serialized change. " +
                "Do not use a global depth bias, render-queue change, or shader ZTest " +
                "change as a first response.");

            foreach (SceneAudit scene in audit.Scenes)
            {
                report.AppendLine();
                report.AppendLine("## " + EscapeHeading(scene.Name));
                report.AppendLine();
                report.AppendLine("Scene asset: `" + EscapeCode(scene.Path) + "`");

                if (scene.Candidates.Count == 0)
                {
                    report.AppendLine();
                    report.AppendLine(
                        "No geometrically corroborated candidate was found within the " +
                        "configured 12.5 mm plane tolerance.");
                }
                else
                {
                    AppendCandidateGroupSummary(report, scene);
                    report.AppendLine();
                    report.AppendLine("### Highest-risk individual candidates");

                    int detailCount = Math.Min(
                        MaximumMarkdownCandidateDetailsPerScene,
                        scene.Candidates.Count);
                    for (int candidateIndex = 0;
                        candidateIndex < detailCount;
                        candidateIndex++)
                    {
                        AppendCandidate(
                            report,
                            candidateIndex + 1,
                            scene.Candidates[candidateIndex]);
                    }

                    if (detailCount < scene.Candidates.Count)
                    {
                        report.AppendLine();
                        report.AppendLine(
                            "The remaining " + (scene.Candidates.Count - detailCount) +
                            " candidate(s) are indexed in `z-fighting-candidates.csv`.");
                    }
                }

                if (scene.UnresolvedContacts.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("### Unresolved broad-phase contacts");
                    report.AppendLine();
                    foreach (UnresolvedContact unresolved in scene.UnresolvedContacts)
                    {
                        report.AppendLine(
                            "- `" + EscapeCode(unresolved.First.Path) + "` ↔ `" +
                            EscapeCode(unresolved.Second.Path) + "` — " +
                            unresolved.Reason);
                    }
                }

                if (scene.ReadFailures.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("### Mesh-read notes");
                    report.AppendLine();
                    foreach (string failure in scene.ReadFailures.Distinct())
                        report.AppendLine("- " + failure);
                }
            }

            if (audit.Failures.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("## Scene-load failures");
                report.AppendLine();
                foreach (string failure in audit.Failures)
                    report.AppendLine("- " + failure);
            }

            return report.ToString();
        }

        private static string BuildCsv(AuditRun audit)
        {
            var report = new StringBuilder();
            report.AppendLine(
                "Scene,Category,Eligible Chapters,Current Activity,First Renderer," +
                "Second Renderer,First Mesh,Second Mesh,First Source,Second Source," +
                "First Materials,Second Materials,Closest Plane Separation (m)," +
                "Largest Sampled Overlap (m2),Witness Pairs,Triangle Pair Tests");

            foreach (SceneAudit scene in audit.Scenes)
            {
                foreach (Candidate candidate in scene.Candidates)
                {
                    TriangleWitness? closest = candidate.Evidence.HasWitnesses
                        ? candidate.Evidence.Witnesses
                            .OrderBy(witness => witness.PlaneSeparation)
                            .First()
                        : (TriangleWitness?)null;
                    AppendCsvRow(
                        report,
                        scene.Name,
                        candidate.Description,
                        FormatChapters(candidate.Chapters),
                        FormatCurrentActivity(
                            candidate.First,
                            candidate.Second,
                            candidate.Internal),
                        candidate.First.Path,
                        candidate.Internal ? string.Empty : candidate.Second.Path,
                        candidate.First.Mesh.name,
                        candidate.Internal ? string.Empty : candidate.Second.Mesh.name,
                        candidate.First.SourceIdentity,
                        candidate.Internal ? string.Empty : candidate.Second.SourceIdentity,
                        candidate.First.MaterialDescription,
                        candidate.Internal ? string.Empty : candidate.Second.MaterialDescription,
                        closest.HasValue
                            ? closest.Value.PlaneSeparation.ToString("0.000000")
                            : string.Empty,
                        candidate.Evidence.LargestProjectedOverlapArea.ToString("0.000000"),
                        candidate.Evidence.Witnesses.Count.ToString(),
                        candidate.Evidence.TrianglePairTests.ToString());
                }
            }

            return report.ToString();
        }

        private static void AppendCandidateGroupSummary(
            StringBuilder report,
            SceneAudit scene)
        {
            var groups = scene.Candidates
                .GroupBy(CandidateSourceKey)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => group.Max(candidate => candidate.RiskScore))
                .ToList();
            int internalCount = scene.Candidates.Count(candidate => candidate.Internal);
            int externalCount = scene.Candidates.Count(candidate => !candidate.Internal);
            int exactCount = scene.Candidates.Count(candidate => candidate.ExactDuplicate);

            report.AppendLine();
            report.AppendLine(
                "- " + internalCount + " internal-mesh candidate(s), " +
                externalCount + " cross-renderer candidate(s), and " +
                exactCount + " exact duplicate(s).");
            report.AppendLine();
            report.AppendLine("| Source group | Candidates | Largest sampled overlap |");
            report.AppendLine("| --- | ---: | ---: |");
            foreach (IGrouping<string, Candidate> group in groups.Take(10))
            {
                float largestOverlap = group.Max(candidate =>
                    candidate.Evidence.LargestProjectedOverlapArea);
                report.AppendLine(
                    "| " + EscapeTable(group.Key) + " | " + group.Count() + " | " +
                    FormatSquareMeters(largestOverlap) + " |");
            }

            if (groups.Count > 10)
            {
                report.AppendLine(
                    "| " + (groups.Count - 10) + " additional source group(s) | — | — |");
            }
        }

        private static string CandidateSourceKey(Candidate candidate)
        {
            if (candidate.Internal)
                return "Internal mesh: " + candidate.First.SourceIdentity;
            if (candidate.ExactDuplicate)
            {
                return "Exact duplicate: " + candidate.First.SourceIdentity +
                    " ↔ " + candidate.Second.SourceIdentity;
            }

            return "Cross renderer: " + candidate.First.SourceIdentity +
                " ↔ " + candidate.Second.SourceIdentity;
        }

        private static void AppendCsvRow(StringBuilder report, params string[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (index > 0)
                    report.Append(',');
                report.Append('"');
                report.Append((values[index] ?? string.Empty).Replace("\"", "\"\""));
                report.Append('"');
            }

            report.AppendLine();
        }

        private static void AppendCandidate(
            StringBuilder report,
            int ordinal,
            Candidate candidate)
        {
            report.AppendLine();
            report.AppendLine(
                "### " + ordinal + ". " + candidate.RiskLabel + " — " +
                EscapeHeading(candidate.Description));
            report.AppendLine();
            report.AppendLine("- Evidence: " + candidate.EvidenceDescription);
            report.AppendLine("- Eligible chapters: " + FormatChapters(candidate.Chapters));
            report.AppendLine(
                "- Current serialized activity: " +
                FormatCurrentActivity(candidate.First, candidate.Second, candidate.Internal));
            report.AppendLine(
                "- First renderer: `" + EscapeCode(candidate.First.Path) + "`  ");
            report.AppendLine("  " + DescribeRenderer(candidate.First));
            if (!candidate.Internal)
            {
                report.AppendLine(
                    "- Second renderer: `" + EscapeCode(candidate.Second.Path) + "`  ");
                report.AppendLine("  " + DescribeRenderer(candidate.Second));
            }

            if (candidate.Evidence.HasWitnesses)
            {
                TriangleWitness closest = candidate.Evidence.Witnesses
                    .OrderBy(witness => witness.PlaneSeparation)
                    .First();
                report.AppendLine(
                    "- Triangle evidence: " + candidate.Evidence.Witnesses.Count +
                    " overlapping witness pair(s) found after " +
                    candidate.Evidence.TrianglePairTests + " triangle-pair test(s); " +
                    "closest plane separation " + FormatMeters(closest.PlaneSeparation) +
                    ", largest sampled projected overlap " +
                    FormatSquareMeters(candidate.Evidence.LargestProjectedOverlapArea) +
                    ", representative point " + FormatVector(closest.Position) + ".");
            }
        }

        private static string DescribeRenderer(RendererRecord renderer)
        {
            return "Mesh `" + EscapeCode(renderer.Mesh.name) + "` from `" +
                EscapeCode(renderer.MeshPath) + "`; materials " +
                renderer.MaterialDescription + "; source `" +
                EscapeCode(renderer.SourceIdentity) + "`; " +
                renderer.ColliderDescription + ".";
        }

        private static string FormatCurrentActivity(
            RendererRecord first,
            RendererRecord second,
            bool internalCandidate)
        {
            if (internalCandidate)
                return first.CurrentlyActive ? "active" : "inactive";

            return (first.CurrentlyActive ? "first active" : "first inactive") +
                ", " + (second.CurrentlyActive ? "second active" : "second inactive");
        }

        private static string FormatChapters(ChapterMask chapters)
        {
            if (chapters == ChapterMask.All)
                return "I–VII";

            var labels = new List<string>();
            for (int chapter = 1; chapter <= 7; chapter++)
            {
                ChapterMask mask = (ChapterMask)(1 << (chapter - 1));
                if ((chapters & mask) != 0)
                    labels.Add(ToRoman(chapter));
            }

            return labels.Count == 0 ? "none" : string.Join(", ", labels);
        }

        private static string ToRoman(int value)
        {
            string[] labels = { "I", "II", "III", "IV", "V", "VI", "VII" };
            return value >= 1 && value <= labels.Length ? labels[value - 1] : value.ToString();
        }

        private static string FormatMeters(float value) =>
            value.ToString("0.0000") + " m";

        private static string FormatSquareMeters(float value) =>
            value.ToString("0.0000") + " m²";

        private static string FormatVector(Vector3 value) =>
            "(" + value.x.ToString("0.00") + ", " +
            value.y.ToString("0.00") + ", " + value.z.ToString("0.00") + ")";

        private static string EscapeTable(string value) =>
            (value ?? string.Empty).Replace("|", "\\|").Replace("\n", " ");

        private static string EscapeHeading(string value) =>
            (value ?? string.Empty).Replace("\n", " ");

        private static string EscapeCode(string value) =>
            (value ?? string.Empty).Replace("`", "'");

        internal sealed class AuditRun
        {
            public readonly List<SceneAudit> Scenes = new List<SceneAudit>();
            public readonly List<string> Failures = new List<string>();
        }

        internal sealed class SceneAudit
        {
            public SceneAudit(string name, string path)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Unnamed Scene" : name;
                Path = path ?? string.Empty;
            }

            public string Name { get; }
            public string Path { get; }
            public int RendererCount { get; set; }
            public readonly List<Candidate> Candidates = new List<Candidate>();
            public readonly List<UnresolvedContact> UnresolvedContacts =
                new List<UnresolvedContact>();
            public readonly List<string> ReadFailures = new List<string>();
        }

        internal sealed class Candidate
        {
            public Candidate(
                RendererRecord first,
                RendererRecord second,
                bool internalCandidate,
                bool exactDuplicate,
                PairEvidence evidence)
            {
                First = first;
                Second = second;
                Internal = internalCandidate;
                ExactDuplicate = exactDuplicate;
                Evidence = evidence;
                Chapters = first.Chapters & second.Chapters;
            }

            public RendererRecord First { get; }
            public RendererRecord Second { get; }
            public bool Internal { get; }
            public bool ExactDuplicate { get; }
            public PairEvidence Evidence { get; }
            public ChapterMask Chapters { get; }

            public int RiskScore
            {
                get
                {
                    int score = ExactDuplicate ? 10000 : (Internal ? 450 : 400);
                    if (First.CurrentlyActive && Second.CurrentlyActive)
                        score += 1000;
                    if (First.MaterialDescription != Second.MaterialDescription)
                        score += 50;
                    if (Chapters == ChapterMask.All)
                        score += 25;
                    score += Mathf.RoundToInt(Mathf.Min(
                        750f,
                        Evidence.LargestProjectedOverlapArea * 25f));
                    if (Evidence.HasWitnesses)
                    {
                        float closestSeparation = Evidence.Witnesses.Min(
                            witness => witness.PlaneSeparation);
                        score += Mathf.RoundToInt(Mathf.Clamp01(
                            1f - closestSeparation / PlaneDistanceTolerance) * 100f);
                    }
                    return score;
                }
            }

            public string RiskLabel => ExactDuplicate ? "Critical" : "High";

            public string Description
            {
                get
                {
                    if (ExactDuplicate)
                        return "identical mesh at identical world transform";
                    if (Internal)
                        return "overlapping triangles inside one renderer";
                    return "near-coplanar triangles across two renderers";
                }
            }

            public string EvidenceDescription
            {
                get
                {
                    if (ExactDuplicate)
                    {
                        return "The two renderers reference the same mesh and have " +
                            "the same world transform within 0.01 mm.";
                    }

                    return "Parallel triangles overlap in projected area while their " +
                        "planes remain within " + FormatMeters(PlaneDistanceTolerance) + ".";
                }
            }

            public static int CompareByRisk(Candidate first, Candidate second)
            {
                int byRisk = second.RiskScore.CompareTo(first.RiskScore);
                if (byRisk != 0)
                    return byRisk;

                int byOverlap = second.Evidence.LargestProjectedOverlapArea.CompareTo(
                    first.Evidence.LargestProjectedOverlapArea);
                if (byOverlap != 0)
                    return byOverlap;

                int byFirst = string.Compare(
                    first.First.Path,
                    second.First.Path,
                    StringComparison.Ordinal);
                if (byFirst != 0)
                    return byFirst;
                return string.Compare(
                    first.Second.Path,
                    second.Second.Path,
                    StringComparison.Ordinal);
            }
        }

        internal sealed class UnresolvedContact
        {
            public UnresolvedContact(
                RendererRecord first,
                RendererRecord second,
                string reason)
            {
                First = first;
                Second = second;
                Reason = reason;
            }

            public RendererRecord First { get; }
            public RendererRecord Second { get; }
            public string Reason { get; }

            public static int CompareByPaths(
                UnresolvedContact first,
                UnresolvedContact second)
            {
                int byFirst = string.Compare(
                    first.First.Path,
                    second.First.Path,
                    StringComparison.Ordinal);
                if (byFirst != 0)
                    return byFirst;
                return string.Compare(
                    first.Second.Path,
                    second.Second.Path,
                    StringComparison.Ordinal);
            }
        }

        internal sealed class RendererRecord
        {
            public RendererRecord(
                Renderer renderer,
                Mesh mesh,
                ChapterMask chapters,
                Bounds worldBounds)
            {
                Renderer = renderer;
                Mesh = mesh;
                Chapters = chapters;
                WorldBounds = worldBounds;
                LocalToWorld = renderer.localToWorldMatrix;
                Path = GetHierarchyPath(renderer.transform);
                CurrentlyActive = renderer.gameObject.activeInHierarchy;
                MeshPath = AssetDatabase.GetAssetPath(mesh);
                SourceIdentity = FindSourceIdentity(renderer.gameObject, mesh);
                MaterialDescription = DescribeMaterials(renderer.sharedMaterials);
                ColliderDescription = DescribeColliders(renderer.gameObject);
                MayRenderBackfaces = HasDoubleSidedMaterial(renderer.sharedMaterials);
            }

            public Renderer Renderer { get; }
            public Mesh Mesh { get; }
            public ChapterMask Chapters { get; }
            public Bounds WorldBounds { get; }
            public Matrix4x4 LocalToWorld { get; }
            public string Path { get; }
            public bool CurrentlyActive { get; }
            public string MeshPath { get; }
            public string SourceIdentity { get; }
            public string MaterialDescription { get; }
            public string ColliderDescription { get; }
            public bool MayRenderBackfaces { get; }
            public bool GeometryReadAttempted { get; set; }
            public TriangleIndex TriangleIndex { get; set; }

            public static int CompareByMinimumX(
                RendererRecord first,
                RendererRecord second)
            {
                int byX = first.WorldBounds.min.x.CompareTo(second.WorldBounds.min.x);
                return byX != 0
                    ? byX
                    : string.Compare(first.Path, second.Path, StringComparison.Ordinal);
            }

            private static string FindSourceIdentity(GameObject gameObject, Mesh mesh)
            {
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                if (!string.IsNullOrWhiteSpace(prefabPath))
                    return prefabPath;

                string meshPath = AssetDatabase.GetAssetPath(mesh);
                return string.IsNullOrWhiteSpace(meshPath) ? "scene-owned mesh" : meshPath;
            }

            private static string DescribeMaterials(Material[] materials)
            {
                if (materials == null || materials.Length == 0)
                    return "none assigned";

                var descriptions = new List<string>();
                for (int index = 0; index < materials.Length; index++)
                {
                    Material material = materials[index];
                    if (material == null)
                    {
                        descriptions.Add("<missing>");
                        continue;
                    }

                    string materialPath = AssetDatabase.GetAssetPath(material);
                    descriptions.Add(
                        "`" + EscapeCode(material.name) + "` (queue " +
                        material.renderQueue + ", `" + EscapeCode(materialPath) + "`)");
                }

                return string.Join(", ", descriptions);
            }

            private static string DescribeColliders(GameObject gameObject)
            {
                Collider[] colliders = gameObject.GetComponents<Collider>();
                if (colliders.Length == 0)
                    return "no collider on the renderer GameObject";

                return "same-object collider(s): " + string.Join(
                    ", ",
                    colliders.Select(collider => collider.GetType().Name).ToArray());
            }

            private static bool HasDoubleSidedMaterial(Material[] materials)
            {
                if (materials == null)
                    return false;

                for (int index = 0; index < materials.Length; index++)
                {
                    Material material = materials[index];
                    if (material == null || !material.HasProperty("_Cull"))
                        continue;

                    if (Mathf.Approximately(material.GetFloat("_Cull"), 0f))
                        return true;
                }

                return false;
            }
        }

        internal sealed class PairEvidence
        {
            public static PairEvidence ExactDuplicate => new PairEvidence();

            public readonly List<TriangleWitness> Witnesses =
                new List<TriangleWitness>();
            public int TrianglePairTests { get; set; }
            public bool LimitReached { get; set; }
            public bool HasWitnesses => Witnesses.Count > 0;
            public bool ShouldStop =>
                LimitReached || Witnesses.Count >= MaximumWitnessesPerCandidate;
            public float LargestProjectedOverlapArea => Witnesses.Count == 0
                ? 0f
                : Witnesses.Max(witness => witness.ProjectedOverlapArea);
        }

        internal readonly struct TriangleWitness
        {
            public TriangleWitness(
                float planeSeparation,
                float projectedOverlapArea,
                Vector3 position)
            {
                PlaneSeparation = planeSeparation;
                ProjectedOverlapArea = projectedOverlapArea;
                Position = position;
            }

            public float PlaneSeparation { get; }
            public float ProjectedOverlapArea { get; }
            public Vector3 Position { get; }
        }

        internal readonly struct TriangleData
        {
            public TriangleData(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                A = a;
                B = b;
                C = c;
                Normal = normal;
                Center = (a + b + c) / 3f;
                Bounds bounds = new Bounds(a, Vector3.zero);
                bounds.Encapsulate(b);
                bounds.Encapsulate(c);
                Bounds = bounds;
            }

            public Vector3 A { get; }
            public Vector3 B { get; }
            public Vector3 C { get; }
            public Vector3 Normal { get; }
            public Vector3 Center { get; }
            public Bounds Bounds { get; }
        }

        internal sealed class TriangleIndex
        {
            public TriangleIndex(List<TriangleData> triangles)
            {
                Triangles = triangles;
                Root = BuildNode(0, triangles.Count);
            }

            public List<TriangleData> Triangles { get; }
            public TriangleNode Root { get; }

            private TriangleNode BuildNode(int start, int count)
            {
                Bounds bounds = Triangles[start].Bounds;
                Bounds centers = new Bounds(Triangles[start].Center, Vector3.zero);
                int end = start + count;
                for (int index = start + 1; index < end; index++)
                {
                    bounds.Encapsulate(Triangles[index].Bounds);
                    centers.Encapsulate(Triangles[index].Center);
                }

                if (count <= TriangleLeafSize)
                    return new TriangleNode(bounds, start, count, null, null);

                int axis = DominantAxis(centers.size);
                Triangles.Sort(start, count, new TriangleCenterComparer(axis));
                int leftCount = count / 2;
                TriangleNode left = BuildNode(start, leftCount);
                TriangleNode right = BuildNode(start + leftCount, count - leftCount);
                return new TriangleNode(bounds, start, count, left, right);
            }
        }

        internal sealed class TriangleNode
        {
            public TriangleNode(
                Bounds bounds,
                int start,
                int count,
                TriangleNode left,
                TriangleNode right)
            {
                Bounds = bounds;
                Start = start;
                Count = count;
                Left = left;
                Right = right;
                Vector3 size = bounds.size;
                Volume = Mathf.Max(0f, size.x * size.y * size.z);
            }

            public Bounds Bounds { get; }
            public int Start { get; }
            public int Count { get; }
            public TriangleNode Left { get; }
            public TriangleNode Right { get; }
            public float Volume { get; }
            public bool IsLeaf => Left == null && Right == null;
        }

        private sealed class TriangleCenterComparer : IComparer<TriangleData>
        {
            private readonly int axis;

            public TriangleCenterComparer(int axis)
            {
                this.axis = axis;
            }

            public int Compare(TriangleData first, TriangleData second) =>
                first.Center[axis].CompareTo(second.Center[axis]);
        }

        private sealed class DressingContext
        {
            private readonly List<DressingRoot> roots = new List<DressingRoot>();

            public DressingContext(Scene scene)
            {
                GameObject[] sceneRoots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < sceneRoots.Length; rootIndex++)
                {
                    ChapterDressing[] dressings =
                        sceneRoots[rootIndex].GetComponentsInChildren<ChapterDressing>(true);
                    for (int dressingIndex = 0;
                        dressingIndex < dressings.Length;
                        dressingIndex++)
                    {
                        ChapterDressing dressing = dressings[dressingIndex];
                        if (dressing == null)
                            continue;

                        foreach (ChapterDressingGroup group in dressing.Groups)
                        {
                            if (group != null && group.Root != null)
                            {
                                roots.Add(new DressingRoot(
                                    group.Root.transform,
                                    group.ActiveInChapters));
                            }
                        }
                    }
                }
            }

            public ChapterMask PotentialChaptersFor(Transform target)
            {
                ChapterMask chapters = ChapterMask.All;
                for (Transform current = target;
                    current != null;
                    current = current.parent)
                {
                    bool knownDressingRoot = false;
                    for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
                    {
                        DressingRoot root = roots[rootIndex];
                        if (!IsSelfOrChildOf(target, root.Transform))
                            continue;

                        chapters &= root.Chapters;
                        if (ReferenceEquals(current, root.Transform))
                            knownDressingRoot = true;
                    }

                    if (!current.gameObject.activeSelf && !knownDressingRoot)
                        return ChapterMask.None;
                }

                return chapters;
            }

            private static bool IsSelfOrChildOf(Transform target, Transform root)
            {
                for (Transform current = target;
                    current != null;
                    current = current.parent)
                {
                    if (ReferenceEquals(current, root))
                        return true;
                }

                return false;
            }
        }

        private readonly struct DressingRoot
        {
            public DressingRoot(Transform transform, ChapterMask chapters)
            {
                Transform = transform;
                Chapters = chapters;
            }

            public Transform Transform { get; }
            public ChapterMask Chapters { get; }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<missing transform>";

            var segments = new List<string>();
            for (Transform current = transform;
                current != null;
                current = current.parent)
            {
                segments.Add(current.name);
            }

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }
    }
}
