using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Player trong Gameplay -> prefab NET_Player (NetworkObject + NetworkTransform owner + PlayerNetwork), xoa khoi scene,
// tao NetworkPlayerSpawner + 2 SpawnPoint. Chay lai: prefab da co thi chi cap nhat (giu GUID), spawn point giu nguyen
internal static class NetworkPlayerBuilder
{
    private const string MENU_BUILD = "Tools/Test3D/Build Network Player";
    private const string LOG_PREFIX = "[NetworkPlayerBuilder]";
    internal const string NET_PLAYER_PREFAB_PATH = "Assets/_Project/Prefabs/Characters/NET_Player.prefab";
    private const string CAMERA_TARGET_NAME = "CameraTarget";
    private const string SPAWNER_NAME = "NetworkPlayerSpawner";
    private const string SPAWN_POINT_NAME = "SpawnPoint_{0}";
    private const float SECOND_SPAWN_OFFSET = 2f;
    private const float GROUND_PROBE_HEIGHT = 2f;
    private const float GROUND_PROBE_DISTANCE = 6f;
    private static readonly Vector3 DEFAULT_CAMERA_TARGET_OFFSET = new Vector3(0f, 1.8f, 0f);
    // Capsule khop nguoi ~1.85 m, goc model o long ban chan (KI-31)
    private const float CAPSULE_HEIGHT = 1.8f;
    private const float CAPSULE_RADIUS = 0.3f;
    private const float CAPSULE_SKIN_WIDTH = 0.03f;
    private const float CAPSULE_STEP_OFFSET = 0.35f;
    private const float UNITY_DEFAULT_HEIGHT = 2f;
    private const float UNITY_DEFAULT_RADIUS = 0.5f;
    private const float FLOAT_TOLERANCE = 0.0001f;

    [MenuItem(MENU_BUILD)]
    private static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (!ProjectSceneBuilder.TryCollectCatalogPaths(out string[] characterPaths, out string[] weaponPaths))
        {
            return;
        }

