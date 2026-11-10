using UnityEngine;

public class GrenadeTrajectoryPreview : MonoBehaviour
{
    [SerializeField] private GrenadeThrower thrower;
    [SerializeField] private WeaponHolder weaponHolder;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform landingMarker;
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private int maxPoints = 40;
    [SerializeField] private float timeStep = 0.05f;

    private Vector3[] points;
    private bool isConfigured;

    void Awake()
    {
        isConfigured = thrower != null && weaponHolder != null && lineRenderer != null && maxPoints > 1;
        if (!isConfigured)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(GrenadeTrajectoryPreview)} on '{name}' needs Thrower, Weapon Holder and Line Renderer. Component disabled.", this);
#endif
            enabled = false;
            return;
        }

        points = new Vector3[maxPoints];
        Hide();
    }

    void LateUpdate()
    {
        AbstractWeapon heldWeapon = weaponHolder.CurrentInstance;
        if (!thrower.IsCharging || heldWeapon == null)
        {
            Hide();
            return;
        }

        // Dung chung cong thuc van toc voi luc nem that
        Vector3 velocity = thrower.ComputeThrowVelocity(thrower.ChargeNormalized);
        int pointCount = Simulate(heldWeapon.transform.position, velocity, out bool hasHit, out RaycastHit hit);

        lineRenderer.enabled = true;
        lineRenderer.positionCount = pointCount;
        lineRenderer.SetPositions(points);
        UpdateLandingMarker(hasHit, hit);
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
