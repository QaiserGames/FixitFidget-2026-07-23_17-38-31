using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Counter-only presentation. ItemInspector's fuse/bench path is unchanged.
public sealed class CounterRepairView : MonoBehaviour
{
    [SerializeField, Min(.2f)] private float confirmationSeconds = 1.2f;
    [SerializeField, Range(.3f, .65f)] private float screenHeight = .48f;
    [SerializeField, Min(.1f)] private float viewDistance = .65f;
    private readonly List<CinemachineInputAxisController> cameraReaders = new();
    private PlayerInteractor player;
    private CustomerBrain customer;
    private HumanFault fault;
    private GameObject display;
    private CounterPhoneModel model;
    private Canvas overlay;
    private CanvasScaler overlayScaler;
    private TMP_Text caption, instruction;
    private Camera cam;
    private AudioSource speaker;
    private AudioClip ringtone;
    private float readyAt, returnAt, openedAt, modelHeight, modelWidth;
    private Vector3 modelBaseScale, modelOffset;
    private bool hovered;
    private int closedFrame = -1;
    public bool IsOpen => display != null;
    public bool OwnsInput => IsOpen || closedFrame == Time.frameCount;
    public string HoverName => hovered ? model.Switch.DisplayName : "";
    public string HoverAction => hovered ? "Left-click to turn sound on" : "";