        bool built = Build(ProjectSceneBuilder.GAMEPLAY_SCENE_PATH, characterPaths, weaponPaths)
            && GameplaySceneWiring.Wire(ProjectSceneBuilder.GAMEPLAY_SCENE_PATH, ProjectSceneBuilder.TRAJECTORY_MATERIAL_PATH,
                ProjectSceneBuilder.LOCAL_STORE_PROVIDER_PATH, characterPaths);
        EditorUtility.DisplayDialog("Test3D Builder", built ? "NET_Player built and Gameplay wired." : "Build failed - see Console.", "OK");
    }

    internal static bool Build(string scenePath, string[] characterPaths, string[] weaponPaths)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        PlayerMovement scenePlayer = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        bool hasScenePlayer = scenePlayer != null;
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        if (hasScenePlayer)
        {
            GameObject playerObject = scenePlayer.gameObject;
            spawnPosition = playerObject.transform.position;
            spawnRotation = Quaternion.Euler(0f, playerObject.transform.eulerAngles.y, 0f);
            PrefabUtility.SaveAsPrefabAsset(playerObject, NET_PLAYER_PREFAB_PATH, out bool saved);
            if (!saved)
            {
                Debug.LogError($"{LOG_PREFIX} Could not save {NET_PLAYER_PREFAB_PATH}.");
                return false;
            }

            Object.DestroyImmediate(playerObject);
        }
        else if (AssetDatabase.LoadAssetAtPath<GameObject>(NET_PLAYER_PREFAB_PATH) == null)
        {
            Debug.LogError($"{LOG_PREFIX} Gameplay has no Player and {NET_PLAYER_PREFAB_PATH} does not exist.");
            return false;
        }

        if (!ConfigurePrefab(characterPaths, weaponPaths))
        {
            return false;
        }

        // Nap prefab SAU moi thao tac scene/prefab (KI-19)
        GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(NET_PLAYER_PREFAB_PATH);
        PlayerNetwork prefab = prefabObject.GetComponent<PlayerNetwork>();
        RegisterNetworkPrefab(prefabObject);
        WireSpawner(prefab, hasScenePlayer, spawnPosition, spawnRotation);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    private static bool ConfigurePrefab(string[] characterPaths, string[] weaponPaths)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(NET_PLAYER_PREFAB_PATH);
        try
        {
            CharacterDefinition[] characters = ProjectSceneBuilder.LoadAssetsAtPaths<CharacterDefinition>(characterPaths);
            WeaponDefinition[] weapons = ProjectSceneBuilder.LoadAssetsAtPaths<WeaponDefinition>(weaponPaths);
            InputReader inputReader = root.GetComponent<InputReader>();
            PlayerMovement movement = root.GetComponent<PlayerMovement>();
            GrenadeThrower thrower = root.GetComponent<GrenadeThrower>();
            WeaponHolder holder = root.GetComponent<WeaponHolder>();
            CharacterController characterController = root.GetComponent<CharacterController>();
            if (inputReader == null || movement == null || thrower == null || holder == null || characterController == null)
            {
                Debug.LogError($"{LOG_PREFIX} NET_Player root needs InputReader, PlayerMovement, GrenadeThrower, WeaponHolder and CharacterController.");
                return false;
            }

            NetworkObject networkObject = GetOrAdd<NetworkObject>(root);
            ConfigureTransform(GetOrAdd<NetworkTransform>(root));
            PlayerSpawner spawner = GetOrAdd<PlayerSpawner>(root);
            PlayerNetwork playerNetwork = GetOrAdd<PlayerNetwork>(root);
            Transform cameraTarget = EnsureCameraTarget(root.transform);

            SerializedObject holderSerialized = new SerializedObject(holder);
            SerializedObject spawnerSerialized = new SerializedObject(spawner);
            AssignArray(spawnerSerialized.FindProperty("characters"), characters);
            AssignArray(spawnerSerialized.FindProperty("weapons"), weapons);
            spawnerSerialized.FindProperty("currentVisual").objectReferenceValue = holderSerialized.FindProperty("characterVisual").objectReferenceValue;
            spawnerSerialized.FindProperty("weaponHolder").objectReferenceValue = holder;
            spawnerSerialized.FindProperty("visualParent").objectReferenceValue = root.transform;
            spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject networkSerialized = new SerializedObject(playerNetwork);
            networkSerialized.FindProperty("inputReader").objectReferenceValue = inputReader;
            networkSerialized.FindProperty("movement").objectReferenceValue = movement;
            networkSerialized.FindProperty("thrower").objectReferenceValue = thrower;
            networkSerialized.FindProperty("holder").objectReferenceValue = holder;
            networkSerialized.FindProperty("spawner").objectReferenceValue = spawner;
            networkSerialized.FindProperty("characterController").objectReferenceValue = characterController;
            networkSerialized.FindProperty("cameraTarget").objectReferenceValue = cameraTarget;
            networkSerialized.ApplyModifiedPropertiesWithoutUndo();

            // Camera lay luc chay qua LocalPlayerBinder; tham chieu scene cu khong con hop le trong prefab
            SerializedObject movementSerialized = new SerializedObject(movement);
            movementSerialized.FindProperty("cameraTransform").objectReferenceValue = null;
            movementSerialized.ApplyModifiedPropertiesWithoutUndo();

            // KI-21: chi owner bat 3 component nay trong OnNetworkSpawn
            inputReader.enabled = false;
            movement.enabled = false;
            thrower.enabled = false;
            characterController.enabled = true;
            FitCapsuleIfDefault(characterController);
            EditorUtility.SetDirty(networkObject);

            PrefabUtility.SaveAsPrefabAsset(root, NET_PLAYER_PREFAB_PATH, out bool saved);
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Owner-authoritative (KI-21): giu PlayerMovement + CharacterController, may local khong tre
    private static void ConfigureTransform(NetworkTransform networkTransform)
    {
        networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        networkTransform.SyncPositionX = true;
        networkTransform.SyncPositionY = true;
        networkTransform.SyncPositionZ = true;
        networkTransform.SyncRotAngleX = false;
        networkTransform.SyncRotAngleY = true;
        networkTransform.SyncRotAngleZ = false;
        networkTransform.SyncScaleX = false;
        networkTransform.SyncScaleY = false;
        networkTransform.SyncScaleZ = false;
        networkTransform.InLocalSpace = false;
        networkTransform.Interpolate = true;
        EditorUtility.SetDirty(networkTransform);
    }

    // Mac dinh Unity: center 0, height 2 -> day capsule thap hon goc 1 m -> nhan vat lo lung 1 m.
    // Chi sua khi con mac dinh: chinh tay tren Inspector roi chay lai menu se KHONG bi ghi de
    private static void FitCapsuleIfDefault(CharacterController characterController)
    {
        bool isUnityDefault = Mathf.Abs(characterController.height - UNITY_DEFAULT_HEIGHT) < FLOAT_TOLERANCE
            && Mathf.Abs(characterController.radius - UNITY_DEFAULT_RADIUS) < FLOAT_TOLERANCE
            && characterController.center.sqrMagnitude < FLOAT_TOLERANCE;
        if (!isUnityDefault)
        {
            return;
        }

        characterController.height = CAPSULE_HEIGHT;
        characterController.radius = CAPSULE_RADIUS;
        characterController.skinWidth = CAPSULE_SKIN_WIDTH;
        characterController.stepOffset = CAPSULE_STEP_OFFSET;
        // Controller giu khe skinWidth duoi day capsule -> cong them de long ban chan cham dat
        characterController.center = new Vector3(0f, CAPSULE_HEIGHT * 0.5f + CAPSULE_SKIN_WIDTH, 0f);
        EditorUtility.SetDirty(characterController);
    }

    private static Transform EnsureCameraTarget(Transform root)
    {
        Transform cameraTarget = root.Find(CAMERA_TARGET_NAME);
        if (cameraTarget != null)
        {
            return cameraTarget;
        }

        cameraTarget = new GameObject(CAMERA_TARGET_NAME).transform;
        cameraTarget.SetParent(root, false);
        cameraTarget.localPosition = DEFAULT_CAMERA_TARGET_OFFSET;
        return cameraTarget;
    }

    // NGO thuong tu them prefab co NetworkObject vao list mac dinh; them tay de chac chan (KI-21)
    internal static void RegisterNetworkPrefab(GameObject prefabObject)
    {
        NetworkPrefabsList prefabsList = BootstrapSceneFactory.LoadOrCreatePrefabsList();
        if (prefabObject == null || prefabsList == null || prefabsList.Contains(prefabObject))
        {
            return;
        }

        prefabsList.Add(new NetworkPrefab { Prefab = prefabObject });
        EditorUtility.SetDirty(prefabsList);
        AssetDatabase.SaveAssets();
    }

    private static void WireSpawner(PlayerNetwork prefab, bool hasScenePlayer, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        NetworkPlayerSpawner spawner = Object.FindAnyObjectByType<NetworkPlayerSpawner>(FindObjectsInactive.Include);
        // Player vua duoc chuyen thanh prefab -> dat lai spawn point theo vi tri cua no
        if (spawner != null && hasScenePlayer)
        {
            Object.DestroyImmediate(spawner.gameObject);
            spawner = null;
        }

        if (spawner == null)
        {
            if (!hasScenePlayer)
            {
                Debug.LogWarning($"{LOG_PREFIX} No previous Player position - spawn points placed at the origin, move them in the scene.");
            }

            GameObject spawnerObject = new GameObject(SPAWNER_NAME);
            spawner = spawnerObject.AddComponent<NetworkPlayerSpawner>();
            CreateSpawnPoint(spawnerObject.transform, 1, spawnPosition, spawnRotation);
            Vector3 secondPosition = SnapToGround(spawnPosition + spawnRotation * Vector3.right * SECOND_SPAWN_OFFSET);
            CreateSpawnPoint(spawnerObject.transform, 2, secondPosition, spawnRotation);
        }

        Transform spawnerTransform = spawner.transform;
        Transform[] spawnPoints = new Transform[spawnerTransform.childCount];
        for (int index = 0; index < spawnPoints.Length; index++)
        {
            spawnPoints[index] = spawnerTransform.GetChild(index);
        }

        SerializedObject serialized = new SerializedObject(spawner);
        serialized.FindProperty("playerPrefab").objectReferenceValue = prefab;
        AssignArray(serialized.FindProperty("spawnPoints"), spawnPoints);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateSpawnPoint(Transform parent, int number, Vector3 position, Quaternion rotation)
    {
        Transform spawnPoint = new GameObject(string.Format(SPAWN_POINT_NAME, number)).transform;
        spawnPoint.SetParent(parent, false);
        spawnPoint.SetPositionAndRotation(position, rotation);
    }

    // Diem lech 2 m co the roi vao doc / bac them -> ha xuong mat Ground
    private static Vector3 SnapToGround(Vector3 position)
    {
        int groundMask = LayerMask.GetMask(Constants.LAYER_GROUND);
        Vector3 origin = position + Vector3.up * GROUND_PROBE_HEIGHT;
        return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GROUND_PROBE_DISTANCE, groundMask, QueryTriggerInteraction.Ignore)
            ? hit.point
            : position;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void AssignArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
    }
}
