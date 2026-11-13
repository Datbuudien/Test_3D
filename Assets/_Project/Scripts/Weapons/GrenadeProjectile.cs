using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Qua luu dan DANG BAY: server mo phong vat ly + quyet dinh no, client chi noi suy (NetworkTransform + NetworkRigidbody)
// -> moi may thay cung mot diem no (KI-26)
public class GrenadeProjectile : NetworkBehaviour
{
    private const float FLIGHT_CHECK_INTERVAL = 0.25f;

    [SerializeField] private Rigidbody projectileRigidbody;
    [SerializeField] private Collider bodyCollider;
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float minGroundNormalY = 0.6f;
    [SerializeField] private Explosion explosionPrefab;
    [SerializeField] private float spinOnThrow = 6f;
    [SerializeField] private float maxFlightTime = 10f;
    [SerializeField] private float killHeight = -50f;
    [SerializeField] private float despawnDelay = 0.2f;

    private bool isConfigured;
    private ScorchMarkRegistry scorchMarkRegistry;
    private bool hasDetonated;
    private bool hasPlayedExplosion;
    private WaitForSeconds flightCheckWait;
    private WaitForSeconds despawnWait;

    void Awake()
    {
        isConfigured = projectileRigidbody != null && bodyCollider != null && visualRoot != null;
        if (!isConfigured)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(GrenadeProjectile)} on '{name}' needs Rigidbody, Body Collider and Visual Root assigned in the Inspector.", this);
#endif
            return;
        }

        flightCheckWait = new WaitForSeconds(FLIGHT_CHECK_INTERVAL);
        despawnWait = new WaitForSeconds(despawnDelay);
    }

    // Logic chuyen nguyen tu Grenade (KI-16): chi no khi cham Ground voi mat huong len, tuong thi nay tiep
    void OnCollisionEnter(Collision collision)
    {
        if (!isConfigured || !IsServer || !IsSpawned || hasDetonated)
        {
            return;
        }

        if ((groundMask.value & (1 << collision.gameObject.layer)) == 0)
        {
            return;
        }

        for (int contactIndex = 0; contactIndex < collision.contactCount; contactIndex++)
        {
            ContactPoint contact = collision.GetContact(contactIndex);
            if (contact.normal.y >= minGroundNormalY)
            {
                Detonate(contact.point, contact.normal);
                return;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!isConfigured)
        {
            return;
        }

        // Client: NetworkRigidbody tu dat kinematic; tat collider de qua noi suy khong day chot/lever cosmetic
        bodyCollider.enabled = IsServer;
    }

    // Server goi ngay sau Spawn. Projectile la prefab runtime nen registry trong scene duoc truyen vao (KI-27)
    public void Launch(Vector3 velocity, Collider[] ignoredColliders, ScorchMarkRegistry registry)
    {
        if (!isConfigured || !IsServer)
        {
            return;
        }

        scorchMarkRegistry = registry;
        IgnoreCollisions(ignoredColliders);
        projectileRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        projectileRigidbody.linearVelocity = velocity;
        projectileRigidbody.angularVelocity = Random.insideUnitSphere * spinOnThrow;
        StartCoroutine(FlightTimeout());
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ExplodeRpc(Vector3 point, Vector3 normal)
    {
        if (!isConfigured || hasPlayedExplosion)
        {
            return;
        }

        hasPlayedExplosion = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // N6: diem no do server gui -> moi may in cung 1 toa do; lag = quang cach qua dang noi suy tren may nay toi diem no
        Debug.Log($"[{nameof(GrenadeProjectile)}] Explode at {point.ToString("F3")} (server: {IsServer}, visual lag: {Vector3.Distance(transform.position, point):0.000} m)", this);
#endif
        visualRoot.SetActive(false);
        if (explosionPrefab == null)
        {
            return;
        }

        Explosion explosion = Instantiate(explosionPrefab, point, Quaternion.FromToRotation(Vector3.up, normal));
        explosion.Init(IsServer);
    }

    private void Detonate(Vector3 point, Vector3 normal)
    {
        // Dat co truoc tien: nhieu contact trong cung frame khong duoc no 2 lan
        hasDetonated = true;
        StopBody();
        ExplodeRpc(point, normal);
        if (scorchMarkRegistry != null)
        {
            scorchMarkRegistry.AddMark(point, normal);
        }

        // Tre mot chut de RPC toi client truoc goi despawn
        StartCoroutine(DespawnAfterDelay());
    }

    private void StopBody()
    {
        bodyCollider.enabled = false;
        projectileRigidbody.linearVelocity = Vector3.zero;
        projectileRigidbody.angularVelocity = Vector3.zero;
        projectileRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        projectileRigidbody.isKinematic = true;
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
        while (!hasDetonated && elapsedTime < maxFlightTime && transform.position.y > killHeight)
        {
            yield return flightCheckWait;
            elapsedTime += FLIGHT_CHECK_INTERVAL;
        }

        if (hasDetonated || !IsSpawned)
        {
            yield break;
        }

        if (transform.position.y <= killHeight)
        {
            // Roi khoi map: huy tren moi may, khong no
            hasDetonated = true;
            NetworkObject.Despawn(true);
            yield break;
        }

        Detonate(transform.position, Vector3.up);
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return despawnWait;
        if (IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
