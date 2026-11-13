using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// N3: prefab NET_GrenadeProjectile (qua dang bay, server-auth) + don WPN_Grenade_Held (chi con cosmetic) + gan GrenadeNetwork vao NET_Player.
// Chay lai: prefab da co thi chi cap nhat component (giu GUID + fileID -> danh sach Network Prefabs khong mat tham chieu)
internal static class NetworkGrenadeBuilder
{
    private const string MENU_BUILD = "Tools/Test3D/Build Network Grenade";
    private const string LOG_PREFIX = "[NetworkGrenadeBuilder]";
    private const string MODEL_PREFAB_PATH = "Assets/_Project/Prefabs/Weapon/WPN_Grenade_Model.prefab";
    private const string HELD_PREFAB_PATH = "Assets/_Project/Prefabs/Weapon/WPN_Grenade_Held.prefab";
    private const string PROJECTILE_PREFAB_PATH = "Assets/_Project/Prefabs/Weapon/NET_GrenadeProjectile.prefab";
    private const string EXPLOSION_PREFAB_PATH = "Assets/_Project/VFX/FX_Explosion.prefab";
    private const string PROJECTILE_NAME = "NET_GrenadeProjectile";
    private const string VISUAL_NAME = "Visual";
    private const string BODY_NAME = "Body";
    private const string PIN_NAME = "Pin";
    private const string LEVER_NAME = "Lever";
    private const float PROJECTILE_MASS = 0.4f;
    private const float PROJECTILE_ANGULAR_DAMPING = 0.05f;
    private const float SPIN_ON_THROW = 6f;
    private const float MAX_FLIGHT_TIME = 10f;
    private const float KILL_HEIGHT = -50f;
    private const float DESPAWN_DELAY = 0.2f;
    private const float MIN_GROUND_NORMAL_Y = 0.6f;

    [MenuItem(MENU_BUILD)]
    private static void BuildFromMenu()
    {
        bool built = Build();
        EditorUtility.DisplayDialog("Test3D Builder", built ? "NET_GrenadeProjectile built, WPN_Grenade_Held cleaned, NET_Player wired." : "Build failed - see Console.", "OK");
    }

