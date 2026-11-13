using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Root cua NET_Player. InputReader / PlayerMovement / GrenadeThrower TAT san tren prefab, chi owner bat (KI-21):
// 2 nhan vat trong cung 1 tien trinh dung chung InputActionAsset -> ban remote Disable map se tat input cua ban local
public class PlayerNetwork : NetworkBehaviour
{
    private static readonly List<PlayerNetwork> spawnedPlayers = new List<PlayerNetwork>();

    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private GrenadeThrower thrower;
    [SerializeField] private WeaponHolder holder;
    [SerializeField] private PlayerSpawner spawner;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private GrenadeNetwork grenades;

    private readonly NetworkVariable<FixedString64Bytes> characterId = new NetworkVariable<FixedString64Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString64Bytes> weaponId = new NetworkVariable<FixedString64Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> equippedSlot = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private string pendingCharacterId = string.Empty;
    private string pendingWeaponId = string.Empty;
    private bool hasPendingLoadout;

    public static event Action<PlayerNetwork> LocalPlayerSpawned;
    public static event Action<PlayerNetwork> LocalPlayerDespawned;

    public static PlayerNetwork Local { get; private set; }
    public static IReadOnlyList<PlayerNetwork> Spawned => spawnedPlayers;

    public Transform CameraTarget => cameraTarget;
    public InputReader InputReader => inputReader;
    public PlayerMovement Movement => movement;
    public GrenadeThrower Thrower => thrower;
    public WeaponHolder Holder => holder;
    public GrenadeNetwork Grenades => grenades;
    public CharacterController CharacterController => characterController;
    public bool IsHostPlayer => OwnerClientId == NetworkManager.ServerClientId;
    public string CharacterId => characterId.Value.ToString();
    public string WeaponId => weaponId.Value.ToString();

    // Enter Play Mode khong reload domain -> static con giu gia tri cua lan Play truoc
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        spawnedPlayers.Clear();
        Local = null;
        LocalPlayerSpawned = null;
        LocalPlayerDespawned = null;
    }

    // Server goi TRUOC Spawn. Ghi NetworkVariable truoc Spawn bi NGO canh bao (KI-23) -> giu tam, ghi trong OnNetworkSpawn
    public void SetLoadout(string newCharacterId, string newWeaponId)
    {
        pendingCharacterId = newCharacterId ?? string.Empty;
        pendingWeaponId = newWeaponId ?? string.Empty;
        hasPendingLoadout = true;
    }

    public override void OnNetworkSpawn()
    {
        if (!HasReferences())
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(PlayerNetwork)} on '{name}' is missing Inspector references.", this);
#endif
            return;
        }

        spawnedPlayers.Add(this);
        // Server ghi trong OnNetworkSpawn (truoc khi gui goi spawn) va TRUOC khi dang ky OnValueChanged -> chi Apply 1 lan
        if (IsServer && hasPendingLoadout)
        {
            characterId.Value = new FixedString64Bytes(pendingCharacterId);
            weaponId.Value = new FixedString64Bytes(pendingWeaponId);
            hasPendingLoadout = false;
        }

        characterId.OnValueChanged += OnLoadoutChanged;
        weaponId.OnValueChanged += OnLoadoutChanged;
        equippedSlot.OnValueChanged += OnEquippedSlotChanged;

        if (IsOwner)
        {
            // Bat truoc ApplyLoadout: GrenadeThrower phai subscribe WeaponEquipped truoc khi Rebuild phat event (KI-19)
            SetOwnerComponentsEnabled(true);
            holder.WeaponEquipped += OnOwnerWeaponEquipped;
        }

        ApplyLoadout();
        if (!IsOwner)
        {
            holder.Equip(equippedSlot.Value);
            return;
        }

        Local = this;
        LocalPlayerSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        spawnedPlayers.Remove(this);
        characterId.OnValueChanged -= OnLoadoutChanged;
        weaponId.OnValueChanged -= OnLoadoutChanged;
        equippedSlot.OnValueChanged -= OnEquippedSlotChanged;
        if (holder != null)
        {
            holder.WeaponEquipped -= OnOwnerWeaponEquipped;
        }

        if (IsOwner)
        {
            SetOwnerComponentsEnabled(false);
        }

        if (Local == this)
        {
            Local = null;
            LocalPlayerDespawned?.Invoke(this);
        }
    }

    private void ApplyLoadout()
    {
        spawner.Apply(CharacterId, WeaponId);
    }

    private void OnLoadoutChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        ApplyLoadout();
        if (!IsOwner)
        {
            holder.Equip(equippedSlot.Value);
        }
    }

    private void OnOwnerWeaponEquipped(WeaponDefinition definition, AbstractWeapon instance)
    {
        if (IsSpawned && holder.CurrentSlot >= 0 && equippedSlot.Value != holder.CurrentSlot)
        {
            equippedSlot.Value = holder.CurrentSlot;
        }
    }

    private void OnEquippedSlotChanged(int previous, int current)
    {
        if (!IsOwner)
        {
            holder.Equip(current);
        }
    }

    private void SetOwnerComponentsEnabled(bool isEnabled)
    {
        inputReader.enabled = isEnabled;
        movement.enabled = isEnabled;
        thrower.enabled = isEnabled;
    }

    private bool HasReferences()
    {
        return inputReader != null && movement != null && thrower != null && holder != null
            && spawner != null && characterController != null && cameraTarget != null;
    }
}
