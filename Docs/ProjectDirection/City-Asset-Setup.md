# City asset setup

The September 23 city pass uses the owner's purchased **POLYGON – City Pack – Art by Synty**, downloaded through the Unity Asset Store. Its City and Generic asset libraries belong under `Assets/Synty`. They are licensed assets, not CC0.

The existing GitHub repository is public. Purchased source files and generated character assets that depend on them are excluded by `.gitignore`. Project-authored scripts and placement recipes can be backed up publicly. A fresh checkout must import the purchased pack and rebuild its local character appearances before using this pass; GitHub alone is not a complete backup of these licensed sources.

Original download: **Unity Asset Store → My Assets → POLYGON – City Pack – Art by Synty**. The local Asset Store cache retains the original package. Imported source assets retain their original GUIDs. The optional Synty package-installer helper was omitted: the project already uses URP and Shader Graph through its existing packages.

The second download, **Lighting Optimisation 3D Sample Project**, is a Viking village tutorial. Only its two procedural skybox materials and seven lightmap parameter assets are imported under `Assets/LightingOptimizationTutorial` as references. Its package manifest, legacy installer DLL, demonstration cameras, scene-specific baked lighting, and village scenes are not part of this game's setup. The café's existing daylight system remains responsible for its day/night progression.

The earlier CC0 Kenney/Quaternius neighborhood pass is independently checkpointed and remains available in Git history. These placeholder characters do not establish the appearance of Grace or the eventual authored cast.

Sources: [POLYGON City](https://marketplace.unity.com/packages/3d/environments/urban/polygon-city-pack-art-by-synty-95214), [Lighting sample](https://assetstore.unity.com/packages/3d/environments/lighting-optimisation-3d-sample-project-73563).
