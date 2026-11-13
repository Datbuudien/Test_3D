using System;
using System.Collections;
using UnityEngine;

// Chi bat tren owner (KI-21). Nhan input, tinh luc nem, doi state qua tren tay; khong biet mang:
// GrenadeNetwork nghe PinPullPerformed / ThrowPerformed de gui RPC
public class GrenadeThrower : MonoBehaviour
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private WeaponHolder weaponHolder;
    [SerializeField] private Collider[] ownerColliders;
    [SerializeField] private float minThrowSpeed = 6f;
    [SerializeField] private float maxThrowSpeed = 18f;
    [SerializeField] private float maxChargeTime = 1.2f;
    [SerializeField] private float launchAngle = 25f;
    [SerializeField] private float respawnDelay = 1.5f;
    [SerializeField] private float pinEjectBackward = 0.5f;

    private Grenade currentGrenade;
    private float chargeStartTime;
    private WaitForSeconds respawnWait;

    public event Action<Vector3> PinPullPerformed;
    public event Action<Vector3, Quaternion, Vector3> ThrowPerformed;

    public bool IsCharging { get; private set; }
    public float ChargeNormalized => IsCharging ? CalculateCharge() : 0f;
    public float MaxThrowSpeed => maxThrowSpeed;
    public float RespawnDelay => respawnDelay;
    public Collider[] OwnerColliders => ownerColliders;

    void Awake()
    {
        if (inputReader == null || weaponHolder == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(GrenadeThrower)} on '{name}' needs InputReader and WeaponHolder assigned in the Inspector. Component disabled.", this);
#endif
            enabled = false;
            return;
        }

        respawnWait = new WaitForSeconds(respawnDelay);
    }

    void OnEnable()
    {
        if (inputReader == null || weaponHolder == null)
        {
            return;
        }

        inputReader.PullPinPressed += OnPullPinPressed;
        inputReader.ThrowStarted += OnThrowStarted;
        inputReader.ThrowReleased += OnThrowReleased;
        weaponHolder.WeaponEquipped += OnWeaponEquipped;
        // Bat sau khi loadout da dung (owner trong phien mang) -> lay luon vu khi dang cam
        currentGrenade = weaponHolder.CurrentInstance as Grenade;
    }

    void OnDisable()
    {
        if (inputReader == null || weaponHolder == null)
        {
            return;
        }

        inputReader.PullPinPressed -= OnPullPinPressed;
        inputReader.ThrowStarted -= OnThrowStarted;
        inputReader.ThrowReleased -= OnThrowReleased;
        weaponHolder.WeaponEquipped -= OnWeaponEquipped;
        IsCharging = false;
    }

    public Vector3 ComputeThrowVelocity(float charge)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 pitchAxis = Vector3.Cross(Vector3.up, flatForward);
        // Goc am quanh truc phai = ngang len tren
        Vector3 direction = Quaternion.AngleAxis(-launchAngle, pitchAxis) * flatForward;
        return direction * Mathf.Lerp(minThrowSpeed, maxThrowSpeed, Mathf.Clamp01(charge));
    }

    private void OnWeaponEquipped(WeaponDefinition definition, AbstractWeapon instance)
    {
        currentGrenade = instance as Grenade;
        IsCharging = false;
    }

    private void OnPullPinPressed()
    {
        // Khuy van sang phai va hoi lui ve phia camera de nhin thay tu sau lung
        Vector3 ejectDirection = (transform.right - transform.forward * pinEjectBackward).normalized;
        if (currentGrenade != null && currentGrenade.TryPullPin(ejectDirection))
        {
            weaponHolder.SetSwitchLocked(true);
            PinPullPerformed?.Invoke(ejectDirection);
        }
    }

    private void OnThrowStarted()
    {
        if (currentGrenade == null || currentGrenade.State != GrenadeState.PinPulled)
        {
            return;
        }

        IsCharging = true;
        chargeStartTime = Time.time;
    }

    private void OnThrowReleased()
    {
        if (!IsCharging || currentGrenade == null)
        {
            IsCharging = false;
            return;
        }

        Vector3 velocity = ComputeThrowVelocity(CalculateCharge());
        IsCharging = false;
        // Lay vi tri TRUOC TryThrow vi qua tren tay se bi an
        Vector3 origin = currentGrenade.ReleasePosition;
        Quaternion rotation = currentGrenade.ReleaseRotation;
        if (!currentGrenade.TryThrow(velocity))
        {
            return;
        }

        currentGrenade = null;
        ThrowPerformed?.Invoke(origin, rotation, velocity);
        StartCoroutine(RespawnAfterDelay(weaponHolder.CurrentSlot));
    }

    private float CalculateCharge()
    {
        return maxChargeTime > 0f ? Mathf.Clamp01((Time.time - chargeStartTime) / maxChargeTime) : 1f;
    }

    // Giu khoa doi vu khi toi khi co qua moi: doi sang slot luu dan luc dang trong se bat lai qua da an
    private IEnumerator RespawnAfterDelay(int slot)
    {
        yield return respawnWait;
        weaponHolder.SetSwitchLocked(false);
        weaponHolder.RespawnSlot(slot);
    }
}
