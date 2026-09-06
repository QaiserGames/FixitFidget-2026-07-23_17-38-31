using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// Counter-only presentation. ItemInspector's fuse/bench path is unchanged.
public sealed class CounterRepairView : MonoBehaviour
{
    [SerializeField, Min(.2f)] private float confirmationSeconds = 1.2f;
    private readonly List<CinemachineInputAxisController> cameraReaders = new();
    private readonly List<Material> materials = new();
    private PlayerInteractor player;
    private CustomerBrain customer;
    private HumanFault fault;
    private GameObject display;
    private Transform slider;
    private Collider switchCollider;
    private Renderer switchRenderer;
    private PhysicalToggle physicalSwitch;
    private TMP_Text screen;
    private Camera cam;
    private AudioSource speaker;
    private AudioClip ringtone;
    private Material sourceMaterial;
    private float readyAt, returnAt;
    private bool hovered;
    private int closedFrame = -1;
    public bool IsOpen => display != null;
    public bool OwnsInput => IsOpen || closedFrame == Time.frameCount;
    public string HoverName => hovered ? "Mute switch" : "";
    public string HoverAction => hovered ? "Left-click to turn sound on" : "";

    public bool Open(CustomerBrain owner)
    {
        player = GetComponent<PlayerInteractor>();
        cam = Camera.main;
        if (OwnsInput || owner == null || !owner.CanFixAtCounter || cam == null || player == null
            || !player.IsAtStation || player.CurrentStation.IsWorkSurface || Time.timeScale <= 0f) return false;
        customer = owner;
        fault = owner.HumanConversation;
        var source = fault.Job.GetComponentInChildren<Renderer>(true);
        if (source == null || source.sharedMaterial == null)
        {
            Debug.LogError("Counter repair needs a device renderer/material.", fault);
            customer = null; fault = null; return false;
        }
        sourceMaterial = source.sharedMaterial;
        foreach (var input in FindObjectsByType<CinemachineInputAxisController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (input.enabled) { cameraReaders.Add(input); input.enabled = false; }
        display = new GameObject("Counter phone inspection");
        Part("Phone", Vector3.zero, new Vector3(.21f, .37f, .026f), new Color(.12f, .16f, .20f));
        Part("Screen", new Vector3(0, 0, -.017f), new Vector3(.18f, .31f, .006f), new Color(.07f, .24f, .28f));
        Part("Switch recess", new Vector3(-.119f, .09f, -.01f), new Vector3(.033f, .080f, .028f), new Color(.06f, .07f, .08f));
        var knob = Part("Mute switch", new Vector3(-.12f, .075f, -.025f), new Vector3(.03f, .037f, .025f), new Color(1f, .43f, .08f), true);
        slider = knob.transform; switchCollider = knob.GetComponent<Collider>(); switchRenderer = knob.GetComponent<Renderer>();
        physicalSwitch = knob.AddComponent<PhysicalToggle>(); physicalSwitch.Bind(fault, owner);
        var label = new GameObject("Phone status"); label.transform.SetParent(display.transform, false);
        label.transform.localPosition = new Vector3(0, 0, -.023f);
        screen = label.AddComponent<TextMeshPro>();
        screen.rectTransform.sizeDelta = new Vector2(.17f, .28f);
        screen.fontSize = .23f; screen.alignment = TextAlignmentOptions.Center;
        screen.text = "INCOMING CALL\n\nSound off\n\n<color=#FFAB52>Mute switch</color>";
        speaker = display.AddComponent<AudioSource>(); speaker.playOnAwake = false; speaker.volume = .16f;
        ringtone = RepairAudio.MakeTone("Phone confirmation", false); speaker.clip = ringtone;
        readyAt = Time.time + .25f; returnAt = fault.Finished ? Time.time + .2f : float.PositiveInfinity;
        PositionDisplay();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        return true;
    }

    private GameObject Part(string label, Vector3 position, Vector3 size, Color color, bool clickable = false)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = label;
        part.transform.SetParent(display.transform, false); part.transform.localPosition = position; part.transform.localScale = size;
        // Reuse an authored shader so player builds cannot strip a shader
        // referenced only through Shader.Find.
        var material = new Material(sourceMaterial); material.color = color;
        materials.Add(material); part.GetComponent<Renderer>().sharedMaterial = material;
        part.GetComponent<Collider>().enabled = clickable;
        part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return part;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (customer == null || customer.IsLeaving || fault == null || !player.IsAtStation
            || (DayClock.Instance != null && DayClock.Instance.DayOver)) { Close(); return; }
        if (Time.timeScale <= 0f) { if (speaker != null) speaker.Pause(); return; }
        if (speaker != null) speaker.UnPause();
        if (fault.Finished)
        {
            slider.localPosition = Vector3.MoveTowards(slider.localPosition, new Vector3(-.12f, .105f, -.025f), Time.deltaTime * .2f);
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
        hovered = mouse != null && switchCollider.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out _, 2f);
        physicalSwitch.SetHighlight(hovered);
        if (hovered && mouse.leftButton.wasPressedThisFrame && physicalSwitch.CanInteract)
        {
            physicalSwitch.Activate();
            hovered = false; switchRenderer.sharedMaterial.color = new Color(.3f, .95f, .6f);
            screen.text = "INCOMING CALL\n\n<color=#67FFAB>RINGING</color>\n\nSound on";
            speaker.Play(); returnAt = Time.time + Mathf.Max(.2f, confirmationSeconds);
        }
    }

    private void LateUpdate() { if (IsOpen) PositionDisplay(); }
    private void PositionDisplay()
    {
        display.transform.SetPositionAndRotation(cam.transform.TransformPoint(new Vector3(0, -.04f, .65f)), cam.transform.rotation);
    }
    public void Close()
    {
        if (IsOpen) closedFrame = Time.frameCount;
        if (display != null) { display.SetActive(false); Destroy(display); }
        display = null; fault = null; customer = null; hovered = false;
        foreach (var input in cameraReaders) if (input != null) input.enabled = true;
        cameraReaders.Clear();
        foreach (var material in materials) if (material != null) Destroy(material);
        materials.Clear();
        if (ringtone != null) Destroy(ringtone);
        if (player != null && player.IsAtStation && !(DayClock.Instance != null && DayClock.Instance.DayOver))
        { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }
    private void OnDisable() => Close();
}
