using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour
{
    private const string MAP_PLAYER = "Player";
    private const string ACTION_MOVE = "Move";
    private const string ACTION_PULL_PIN = "PullPin";
    private const string ACTION_THROW = "Throw";
    private const string ACTION_WEAPON_1 = "Weapon1";
    private const string ACTION_WEAPON_2 = "Weapon2";
    private const string ACTION_BACK = "Back";
    private const int WEAPON_SLOT_1 = 0;
    private const int WEAPON_SLOT_2 = 1;

    [SerializeField] private InputActionAsset actions;

    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction pullPinAction;
    private InputAction throwAction;
    private InputAction weapon1Action;
    private InputAction weapon2Action;
    private InputAction backAction;

    public event Action<Vector2> MoveChanged;
    public event Action BackPressed;
    public event Action PullPinPressed;
    public event Action ThrowStarted;
    public event Action ThrowReleased;
    public event Action<int> WeaponSlotSelected;

    public Vector2 Move { get; private set; }

    void Awake()
    {
        if (actions == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(InputReader)} on '{name}' has no InputActionAsset assigned.", this);
#endif
            enabled = false;
            return;
        }

        playerMap = actions.FindActionMap(MAP_PLAYER, throwIfNotFound: true);
        moveAction = playerMap.FindAction(ACTION_MOVE, throwIfNotFound: true);
        pullPinAction = playerMap.FindAction(ACTION_PULL_PIN, throwIfNotFound: true);
        throwAction = playerMap.FindAction(ACTION_THROW, throwIfNotFound: true);
        weapon1Action = playerMap.FindAction(ACTION_WEAPON_1, throwIfNotFound: true);
        weapon2Action = playerMap.FindAction(ACTION_WEAPON_2, throwIfNotFound: true);
        backAction = playerMap.FindAction(ACTION_BACK, throwIfNotFound: true);
    }

    void OnEnable()
    {
        if (playerMap == null)
        {
            return;
        }

        moveAction.performed += OnMovePerformed;
        moveAction.canceled += OnMoveCanceled;
        pullPinAction.performed += OnPullPinPerformed;
        throwAction.started += OnThrowStarted;
        throwAction.canceled += OnThrowCanceled;
        weapon1Action.performed += OnWeapon1Performed;
        weapon2Action.performed += OnWeapon2Performed;
        backAction.performed += OnBackPerformed;
        playerMap.Enable();
    }

    void OnDisable()
    {
        if (playerMap == null)
        {
            return;
        }

        // Huy dang ky truoc khi Disable: Disable ban canceled cho phim dang giu -> ThrowReleased gia
        moveAction.performed -= OnMovePerformed;
        moveAction.canceled -= OnMoveCanceled;
        pullPinAction.performed -= OnPullPinPerformed;
        throwAction.started -= OnThrowStarted;
        throwAction.canceled -= OnThrowCanceled;
        weapon1Action.performed -= OnWeapon1Performed;
        weapon2Action.performed -= OnWeapon2Performed;
        backAction.performed -= OnBackPerformed;
        playerMap.Disable();
        Move = Vector2.zero;
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        Move = context.ReadValue<Vector2>();
        MoveChanged?.Invoke(Move);
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        Move = Vector2.zero;
        MoveChanged?.Invoke(Move);
    }

    private void OnPullPinPerformed(InputAction.CallbackContext context)
    {
        PullPinPressed?.Invoke();
    }

    private void OnThrowStarted(InputAction.CallbackContext context)
    {
        ThrowStarted?.Invoke();
    }

    private void OnThrowCanceled(InputAction.CallbackContext context)
    {
        ThrowReleased?.Invoke();
    }

    private void OnWeapon1Performed(InputAction.CallbackContext context)
    {
        WeaponSlotSelected?.Invoke(WEAPON_SLOT_1);
    }

    private void OnWeapon2Performed(InputAction.CallbackContext context)
    {
        WeaponSlotSelected?.Invoke(WEAPON_SLOT_2);
    }

    private void OnBackPerformed(InputAction.CallbackContext context)
    {
        BackPressed?.Invoke();
    }
}
