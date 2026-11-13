using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Tren NET_Player, canh PlayerNetwork. Owner: nghe GrenadeThrower -> gui RPC. Server: validate + spawn GrenadeProjectile.
// May khac: dien lai chot van / an qua tren tay / hoi qua moi (cosmetic). Tach khoi PlayerNetwork de moi class mot viec (KI-26)
public class GrenadeNetwork : NetworkBehaviour
{
    [SerializeField] private GrenadeThrower thrower;
    [SerializeField] private WeaponHolder holder;
    [SerializeField] private GrenadeProjectile projectilePrefab;
    [SerializeField] private float maxThrowOriginDistance = 3f;
    [SerializeField] private float throwSpeedTolerance = 0.5f;
    [SerializeField] private float cooldownTolerance = 0.3f;

    private ScorchMarkRegistry scorchMarkRegistry;
    private float lastThrowTime = float.NegativeInfinity;
    private WaitForSeconds remoteRespawnWait;
    private bool isListeningToThrower;

    public override void OnNetworkSpawn()
    {
        if (thrower == null || holder == null || projectilePrefab == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(GrenadeNetwork)} on '{name}' needs Thrower, Holder and Projectile Prefab assigned in the Inspector.", this);
#endif
            return;
        }

        remoteRespawnWait = new WaitForSeconds(thrower.RespawnDelay);
        if (IsOwner)
        {
            thrower.PinPullPerformed += OnOwnerPinPulled;
            thrower.ThrowPerformed += OnOwnerThrown;
            isListeningToThrower = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!isListeningToThrower)
        {
            return;
        }

        thrower.PinPullPerformed -= OnOwnerPinPulled;
        thrower.ThrowPerformed -= OnOwnerThrown;
        isListeningToThrower = false;
    }

    // Server: NetworkPlayerSpawner (trong scene) truyen vao truoc Spawn -> khong singleton, khong Find (KI-27)
    public void SetScorchMarkRegistry(ScorchMarkRegistry registry)
    {
        scorchMarkRegistry = registry;
    }

    private void OnOwnerPinPulled(Vector3 ejectDirection)
    {
        PullPinRpc(ejectDirection);
    }

    private void OnOwnerThrown(Vector3 origin, Quaternion rotation, Vector3 velocity)
    {
        ThrowRpc(origin, rotation, velocity);
    }

    [Rpc(SendTo.NotOwner, InvokePermission = RpcInvokePermission.Owner)]
    private void PullPinRpc(Vector3 ejectDirection)
    {
        Grenade grenade = holder.CurrentInstance as Grenade;
        if (grenade != null)
        {
            grenade.TryPullPin(ejectDirection);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ThrowRpc(Vector3 origin, Quaternion rotation, Vector3 velocity, RpcParams rpcParams = default)
    {
        if (!IsValidThrow(origin, velocity, rpcParams.Receive.SenderClientId))
        {
#if UNITY_EDITOR
            Debug.LogWarning($"{nameof(GrenadeNetwork)}: rejected throw from client {rpcParams.Receive.SenderClientId} (origin {origin}, speed {velocity.magnitude:0.0}).", this);
#endif
            return;
        }

        lastThrowTime = Time.time;
        GrenadeProjectile projectile = Instantiate(projectilePrefab, origin, NormalizeOrIdentity(rotation));
        projectile.NetworkObject.Spawn(true);
        projectile.Launch(velocity, thrower.OwnerColliders, scorchMarkRegistry);
        ThrownRpc(velocity);
    }

    [Rpc(SendTo.NotOwner, InvokePermission = RpcInvokePermission.Server)]
    private void ThrownRpc(Vector3 velocity)
    {
        Grenade grenade = holder.CurrentInstance as Grenade;
        if (grenade == null)
        {
            return;
        }

        // Join muon luc owner da rut khuy -> ban nay chua rut: rut luon de TryThrow hop le
        if (grenade.State == GrenadeState.Held)
        {
            grenade.TryPullPin(transform.right);
        }

        int slot = holder.CurrentSlot;
        if (grenade.TryThrow(velocity))
        {
            StartCoroutine(RespawnRemoteAfterDelay(slot));
        }
    }

    // Rule security: khong tin client. Sender phai la owner, vi tri tay hop ly, toc do trong gioi han, khong nem nhanh hon toc do hoi qua
    private bool IsValidThrow(Vector3 origin, Vector3 velocity, ulong senderClientId)
    {
        if (senderClientId != OwnerClientId || !IsFinite(origin) || !IsFinite(velocity))
        {
            return false;
        }

        if ((origin - transform.position).sqrMagnitude > maxThrowOriginDistance * maxThrowOriginDistance)
        {
            return false;
        }

        if (velocity.magnitude > thrower.MaxThrowSpeed + throwSpeedTolerance)
        {
            return false;
        }

        if (Time.time - lastThrowTime < thrower.RespawnDelay - cooldownTolerance)
        {
            return false;
        }

        WeaponDefinition currentWeapon = holder.CurrentDefinition;
        return currentWeapon != null && currentWeapon.Type == WeaponType.Grenade;
    }

    private IEnumerator RespawnRemoteAfterDelay(int slot)
    {
        yield return remoteRespawnWait;
        holder.RespawnSlot(slot);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    private static Quaternion NormalizeOrIdentity(Quaternion rotation)
    {
        float lengthSquared = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        return float.IsFinite(lengthSquared) && lengthSquared > Mathf.Epsilon ? Quaternion.Normalize(rotation) : Quaternion.identity;
    }
}
