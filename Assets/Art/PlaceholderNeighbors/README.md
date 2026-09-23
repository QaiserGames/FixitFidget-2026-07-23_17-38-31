# Temporary neighborhood appearances

Five unmodified FBX models by **Quaternius**, from the **Ultimate Modular Men Pack**:
Casual_2, Casual_Hoodie, Farmer, Suit and Worker.

- Official source: https://quaternius.com/packs/ultimatemodularcharacters.html
- Official download collection: https://drive.google.com/drive/folders/1USAAquX2JJWuA2m6zol0KUkFe3UkZ8zX
- License: **CC0 1.0 Universal**, confirmed on the official pack page on 2026-09-23.
- Legal text: `CC0-1.0.txt`; https://creativecommons.org/publicdomain/zero/1.0/
- File sizes and SHA-256 hashes: `source-manifest.json`.

These are temporary walk-in, patron and street-neighbor bodies, not finalized
character designs. Authored regulars keep their current appearance; portraits and
the owner's character work remain authoritative.

Unity imports only the meshes and rig from these files. The added renderers use
the actor's existing named bones and Animator. Navigation, interactions, speech,
patience bars, colliders and carried-item attachment points are left intact.
Materials are generated from the source model colors with a modest matte finish.

The explicit editor helper `NpcVisualVariantSetup` checks that every model was
bound to the same skeleton as the actor (it compares the meshes' bind poses, not
the live bone pose: Beach.fbx is imported with its animations, so its bones rest
in an animation pose 7.8 mm off the true rest pose) before adding the appearances. `PrepareModels()` configures these imports;
`ApplyToPrefabs()` authors Customer and Patron prefab renderers;
`ValidatePrefabs()` verifies the five mesh sets and their bone/material references.
Nothing runs automatically in edit mode. Removing `NpcVisualVariants` and the
`Temporary CC0 walk-in appearances` child restores the original visual setup once
the original renderers are enabled.

## How it is applied (23 Sept 2026)

Run from **Fixit Fidget > Neighborhood refresh** in Unity, in order:

1. *Record gameplay baseline and before photos* (evidence in `Logs/NeighborhoodRefresh/<date-time>/`)
2. *Placeholder neighbors for walk-ins and patrons* (prepares these imports, saves the
   Customer and Patron prefabs, validates skinning in idle/walk poses, writes `npc-lineup.png`)
3. *Apply scenery refresh to the cafe* (also gives the six street neighbors fixed looks)
4. *Verify: gameplay guards, baseline, after photos*

Walk-ins pick a look from a stable hash of their name, patrons cycle through the
five, and named regulars (Grace) keep the Beach body, so for now the Beach body
means "a regular".

