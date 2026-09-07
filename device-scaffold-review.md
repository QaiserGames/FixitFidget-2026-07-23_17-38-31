# Device scaffold review

This is the content-authoring step recorded next in `roadmap-progress.md`.
GDD v4 §6.2 lists watch, phone, lamp, radio, toaster, film camera, turntable and
handheld game as the core eight devices. This tool prepares copies for authoring;
it does not claim the remaining six devices are built.

## In Unity

1. Exit Play Mode. Run **Fixit Fidget → Content → Check device scaffold rules**.
   Expected Console result: **518 assertions passed**. This uses temporary
   objects in a preview scene and does not write assets.
2. Open **Fixit Fidget → Content → Device scaffold**. Drag `PhoneRepair.prefab`
   from the Project window into **Template prefab**, then **Inspect template**.
   Read the fault list and resolve any wiring errors before copying.
3. Enter `Scaffold test` as the display name. Choose **Create separate prefab…**
   and save to a new `ScaffoldTest.prefab` in an existing project folder.
   Existing assets and their meta files are protected from overwrite.
4. Open the new prefab. Confirm the display name changed, the original did not,
   and its cover, screws, tasks and internal references belong to the new copy.
   The meshes, materials, fault descriptions, payouts and puzzle settings are
   deliberately inherited; a copied phone is still visually and mechanically a phone.
5. Inspect the new prefab using the same window. In a disposable test scene,
   exercise each fault with the existing repair setup. Check pickup, opening,
   task interaction, reassembly and handback. Confirm the original phone still
   behaves normally. Do not enroll a new device in customer arrivals until its
   visuals, descriptions, reachability and job balance have been reviewed.

Negative checks: a disabled parent above a selected task should report an error;
a fault referencing another prefab should report an error; an empty selected
fault with no other active task should report an error. Shared task objects are
allowed. Pickup requires a root `ItemInteractable` and at least one active collider;
the collider may sit on any child because the runtime resolves the interactable
through its parent chain. The checks simulate activation without running `ApplyFault` or generating
a circuit. Counts describe task components, not final grade credits.

## Scope and limitations

- One root `RepairJob`, root `DeviceDefinition` and `InspectableItem` are required.
  Drinks and support calls have different lifecycles and are rejected as templates.
- Whole-prefab copying retains internal wiring. A copied root variant is unpacked
  to become independent; nested visual prefabs may remain connected.
- Materials, meshes and ScriptableObject profiles remain shared. Duplicate those
  assets before making changes intended only for the new device.
- Human faults copied from a phone still need phone-specific visual/content review;
  the tool does not invent a new physical toggle for a different device.
- Validation catches structural issues, not occlusion, camera framing, model fit,
  every missing optional field, runtime-generated geometry, or playability.
- No runtime scripts, existing prefabs, schedules, save data or payout rules change.

Source syntax and the proposed repository diff can be checked outside Unity.
The 518 Editor assertions, prefab creation and actual gameplay must be run in
Unity; they have not been executed in this workspace.

After this gate, build one deliberately authored device slice using existing
verbs, test it end to end, then expand the roster. Two more verbs are on the
design backlog; choose their distinct interactions before committing to implementation.
