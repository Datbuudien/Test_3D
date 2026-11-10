using System;
using UnityEngine;

public class WeaponHolder : MonoBehaviour
{
    private const int NO_SLOT = -1;

    [SerializeField] private InputReader inputReader;
    [SerializeField] private CharacterVisual characterVisual;
    [SerializeField] private WeaponDefinition[] loadout;
    [SerializeField] private int startSlot;

    private AbstractWeapon[] instances = Array.Empty<AbstractWeapon>();
    private bool isBuilt;

    public event Action<WeaponDefinition, AbstractWeapon> WeaponEquipped;

    public int CurrentSlot { get; private set; } = NO_SLOT;
    public bool IsSwitchLocked { get; private set; }
    public WeaponDefinition CurrentDefinition => IsValidSlot(CurrentSlot) ? loadout[CurrentSlot] : null;
    public AbstractWeapon CurrentInstance => IsValidSlot(CurrentSlot) ? instances[CurrentSlot] : null;

    void Awake()
    {
        if (inputReader == null || characterVisual == null || characterVisual.RightHandSocket == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(WeaponHolder)} on '{name}' needs InputReader, CharacterVisual and its RightHandSocket assigned in the Inspector. Component disabled.", this);
#endif
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.WeaponSlotSelected += OnWeaponSlotSelected;
        }
    }

    void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.WeaponSlotSelected -= OnWeaponSlotSelected;
        }
    }

    // Start (khong Awake) de PlayerSpawner kip goi Rebuild truoc
    void Start()
    {
        if (isBuilt)
        {
            return;
        }

        BuildLoadout();
        Equip(startSlot);
    }

    public void Rebuild(CharacterVisual visual, WeaponDefinition[] newLoadout, int slotToEquip)
    {
        DestroyHeldInstances();
        characterVisual = visual;
        loadout = newLoadout;
        CurrentSlot = NO_SLOT;
        IsSwitchLocked = false;
        BuildLoadout();
        Equip(slotToEquip);
    }

    public void Equip(int slot)
    {
        if (!IsValidSlot(slot) || slot == CurrentSlot || IsSwitchLocked || instances[slot] == null)
        {
            return;
        }

        AbstractWeapon previous = CurrentInstance;
        if (previous != null && IsHeld(previous))
        {
            previous.gameObject.SetActive(false);
        }

        CurrentSlot = slot;
        instances[slot].gameObject.SetActive(true);
        WeaponEquipped?.Invoke(loadout[slot], instances[slot]);
    }

    // Goi sau khi luu dan da roi tay: instance cu thuoc ve vat ly, chi tao instance moi
    public void RespawnSlot(int slot)
    {
        if (!IsValidSlot(slot) || loadout[slot] == null)
        {
            return;
        }

        AbstractWeapon previous = instances[slot];
        if (previous != null && IsHeld(previous))
        {
            Destroy(previous.gameObject);
        }

        instances[slot] = SpawnHeldInstance(loadout[slot]);
        if (slot != CurrentSlot || instances[slot] == null)
        {
            return;
        }

        instances[slot].gameObject.SetActive(true);
        WeaponEquipped?.Invoke(loadout[slot], instances[slot]);
    }

    public void SetSwitchLocked(bool locked)
    {
        IsSwitchLocked = locked;
    }

    private void OnWeaponSlotSelected(int slot)
    {
        Equip(slot);
    }

    private void BuildLoadout()
    {
        int slotCount = loadout != null ? loadout.Length : 0;
        instances = new AbstractWeapon[slotCount];
        for (int slot = 0; slot < slotCount; slot++)
        {
            instances[slot] = loadout[slot] != null ? SpawnHeldInstance(loadout[slot]) : null;
        }

        isBuilt = true;
    }

    private AbstractWeapon SpawnHeldInstance(WeaponDefinition definition)
    {
        if (definition.HeldPrefab == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(WeaponDefinition)} '{definition.name}' has no Held Prefab.", definition);
#endif
            return null;
        }

        AbstractWeapon instance = Instantiate(definition.HeldPrefab, characterVisual.RightHandSocket);
        Transform instanceTransform = instance.transform;
        instanceTransform.localPosition = Vector3.zero;
        instanceTransform.localRotation = Quaternion.identity;
        instanceTransform.localScale = Vector3.one;
        instance.gameObject.SetActive(false);
        return instance;
    }

    private void DestroyHeldInstances()
    {
        foreach (AbstractWeapon instance in instances)
        {
            if (instance != null && IsHeld(instance))
            {
                Destroy(instance.gameObject);
            }
        }

        instances = Array.Empty<AbstractWeapon>();
    }

    private bool IsHeld(AbstractWeapon instance)
    {
        return characterVisual != null && instance.transform.IsChildOf(characterVisual.transform);
    }

    private bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot < instances.Length && loadout != null && slot < loadout.Length;
    }
}
