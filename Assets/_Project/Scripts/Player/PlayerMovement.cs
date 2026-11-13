using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private const float DIRECTION_EPSILON_SQR = 0.0001f;

    [SerializeField] private InputReader inputReader;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickVelocity = -2f;

    private Vector2 moveInput;
    private float verticalVelocity;

    public bool IsMoving { get; private set; }
    public Vector3 Velocity { get; private set; }

    void Awake()
    {
        if (inputReader == null || characterController == null)
        {
#if UNITY_EDITOR
            string missingFields = inputReader == null ? nameof(inputReader) : string.Empty;
            if (characterController == null)
            {
                missingFields += missingFields.Length > 0 ? ", " + nameof(characterController) : nameof(characterController);
            }

            Debug.LogError($"{nameof(PlayerMovement)} on '{name}' is missing Inspector reference(s): {missingFields}. Component disabled.", this);
#endif
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.MoveChanged += OnMoveChanged;
        }
    }

    void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.MoveChanged -= OnMoveChanged;
        }

        moveInput = Vector2.zero;
        IsMoving = false;
        Velocity = Vector3.zero;
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        Vector3 direction = CalculateMoveDirection();
        UpdateVerticalVelocity();

        Vector3 frameVelocity = direction * moveSpeed + Vector3.up * verticalVelocity;
        characterController.Move(frameVelocity * Time.deltaTime);
        Velocity = characterController.velocity;

        IsMoving = direction.sqrMagnitude > DIRECTION_EPSILON_SQR;
        if (IsMoving)
        {
            RotateTowards(direction);
        }
    }

    public void SetCameraTransform(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    private void OnMoveChanged(Vector2 input)
    {
        moveInput = input;
    }

    private Vector3 CalculateMoveDirection()
    {
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }

        Vector3 direction = forward * moveInput.y + right * moveInput.x;
        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void UpdateVerticalVelocity()
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            // Giu luc ep nho de isGrounded on dinh khi xuong doc
            verticalVelocity = groundedStickVelocity;
            return;
        }

        verticalVelocity += gravity * Time.deltaTime;
    }

    private void RotateTowards(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
}
