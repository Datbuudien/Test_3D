using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayHud : MonoBehaviour
{
    private const string CONTROLS_TEXT = "WASD / Arrows: Move   |   1: AK-47   2: Grenade   |   F / RMB: Pull pin   |   Hold LMB: Throw   |   Esc: Back";
    private const string STATUS_EQUIPPED = "{0} equipped";
    private const string STATUS_GRENADE_READY = "Grenade ready - press F to pull the pin";
    private const string STATUS_PIN_PULLED = "Pin pulled - hold LMB to charge, release to throw";
    private const string STATUS_THROWN = "Grenade thrown!";

    [SerializeField] private WeaponHolder weaponHolder;
    [SerializeField] private GrenadeThrower thrower;
    [SerializeField] private TMP_Text controlsText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject chargeRoot;
    [SerializeField] private Image chargeFill;

    private Grenade trackedGrenade;

    void OnEnable()
    {
        if (controlsText != null)
        {
            controlsText.text = CONTROLS_TEXT;
        }

        if (weaponHolder != null)
        {
            weaponHolder.WeaponEquipped += OnWeaponEquipped;
        }
    }

    void OnDisable()
    {
        if (weaponHolder != null)
        {
            weaponHolder.WeaponEquipped -= OnWeaponEquipped;
        }

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

    private void OnWeaponEquipped(WeaponDefinition definition, AbstractWeapon instance)
    {
        StopTrackingGrenade();
        trackedGrenade = instance as Grenade;
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

    // Huy dang ky ngay khi nem: luu dan se bi Destroy luc no
    private void OnGrenadeThrown(Grenade grenade)
    {
        SetStatus(STATUS_THROWN);
        StopTrackingGrenade();
    }

    private void StopTrackingGrenade()
    {
        if (trackedGrenade == null)
        {
            return;
        }

        trackedGrenade.PinPulled -= OnGrenadePinPulled;
        trackedGrenade.Thrown -= OnGrenadeThrown;
        trackedGrenade = null;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
