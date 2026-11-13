using System.Collections;
using UnityEngine;

// Dung chung cho khuy va lever: phan roi khoi than luu dan (cosmetic, moi may tu mo phong)
public class GrenadePin : MonoBehaviour
{
    [SerializeField] private Rigidbody pinRigidbody;
    [SerializeField] private Collider pinCollider;
    [SerializeField] private float ejectSpeed = 1.5f;
    [SerializeField] private float spinSpeed = 10f;
    [SerializeField] private float lifetimeAfterEject = 5f;
    // Lever bat ra chong len cho GrenadeProjectile vua spawn (object khac, co the o may khac) -> bat collider tre de khong day lech qua dang bay
    [SerializeField] private float colliderEnableDelay = 0.15f;

    private bool isConfigured;
    private bool isEjected;

    public Collider PinCollider => pinCollider;

    void Awake()
    {
        isConfigured = pinRigidbody != null && pinCollider != null;
        if (!isConfigured)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(GrenadePin)} on '{name}' needs Pin Rigidbody and Pin Collider assigned in the Inspector.", this);
#endif
            return;
        }

        pinRigidbody.isKinematic = true;
        pinCollider.enabled = false;
    }

    public void Eject(Vector3 sideDirection)
    {
        if (!isConfigured || isEjected)
        {
            return;
        }

        isEjected = true;
        transform.SetParent(null, true);
        pinRigidbody.isKinematic = false;
        // VelocityChange: khuy chi nang vai gram, Impulse se ban qua manh
        pinRigidbody.AddForce((sideDirection + Vector3.up).normalized * ejectSpeed, ForceMode.VelocityChange);
        pinRigidbody.AddTorque(Random.insideUnitSphere * spinSpeed, ForceMode.VelocityChange);
        StartCoroutine(EnableColliderAfterDelay());
        Destroy(gameObject, lifetimeAfterEject);
    }

    private IEnumerator EnableColliderAfterDelay()
    {
        yield return new WaitForSeconds(colliderEnableDelay);
        pinCollider.enabled = true;
    }
}
