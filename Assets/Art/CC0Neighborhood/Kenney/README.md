# Kenney neighborhood assets

Curated CC0 assets for the Fix It Fiasco neighborhood refresh. Downloaded from official Kenney pages and license verified on 2026-09-23. Kenney credit is retained voluntarily. Each pack includes its original license.

- Nature Kit: https://kenney.nl/assets/nature-kit
- Furniture Kit: https://kenney.nl/assets/furniture-kit
- City Kit (Roads): https://kenney.nl/assets/city-kit-roads
- CC0 legal text: https://creativecommons.org/publicdomain/zero/1.0/

The source FBX models are unmodified. Nature and furniture use flat-color material slots, intended to be mapped onto the cafe palette. CityRoads uses its bundled colormap texture; source FBXs may refer to an exporter's absolute texture paths, so assign the bundled texture explicitly.

`SourceManifest.json` records archive and selected-file SHA-256 digests, source material names, and source OBJ bounds (Y up). These models are visual only: retain existing gameplay colliders, seating anchors, traffic-signal renderer references, and navigation routes when placing them.

`Adapted` contains a traffic-housing derivative split into flat-color material slots and omitting only the three baked-color lens faces so the existing live signal lenses can be used. Source geometry is otherwise preserved. No changes to licensing.
