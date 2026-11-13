using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Moi Rigidbody dong dat san trong Gameplay (thung/hop test luc no) -> NetworkObject + NetworkTransform (server) + NetworkRigidbody.
// Luc no chi chay tren server (KI-26) -> vat bi day phai dong bo, client chi noi suy; join muon thay dung vi tri moi (KI-32)
internal static class NetworkPropsBuilder
{
    private const string MENU_BUILD = "Tools/Test3D/Network Physics Props In Gameplay";
    private const string LOG_PREFIX = "[NetworkPropsBuilder]";

    [MenuItem(MENU_BUILD)]
    private static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        int count = Build(ProjectSceneBuilder.GAMEPLAY_SCENE_PATH);
        EditorUtility.DisplayDialog("Test3D Builder", $"{count} physics prop(s) in Gameplay are networked.", "OK");
    }

    internal static int Build(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int groundLayer = LayerMask.NameToLayer(Constants.LAYER_GROUND);
        int count = 0;
        foreach (Rigidbody body in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsScenePhysicsProp(body, groundLayer))
            {
                continue;
            }

            GameObject prop = body.gameObject;
            GetOrAdd<NetworkObject>(prop);
            ConfigureTransform(GetOrAdd<NetworkTransform>(prop));
            GetOrAdd<NetworkRigidbody>(prop);
            // Host nhin vat ly truc tiep -> Interpolate cho muot; client do NetworkRigidbody dat kinematic + noi suy
            body.interpolation = RigidbodyInterpolation.Interpolate;
            EditorUtility.SetDirty(prop);
            count++;
        }

        NetworkEditorUtility.SaveSceneWithFreshNetworkIds(scene);
        Debug.Log($"{LOG_PREFIX} Done. {count} physics prop(s) networked in {scenePath}.");
        return count;
    }

    // Chi vat dong nam truc tiep trong scene: bo qua kinematic, moi truong (Ground) va object da nam duoi NetworkObject khac
    private static bool IsScenePhysicsProp(Rigidbody body, int groundLayer)
    {
        if (body.isKinematic || body.gameObject.layer == groundLayer)
        {
            return false;
        }

        Transform parent = body.transform.parent;
        return parent == null || parent.GetComponentInParent<NetworkObject>(true) == null;
    }

    private static void ConfigureTransform(NetworkTransform networkTransform)
    {
        networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
        networkTransform.SyncPositionX = true;
        networkTransform.SyncPositionY = true;
        networkTransform.SyncPositionZ = true;
        networkTransform.SyncRotAngleX = true;
        networkTransform.SyncRotAngleY = true;
        networkTransform.SyncRotAngleZ = true;
        networkTransform.SyncScaleX = false;
        networkTransform.SyncScaleY = false;
        networkTransform.SyncScaleZ = false;
        networkTransform.InLocalSpace = false;
        networkTransform.Interpolate = true;
        EditorUtility.SetDirty(networkTransform);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}
