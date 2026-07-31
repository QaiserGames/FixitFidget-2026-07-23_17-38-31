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

    private InspectableItem heldItem;
    private InspectableItem currentHover;
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

        // SAFETY 1: the held item can be destroyed out from under us (customer stormed out).
        // Unity reports destroyed objects as null, so catch the orphaned camera here.
        if (heldItem == null && inspectCam.Priority > 0)
        {
            inspectCam.Priority = 0;
            ClearTool();
            RelockCursor();
        }

        // SAFETY 2: if we leave the station while holding something, put it down.
        if (heldItem != null && !interaction.IsAtStation)
        {
            PutItemBack();
            return;
        }

        if (heldItem == null)
        {
            // ---------- NOT HOLDING: look-based hover + pickup ----------

            if (!interaction.IsAtStation)
            {
                SetHover(null);
                return;
            }

            Vector2 screenCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray hoverRay = cam.ScreenPointToRay(screenCentre);

            InspectableItem found = null;
            if (Physics.Raycast(hoverRay, out RaycastHit hoverHit, pickupRange, pickupMask))
                found = hoverHit.collider.GetComponentInParent<InspectableItem>();

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
        else
        {
            // ---------- HOLDING: cursor-driven tools, rotate, scrub ----------

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

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
            }

            if (mouse.leftButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();

                if (currentTool == ToolType.Brush)
                {
                    if (Physics.Raycast(ray, out RaycastHit hit, 2f) &&
                        hit.collider.TryGetComponent(out GrimeSpot grime))
                    {
                        grime.Scrub(delta.magnitude * scrubPower);
                    }
                }
                else
                {
                    heldItem.transform.Rotate(cam.transform.up, -delta.x * rotateSpeed, Space.World);
                    heldItem.transform.Rotate(cam.transform.right, delta.y * rotateSpeed, Space.World);
                }
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (currentTool != ToolType.Hand)
                {
                    ClearTool();
                }
                else
                {
                    PutItemBack();
                }
            }
        }
    }

    private void PutItemBack()
    {
        if (heldItem != null) heldItem.ReturnToPose();
        heldItem = null;
        inspectCam.Priority = 0;
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
}