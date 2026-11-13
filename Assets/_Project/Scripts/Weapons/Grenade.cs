using System;
using UnityEngine;

// Luu dan TREN TAY: chi con cosmetic (khuy, lever, an khi nem). Qua dang bay la GrenadeProjectile do server mo phong (KI-26)
public class Grenade : AbstractWeapon
{
    [SerializeField] private Transform body;
    [SerializeField] private GrenadePin pin;
    [SerializeField] private GrenadePin lever;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pinPullClip;

    private bool isConfigured;

    public event Action<Grenade> PinPulled;
    public event Action<Grenade> Thrown;

    public GrenadeState State { get; private set; } = GrenadeState.Held;
    // Projectile dat pivot o tam than -> spawn dung cho qua tren tay dang nam
    public Vector3 ReleasePosition => isConfigured ? body.position : transform.position;
    public Quaternion ReleaseRotation => transform.rotation;

    void Awake()
    {
        isConfigured = body != null && pin != null;
        if (!isConfigured)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(Grenade)} on '{name}' needs Body and Pin assigned in the Inspector.", this);
#endif
        }
    }

    public bool TryPullPin(Vector3 ejectDirection)
    {
        if (!isConfigured || State != GrenadeState.Held)
        {
            return false;
        }

        State = GrenadeState.PinPulled;
        pin.Eject(ejectDirection);
        if (audioSource != null && pinPullClip != null)
        {
            audioSource.PlayOneShot(pinPullClip);
        }

        PinPulled?.Invoke(this);
        return true;
    }

    // Lever bat nguoc huong nem roi an qua tren tay; WeaponHolder.RespawnSlot huy instance nay khi hoi qua moi
    public bool TryThrow(Vector3 throwDirection)
    {
        if (!isConfigured || State != GrenadeState.PinPulled)
        {
            return false;
        }

        if (lever != null)
        {
            lever.Eject(-throwDirection.normalized);
        }

        State = GrenadeState.Thrown;
        Thrown?.Invoke(this);
        gameObject.SetActive(false);
        return true;
    }
}
