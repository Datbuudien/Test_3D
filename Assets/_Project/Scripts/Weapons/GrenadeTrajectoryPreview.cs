using UnityEngine;

public class GrenadeTrajectoryPreview : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform landingMarker;
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private int maxPoints = 40;
    [SerializeField] private float timeStep = 0.05f;

    // Nhan vat local sinh luc chay -> LocalPlayerBinder truyen vao qua Bind
    private GrenadeThrower thrower;
    private WeaponHolder weaponHolder;
    private Vector3[] points;

    void Awake()
    {
        if (lineRenderer == null || maxPoints <= 1)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(GrenadeTrajectoryPreview)} on '{name}' needs a Line Renderer and Max Points > 1. Component disabled.", this);
#endif
            enabled = false;
            return;
        }

        points = new Vector3[maxPoints];
        Hide();
    }

    void LateUpdate()
    {
        if (thrower == null || weaponHolder == null)
        {
            Hide();
            return;
        }

        AbstractWeapon heldWeapon = weaponHolder.CurrentInstance;
        if (!thrower.IsCharging || heldWeapon == null)
        {
            Hide();
            return;
        }

        // Dung chung cong thuc van toc + diem xuat phat voi luc nem that (GrenadeProjectile spawn tai ReleasePosition)
        Vector3 velocity = thrower.ComputeThrowVelocity(thrower.ChargeNormalized);
        Grenade heldGrenade = heldWeapon as Grenade;
        Vector3 origin = heldGrenade != null ? heldGrenade.ReleasePosition : heldWeapon.transform.position;
        int pointCount = Simulate(origin, velocity, out bool hasHit, out RaycastHit hit);

        lineRenderer.enabled = true;
        lineRenderer.positionCount = pointCount;
        lineRenderer.SetPositions(points);
        UpdateLandingMarker(hasHit, hit);
    }

    public void Bind(GrenadeThrower grenadeThrower, WeaponHolder holder)
    {
        thrower = grenadeThrower;
        weaponHolder = holder;
    }

    public void Unbind()
    {
        thrower = null;
        weaponHolder = null;
        if (lineRenderer != null)
        {
            Hide();
        }
    }

    private int Simulate(Vector3 origin, Vector3 velocity, out bool hasHit, out RaycastHit hit)
    {
        hasHit = false;
        hit = default;
        points[0] = origin;
        for (int index = 1; index < maxPoints; index++)
        {
            float time = index * timeStep;
            Vector3 next = origin + velocity * time + 0.5f * time * time * Physics.gravity;
            if (Physics.Linecast(points[index - 1], next, out hit, collisionMask, QueryTriggerInteraction.Ignore))
            {
                points[index] = hit.point;
                hasHit = true;
                return index + 1;
            }

            points[index] = next;
        }

        return maxPoints;
    }

    private void UpdateLandingMarker(bool hasHit, RaycastHit hit)
    {
        if (landingMarker == null)
        {
            return;
        }

        landingMarker.gameObject.SetActive(hasHit);
        if (hasHit)
        {
            landingMarker.SetPositionAndRotation(hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
        }
    }

    private void Hide()
    {
        if (lineRenderer.enabled)
        {
            lineRenderer.enabled = false;
        }

        if (landingMarker != null && landingMarker.gameObject.activeSelf)
        {
            landingMarker.gameObject.SetActive(false);
        }
    }
}
