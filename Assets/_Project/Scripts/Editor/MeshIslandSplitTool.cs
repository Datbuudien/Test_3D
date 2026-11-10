using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MeshIslandSplitTool
{
    private const string MENU_SPLIT = "Tools/Test3D/Split Mesh By Loose Parts";
    private const string DIALOG_TITLE = "Split Mesh By Loose Parts";
    private const string DIALOG_OK = "OK";
    private const string LOG_PREFIX = "[MeshIslandSplitTool]";
    private const string SPLIT_FOLDER_NAME = "Split";
    private const string SPLIT_ROOT_SUFFIX = "_Split";
    private const string PART_NAME_FORMAT = "{0}_Part{1:00}";
    private const string MESH_ASSET_EXTENSION = ".asset";
    private const float WELD_EPSILON = 0.0001f;
    private const int MAX_UINT16_VERTEX_COUNT = 65535;
    private const int UV_CHANNEL_COUNT = 4;

    private sealed class Island
    {
        public List<int>[] TrianglesBySubMesh { get; }
        public int TriangleCount { get; private set; }

        public Island(int subMeshCount)
        {
            TrianglesBySubMesh = new List<int>[subMeshCount];
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                TrianglesBySubMesh[subMeshIndex] = new List<int>();
            }
        }

        public void AddTriangle(int subMeshIndex, int first, int second, int third)
        {
            List<int> triangles = TrianglesBySubMesh[subMeshIndex];
            triangles.Add(first);
            triangles.Add(second);
            triangles.Add(third);
            TriangleCount++;
        }
    }

    private sealed class SourceMeshData
    {
        public Vector3[] Vertices { get; }
        public Vector3[] Normals { get; }
        public Vector4[] Tangents { get; }
        public Color[] Colors { get; }
        public List<Vector4>[] Uvs { get; }

        public SourceMeshData(Mesh mesh)
        {
            Vertices = mesh.vertices;
            Normals = mesh.normals;
            Tangents = mesh.tangents;
            Colors = mesh.colors;
            Uvs = new List<Vector4>[UV_CHANNEL_COUNT];
            for (int channel = 0; channel < UV_CHANNEL_COUNT; channel++)
            {
                Uvs[channel] = new List<Vector4>();
                mesh.GetUVs(channel, Uvs[channel]);
            }
        }
    }

    [MenuItem(MENU_SPLIT)]
    private static void SplitSelectedMesh()
    {
        GameObject source = Selection.activeGameObject;
        if (!TryGetSourceMesh(source, out Mesh mesh, out MeshRenderer meshRenderer, out string sourceAssetPath))
        {
            return;
        }

        List<Island> islands = FindIslands(mesh);
        if (islands.Count < 2)
        {
            EditorUtility.DisplayDialog(DIALOG_TITLE, $"'{mesh.name}' has only one loose part, nothing to split.", DIALOG_OK);
            return;
        }

        string outputFolder = EnsureOutputFolder(sourceAssetPath);
        SourceMeshData sourceData = new SourceMeshData(mesh);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(MENU_SPLIT);

        GameObject splitRoot = CreateSplitRoot(source);
        string baseName = SanitizeFileName(mesh.name);
        for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
        {
            string partName = string.Format(PART_NAME_FORMAT, baseName, islandIndex);
            Mesh partMesh = BuildIslandMesh(sourceData, islands[islandIndex], out Vector3 center, out List<int> usedSubMeshes);
            partMesh.name = partName;
            partMesh = SaveMeshAsset(partMesh, $"{outputFolder}/{partName}{MESH_ASSET_EXTENSION}");
            CreatePartObject(splitRoot.transform, partName, partMesh, center, usedSubMeshes, meshRenderer.sharedMaterials);

            Vector3 size = partMesh.bounds.size;
            Debug.Log($"{LOG_PREFIX} {partName}: {islands[islandIndex].TriangleCount} tris, size ({size.x:0.####}, {size.y:0.####}, {size.z:0.####})");
        }

        Undo.RecordObject(source, MENU_SPLIT);
        source.SetActive(false);

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(source.scene);
        Selection.activeGameObject = splitRoot;
        Debug.Log($"{LOG_PREFIX} '{mesh.name}' split into {islands.Count} parts in {outputFolder}. Original '{source.name}' disabled.");
    }

    [MenuItem(MENU_SPLIT, true)]
    private static bool ValidateSplitSelectedMesh()
    {
        GameObject selected = Selection.activeGameObject;
        return selected != null && selected.TryGetComponent(out MeshFilter _);
    }

    private static bool TryGetSourceMesh(GameObject source, out Mesh mesh, out MeshRenderer meshRenderer, out string sourceAssetPath)
    {
        mesh = null;
        meshRenderer = null;
        sourceAssetPath = null;

        if (source == null || EditorUtility.IsPersistent(source)
            || !source.TryGetComponent(out MeshFilter meshFilter) || !source.TryGetComponent(out meshRenderer))
        {
            EditorUtility.DisplayDialog(DIALOG_TITLE, "Select one scene GameObject that has a MeshFilter and a MeshRenderer.", DIALOG_OK);
            return false;
        }

        mesh = meshFilter.sharedMesh;
        sourceAssetPath = mesh != null ? AssetDatabase.GetAssetPath(mesh) : null;
        if (string.IsNullOrEmpty(sourceAssetPath))
        {
            EditorUtility.DisplayDialog(DIALOG_TITLE, "The mesh must be an imported asset (e.g. from a .glb).", DIALOG_OK);
            return false;
        }

        if (!mesh.isReadable || !HasOnlyTriangles(mesh))
        {
            EditorUtility.DisplayDialog(DIALOG_TITLE, $"'{mesh.name}' must be readable and made of triangles only.", DIALOG_OK);
            return false;
        }

        return true;
    }

    private static List<Island> FindIslands(Mesh mesh)
    {
        int[] weldedIds = WeldVertices(mesh.vertices);
        int[] parents = new int[weldedIds.Length];
        for (int vertexIndex = 0; vertexIndex < parents.Length; vertexIndex++)
        {
            parents[vertexIndex] = vertexIndex;
        }

        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            int[] triangles = mesh.GetTriangles(subMeshIndex);
            for (int corner = 0; corner < triangles.Length; corner += 3)
            {
                int first = weldedIds[triangles[corner]];
                Union(parents, first, weldedIds[triangles[corner + 1]]);
                Union(parents, first, weldedIds[triangles[corner + 2]]);
            }
        }

        Dictionary<int, Island> islandsByRoot = new Dictionary<int, Island>();
        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            int[] triangles = mesh.GetTriangles(subMeshIndex);
            for (int corner = 0; corner < triangles.Length; corner += 3)
            {
                int root = Find(parents, weldedIds[triangles[corner]]);
                if (!islandsByRoot.TryGetValue(root, out Island island))
                {
                    island = new Island(mesh.subMeshCount);
                    islandsByRoot.Add(root, island);
                }

                island.AddTriangle(subMeshIndex, triangles[corner], triangles[corner + 1], triangles[corner + 2]);
            }
        }

        List<Island> islands = new List<Island>(islandsByRoot.Values);
        islands.Sort((left, right) => right.TriangleCount.CompareTo(left.TriangleCount));
        return islands;
    }

    // Gop cac dinh trung vi tri (seam UV/normal) de khong tach nham 1 khoi thanh nhieu khoi
    private static int[] WeldVertices(Vector3[] vertices)
    {
        Dictionary<Vector3Int, int> firstIndexByPosition = new Dictionary<Vector3Int, int>(vertices.Length);
        int[] weldedIds = new int[vertices.Length];
        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
        {
            Vector3 vertex = vertices[vertexIndex];
            Vector3Int key = new Vector3Int(
                Mathf.RoundToInt(vertex.x / WELD_EPSILON),
                Mathf.RoundToInt(vertex.y / WELD_EPSILON),
                Mathf.RoundToInt(vertex.z / WELD_EPSILON));

            if (!firstIndexByPosition.TryGetValue(key, out int firstIndex))
            {
                firstIndex = vertexIndex;
                firstIndexByPosition.Add(key, firstIndex);
            }

            weldedIds[vertexIndex] = firstIndex;
        }

        return weldedIds;
    }

    private static int Find(int[] parents, int element)
    {
        while (parents[element] != element)
        {
            parents[element] = parents[parents[element]];
            element = parents[element];
        }

        return element;
    }

    private static void Union(int[] parents, int first, int second)
    {
        int firstRoot = Find(parents, first);
        int secondRoot = Find(parents, second);
        if (firstRoot != secondRoot)
        {
            parents[secondRoot] = firstRoot;
        }
    }

    private static Mesh BuildIslandMesh(SourceMeshData source, Island island, out Vector3 center, out List<int> usedSubMeshes)
    {
        Dictionary<int, int> newIndexBySourceIndex = new Dictionary<int, int>();
        List<int> sourceIndices = new List<int>();
        foreach (List<int> triangles in island.TrianglesBySubMesh)
        {
            foreach (int sourceIndex in triangles)
            {
                if (!newIndexBySourceIndex.ContainsKey(sourceIndex))
                {
                    newIndexBySourceIndex.Add(sourceIndex, sourceIndices.Count);
                    sourceIndices.Add(sourceIndex);
                }
            }
        }

        Bounds bounds = new Bounds(source.Vertices[sourceIndices[0]], Vector3.zero);
        foreach (int sourceIndex in sourceIndices)
        {
            bounds.Encapsulate(source.Vertices[sourceIndex]);
        }

        // Dua pivot ve tam khoi de khuy/lever xoay quanh chinh no khi bay ra
        center = bounds.center;
        Mesh partMesh = new Mesh
        {
            indexFormat = sourceIndices.Count > MAX_UINT16_VERTEX_COUNT ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        CopyVertexAttributes(source, sourceIndices, center, partMesh);

        usedSubMeshes = new List<int>();
        for (int subMeshIndex = 0; subMeshIndex < island.TrianglesBySubMesh.Length; subMeshIndex++)
        {
            if (island.TrianglesBySubMesh[subMeshIndex].Count > 0)
            {
                usedSubMeshes.Add(subMeshIndex);
            }
        }

        partMesh.subMeshCount = usedSubMeshes.Count;
        for (int partSubMesh = 0; partSubMesh < usedSubMeshes.Count; partSubMesh++)
        {
            List<int> sourceTriangles = island.TrianglesBySubMesh[usedSubMeshes[partSubMesh]];
            int[] partTriangles = new int[sourceTriangles.Count];
            for (int corner = 0; corner < sourceTriangles.Count; corner++)
            {
                partTriangles[corner] = newIndexBySourceIndex[sourceTriangles[corner]];
            }

            partMesh.SetTriangles(partTriangles, partSubMesh);
        }

        partMesh.RecalculateBounds();
        return partMesh;
    }

    private static void CopyVertexAttributes(SourceMeshData source, List<int> sourceIndices, Vector3 center, Mesh partMesh)
    {
        int vertexCount = source.Vertices.Length;
        List<Vector3> vertices = new List<Vector3>(sourceIndices.Count);
        foreach (int sourceIndex in sourceIndices)
        {
            vertices.Add(source.Vertices[sourceIndex] - center);
        }

        partMesh.SetVertices(vertices);

        if (source.Normals.Length == vertexCount)
        {
            partMesh.SetNormals(Pick(source.Normals, sourceIndices));
        }

        if (source.Tangents.Length == vertexCount)
        {
            partMesh.SetTangents(Pick(source.Tangents, sourceIndices));
        }

        if (source.Colors.Length == vertexCount)
        {
            partMesh.SetColors(Pick(source.Colors, sourceIndices));
        }

        for (int channel = 0; channel < UV_CHANNEL_COUNT; channel++)
        {
            if (source.Uvs[channel].Count == vertexCount)
            {
                partMesh.SetUVs(channel, Pick(source.Uvs[channel], sourceIndices));
            }
        }
    }

    private static List<T> Pick<T>(IList<T> values, List<int> sourceIndices)
    {
        List<T> picked = new List<T>(sourceIndices.Count);
        foreach (int sourceIndex in sourceIndices)
        {
            picked.Add(values[sourceIndex]);
        }

        return picked;
    }

    private static GameObject CreateSplitRoot(GameObject source)
    {
        GameObject splitRoot = new GameObject(source.name + SPLIT_ROOT_SUFFIX);
        Undo.RegisterCreatedObjectUndo(splitRoot, MENU_SPLIT);
        splitRoot.layer = source.layer;

        Transform splitTransform = splitRoot.transform;
        splitTransform.SetParent(source.transform.parent, false);
        splitTransform.localPosition = source.transform.localPosition;
        splitTransform.localRotation = source.transform.localRotation;
        splitTransform.localScale = source.transform.localScale;
        splitTransform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
        return splitRoot;
    }

    private static void CreatePartObject(Transform parent, string partName, Mesh partMesh, Vector3 center,
        List<int> usedSubMeshes, Material[] sourceMaterials)
    {
        GameObject part = new GameObject(partName);
        Undo.RegisterCreatedObjectUndo(part, MENU_SPLIT);
        part.layer = parent.gameObject.layer;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = center;

        part.AddComponent<MeshFilter>().sharedMesh = partMesh;
        Material[] materials = new Material[usedSubMeshes.Count];
        for (int partSubMesh = 0; partSubMesh < usedSubMeshes.Count; partSubMesh++)
        {
            int sourceSubMesh = usedSubMeshes[partSubMesh];
            materials[partSubMesh] = sourceSubMesh < sourceMaterials.Length ? sourceMaterials[sourceSubMesh] : null;
        }

        part.AddComponent<MeshRenderer>().sharedMaterials = materials;
    }

    // Ghi de vao asset cu (giu GUID) de chay lai tool khong lam gay prefab da tham chieu
    private static Mesh SaveMeshAsset(Mesh partMesh, string assetPath)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(partMesh, assetPath);
            return partMesh;
        }

        EditorUtility.CopySerialized(partMesh, existing);
        Object.DestroyImmediate(partMesh);
        return existing;
    }

    private static string EnsureOutputFolder(string sourceAssetPath)
    {
        string sourceFolder = Path.GetDirectoryName(sourceAssetPath).Replace('\\', '/');
        string outputFolder = $"{sourceFolder}/{SPLIT_FOLDER_NAME}";
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            AssetDatabase.CreateFolder(sourceFolder, SPLIT_FOLDER_NAME);
        }

        return outputFolder;
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }

    private static bool HasOnlyTriangles(Mesh mesh)
    {
        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            if (mesh.GetTopology(subMeshIndex) != MeshTopology.Triangles)
            {
                return false;
            }
        }

        return true;
    }
}
