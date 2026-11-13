using UnityEngine;
using UnityEngine.Rendering.Universal;

// Mot o trong pool vet no. DecalProjector chieu theo truc +Z -> huong chieu = -normal, hop chieu nam giua diem no (pivot 0)
public class ScorchDecal : MonoBehaviour
{
    private const float PARALLEL_THRESHOLD = 0.99f;

    [SerializeField] private DecalProjector projector;

    public bool IsConfigured => projector != null;

    public void Show(ScorchMark mark)
    {
        Vector3 normal = mark.Normal.sqrMagnitude > Mathf.Epsilon ? mark.Normal.normalized : Vector3.up;
        Vector3 reference = Mathf.Abs(normal.y) < PARALLEL_THRESHOLD ? Vector3.up : Vector3.forward;
        Quaternion facing = Quaternion.LookRotation(-normal, reference);
        transform.SetPositionAndRotation(mark.Position, Quaternion.AngleAxis(mark.RotationAngle, normal) * facing);
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    public void Hide()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }
}
