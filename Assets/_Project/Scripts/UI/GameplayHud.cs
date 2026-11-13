using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayHud : MonoBehaviour
{
    private const string CONTROLS_SEPARATOR = "   |   ";
    private const string CONTROLS_MOVE = "WASD / Arrows: Move";
    private const string CONTROLS_SWITCH = "1 / 2: Switch weapon";
    private const string CONTROLS_GRENADE = "F / RMB: Pull pin   |   Hold LMB: Throw";
    private const string CONTROLS_SAVE = "F5: Save positions";
    private const string CONTROLS_BACK = "Esc: Back";
    private const string STATUS_EQUIPPED = "{0} equipped";
    private const string STATUS_GRENADE_READY = "Grenade ready - press F to pull the pin";
    private const string STATUS_PIN_PULLED = "Pin pulled - hold LMB to charge, release to throw";
    private const string STATUS_THROWN = "Grenade thrown!";

    [SerializeField] private TMP_Text controlsText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject chargeRoot;
    [SerializeField] private Image chargeFill;

    // Nhan vat local sinh luc chay -> LocalPlayerBinder truyen vao qua Bind, khong gan Inspector
    private WeaponHolder weaponHolder;
    private GrenadeThrower thrower;
    private Grenade trackedGrenade;
    private bool isSubscribed;

    void OnEnable()
    {
        Subscribe();
        SyncWithCurrentWeapon();
    }

    void OnDisable()
    {
        Unsubscribe();
        StopTrackingGrenade();
    }

    void Update()
    {
        if (thrower == null || chargeRoot == null)
        {
            return;
        }

        bool isCharging = thrower.IsCharging;
        if (chargeRoot.activeSelf != isCharging)
        {
            chargeRoot.SetActive(isCharging);
        }

        if (isCharging && chargeFill != null)
        {
            chargeFill.fillAmount = thrower.ChargeNormalized;
        }
    }

    public void Bind(WeaponHolder holder, GrenadeThrower grenadeThrower)
    {
        Unbind();
        weaponHolder = holder;
        thrower = grenadeThrower;
        if (isActiveAndEnabled)
        {
            Subscribe();
        }

        // Rebuild da phat WeaponEquipped truoc khi HUD duoc bind -> tu doc trang thai hien tai
        SyncWithCurrentWeapon();
    }

    public void Unbind()
    {
        Unsubscribe();
        StopTrackingGrenade();
        weaponHolder = null;
        thrower = null;
        if (chargeRoot != null)
        {
            chargeRoot.SetActive(false);
        }

        UpdateControls(false);
        SetStatus(string.Empty);
    }

    private void Subscribe()
    {
        if (isSubscribed || weaponHolder == null)
        {
            return;
        }

        weaponHolder.WeaponEquipped += OnWeaponEquipped;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed)
        {
            return;
        }

        if (!ReferenceEquals(weaponHolder, null))
        {
            weaponHolder.WeaponEquipped -= OnWeaponEquipped;
        }

        isSubscribed = false;
    }

    private void SyncWithCurrentWeapon()
    {
        if (weaponHolder != null && weaponHolder.CurrentInstance != null)
        {
            OnWeaponEquipped(weaponHolder.CurrentDefinition, weaponHolder.CurrentInstance);
            return;
        }

        UpdateControls(false);
    }

    private void OnWeaponEquipped(WeaponDefinition definition, AbstractWeapon instance)
    {
        StopTrackingGrenade();
        trackedGrenade = instance as Grenade;
        UpdateControls(trackedGrenade != null);
        if (trackedGrenade != null)
        {
            trackedGrenade.PinPulled += OnGrenadePinPulled;
            trackedGrenade.Thrown += OnGrenadeThrown;
            SetStatus(STATUS_GRENADE_READY);
            return;
        }

        SetStatus(definition != null ? string.Format(STATUS_EQUIPPED, definition.DisplayName) : string.Empty);
    }

    private void OnGrenadePinPulled(Grenade grenade)
    {
        SetStatus(STATUS_PIN_PULLED);
    }

    // Huy dang ky ngay khi nem: qua tren tay bi an roi Destroy luc hoi qua moi
    private void OnGrenadeThrown(Grenade grenade)
    {
        SetStatus(STATUS_THROWN);
        StopTrackingGrenade();
    }

    private void StopTrackingGrenade()
    {
        if (ReferenceEquals(trackedGrenade, null))
        {
            return;
        }

        trackedGrenade.PinPulled -= OnGrenadePinPulled;
        trackedGrenade.Thrown -= OnGrenadeThrown;
        trackedGrenade = null;
    }

    // Chi hien phim dung voi loadout hien tai (kich ban 1 vu khi thi an phim doi)
    private void UpdateControls(bool isGrenadeEquipped)
    {
        if (controlsText == null)
        {
            return;
        }

        string controls = CONTROLS_MOVE;
        if (weaponHolder != null && weaponHolder.SlotCount > 1)
        {
            controls += CONTROLS_SEPARATOR + CONTROLS_SWITCH;
        }

        if (isGrenadeEquipped)
        {
            controls += CONTROLS_SEPARATOR + CONTROLS_GRENADE;
        }

        controlsText.text = controls + CONTROLS_SEPARATOR + CONTROLS_SAVE + CONTROLS_SEPARATOR + CONTROLS_BACK;
    }

    // ScenarioPositionRecorder bao ket qua luu toa do (F5) len dong trang thai
    public void ShowMessage(string message)
    {
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
