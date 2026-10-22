using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInput : MonoBehaviour
{
    public event EventHandler OnInteractAction;
    public event Action UseRequested;
    public event Action ThrowRequested;
    public event Action InventoryRequested;
    public event Action StowRequested;
    public event Action CancelRequested;
    private GameControl gameControl;
    private InputAction useAction, throwAction, inventoryAction, stowAction, cancelAction;
    private InputActionMap uiMap;
    private InputAction dialogueAction, skipAction;
    public bool InteractHeld => dialogueAction != null ? dialogueAction.IsPressed() : Keyboard.current != null && Keyboard.current.eKey.isPressed;
    public bool DialogueAdvancePressed => dialogueAction != null ? dialogueAction.WasPressedThisFrame() : Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    public bool SkipHeld => skipAction != null ? skipAction.IsPressed() : Keyboard.current != null && Keyboard.current.escapeKey.isPressed;

    private void Awake()
    {
        gameControl = new GameControl();
        useAction = gameControl.asset.FindAction("Player/Use", true);
        throwAction = gameControl.asset.FindAction("Player/Throw", true);
        uiMap = gameControl.asset.FindActionMap("UI", true);
        inventoryAction = uiMap.FindAction("ToggleInventory", true);
        stowAction = uiMap.FindAction("Stow", true);
        cancelAction = uiMap.FindAction("Cancel", true);
        dialogueAction = uiMap.FindAction("DialogueAdvance");
        skipAction = uiMap.FindAction("SkipCinematic");
    }

    private void OnEnable()
    {
        gameControl.Player.Interact.performed += HandleInteractPerformed;
        useAction.performed += HandleUse;
        throwAction.performed += HandleThrow;
        inventoryAction.performed += HandleInventory;
        stowAction.performed += HandleStow;
        cancelAction.performed += HandleCancel;
        GameplayPause.Changed += UpdateActionMaps;
        uiMap.Enable();
        UpdateActionMaps();
    }

    private void UpdateActionMaps()
    {
        if (!isActiveAndEnabled || gameControl == null)
            return;
        if (GameplayPause.IsPaused)
            gameControl.Player.Disable();
        else
            gameControl.Player.Enable();
    }

    private void OnDisable()
    {
        if (gameControl == null)
            return;
        GameplayPause.Changed -= UpdateActionMaps;
        gameControl.Player.Interact.performed -= HandleInteractPerformed;
        useAction.performed -= HandleUse;
        throwAction.performed -= HandleThrow;
        inventoryAction.performed -= HandleInventory;
        stowAction.performed -= HandleStow;
        cancelAction.performed -= HandleCancel;
        gameControl.Disable();
    }

    private void OnDestroy()
    {
        if (gameControl == null)
            return;
        gameControl.Disable();
        gameControl.Dispose();
        gameControl = null;
        OnInteractAction = null;
        UseRequested = ThrowRequested = InventoryRequested = StowRequested = CancelRequested = null;
    }

    private void HandleInteractPerformed(InputAction.CallbackContext context) => OnInteractAction?.Invoke(this, EventArgs.Empty);
    private void HandleUse(InputAction.CallbackContext context) => UseRequested?.Invoke();
    private void HandleThrow(InputAction.CallbackContext context) => ThrowRequested?.Invoke();
    private void HandleInventory(InputAction.CallbackContext context) => InventoryRequested?.Invoke();
    private void HandleStow(InputAction.CallbackContext context) => StowRequested?.Invoke();
    private void HandleCancel(InputAction.CallbackContext context) => CancelRequested?.Invoke();
    public Vector2 GetPointerPosition() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    public Vector3 GetMovementDirectionNormalized()
    {
        if (gameControl == null || !isActiveAndEnabled || GameplayPause.IsPaused || SceneLoader.IsLoading)
            return Vector3.zero;
        Vector2 input = gameControl.Player.Move.ReadValue<Vector2>();
        return new Vector3(input.x, 0f, input.y).normalized;
    }
}
