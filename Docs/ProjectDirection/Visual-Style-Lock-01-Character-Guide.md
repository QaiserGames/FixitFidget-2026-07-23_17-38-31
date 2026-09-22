# Fix It Fiasco — Visual Style Lock 01
## First character prototype: production guide

**Status: guide for approval only.** You build the character in Blender; I coach, inspect references and help diagnose problems. The previous Grace concept is superseded. No character redesign, model generation or Unity implementation is authorized at this stage.

**Authority:** your girlfriend’s illustration defines the character. The Meshy experiment is a reference for dimensional translation, not production geometry. The new images have not yet been inspected, so exact ratios, likeness and unseen views remain undecided.

### 1. Proportions before detail

Measure relationships from the drawing: head height/width, shoulder width, torso length, limb length, hand size and shoe size. Use head height as a measuring unit without imposing a realistic “heads tall” formula. Preserve the combination of oversized head and long body; do not compress it into chibi proportions.

Begin with a simple blockout, roughly 2k–5k triangles if useful. Check its silhouette in flat color from the reference angle, front, side and three-quarter. Unseen profile/back/body choices are interpretations to agree with the artist. Detail cannot rescue incorrect proportions.

### 2. Construction by area

| Area | Beginner-friendly approach |
|---|---|
| Head | Start from a low-resolution cube, shape the skull/jaw, and mirror one half. Establish forehead, cheek and chin masses before adding eye or mouth detail. |
| Facial exaggeration | Preserve the drawing’s distances and angles. Emphasize the same features; do not add realistic anatomy merely because it is familiar. |
| Nose | Build the characteristic wedge, bulb or hooked profile early. Check projection and width together. Use deliberate planes rather than many tiny faces. |
| Ears | Match size, placement and outward angle. A thick rim and one inner fold usually explain the form; avoid a detailed anatomical maze. |
| Eyes | Use simple eye geometry with lids defining the illustrated opening. Match spacing and lid tilt; do not expose complete spheres or enlarge the eyes beyond the drawing. Allow room for a blink. |
| Hair | Create a few recognizable masses: cap, fringe and major clumps as needed. Model the outline and parting; skip individual strands and transparency-heavy hair cards initially. |
| Neck | Preserve its illustrated length and thickness. Use a few rings for turning/bending and a clean transition into the collar. |
| Torso | Build the visible clothing silhouette first: shoulders, chest, waist and hem. Do not build detailed anatomy beneath permanently covered clothing. |
| Arms | Use simple tapered forms, with edge rings at shoulder, elbow and wrist. Keep enough separation from the torso to model and weight comfortably. |
| Hands | Start with a broad palm, an angled thumb and the illustrated finger count. Give fingers enough segments to curl around a cup. Preserve chunky readability; postpone nails and tiny knuckles. |
| Legs | Match the drawing’s long-body rhythm, knee placement and taper. Add useful bend loops at hip, knee and ankle rather than evenly increasing density everywhere. |
| Shoes | Treat them as important silhouette shapes: toe, upper and thick sole. Model those masses separately if easier; bevel edges sparingly. |
| Clothing | Separate major garments when useful. Add designed folds at bends and attachment points, not random wrinkles. Keep overlap beneath cuffs/hems so poses do not reveal gaps. |

### 3. Topology priorities

Use mostly quads while learning because edge loops are easier to edit. A loop is a continuous row of edges following a feature or joint. Prioritize eyelids, mouth corners, shoulder/armpit, elbow, thumb base, hip and knee. Start with a ring through a bend and supporting rings either side; adjust after posing.

Keep long thin faces and crowded star-shaped edge junctions away from bending creases. Triangles are fine in quiet areas; the export is triangulated anyway. Separate hair, eyes and garments are acceptable when their connections remain hidden during movement.

### 4. Modifiers and subdivision

| Modifier | Recommended use |
|---|---|
| Mirror | Main modeling helper. Enable Merge and Clipping at the center seam; keep asymmetry for a deliberate later pass. |
| Subdivision Surface | Optional, usually 0–1 level initially. Useful for head, ears or hair curves when it improves the intended silhouette. |
| Bevel | Small controlled bevels on shoe soles or hard accessories; usually 1–2 segments initially. |
| Solidify | Only where visible garment edges need thickness. Do not double every surface by default. |
| Armature | Later, for skeletal deformation. Keep it working as a rig; do not blindly apply it as mesh geometry. |
| Triangulate | Use a controlled export copy to inspect final triangles and prevent unexpected changes. Preserve the editable source. |

