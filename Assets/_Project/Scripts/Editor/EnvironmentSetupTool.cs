using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnvironmentSetupTool
{
    private const string MENU_SETUP_ENVIRONMENT = "Tools/Test3D/Setup Selected Environment";
    private const string MENU_REMOVE_COLLIDERS = "Tools/Test3D/Remove Colliders From Selected";
    private const string DIALOG_TITLE = "Environment Setup";
    private const string DIALOG_OK = "OK";
    private const string LOG_PREFIX = "[EnvironmentSetupTool]";

    private const StaticEditorFlags ENVIRONMENT_STATIC_FLAGS =
        StaticEditorFlags.BatchingStatic
        | StaticEditorFlags.OccluderStatic
        | StaticEditorFlags.OccludeeStatic
        | StaticEditorFlags.ReflectionProbeStatic
        | StaticEditorFlags.ContributeGI;

    [MenuItem(MENU_SETUP_ENVIRONMENT)]
    private static void SetupSelectedEnvironment()
    {
        GameObject[] selection = Selection.gameObjects;
        if (selection.Length != 1 || !IsSceneObject(selection[0]))
        {
            EditorUtility.DisplayDialog(DIALOG_TITLE, "Select exactly one GameObject in the scene (e.g. BG).", DIALOG_OK);
            return;
        }

        int groundLayer = LayerMask.NameToLayer(Constants.LAYER_GROUND);
        if (groundLayer == -1)
        {
            EditorUtility.DisplayDialog(DIALOG_TITLE,
                $"Layer '{Constants.LAYER_GROUND}' does not exist.\nCreate it in Project Settings > Tags and Layers, then run again.",
                DIALOG_OK);
            return;
        }

        GameObject root = selection[0];
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(MENU_SETUP_ENVIRONMENT);

        AddMeshColliders(root, out int addedColliderCount, out int skippedMeshCount);
        int changedLayerCount = ApplyGroundLayerAndStatic(root, groundLayer);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log($"{LOG_PREFIX} '{root.name}': +{addedColliderCount} MeshCollider, "
            + $"{changedLayerCount} object(s) moved to layer '{Constants.LAYER_GROUND}', "
            + $"{skippedMeshCount} non-triangle mesh(es) skipped.");
    }

    [MenuItem(MENU_SETUP_ENVIRONMENT, true)]
    private static bool ValidateSetupSelectedEnvironment()
    {
        return Selection.gameObjects.Length > 0;
    }

    [MenuItem(MENU_REMOVE_COLLIDERS)]
    private static void RemoveCollidersFromSelected()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(MENU_REMOVE_COLLIDERS);

        int removedColliderCount = 0;
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (!IsSceneObject(selected))
            {
                continue;
            }

            MeshCollider[] meshColliders = selected.GetComponents<MeshCollider>();
            foreach (MeshCollider meshCollider in meshColliders)
            {
                Undo.DestroyObjectImmediate(meshCollider);
            }

            if (meshColliders.Length > 0)
            {
                removedColliderCount += meshColliders.Length;
                EditorSceneManager.MarkSceneDirty(selected.scene);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"{LOG_PREFIX} Removed {removedColliderCount} MeshCollider(s) from {Selection.gameObjects.Length} selected object(s).");
    }

    [MenuItem(MENU_REMOVE_COLLIDERS, true)]
    private static bool ValidateRemoveCollidersFromSelected()
    {
        return Selection.gameObjects.Length > 0;
    }

    private static void AddMeshColliders(GameObject root, out int addedColliderCount, out int skippedMeshCount)
    {
        addedColliderCount = 0;
        skippedMeshCount = 0;

        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null || meshFilter.TryGetComponent(out MeshCollider _))
            {
                continue;
            }

            // MeshCollider chi cook duoc mesh tam giac, bo qua line/point
            if (!HasOnlyTriangles(mesh))
            {
                skippedMeshCount++;
                continue;
            }

            MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(meshFilter.gameObject);
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = false;
            addedColliderCount++;
        }
    }

    private static int ApplyGroundLayerAndStatic(GameObject root, int groundLayer)
    {
        int changedLayerCount = 0;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject target = child.gameObject;
            Undo.RecordObject(target, MENU_SETUP_ENVIRONMENT);

            if (target.layer != groundLayer)
            {
                target.layer = groundLayer;
                changedLayerCount++;
            }

            StaticEditorFlags currentFlags = GameObjectUtility.GetStaticEditorFlags(target);
            GameObjectUtility.SetStaticEditorFlags(target, currentFlags | ENVIRONMENT_STATIC_FLAGS);
        }

        return changedLayerCount;
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

    private static bool IsSceneObject(GameObject gameObject)
    {
        return !EditorUtility.IsPersistent(gameObject) && gameObject.scene.IsValid();
    }
}
