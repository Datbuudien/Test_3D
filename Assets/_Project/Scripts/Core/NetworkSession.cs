using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

// Nam cung GameObject NetworkManager trong scene Bootstrap -> song xuyen scene theo DontDestroyOnLoad cua NetworkManager
public class NetworkSession : MonoBehaviour
{
    private const string REASON_SESSION_FULL = "Session is full.";
    private const string REASON_TRANSPORT_FAILURE = "Network transport failure.";
    private const string REASON_START_FAILED = "Could not start the network session (port in use or invalid address).";
    private const string REASON_SCENE_LOAD_FAILED = "Could not load Gameplay: {0}";

    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;
    [SerializeField] private CharacterDefinition[] characters;
    [SerializeField] private WeaponDefinition[] weapons;
    [SerializeField] private int maxPlayers = 4;

    private readonly Dictionary<ulong, ConnectionPayload> payloads = new Dictionary<ulong, ConnectionPayload>();
    private bool isSubscribed;
    private bool isSessionActive;
    private bool isLeaving;
    private bool wasConnected;
    private bool closedByRemote;
    private bool hasRaisedEnding;
    private ConnectionStatus? forcedStopStatus;

    // Ngoai le singleton duy nhat (KI-22): song xuyen scene, Setup/Gameplay khong gan Inspector toi object cua Bootstrap duoc.
    // Instance chi dung cho LENH (Start/Leave) va truy van; thong bao phat qua EventManager (KI-34)
    public static NetworkSession Instance { get; private set; }

    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Idle;
    public string LastReason { get; private set; } = string.Empty;
    public bool IsBusy => isSessionActive;
    // Diem truy cap NetworkManager duy nhat cho code gameplay (khong dung NetworkManager.Singleton rai rac)
    public NetworkManager Manager => networkManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (networkManager == null || transport == null || FirstValid(characters) == null || FirstValid(weapons) == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(NetworkSession)} needs NetworkManager, UnityTransport and a non-empty character/weapon catalog.", this);
#endif
            enabled = false;
            return;
        }

        Instance = this;
        Subscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool StartHost(ConnectionPayload payload, ushort port)
    {
        if (!CanStart())
        {
            return false;
        }

        transport.SetConnectionData(Constants.DEFAULT_ADDRESS, port, Constants.LISTEN_ADDRESS);
        BeginSession(payload);
        if (!networkManager.StartHost())
        {
            FailStart(REASON_START_FAILED);
            return false;
        }

        wasConnected = true;
        SetStatus(ConnectionStatus.Connected);
        SceneEventProgressStatus loadStatus = networkManager.SceneManager.LoadScene(Constants.SCENE_GAMEPLAY, LoadSceneMode.Single);
        if (loadStatus != SceneEventProgressStatus.Started)
        {
            LastReason = string.Format(REASON_SCENE_LOAD_FAILED, loadStatus);
            forcedStopStatus = ConnectionStatus.Failed;
            networkManager.Shutdown();
            return false;
        }

        return true;
    }

    public bool StartClient(ConnectionPayload payload, string address, ushort port)
    {
        if (!CanStart())
        {
            return false;
        }

        transport.SetConnectionData(address, port);
        BeginSession(payload);
        if (!networkManager.StartClient())
        {
            FailStart(REASON_START_FAILED);
            return false;
        }

        return true;
    }

    // Shutdown cua NGO chay tre 1 frame -> ve Setup trong OnClientStopped/OnServerStopped, khong load scene khi NGO con song
    public void Leave()
    {
        if (isSessionActive && (networkManager.IsListening || networkManager.ShutdownInProgress))
        {
            RaiseSessionEnding();
            isLeaving = true;
            networkManager.Shutdown();
            return;
        }

        isSessionActive = false;
        SetStatus(ConnectionStatus.Idle);
        LoadSetupScene();
    }

    public bool TryGetPayload(ulong clientId, out ConnectionPayload payload)
    {
        return payloads.TryGetValue(clientId, out payload);
    }

    private bool CanStart()
    {
        return enabled && !isSessionActive && !networkManager.IsListening && !networkManager.ShutdownInProgress;
    }

    private void BeginSession(ConnectionPayload payload)
    {
        isSessionActive = true;
        isLeaving = false;
        wasConnected = false;
        closedByRemote = false;
        hasRaisedEnding = false;
        forcedStopStatus = null;
        LastReason = string.Empty;
        payloads.Clear();
        networkManager.NetworkConfig.ConnectionData = (payload ?? new ConnectionPayload()).ToBytes();
        SetStatus(ConnectionStatus.Connecting);
    }

    private void FailStart(string reason)
    {
        isSessionActive = false;
        payloads.Clear();
        LastReason = reason;
        SetStatus(ConnectionStatus.Failed);
    }

    private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // Player do server tu spawn trong Gameplay (N2), khong spawn luc approve
        response.CreatePlayerObject = false;
        response.Pending = false;
        if (payloads.Count >= maxPlayers)
        {
            response.Approved = false;
            response.Reason = REASON_SESSION_FULL;
            return;
        }

        // Khong tin client: id ngoai catalog -> thay bang lua chon mac dinh
        ConnectionPayload payload = ConnectionPayload.FromBytes(request.Payload);
        payload.characterId = ResolveCharacterId(payload.characterId);
        payload.weaponId = ResolveWeaponId(payload.weaponId);
        payloads[request.ClientNetworkId] = payload;
        response.Approved = true;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != networkManager.LocalClientId)
        {
            return;
        }

        wasConnected = true;
        SetStatus(ConnectionStatus.Connected);
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (networkManager.IsServer)
        {
            payloads.Remove(clientId);
            return;
        }

        if (clientId != networkManager.LocalClientId)
        {
            return;
        }

        // Doc ngay luc ngat: sau Shutdown NGO/transport se reset cac gia tri nay
        LastReason = networkManager.DisconnectReason ?? string.Empty;
        closedByRemote = transport.DisconnectEvent == NetworkTransport.DisconnectEvents.ClosedByRemote;
        // NGO goi callback nay TRUOC khi tu Shutdown -> object mang chua bi huy
        if (wasConnected)
        {
            RaiseSessionEnding();
        }
    }

    private void OnTransportFailure()
    {
        LastReason = REASON_TRANSPORT_FAILURE;
    }

    private void OnClientStopped(bool wasHost)
    {
        HandleSessionStopped(wasHost);
    }

    // Host nhan ca OnClientStopped lan OnServerStopped -> HandleSessionStopped chi xu ly lan dau
    private void OnServerStopped(bool wasHost)
    {
        HandleSessionStopped(true);
    }

    private void HandleSessionStopped(bool wasHost)
    {
        if (!isSessionActive)
        {
            return;
        }

        isSessionActive = false;
        payloads.Clear();
        SetStatus(ResolveStopStatus(wasHost));
        isLeaving = false;
        LoadSetupScene();
    }

    private ConnectionStatus ResolveStopStatus(bool wasHost)
    {
        if (forcedStopStatus.HasValue)
        {
            return forcedStopStatus.Value;
        }

        if (isLeaving)
        {
            return ConnectionStatus.Idle;
        }

        if (!wasConnected)
        {
            return ConnectionStatus.Failed;
        }

        if (wasHost)
        {
            return ConnectionStatus.Disconnected;
        }

        return closedByRemote ? ConnectionStatus.HostStopped : ConnectionStatus.Disconnected;
    }

    private void RaiseSessionEnding()
    {
        if (hasRaisedEnding)
        {
            return;
        }

        hasRaisedEnding = true;
        // RaiseImmediately: nguoi nghe phai chup toa do XONG truoc khi Shutdown huy nhan vat (KI-29)
        EventManager.RaiseImmediately(new SessionEndingEvent(networkManager.IsServer));
    }

    private void SetStatus(ConnectionStatus status)
    {
        Status = status;
        EventManager.Raise(new ConnectionStatusChangedEvent(status));
    }

    private static void LoadSetupScene()
    {
        if (SceneManager.GetActiveScene().name != Constants.SCENE_SCENARIO_SETUP)
        {
            SceneManager.LoadScene(Constants.SCENE_SCENARIO_SETUP);
        }
    }

    private string ResolveCharacterId(string characterId)
    {
        foreach (CharacterDefinition character in characters)
        {
            if (character != null && character.Id == characterId)
            {
                return characterId;
            }
        }

        return FirstValid(characters).Id;
    }

    private string ResolveWeaponId(string weaponId)
    {
        foreach (WeaponDefinition weapon in weapons)
        {
            if (weapon != null && weapon.Id == weaponId)
            {
                return weaponId;
            }
        }

        return FirstValid(weapons).Id;
    }

    private static T FirstValid<T>(T[] items) where T : UnityEngine.Object
    {
        if (items == null)
        {
            return null;
        }

        foreach (T item in items)
        {
            if (item != null)
            {
                return item;
            }
        }

        return null;
    }

    private void Subscribe()
    {
        networkManager.ConnectionApprovalCallback = OnConnectionApproval;
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
        networkManager.OnClientStopped += OnClientStopped;
        networkManager.OnServerStopped += OnServerStopped;
        networkManager.OnTransportFailure += OnTransportFailure;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        // Cung GameObject nen NetworkManager co the bi huy truoc: chi go khi object C# con
        if (!isSubscribed || ReferenceEquals(networkManager, null))
        {
            return;
        }

        networkManager.ConnectionApprovalCallback = null;
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        networkManager.OnClientStopped -= OnClientStopped;
        networkManager.OnServerStopped -= OnServerStopped;
        networkManager.OnTransportFailure -= OnTransportFailure;
        isSubscribed = false;
    }
}