Subdivision is unnecessary on flat clothing areas, hidden surfaces, already smooth eyes or shapes whose outline is correct. It can shrink an exaggerated nose or round away a distinctive jaw. Smooth shading changes appearance without adding geometry; subdivision adds geometry. Each level roughly quadruples faces on an all-quad region. [Blender subdivision documentation](https://docs.blender.org/manual/en/4.3/modeling/modifiers/generate/subdivision_surface.html)

Finalize topology-changing operations before facial shape keys. Avoid dense sculpting, remeshing and automatic decimation as the first production workflow.

### 5. Triangle allowance

Treat **20k–35k finished triangles** as an approximate hero-regular allowance, not a target to fill. Count the evaluated/exported mesh after chosen modifiers.

| Part | Example allocation |
|---|---:|
| Head, face, ears and neck | 6,000 |
| Both eyes | 1,000 |
| Hair masses | 2,500 |
| Clothed torso and arms | 6,000 |
| Both hands | 3,000 |
| Legs/trousers | 3,000 |
| Both shoes | 2,500 |
| Small worn accessories | 1,000 |
| **Illustrative total** | **25,000** |

Reallocate for the actual design; fewer triangles are preferable when the silhouette and bends are equally good. Handheld repair objects have separate budgets. Materials, bones and simultaneous characters also affect performance; this is not a measured frame-rate guarantee.

### 6. Materials and UVs

Start with clean flat colors under neutral soft lighting. Use Blender’s Principled BSDF with broad, restrained roughness differences: matte clothing, softly reflective skin/hair, slightly glossier eyes. Keep nonmetals nonmetallic. No skin pores, noisy fabric maps or painted directional shadows unless the illustration requires them.

Aim initially for roughly 2–4 material slots, using textures to vary colors rather than assigning a material to every color patch. One 2K character texture set is a reasonable starting allowance, not a requirement; test whether 1K holds up.

Unwrap after proportions stabilize. Put seams behind the head, inside limbs and along clothing seams. Check stretching with a checker texture; allocate more UV space to face and hands. Mirror UVs only where symmetry is intended; reserve unique space for asymmetrical markings. Leave padding between islands.

### 7. Rigging implications

Use a simple conventional biped: root, pelvis, spine, neck/head, arms, legs and fingers. Model in a relaxed A-pose with slight elbow/knee bends. Automatic weights are a starting point; manually inspect shoulders, thumb, hips and oversized head movement.

Test head turn, raised arm, elbow bend, sit and cup grip before polishing textures. Start with normalized weights and up to four bone influences per vertex. Unity supports both Generic and Humanoid rigs; choose Humanoid if animation retargeting is needed, or Generic for deliberately authored animation. Stylization alone does not require Generic. [Unity rig documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/FBXImporter-Rig.html)

### 8. Unity export considerations — later, after approval

Keep the editable .blend source and export a controlled FBX. Establish meter scale, a clear ground-level root and consistent forward direction. Settle mesh rotation/scale before binding the rig; do not casually apply armature transforms afterward.

Export intended meshes and skeleton, excluding lights, cameras and unwanted control objects. Preserve required root/attachment bones and omit unnecessary leaf bones. Confirm modifier evaluation, normals, UVs and any shape keys survive the actual exporter version. Rebuild the simple material response in Unity’s existing shader; Blender node networks do not transfer as an identical appearance. Unity recommends exported model files such as FBX. [Unity model import guidance](https://docs.unity.com/en-us/engine/6000.3/manual/assets-and-media/asset-types/models/importing/importing-model-files)

The first later import should verify size, orientation, shading, joint bends and portrait resemblance at conversation and isometric distances. No exact FBX preset is locked before checking the installed Blender version.

**Approval test:** does the simple, neutrally lit model already look like the illustration came to life? If not, correct shapes before adding geometry or texture detail.

**Stop point:** approve or revise this guide first. No modeling, game assets or Unity changes begin from this document alone.
