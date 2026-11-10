using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Grenade : AbstractWeapon
{
    private const float FLIGHT_CHECK_INTERVAL = 0.25f;

    [SerializeField] private Rigidbody grenadeRigidbody;
    [SerializeField] private Collider bodyCollider;
    [SerializeField] private GrenadePin pin;
    [SerializeField] private GrenadePin lever;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float minGroundNormalY = 0.6f;
    [SerializeField] private Explosion explosionPrefab;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pinPullClip;
    [SerializeField] private float spinOnThrow = 6f;
    [SerializeField] private float maxFlightTime = 10f;
    [SerializeField] private float killHeight = -50f;

    private bool isConfigured;
    private WaitForSeconds flightCheckWait;

    public event Action<Grenade> PinPulled;
    public event Action<Grenade> Thrown;
    public event Action<Vector3> Exploded;

    public GrenadeState State { get; private set; } = GrenadeState.Held;

    void Awake()
    {
        isConfigured = grenadeRigidbody != null && bodyCollider != null && pin != null;
        if (!isConfigured)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(Grenade)} on '{name}' needs Grenade Rigidbody, Body Collider and Pin assigned in the Inspector.", this);
#endif
            return;
        }

        flightCheckWait = new WaitForSeconds(FLIGHT_CHECK_INTERVAL);
        grenadeRigidbody.isKinematic = true;
        // Interpolate ghi de transform moi frame -> luu dan khong bam theo tay; chi bat khi nem (TryThrow)
        grenadeRigidbody.interpolation = RigidbodyInterpolation.None;
        bodyCollider.enabled = false;
    }

    // Unity van goi OnCollisionEnter khi script bi tat -> chan bang isConfigured + State
    void OnCollisionEnter(Collision collision)
    {
        if (!isConfigured || State != GrenadeState.Thrown)
        {
            return;
        }

        if ((groundMask.value & (1 << collision.gameObject.layer)) == 0)
        {
            return;
        }

        // Tuong cung thuoc layer Ground: chi no khi mat cham huong len (nen dat), va tuong thi nay tiep
        for (int contactIndex = 0; contactIndex < collision.contactCount; contactIndex++)
        {
            ContactPoint contact = collision.GetContact(contactIndex);
            if (contact.normal.y >= minGroundNormalY)
            {
                Explode(contact.point, contact.normal);
                return;
            }
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

    public bool TryThrow(Vector3 velocity, Collider[] ignoredColliders)
    {
        if (!isConfigured || State != GrenadeState.PinPulled)
        {
            return false;
        }

        transform.SetParent(null, true);
        bodyCollider.enabled = true;
        IgnoreCollisions(ignoredColliders);

        grenadeRigidbody.isKinematic = false;
        grenadeRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        grenadeRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        grenadeRigidbody.linearVelocity = velocity;
        grenadeRigidbody.angularVelocity = UnityEngine.Random.insideUnitSphere * spinOnThrow;

        if (lever != null)
        {
            lever.Eject(-velocity.normalized);
            // Goi sau khi ca 2 collider da bat; lever nam chong len than nen phai bo qua va cham
            if (lever.PinCollider != null)
            {
                Physics.IgnoreCollision(bodyCollider, lever.PinCollider, true);
            }
        }

        State = GrenadeState.Thrown;
        Thrown?.Invoke(this);
        StartCoroutine(FlightTimeout());
        return true;
    }

    private void IgnoreCollisions(Collider[] ignoredColliders)
    {
        if (ignoredColliders == null)
        {
            return;
        }

        foreach (Collider ignoredCollider in ignoredColliders)
        {
            if (ignoredCollider != null)
            {
                Physics.IgnoreCollision(bodyCollider, ignoredCollider, true);
            }
        }
    }

    private IEnumerator FlightTimeout()
    {
        float elapsedTime = 0f;
        while (State == GrenadeState.Thrown && elapsedTime < maxFlightTime && transform.position.y > killHeight)
        {
            yield return flightCheckWait;
            elapsedTime += FLIGHT_CHECK_INTERVAL;
        }

        if (State != GrenadeState.Thrown)
        {
            yield break;
        }

        if (transform.position.y <= killHeight)
        {
            State = GrenadeState.Exploded;
            Destroy(gameObject);
            yield break;
        }

        Explode(transform.position, Vector3.up);
    }

    private void Explode(Vector3 point, Vector3 normal)
    {
        // Dat state truoc tien de khong no 2 lan khi nhieu contact trong cung frame
        State = GrenadeState.Exploded;
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, point, Quaternion.FromToRotation(Vector3.up, normal));
        }

        Exploded?.Invoke(point);
        Destroy(gameObject);
    }
}
