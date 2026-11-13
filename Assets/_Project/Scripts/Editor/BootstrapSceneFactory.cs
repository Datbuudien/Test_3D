using Unity.Netcode;
using Unity.Netcode.Editor.Configuration;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene Bootstrap: NetworkManager + UnityTransport + NetworkSession + BootstrapLoader. Dung lai tu dau moi lan chay
internal static class BootstrapSceneFactory
{
    private const string LOG_PREFIX = "[BootstrapSceneFactory]";
    private const string FALLBACK_PREFABS_LIST_PATH = "Assets/_Project/Data/NetworkPrefabs.asset";
    // Mac dinh UTP thu 60 lan x 1 s -> Join sai IP phai cho 1 phut moi bao loi
    private const int CONNECT_TIMEOUT_MS = 500;
    private const int MAX_CONNECT_ATTEMPTS = 10;

    internal static void Build(string scenePath, string[] characterPaths, string[] weaponPaths)
    {
        Scene scene = ProjectSceneBuilder.CreateEmptyScene(scenePath);
        // Nap asset SAU khi tao scene (KI-19)
        CharacterDefinition[] characters = ProjectSceneBuilder.LoadAssetsAtPaths<CharacterDefinition>(characterPaths);
        WeaponDefinition[] weapons = ProjectSceneBuilder.LoadAssetsAtPaths<WeaponDefinition>(weaponPaths);
        NetworkPrefabsList prefabsList = LoadOrCreatePrefabsList();

        GameObject managerObject = new GameObject("NetworkManager");
        NetworkManager networkManager = managerObject.AddComponent<NetworkManager>();
        UnityTransport transport = managerObject.AddComponent<UnityTransport>();
        ConfigureTransport(transport);
        ConfigureNetworkManager(networkManager, transport, prefabsList);

        NetworkSession session = managerObject.AddComponent<NetworkSession>();
        SerializedObject serialized = new SerializedObject(session);
        serialized.FindProperty("networkManager").objectReferenceValue = networkManager;
        serialized.FindProperty("transport").objectReferenceValue = transport;
        AssignArray(serialized.FindProperty("characters"), characters);
        AssignArray(serialized.FindProperty("weapons"), weapons);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        managerObject.AddComponent<BootstrapLoader>();
        EditorSceneManager.SaveScene(scene, scenePath);
    }

    private static void ConfigureNetworkManager(NetworkManager networkManager, UnityTransport transport, NetworkPrefabsList prefabsList)
    {
        if (networkManager.NetworkConfig == null)
        {
            networkManager.NetworkConfig = new NetworkConfig();
        }

        NetworkConfig config = networkManager.NetworkConfig;
        config.NetworkTransport = transport;
        config.EnableSceneManagement = true;
        config.ConnectionApproval = true;
        // Player do server tu spawn trong Gameplay (N2) -> khong de NGO tu spawn o scene Setup
        config.PlayerPrefab = null;
        config.Prefabs.NetworkPrefabsLists.Clear();
        if (prefabsList != null)
        {
            config.Prefabs.NetworkPrefabsLists.Add(prefabsList);
        }

        EditorUtility.SetDirty(networkManager);
    }

    private static void ConfigureTransport(UnityTransport transport)
    {
        transport.SetConnectionData(Constants.DEFAULT_ADDRESS, Constants.DEFAULT_PORT, Constants.LISTEN_ADDRESS);
        transport.ConnectTimeoutMS = CONNECT_TIMEOUT_MS;
        transport.MaxConnectAttempts = MAX_CONNECT_ATTEMPTS;
        EditorUtility.SetDirty(transport);
    }

    // NGO tu tao + tu dien danh sach mac dinh moi prefab co NetworkObject -> dung chinh danh sach do (KI-21)
    internal static NetworkPrefabsList LoadOrCreatePrefabsList()
    {
        string defaultPath = NetworkPrefabProcessor.DefaultNetworkPrefabsPath;
        NetworkPrefabsList prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(defaultPath);
        if (prefabsList != null)
        {
            return prefabsList;
        }

        prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(FALLBACK_PREFABS_LIST_PATH);
        if (prefabsList != null)
        {
            return prefabsList;
        }

        Debug.LogWarning($"{LOG_PREFIX} Default network prefabs list not found at '{defaultPath}' - creating {FALLBACK_PREFABS_LIST_PATH}.");
        prefabsList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
        AssetDatabase.CreateAsset(prefabsList, FALLBACK_PREFABS_LIST_PATH);
        AssetDatabase.SaveAssets();
        return prefabsList;
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
