using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Chi server lam viec. MonoBehaviour thuong (khong can NetworkObject): khong co state nao phai dong bo
public class NetworkPlayerSpawner : MonoBehaviour
{
    [SerializeField] private PlayerNetwork playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private ScorchMarkRegistry scorchMarkRegistry;

    private readonly HashSet<ulong> spawnedClients = new HashSet<ulong>();
    private NetworkSession session;
    private NetworkManager networkManager;
    private NetworkSceneManager sceneManager;
    private int spawnCount;

    void Start()
    {
        if (playerPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(NetworkPlayerSpawner)} needs a player prefab and at least one spawn point.", this);
#endif
            enabled = false;
            return;
        }

        session = NetworkSession.Instance;
        if (session == null || session.Manager == null || !session.Manager.IsServer)
        {
            enabled = false;
            return;
        }

        networkManager = session.Manager;
        sceneManager = networkManager.SceneManager;
        sceneManager.OnSynchronizeComplete += OnClientSynchronized;
        sceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        // Host da o trong scene nay khi Start chay; client khac vao qua 2 callback tren (trung clientId bi chan)
        if (networkManager.IsHost)
        {
            SpawnPlayer(networkManager.LocalClientId);
        }
    }

    void OnDestroy()
    {
        // NGO huy SceneManager khi Shutdown -> giu tham chieu rieng de go event an toan
        if (sceneManager != null)
        {
            sceneManager.OnSynchronizeComplete -= OnClientSynchronized;
            sceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }

        if (!ReferenceEquals(networkManager, null))
        {
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // Client join muon: da tai xong scene host dang mo
    private void OnClientSynchronized(ulong clientId)
    {
        SpawnPlayer(clientId);
    }

    // Client da ket noi tu truoc va cung tai scene nay voi host
    private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName != gameObject.scene.name)
        {
            return;
        }

        foreach (ulong clientId in clientsCompleted)
        {
            SpawnPlayer(clientId);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // NGO tu huy player object cua client roi phien; chi can cho phep spawn lai neu ket noi lai
        spawnedClients.Remove(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (!networkManager.IsServer || spawnedClients.Contains(clientId) || !networkManager.ConnectedClients.ContainsKey(clientId))
        {
            return;
        }

        Transform spawnPoint = NextSpawnPoint();
        PlayerNetwork player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        if (session.TryGetPayload(clientId, out ConnectionPayload payload))
        {
            player.SetLoadout(payload.characterId, payload.weaponId);
        }

        // Projectile la prefab runtime, khong gan Inspector toi scene duoc -> truyen registry qua player (server only)
        if (player.Grenades != null)
        {
            player.Grenades.SetScorchMarkRegistry(scorchMarkRegistry);
        }

        // Vi tri da dat truoc Spawn -> goi spawn mang theo toa do nay, owner khong bi keo ve (0,0,0)
        player.NetworkObject.SpawnAsPlayerObject(clientId, true);
        spawnedClients.Add(clientId);
    }

    private Transform NextSpawnPoint()
    {
        for (int attempt = 0; attempt < spawnPoints.Length; attempt++)
        {
            Transform candidate = spawnPoints[(spawnCount + attempt) % spawnPoints.Length];
            if (candidate != null)
            {
                spawnCount += attempt + 1;
                return candidate;
            }
        }

        spawnCount++;
        return transform;
    }
}
