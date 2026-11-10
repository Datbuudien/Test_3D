using UnityEngine;

public class PreviewStage : MonoBehaviour
{
    private const float MIN_NEAR_CLIP = 0.01f;
    private const float NEAR_CLIP_RATIO = 0.05f;
    private const float FAR_CLIP_RATIO = 4f;

    [SerializeField] private Camera previewCamera;
    [SerializeField] private Transform pivot;
    [SerializeField] private Vector3 lookOffset;
    [SerializeField] private Vector3 viewDirection = new Vector3(0f, -0.2f, -1f);
    [SerializeField] private float rotationSpeed = 30f;

    private GameObject currentInstance;
    private int previewLayer;

    void Awake()
    {
        previewLayer = LayerMask.NameToLayer(Constants.LAYER_PREVIEW);
        if (previewCamera == null || pivot == null || previewLayer < 0)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(PreviewStage)} on '{name}' needs Preview Camera, Pivot and layer '{Constants.LAYER_PREVIEW}'. Component disabled.", this);
#endif
            enabled = false;
        }
    }

    void Update()
    {
        if (currentInstance != null)
        {
            pivot.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);
        }
    }

    public void Show(GameObject prefab, float cameraDistance)
    {
        if (!enabled)
        {
            return;
        }

        if (currentInstance != null)
        {
            Destroy(currentInstance);
            currentInstance = null;
        }

        if (prefab == null)
        {
            return;
        }

        currentInstance = Instantiate(prefab, pivot);
        currentInstance.transform.localPosition = Vector3.zero;
        currentInstance.transform.localRotation = Quaternion.identity;
        SetLayerRecursively(currentInstance.transform, previewLayer);
        PlaceCamera(cameraDistance);
    }

    private void PlaceCamera(float cameraDistance)
    {
        Vector3 target = pivot.position + lookOffset;
        previewCamera.transform.position = target - viewDirection.normalized * cameraDistance;
        previewCamera.transform.LookAt(target);
        previewCamera.nearClipPlane = Mathf.Max(MIN_NEAR_CLIP, cameraDistance * NEAR_CLIP_RATIO);
        previewCamera.farClipPlane = cameraDistance * FAR_CLIP_RATIO;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
        {
            SetLayerRecursively(child, layer);
        }
    }
}
