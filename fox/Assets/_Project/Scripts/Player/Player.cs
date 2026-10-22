using System;
using UnityEngine;

public class Player : KitchenObjectHolder
{
    public static Player Instance
    {
        get; private set;
    }
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private GameInput gameInput;
    [SerializeField] private LayerMask countersLayerMask;
    private BaseCounter selectedCounter;
    private WorldItemPickup selectedPickup;
    private Rigidbody body;
    public GameInput Input => gameInput;
    public bool IsWalking
    {
        get; private set;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;

    private void Awake()
    {
        Instance = this;
        body = GetComponent<Rigidbody>();
        if (gameInput == null)
        {
            Debug.LogError("Player 未配置 GameInput。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (gameInput != null)
            gameInput.OnInteractAction += HandleInteractAction;
    }

    private void OnDisable()
    {
        if (gameInput != null)
            gameInput.OnInteractAction -= HandleInteractAction;
        SetSelectedCounter(null);
        IsWalking = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update() => HandleInteraction();
    private void FixedUpdate() => HandleMovement();

    private void HandleInteractAction(object sender, EventArgs args)
    {
        if (GameplayPause.IsPaused || SceneLoader.IsLoading || (RunSession.Current != null && RunSession.Current.IsDead))
            return;
        if (FoxEscortQuest.Current != null && FoxEscortQuest.Current.TryInteract())
            return;
        if (selectedCounter != null)
            selectedCounter.Interact(this);
        else if (selectedPickup != null)
            selectedPickup.TryCollect(GetComponent<PlayerItemController>());
    }

    private void HandleMovement()
    {
        Vector3 direction = gameInput.GetMovementDirectionNormalized();
        if (RunSession.Current != null && RunSession.Current.IsDead)
            direction = Vector3.zero;
        IsWalking = direction != Vector3.zero;
        float multiplier = RunSession.Current != null ? RunSession.Current.BoostMultiplier : 1f;
        Vector3 position = (body != null ? body.position : transform.position) + direction * Time.fixedDeltaTime * moveSpeed * multiplier;
        if (body != null)
            body.MovePosition(position);
        else
            transform.position = position;
        if (IsWalking)
            transform.forward = Vector3.Slerp(transform.forward, direction, Time.fixedDeltaTime * rotationSpeed);
    }

    private void HandleInteraction()
    {
        BaseCounter counter = null;
        selectedPickup = null;
        int itemLayer = LayerMask.NameToLayer("WorldItem");
        if (!GameplayPause.IsPaused && Physics.Raycast(transform.position + Vector3.up * 0.8f, transform.forward,
                out RaycastHit hit, 2.5f, countersLayerMask | (itemLayer >= 0 ? 1 << itemLayer : 0)))
        {
            counter = hit.collider.GetComponentInParent<BaseCounter>();
            if (counter == null)
                selectedPickup = hit.collider.GetComponentInParent<WorldItemPickup>();
        }
        SetSelectedCounter(counter);
    }

    public void SetSelectedCounter(BaseCounter counter)
    {
        if (counter == selectedCounter)
            return;
        if (selectedCounter != null)
            selectedCounter.CancelSelect();
        if (counter != null)
            counter.SelectCounter();
        selectedCounter = counter;
    }
}