    public bool Open(CustomerBrain owner)
    {
        player = GetComponent<PlayerInteractor>();
        cam = Camera.main;
        if (OwnsInput || owner == null || !owner.CanFixAtCounter || cam == null || player == null
            || !player.IsAtStation || player.CurrentStation.IsWorkSurface || Time.timeScale <= 0f) return false;
        customer = owner; fault = owner.HumanConversation;
        display = new GameObject("Counter phone inspection");
        if (fault.PresentationPrefab != null)
        {
            model = Instantiate(fault.PresentationPrefab, display.transform);
            if (!model.IsValid)
            {
                Debug.LogError("Counter phone prefab needs a toggle with a collider and a switch slider.", fault);
                Close(); return false;
            }
        }
        else
        {
            Material source = null;
            foreach (var renderer in fault.Job.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.GetComponent<TMP_Text>() == null && renderer.sharedMaterial != null)
                { source = renderer.sharedMaterial; break; }
            if (source == null)
            {
                Debug.LogError("Counter repair needs a device renderer/material for its prototype.", fault);
                Close(); return false;
            }
            model = CounterPhoneModel.CreatePrototype(display.transform, source);
        }
        model.Bind(fault, owner);
        Bounds bounds = model.VisualBounds();
        modelHeight = Mathf.Max(.01f, bounds.size.y); modelWidth = Mathf.Max(.01f, bounds.size.x);
        modelBaseScale = model.transform.localScale;
        modelOffset = model.transform.localPosition - display.transform.InverseTransformPoint(bounds.center);
        foreach (var input in FindObjectsByType<CinemachineInputAxisController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (input.enabled) { cameraReaders.Add(input); input.enabled = false; }
        BuildOverlay(owner);
        speaker = display.AddComponent<AudioSource>(); speaker.playOnAwake = false; speaker.volume = .16f;
        ringtone = RepairAudio.MakeTone("Phone confirmation", false); speaker.clip = ringtone;
        openedAt = Time.time; readyAt = Time.time + .25f;
        returnAt = fault.Finished ? Time.time + .2f : float.PositiveInfinity;
        PositionDisplay();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        return true;
    }

    private void BuildOverlay(CustomerBrain owner)
    {
        // A separate canvas keeps the caption upright while the phone moves.
        overlay = RepairOverlayUI.Canvas("Counter repair caption", null, 55);
        overlayScaler = overlay.GetComponent<CanvasScaler>();
        var panel = RepairOverlayUI.Panel("Caption", overlay.transform, Vector2.zero, new Vector2(480, 74), RepairOverlayUI.Background).rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0); panel.pivot = new Vector2(.5f, 0);
        panel.anchoredPosition = new Vector2(0, 88);
        caption = RepairOverlayUI.Text("Customer", panel, new Vector2(18, -5), new Vector2(444, 28), 23, Color.white);
        caption.text = owner.CustomerName + " · phone";
        instruction = RepairOverlayUI.Text("Action", panel, new Vector2(18, -37), new Vector2(444, 26), 19, RepairOverlayUI.Muted);
        instruction.text = "Click switch · Right-click put down · F step back";
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (customer == null || customer.IsLeaving || fault == null || !player.IsAtStation
            || (DayClock.Instance != null && DayClock.Instance.DayOver)) { Close(); return; }
        if (Time.timeScale <= 0f) { if (speaker != null) speaker.Pause(); return; }
        if (speaker != null) speaker.UnPause();
        model.Show(fault.Finished);
        if (fault.Finished)
        {
            caption.text = "Sound restored"; caption.color = RepairOverlayUI.Mint;
            instruction.text = "Returning the phone";
            if (Time.time >= returnAt)
            {
                var owner = customer; var job = fault.Job;
                Close(); owner.CompleteCounterRepair(job);
            }
            return;
        }
        var mouse = Mouse.current; var keys = Keyboard.current;
        if (Time.time < readyAt) return;
        if ((mouse != null && mouse.rightButton.wasPressedThisFrame) || (keys != null && keys.escapeKey.wasPressedThisFrame)) { Close(); return; }
        hovered = mouse != null && model.Switch.HitTarget.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out _, Mathf.Max(viewDistance, cam.nearClipPlane + .3f) + 2f);
        model.Switch.SetHighlight(hovered);
        if (hovered && mouse.leftButton.wasPressedThisFrame && model.Switch.CanInteract)
        {
            model.Switch.Activate(); model.Show(true);
            hovered = false; speaker.Play();
            returnAt = Time.time + Mathf.Max(.2f, confirmationSeconds);
        }
    }

    private void LateUpdate() { if (IsOpen) PositionDisplay(); }
    private void PositionDisplay()
    {
        float distance = Mathf.Max(viewDistance, cam.nearClipPlane + .3f);
        float height = cam.orthographic ? cam.orthographicSize * 2 : 2 * distance * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
        float scale = Mathf.Min(height * screenHeight / modelHeight, height * cam.aspect * .52f / modelWidth);
        float entrance = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - openedAt) / .2f));
        // The model moves into the view; input waits until that motion settles.
        Vector3 position = cam.transform.TransformPoint(new Vector3(0, -height * (.035f + (1 - entrance) * .08f), distance));
        float ringMotion = fault.Finished ? Mathf.Sin((Time.time - returnAt) * 30) * .6f : 0f;
        display.transform.SetPositionAndRotation(position, cam.transform.rotation * Quaternion.Euler(0, -12, -3 + ringMotion));
        // Preserve an imported model's unit conversion and centre its visible mesh.
        model.transform.localScale = modelBaseScale * scale;
        model.transform.localPosition = modelOffset * scale;
        if (overlayScaler != null)
            overlayScaler.scaleFactor = Mathf.Clamp(Mathf.Min(Screen.width / 1600f, Screen.height / 900f), .8f, 1.6f);
    }

    public void Close()
    {
        if (IsOpen) closedFrame = Time.frameCount;
        if (display != null) { display.SetActive(false); Destroy(display); }
        if (overlay != null) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); }
        overlay = null; overlayScaler = null;
        display = null; model = null; fault = null; customer = null; hovered = false;
        foreach (var input in cameraReaders) if (input != null) input.enabled = true;
        cameraReaders.Clear();
        if (ringtone != null) Destroy(ringtone);
        if (player != null && player.IsAtStation && !(DayClock.Instance != null && DayClock.Instance.DayOver))
        { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }
    private void OnDisable() => Close();
}
