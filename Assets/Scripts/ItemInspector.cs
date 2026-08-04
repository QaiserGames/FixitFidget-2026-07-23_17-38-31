using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class ItemInspector : MonoBehaviour
{
    [SerializeField] private Transform inspectPoint;
    [SerializeField] private CinemachineCamera inspectCam;
    [SerializeField] private float rotateSpeed = 0.4f;
    [SerializeField] private float pickupRange = 4f;
    [SerializeField] private LayerMask pickupMask = ~0;
    [SerializeField] private float scrubPower = 1.5f;
    [SerializeField] private float benchReach = 3f;

    private InspectableItem heldItem;
    private InspectableItem currentHover;
    private BenchInteractable currentBenchHover;
    private ToolType currentTool = ToolType.Hand;
    private ToolPickup currentToolPickup;
    private Camera cam;
    private PlayerInteractor interaction;

    public bool IsHoldingItem => heldItem != null;

    private void Awake()
    {
        cam = Camera.main;
        interaction = GetComponent<PlayerInteractor>();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (heldItem == null && inspectCam.Priority > 0)
        {
            inspectCam.Priority = 0;
            ClearTool();
            RelockCursor();
        }

        if (heldItem != null && !interaction.IsAtStation)
        {
            PutItemBack();
            return;
        }

        if (heldItem == null)
        {
            HandlePickup(mouse);
        }
        else
        {
            HandleBench(mouse);
        }
    }

    // ---------- picking an item up off the counter ----------

    private void HandlePickup(Mouse mouse)
    {
        if (!interaction.IsAtStation)
        {
            SetHover(null);
            return;
        }

        Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = cam.ScreenPointToRay(centre);

        InspectableItem found = null;
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupMask))
            found = hit.collider.GetComponentInParent<InspectableItem>();

        SetHover(found);

        if (found != null && mouse.leftButton.wasPressedThisFrame)
        {
            SetHover(null);
            found.RememberPose();
            heldItem = found;
            heldItem.transform.position = inspectPoint.position;
            inspectCam.Priority = 30;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ---------- working on it ----------

    private void HandleBench(Mouse mouse)
    {
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        // What's under the cursor?
        BenchInteractable target = null;
        GrimeSpot grime = null;

        if (Physics.Raycast(ray, out RaycastHit hit, benchReach))
        {
            target = hit.collider.GetComponent<BenchInteractable>();
            grime = hit.collider.GetComponent<GrimeSpot>();

            // Only highlight things we could actually act on with this tool.
            if (target != null && !(target.CanInteract &&
                (currentTool == target.RequiredTool || target.RequiredTool == ToolType.Hand)))
                target = null;
        }

        SetBenchHover(target);

        // Left click: tool pickup takes priority, then bench actions.
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(ray, out RaycastHit toolHit, 2f) &&
                toolHit.collider.TryGetComponent(out ToolPickup pickup))
            {
                if (currentToolPickup != null) currentToolPickup.SetSelected(false);
                currentToolPickup = pickup;
                currentTool = pickup.tool;
                pickup.SetSelected(true);
            }
            else if (target != null)
            {
                target.Activate();
            }
        }

        // Hold to scrub or rotate.
        if (mouse.leftButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();

            if (currentTool == ToolType.Brush)
            {
                if (grime != null) grime.Scrub(delta.magnitude * scrubPower);
            }
            else if (currentTool == ToolType.Hand)
            {
                heldItem.transform.Rotate(cam.transform.up, -delta.x * rotateSpeed, Space.World);
                heldItem.transform.Rotate(cam.transform.right, delta.y * rotateSpeed, Space.World);
            }
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (currentTool != ToolType.Hand) ClearTool();
            else PutItemBack();
        }
    }

    // ---------- helpers ----------

    private void PutItemBack()
    {
        if (heldItem != null) heldItem.ReturnToPose();
        heldItem = null;
        inspectCam.Priority = 0;
        SetBenchHover(null);
        ClearTool();
        RelockCursor();
    }

    private void ClearTool()
    {
        currentTool = ToolType.Hand;
        if (currentToolPickup != null) currentToolPickup.SetSelected(false);
        currentToolPickup = null;
    }

    private void RelockCursor()
    {
        if (!interaction.IsAtStation) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetHover(InspectableItem item)
    {
        if (currentHover == item) return;
        if (currentHover != null) currentHover.SetHovered(false);
        currentHover = item;
        if (currentHover != null) currentHover.SetHovered(true);
    }

    private void SetBenchHover(BenchInteractable item)
    {
        if (currentBenchHover == item) return;
        if (currentBenchHover != null) currentBenchHover.SetHighlight(false);
        currentBenchHover = item;
        if (currentBenchHover != null) currentBenchHover.SetHighlight(true);
    }
}