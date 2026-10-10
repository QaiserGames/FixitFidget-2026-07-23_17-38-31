using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE CAFÉ REDESIGN'S RECORD (10 Oct 2026; claude/studio/cafe-sf-research-and-redesign.md, his decisions of 9 Oct, 23:40)
//
// What one café-redesign step changed in the scene, kept on the café's root so that step's Undo menu item can put every
// piece back after a save and on either machine (Unity's own undo history ends when the scene closes). One component per
// step, told apart by `step`; `order` says which came first, and only the latest step may be undone (a later step may
// stand on what an earlier one placed). CafeRedesignShared (Assets/Editor) writes it and reads it back. No behaviour at
// run time. The repair bar keeps its own record (CafeRepairBar), written before this one existed.
//
// Nothing a step finds in the scene is ever deleted: it is switched off (the night's set-up finds things by name). The
// only things an Undo deletes are the ones the step made (`made`, `addedComponents`).
// ---------------------------------------------------------------------------
public sealed class CafeRedesignRecord : MonoBehaviour
{
    [Serializable] public sealed class Off { public GameObject target; public string why; }

    [Serializable] public sealed class Pose
    {
        public Transform target, parent;
        public Vector3 localPosition, localScale;
        public Quaternion localRotation;
        public int siblingIndex;
        public string why;
    }

    [Serializable] public sealed class Paint { public Renderer target; public Material[] materials; public string why; }

    [Serializable] public sealed class Lamp
    {
        public Light target;
        public bool enabled;
        public LightType type;
        public Color color;
        public float intensity, range, spotAngle, innerSpotAngle;
        public LightShadows shadows;
        public string why;
    }

    [Serializable] public sealed class Box { public BoxCollider target; public bool enabled; public Vector3 center, size; public string why; }

    [Serializable] public sealed class Shape { public MeshFilter filter; public Mesh mesh; public MeshCollider collider; public Mesh colliderMesh; public string why; }

    [Serializable] public sealed class Toggle { public Component target; public bool enabled; public string why; }

    [Serializable] public sealed class Listed { public Component owner; public string field; public Object item; public string why; }

    [Tooltip("The step that wrote this record, as its menu names it (e.g. \"Bar 1 - The front counter\").")]
    public string step;
    [Tooltip("When it ran (yyyy-MM-dd HH:mm, the machine's clock).")]
    public string when;
    [Tooltip("Its place in the order the redesign's steps ran (1 = the first). Only the latest may be undone.")]
    public int order;

    [Tooltip("What the step switched off, never deleted, and why.")]
    public List<Off> switchedOff = new List<Off>();
    [Tooltip("What the step made (kit pieces, groups, lights): the only things its Undo deletes. In the order made.")]
    public List<GameObject> made = new List<GameObject>();
    [Tooltip("Components the step added to things it didn't make (a collider, a light): its Undo removes them.")]
    public List<Component> addedComponents = new List<Component>();
    [Tooltip("What the step moved or re-parented, with its parent and local pose before.")]
    public List<Pose> moved = new List<Pose>();
    [Tooltip("Renderers whose materials the step changed, with the materials before.")]
    public List<Paint> repainted = new List<Paint>();
    [Tooltip("Lights the step changed, as they were.")]
    public List<Lamp> lights = new List<Lamp>();
    [Tooltip("Box colliders the step refitted, as they were.")]
    public List<Box> boxes = new List<Box>();
    [Tooltip("Meshes the step replaced (a ceiling with holes cut for the skylights), with the mesh before.")]
    public List<Shape> shapes = new List<Shape>();
    [Tooltip("Components (renderers, colliders, behaviours) the step switched on or off, with their state before.")]
    public List<Toggle> toggled = new List<Toggle>();
    [Tooltip("Entries the step added to a scene list (CafeViewMode.overheadFixtures, CafeDaylight.warmLights, ...).")]
    public List<Listed> listed = new List<Listed>();
    [Tooltip("Anything worth knowing later.")]
    public List<string> notes = new List<string>();
}
