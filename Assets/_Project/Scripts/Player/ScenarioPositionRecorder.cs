using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// Trong Gameplay. Ghi toa do moi nhan vat MAY NAY dang thay vao JSON kich ban: F5 + tu ghi khi roi phien (SessionEndingEvent).
// File co hau to role cua may dang luu (KI-21, KI-29). Luu qua IScenarioStore, khong new repository (KI-20).
// Khong cham NetworkSession: su kien roi phien nhan qua EventManager (KI-34)
public class ScenarioPositionRecorder : MonoBehaviour
{
    private const float CACHE_INTERVAL = 1f;
    private const string DEFAULT_SCENARIO_NAME = "Untitled";
    private const string MESSAGE_SAVED = "Positions saved ({0} players): {1}";
    private const string MESSAGE_FAILED = "Could not save positions: {0}";
    private const string MESSAGE_NO_PLAYERS = "No players to save yet.";
    private const string MESSAGE_MISSING_STORE = "No scenario store assigned - positions not saved.";

    [SerializeField] private AbstractScenarioStoreProvider storeProvider;
    [SerializeField] private CharacterDefinition[] characters;
    [SerializeField] private GameplayHud hud;

    private readonly List<PlayerSnapshot> snapshotBuffer = new List<PlayerSnapshot>();
    private IScenarioStore store;
    private InputReader inputReader;
    private PlayerSnapshot[] latestSnapshots = Array.Empty<PlayerSnapshot>();
    private WaitForSeconds cacheWait;
    private bool isManualSaveRunning;

    void Awake()
    {
        store = storeProvider != null ? storeProvider.CreateStore() : null;
        cacheWait = new WaitForSeconds(CACHE_INTERVAL);
    }

    void OnEnable()
    {
        EventManager.SubscribeTo<SessionEndingEvent>(OnSessionEnding);
        StartCoroutine(RefreshCachePeriodically());
    }

    void OnDisable()
    {
        EventManager.UnsubscribeFrom<SessionEndingEvent>(OnSessionEnding);
        Unbind();
    }

    public void Bind(InputReader reader)
    {
        Unbind();
        inputReader = reader;
        if (inputReader != null)
        {
            inputReader.SavePositionsPressed += OnSavePositionsPressed;
        }
    }

    public void Unbind()
    {
        if (!ReferenceEquals(inputReader, null))
        {
            inputReader.SavePositionsPressed -= OnSavePositionsPressed;
        }

        inputReader = null;
    }

    private async void OnSavePositionsPressed()
    {
        if (isManualSaveRunning)
        {
            return;
        }

        isManualSaveRunning = true;
        try
        {
            await SaveAsync(CaptureSnapshots(), IsLocalHost());
        }
        finally
        {
            isManualSaveRunning = false;
        }
    }

    // Phat truoc Shutdown -> chup NGAY trong handler (nhan vat con); da bi huy thi dung ban chup gan nhat.
    // Handler cua bus nhan ref -> khong async duoc (CS1988), viec luu chay o SaveInBackground
    private void OnSessionEnding(ref SessionEndingEvent eventData)
    {
        PlayerSnapshot[] snapshots = CaptureSnapshots();
        SaveInBackground(snapshots.Length > 0 ? snapshots : latestSnapshots, eventData.IsHost);
    }

    // async void co chu dich: diem vao bat dong bo cua handler su kien, loi khong bi nuot nhu Task bo di
    private async void SaveInBackground(PlayerSnapshot[] snapshots, bool isLocalHost)
    {
        await SaveAsync(snapshots, isLocalHost);
    }

    private async Task SaveAsync(PlayerSnapshot[] snapshots, bool isLocalHost)
    {
        if (store == null)
        {
            Report(MESSAGE_MISSING_STORE);
            return;
        }

        if (snapshots.Length == 0)
        {
            Report(MESSAGE_NO_PLAYERS);
            return;
        }

        string role = ScenarioData.GetRole(isLocalHost);
        ScenarioData data = ResolveBaseScenario().WithPlayers(role, snapshots);
        SaveResult result;
        try
        {
            result = await store.SaveAsync(data, true);
        }
        catch (Exception exception)
        {
            result = new SaveResult(SaveStatus.IoError, string.Empty, exception.Message);
        }

        // Roi phien -> scene doi trong luc await (store server): object da huy thi khong cham UI
        if (this == null)
        {
            return;
        }

        Report(result.IsSuccess
            ? string.Format(MESSAGE_SAVED, snapshots.Length, result.FilePath)
            : string.Format(MESSAGE_FAILED, string.IsNullOrEmpty(result.ErrorMessage) ? result.Status.ToString() : result.ErrorMessage));
    }

    private PlayerSnapshot[] CaptureSnapshots()
    {
        snapshotBuffer.Clear();
        DateTime now = DateTime.Now;
        foreach (PlayerNetwork player in PlayerNetwork.Spawned)
        {
            if (player == null)
            {
                continue;
            }

            Transform playerTransform = player.transform;
            snapshotBuffer.Add(PlayerSnapshot.Create(
                ScenarioData.GetRole(player.IsHostPlayer),
                (long)player.OwnerClientId,
                player.CharacterId,
                FindCharacterName(player.CharacterId),
                playerTransform.position,
                playerTransform.eulerAngles.y,
                now));
        }

        // Host (clientId 0) dung truoc cho de doc
        snapshotBuffer.Sort((left, right) => left.clientId.CompareTo(right.clientId));
        return snapshotBuffer.ToArray();
    }

    private IEnumerator RefreshCachePeriodically()
    {
        while (enabled)
        {
            yield return cacheWait;
            PlayerSnapshot[] snapshots = CaptureSnapshots();
            if (snapshots.Length > 0)
            {
                latestSnapshots = snapshots;
            }
        }
    }

    // ScenarioSession do man Setup dat truoc khi Host/Join (ca 2 may); thieu thi dung tu nhan vat local
    private ScenarioData ResolveBaseScenario()
    {
        if (ScenarioSession.Current != null)
        {
            return ScenarioSession.Current;
        }

        PlayerNetwork local = PlayerNetwork.Local;
        string characterId = local != null ? local.CharacterId : string.Empty;
        string weaponId = local != null ? local.WeaponId : string.Empty;
        return ScenarioData.Create(DEFAULT_SCENARIO_NAME, characterId, FindCharacterName(characterId), weaponId, weaponId, DateTime.Now);
    }

    // F5 chi co khi da Bind InputReader cua nhan vat local -> Local luon co; nhan vat cua host co OwnerClientId = server
    private static bool IsLocalHost()
    {
        PlayerNetwork local = PlayerNetwork.Local;
        return local != null && local.IsHostPlayer;
    }

    private string FindCharacterName(string characterId)
    {
        if (characters != null)
        {
            foreach (CharacterDefinition character in characters)
            {
                if (character != null && character.Id == characterId)
                {
                    return character.DisplayName;
                }
            }
        }

        return characterId;
    }

    private void Report(string message)
    {
        if (hud != null)
        {
            hud.ShowMessage(message);
        }

#if UNITY_EDITOR
        Debug.Log($"[{nameof(ScenarioPositionRecorder)}] {message}", this);
#endif
    }
}
