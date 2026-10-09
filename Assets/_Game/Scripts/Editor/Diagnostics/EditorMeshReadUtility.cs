using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hortensia.Editor.Diagnostics
{
    /// <summary>
    /// Reads mesh geometry in editor diagnostics without requiring a mesh to
    /// have CPU-readable import settings.
    /// </summary>
    internal static class EditorMeshReadUtility
    {
        internal static Vector3[] GetVertices(Mesh mesh)
        {
            if (mesh == null)
                return Array.Empty<Vector3>();

            using (Mesh.MeshDataArray dataArray =
                MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                Mesh.MeshData data = dataArray[0];
                using (var source = new NativeArray<Vector3>(
                    data.vertexCount,
                    Allocator.Temp))
                {
                    data.GetVertices(source);
                    var vertices = new Vector3[source.Length];
                    for (int i = 0; i < source.Length; i++)
                        vertices[i] = source[i];
                    return vertices;
                }
            }
        }

        internal static int[] GetTriangles(Mesh mesh)
        {
            if (mesh == null)
                return Array.Empty<int>();

            using (Mesh.MeshDataArray dataArray =
                MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                Mesh.MeshData data = dataArray[0];
                int totalIndices = 0;
                for (int submesh = 0; submesh < data.subMeshCount; submesh++)
                    totalIndices += data.GetSubMesh(submesh).indexCount;

                var triangles = new int[totalIndices];
                int destination = 0;
                for (int submesh = 0; submesh < data.subMeshCount; submesh++)
                {
                    int count = data.GetSubMesh(submesh).indexCount;
                    using (var source = new NativeArray<int>(count, Allocator.Temp))
                    {
                        data.GetIndices(source, submesh);
                        for (int i = 0; i < source.Length; i++)
                            triangles[destination++] = source[i];
                    }
                }

                return triangles;
            }
        }
    }
}