    internal static bool Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerBuilder.NET_PLAYER_PREFAB_PATH) == null)
        {
            Debug.LogError($"{LOG_PREFIX} {NetworkPlayerBuilder.NET_PLAYER_PREFAB_PATH} not found - run Build Network Player first.");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PROJECTILE_PREFAB_PATH) == null && !CreateProjectileSkeleton())
        {
            return false;
        }

        bool built = ConfigureProjectile() && CleanHeldGrenade() && WireNetPlayer();
        if (built)
        {
            NetworkPlayerBuilder.RegisterNetworkPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PROJECTILE_PREFAB_PATH));
            Debug.Log($"{LOG_PREFIX} Done. {PROJECTILE_PREFAB_PATH} registered, {HELD_PREFAB_PATH} cleaned, NET_Player has GrenadeNetwork.");
        }

        return built;
    }

    // Lan dau: copy model (bo chot + lever) vao preview scene, dat pivot root o tam than, luu prefab
    private static bool CreateProjectileSkeleton()
    {
        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_PREFAB_PATH);
        if (modelPrefab == null)
        {
            Debug.LogError($"{LOG_PREFIX} {MODEL_PREFAB_PATH} not found.");
            return false;
        }

        // Preview scene: khong dung toi scene dang mo (MPPM nhai scene active - KI-23)
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = new GameObject(PROJECTILE_NAME);
            SceneManager.MoveGameObjectToScene(root, previewScene);
            GameObject visual = Object.Instantiate(modelPrefab, root.transform, false);
            visual.name = VISUAL_NAME;

            Transform body = FindDeep(visual.transform, BODY_NAME);
            if (body == null)
            {
                Debug.LogError($"{LOG_PREFIX} '{BODY_NAME}' not found under {MODEL_PREFAB_PATH}.");
                return false;
            }

            DestroyChild(visual.transform, PIN_NAME);
            DestroyChild(visual.transform, LEVER_NAME);
            visual.transform.position -= body.position;

            PrefabUtility.SaveAsPrefabAsset(root, PROJECTILE_PREFAB_PATH, out bool saved);
            if (!saved)
            {
                Debug.LogError($"{LOG_PREFIX} Could not save {PROJECTILE_PREFAB_PATH}.");
            }

            return saved;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static bool ConfigureProjectile()
    {
        Explosion explosionPrefab = LoadComponent<Explosion>(EXPLOSION_PREFAB_PATH);
        if (explosionPrefab == null)
        {
            Debug.LogError($"{LOG_PREFIX} {EXPLOSION_PREFAB_PATH} not found - run Tools/Test3D/Create Explosion Prefab first.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PROJECTILE_PREFAB_PATH);
        try
        {
            Transform visual = root.transform.Find(VISUAL_NAME);
            Transform body = visual != null ? FindDeep(visual, BODY_NAME) : null;
            if (body == null)
            {
                Debug.LogError($"{LOG_PREFIX} {PROJECTILE_PREFAB_PATH} needs '{VISUAL_NAME}' with a '{BODY_NAME}' child.");
                return false;
            }

            // AddComponent trong Editor tu khop capsule theo mesh cua Body (giong ban Held cu - KI-16)
            CapsuleCollider bodyCollider = GetOrAdd<CapsuleCollider>(body.gameObject);
            Rigidbody body3D = GetOrAdd<Rigidbody>(root);
            body3D.mass = PROJECTILE_MASS;
            body3D.linearDamping = 0f;
            body3D.angularDamping = PROJECTILE_ANGULAR_DAMPING;
            body3D.useGravity = true;
            body3D.isKinematic = false;
            body3D.interpolation = RigidbodyInterpolation.Interpolate;
            // Launch bat ContinuousDynamic tren server; client kinematic -> de Discrete tren prefab
            body3D.collisionDetectionMode = CollisionDetectionMode.Discrete;

            GetOrAdd<NetworkObject>(root);
            ConfigureTransform(GetOrAdd<NetworkTransform>(root));
            GetOrAdd<NetworkRigidbody>(root);
            GrenadeProjectile projectile = GetOrAdd<GrenadeProjectile>(root);

            SerializedObject serialized = new SerializedObject(projectile);
            serialized.FindProperty("projectileRigidbody").objectReferenceValue = body3D;
            serialized.FindProperty("bodyCollider").objectReferenceValue = bodyCollider;
            serialized.FindProperty("visualRoot").objectReferenceValue = visual.gameObject;
            serialized.FindProperty("groundMask").intValue = LayerMask.GetMask(Constants.LAYER_GROUND);
            serialized.FindProperty("minGroundNormalY").floatValue = MIN_GROUND_NORMAL_Y;
            serialized.FindProperty("explosionPrefab").objectReferenceValue = explosionPrefab;
            serialized.FindProperty("spinOnThrow").floatValue = SPIN_ON_THROW;
            serialized.FindProperty("maxFlightTime").floatValue = MAX_FLIGHT_TIME;
            serialized.FindProperty("killHeight").floatValue = KILL_HEIGHT;
            serialized.FindProperty("despawnDelay").floatValue = DESPAWN_DELAY;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PROJECTILE_PREFAB_PATH, out bool saved);
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Server-auth (mac dinh), dong bo vi tri + xoay du 3 truc vi qua lan xoay khi bay
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

    // Qua tren tay khong bay nua: bo Rigidbody root (Interpolate tren kinematic con cua tay lam lech - KI-16) va collider than
    private static bool CleanHeldGrenade()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HELD_PREFAB_PATH);
        try
        {
            Grenade grenade = root.GetComponent<Grenade>();
            Transform body = FindDeep(root.transform, BODY_NAME);
            if (grenade == null || body == null)
            {
                Debug.LogError($"{LOG_PREFIX} {HELD_PREFAB_PATH} needs a Grenade component and a '{BODY_NAME}' child.");
                return false;
            }

            Rigidbody rootRigidbody = root.GetComponent<Rigidbody>();
            if (rootRigidbody != null)
            {
                Object.DestroyImmediate(rootRigidbody, true);
            }

            CapsuleCollider bodyCollider = body.GetComponent<CapsuleCollider>();
            if (bodyCollider != null)
            {
                Object.DestroyImmediate(bodyCollider, true);
            }

            SerializedObject serialized = new SerializedObject(grenade);
            serialized.FindProperty("body").objectReferenceValue = body;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HELD_PREFAB_PATH, out bool saved);
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool WireNetPlayer()
    {
        GrenadeProjectile projectilePrefab = LoadComponent<GrenadeProjectile>(PROJECTILE_PREFAB_PATH);
        GameObject root = PrefabUtility.LoadPrefabContents(NetworkPlayerBuilder.NET_PLAYER_PREFAB_PATH);
        try
        {
            GrenadeThrower thrower = root.GetComponent<GrenadeThrower>();
            WeaponHolder holder = root.GetComponent<WeaponHolder>();
            if (thrower == null || holder == null || projectilePrefab == null)
            {
                Debug.LogError($"{LOG_PREFIX} NET_Player needs GrenadeThrower + WeaponHolder, and {PROJECTILE_PREFAB_PATH} needs GrenadeProjectile.");
                return false;
            }

            GrenadeNetwork grenadeNetwork = GetOrAdd<GrenadeNetwork>(root);
            SerializedObject serialized = new SerializedObject(grenadeNetwork);
            serialized.FindProperty("thrower").objectReferenceValue = thrower;
            serialized.FindProperty("holder").objectReferenceValue = holder;
            serialized.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PlayerNetwork playerNetwork = root.GetComponent<PlayerNetwork>();
            if (playerNetwork != null)
            {
                SerializedObject playerSerialized = new SerializedObject(playerNetwork);
                playerSerialized.FindProperty("grenades").objectReferenceValue = grenadeNetwork;
                playerSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayerBuilder.NET_PLAYER_PREFAB_PATH, out bool saved);
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T LoadComponent<T>(string prefabPath) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        return prefab != null ? prefab.GetComponent<T>() : null;
    }

    private static void DestroyChild(Transform parent, string childName)
    {
        Transform child = FindDeep(parent, childName);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Transform FindDeep(Transform parent, string childName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}
