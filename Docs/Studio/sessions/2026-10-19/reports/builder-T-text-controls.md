# Builder T report — button icons, the UI font, readable text, conversations, the drinks machine, aim, the day intro (19–20 Oct 2026)

All nine priorities are done in `/root/wt/T`. Every changed `.cs` parses with tree-sitter C# (33 files, 0 errors). Nothing was compiled (no Unity here): §6 lists the APIs I am not certain of.

## 1. What I did (files changed, one line each)

New files
- `Tools/UI/draw_button_icons.py` — draws the icon sheet with Pillow at 4x and shrinks it: 99 icons (PS/Xbox/Switch face buttons, pills, D-pad, sticks, A–Z 0–9 Esc Tab Space Ctrl Shift Enter arrows, mouse, six drinks). Letters in Baloo 2 Bold (falls back to `Tools/Blender/fonts/Inter-Bold.otf`, then the system Inter). Run; outputs kept and checked by eye at 100 % and at 32 px.
- `Assets/Data/Resources/UI/ButtonIcons/button_icons.png` (2048 x 2048, 380 KB) and `button_icons.json` (`icons: [{name, x, y, w, h}]`, top-left origin, plus width/height; an array, not a map, so `JsonUtility` can read it).
- `Assets/Editor/ButtonIconSteps.cs` — menu `Fixit Fidget/UI/Button icons - Make the icon sprite asset and TextMesh Pro's default (with Undo)` + its Undo item: sets the PNG importer (Default, uncompressed, no mipmaps, alpha is transparency, not readable, clamp, bilinear), builds `Assets/Data/Resources/UI/ButtonIcons/Button icons.asset` (sheet, TextMeshPro/Sprite material sub-asset, one glyph + one named character per entry, y flipped), writes `m_Version = "1.1.0"` through a SerializedObject first (otherwise `UpdateLookupTables` treats the asset as legacy and empties the tables), sets the face metrics so a 128-px disc draws at 1.25 x the UI font's cap height and sits centred on the cap band (bearingY = h/2 + cap/2), makes it `TMP_Settings.m_defaultSpriteAsset` (SerializedObject, old value kept in EditorPrefs for the undo), then probes `GetSpriteIndexFromName("ps_cross")` and logs an error if the lookup fails.
- `Assets/Editor/UiFontSteps.cs` — menu `Fixit Fidget/UI/Font - Baloo 2 as the game's UI font (with Undo)` + Undo: `TMP_FontAsset.CreateFontAsset(SemiBold, 90, 9, SDFAA, 1024, 1024, Dynamic, multi-atlas)` saved at `Assets/Data/Resources/UI/UI font.asset` (UiSkin's exact Resources path `UI/UI font`), a second dynamic asset from Baloo2-Bold at `Assets/Fonts/Baloo2/Baloo2-Bold SDF.asset` as the weight-700 entry, TMP's default font as fallback. Undo deletes both.
- `Assets/Fonts/Baloo2/Baloo2-Medium.ttf, -SemiBold.ttf, -Bold.ttf, OFL.txt` — static instances (wght 500/600/700) made with fontTools from Google Fonts' `Baloo2[wght].ttf` (fetched by sparse git clone of google/fonts; the raw URL in the spec is blocked by the proxy).

Changed (Assets/Scripts unless said)
- `PadInput.cs` — `Label(PadButton)` returns `<sprite name="ps_cross">`-style tags for the pad family in hand; new `Word(PadButton)` (the old words), `IconName(PadButton)`, `Icon(name, word)` (the cached safe fallback), `IconsAvailable`. DualSense check cached per pad (it was built every call).
- `ControlHints.cs` — rewritten: every verb an icon (keycap/mouse icons on a keyboard); `Verb` enum, `Word(Verb)`, `ControlHints.Words.X`, `Key(word)`; new verbs `Walk`, `Choose`, `Confirm` (Cross/Enter), `StopNeedle` (Square/Space).
- `UiSkin.cs` — `UiSkin.SemiBold`: `FontStyles.Normal` when the Resources font is in (its regular face is Baloo 2 SemiBold), else `Bold`, so instructing text is never thin on the fallback font.
- `ShopUI.cs` — prompt line without brackets; the strip is three icon+word hints for the state (overhead: Move, Use, Pause; first person: Move, Use, Overhead view; night: Notebook, Torch, Sneak), 22 px semibold on a dark rounded plate, rich text on. `CafeViewMode.ControlsHint` is no longer shown.
- `ConversationController.cs` — reply chips carry the icon (no brackets, no 78 % size), footer `Choose … Step away` with icons.
- `ConversationUI.cs` — one line at a time (no `EarlierLine`), E steps to the next line, their line on a dark plate (32 px semibold), footer semibold with rich text; the line (and so the replies) waits for the main camera's `CinemachineBrain.IsBlending` to end (≥ 0.15 s, ≤ 0.8 s); an E cuts the wait. Replies stay 32 px (see §3).
- `BeverageStation.cs` — rebuilt: "Pouring {drink}" + a gold radial ring (filled 360, from the top) right of the action line; action line 24 px semibold wrapping to 2 lines; controls two lines of two pairs each at 20 px, never cut; "Take the {drink} to {name}" with a filled cup in hand whatever the crosshair is on (the nearest customer whose `CanReceiveDrink` is true); the cup stack glows while the step is "take a cup"; `ActionText`, `ControlsText`, `RingShowing`, `RingFill`, `StackGlowing` for labs.
- `BeverageControl.cs` — "Pouring {drink}" without a number; icons for the hands.
- `BeverageCupSupply.cs` — icons; `SetGlow(bool)` (the dispenser's take surface is this class in the scene, not CupStack).
- `CupStack.cs` — `SetGlow(bool)` and `CupStack.Glow`: an emissive amber breath on the stack's own material instances (kept and restored exactly), used by both stack classes.
- `CounterRepairView.cs` — icons in the controls and the hover line, rich text on, caption 24 bold, controls 20.
- `RecapPhone.cs` — hint under the phone 22 px with icons, rich text on; nothing a player reads under 18 px (labels/kickers/tabs 16); `InkFaint` on light cards → `InkSoft` (5.8:1); "Supplies" → "Shop"; Notes header "Press a name for everything."; the pad focus ring baked gold-with-ink-edges (`FocusRingTexture`).
- `JobTicket.cs` — name 20 bold, mood 16, device/fault 18 semibold with the fault in sentence case (`SentenceCase`), drink row `+ <drink icon> Latte` 20 semibold (`DrinkLine`, `DrinkIconName`), details panel 18 px without "Patience: X%"; with a support call on the card the mood line hides (no room at these sizes).
- `FocusNameUI.cs` — when the name chip over a thing would cover Ace's head on screen, it hangs under the thing instead (anchor pushed toward the camera), with hysteresis; `Nameplate.cs` — skin font, semibold, shadow, alpha cap 0.95.
- `MorningFace.cs`, `CircuitPuzzle.cs` — no brackets; `StopNeedle`/`Boost` icons; the circuit HUD 24/20 px.
- `DayIntro.cs` — skip prompt "Skip  " + Cross/Enter icon, 24 px white, follows the device; `TitleHoldSeconds = 6`; the title also goes (after its 1.2 s minimum) on the player's first move (left stick, WASD/arrows) once the controls are back.
- `GameSettings.cs` — `AimHelpResetKey = "FixitFidget.AimHelp.Reset19Oct"`: once per machine, on launch, aim help goes on and the key is written.
- `PlayerInteractor.cs` — `TargetOf`: a seated customer's aim point drops by NpcSeating's own overhead drop (`standingHipHeight − seatedHip.y − SeatedLift/scale`, scaled to world); new private `SeatedDrop`. The conversation camera tracks the LookTarget transform and conversations are standing, at the counter: unchanged.
- `Diagnostics/AimHelpCheck.cs`, `Diagnostics/StationsCheck.cs` — the player's aim-help setting is put back from `OnDisable` too (Play stopped mid-check is what left his Mac off).
- `Diagnostics/RecapPhoneCheck.cs` — hint checks compare through `ControlHints.Key`/`PadInput.Label`, never literal words.
- `Assets/Editor/ControllerChecks.cs` — `[A]`/`[X]` literals replaced by `PadInput.Label`/`Word`.

## 2. Decisions and reasoning
- Icons are TMP sprite tags inside the same strings, resolved through TMP's default sprite asset: one change reaches every line, the icons scale with the text, and the labs can compare against the same helper. `PadInput.Icon` checks the default sprite asset for the name once per name and returns the word otherwise, so no screen can show a raw tag (the step not yet run, a wrong TMP, an unknown name).
- Icon metrics: TMP draws a sprite at `metrics × fontSize / spriteAsset.pointSize`, so pointSize = 128 / (1.25 × capHeight em of the UI font, read from its face info × its face scale). The icon is centred on the cap band, a tenth of a disc hanging below the baseline like a descender.
- Baloo 2 face metrics: its cap height is 0.602 em (Liberation 0.729), x-height 0.460 (0.519) and its line box 1.6 em (Devanagari room). Set at the same px it would read 12–17 % smaller and push every wrapped line out of its box. The asset gets face scale 1.12 (x-heights match) and Liberation's ascender/descender/line height divided by that scale, so "24 px" of Baloo 2 takes the room 24 px of Liberation took and reads the same size in chunkier letters. The TMP inspector shows these under Face Info; the glyphs are untouched.
- The strip drops orbit/zoom/view hints (six hints, 14–16 words, were a wall for her); Day 1's "walk and use first" is the whole strip every day.
- Conversation: E steps one line at a time (so a line is never skipped unread), the camera hold delays only the automatic reveal and beat; an E during the hold ends it, and on a one-line speech finishes the beat too, so playtest 4's quick second E still brings the replies up at once (ConversationCheck's timing check keeps passing).
- The name pin: lifting it a head higher is 10 px from the overhead camera (36–42 px/m) and still on Ace; anchoring it at the counter front lands on the queue. Hanging it under the thing (a name badge on the chest, a label on the counter front) only when Ace's head would be under it covers nobody.
- Seated aim: the exact drop NpcSeating applies to its bar and bubble, read from `NpcSeating.Data`/`IsSeated` and `PolygonNpcVisual.SeatedLift` (all public), not a head-bone guess.
- Cup-stack glow is emissive (URP Lit `_EMISSION` + `_EmissionColor`) on material instances, not the bench's outline hull: no new shader, nothing in the scene.

## 3. Decisions his to overrule
- Baloo 2 as the UI font at all, and SemiBold as its regular face (Bold = the Bold cut); the face scale 1.12.
- Replies kept at 32 px (the spec said 30: bigger is the direction, and ConversationViewProbe asserts ≥ 32).
- The strip's three hints per state, and "Overhead view" instead of Pause in first person.
- The icon letters set in Baloo 2 Bold (Inter is the fallback); PS symbol colours; the pressed D-pad arm plain paper (an arrowhead was noise at 32 px).
- The phone's all-caps kickers, tab labels, badge digits and section titles at 16 px, not 18 (labels, not text); the tags/verdict pills 16.
- With a support call on a ticket the mood line hides (the call state and the portrait frame carry urgency).
- A filled cup nobody ordered: "Nobody has ordered a {drink} · the cup return takes it back".
- The focus ring's edges: ink 2.5/12 of the stroke either side of gold.
- The title's 6-s hold also ends on the first move, not only a press.

## 4. Outside my files (exact edits)
- `Assets/Scripts/Diagnostics/Day1GuideCheck.cs:124` → `Check(DayOneGuideUI.MovementHintWanted && shop.ViewHint.Contains(ControlHints.Walk) && shop.ViewHint.Contains("Move") && shop.ViewHint.Contains("Use"), …)`; `:221` → `Check(!DayOneGuideUI.MovementHintWanted, "walked a few metres and used E: the guide's movement words go (the strip keeps Move and Use every day)")`.
- `Assets/Editor/TicketLayoutChecks.cs:108` → `Require(drinkLine.gameObject.activeSelf && drinkLine.text == JobTicket.DrinkLine(drink), …)` (the row is `+ <sprite> Hot Chocolate` once the sheet is in).
- `Assets/Scripts/CustomerBrain.cs` (Grace's latte bubble, 30 px, staying): in `TickDrinkWish` replace `Say(OrderLine(), broadcast: true);` with
  `string order = OrderLine(); string icon = PadInput.Icon(JobTicket.DrinkIconName(WantedDrink), ""); Say(order, broadcast: true); Barks.Say(transform, Identity != null ? Identity.DisplayName : name, (icon.Length > 0 ? icon + " " : "") + order, 8f);` — Barks draws 30 px on screen at any zoom with rich text on. (Ending it on accept needs a hide call Barks doesn't expose; 8 s is the spec's other half.) The world bubble's own size is the customer prefab's (world space), not reachable from code.
- Builder N's files that still bracket a label: `GraceAtHome.cs:1693` (`$"Hidden in {place.what}. {ControlHints.Interact}  Come out"`), `DayOneGuideUI.cs:587` (`$"{name} is getting impatient. {ControlHints.Cancel}  Step back, then {ControlHints.Interact}."`), `Barks.cs:888–889` (the chips: N's spec already replaces them), `NightZero.cs` is fine (no brackets). Builder B's: `BenchCards.K/KP` (lines 142–146: return the label as is), and its labs `BenchViewProbe.cs:369–370`, `BenchWordsCheck.cs:364, 428`, `Editor/BenchWordsChecks.cs:158–213` (drop the `"[" + … + "]"` round `ControlHints.*`).
- `CafeViewMode.cs:166–175` builds a line the strip no longer shows; harmless, but "Right stick" there is a literal: `PadInput.Icon("rs", "Right stick")` if it is ever shown again.
- `UiPins.cs` would be the right home for the pin-clear-of-Ace rule (a pointer that can point up); mine works from outside it.

## 5. Labs and menus that prove it on his Mac (in this order)
1. `Fixit Fidget › UI › Font - Baloo 2 as the game's UI font` — Console: the two assets made, the bold weight and the fallback listed. Then `Fixit Fidget › UI › Button icons - Make the icon sprite asset…` — Console: "99 icons on 2048 x 2048; point size … (cap height 0.674 em from the UI font (UI font))", "TMP Settings › Default Sprite Asset = Button icons", no error. (The icon step reads the font's cap height, so the font first; re-run the icon step if the font is undone.)
2. Enter Play with Unity in front: the strip reads icon Move / icon Use / icon Pause on a dark plate; the prompt line "icon  Talk to Grace", no brackets anywhere; Console shows "[Settings] Aim help put back on, once" the first time.
3. `Fixit Fidget › Checks › Controller (Play Mode)` — 29/29 as before; the dispenser lines now say "offers A Drinks" with the icon in the prompt.
4. The conversation lab (Dialogue › conversation check) — all pass; photos 02–08: one line on a dark plate, no dimmed line above it, replies with icons, the walk-in's line only after the camera has landed.
5. Stations (first person and overhead) — pass as before; photos drink-2/3: "Pouring coffee" with the ring, the cup stack glowing amber in drink-2a, "Take the coffee to …" in drink-3.
6. Aim help — 26/26; stop Play mid-check once on purpose, re-enter Play: Settings › CONTROLS shows aim help on.
7. Recap phone and Pause checks — pass; photos: 18-px lines, "Shop" header, "Press a name for everything.", the gold ring with dark edges on the restock button.
8. Day intro — pass (each morning watches ~4 s longer: the title holds 6 s); the skip prompt in the bottom bar is "Skip" + the A/Cross icon, white, 24 px.
9. Ticket layout (Editor check) fails on line 108 until §4's edit; Day 1 guide fails on 124 until §4's edit.

## 6. Risks and APIs I am not certain compile
- `TMP_SpriteGlyph`/`TMP_SpriteCharacter` setters (`index, metrics, glyphRect, scale, atlasIndex`; `glyphIndex, name, scale`), `TMP_SpriteCharacter(uint, TMP_SpriteGlyph)`, `TMP_SpriteAsset.spriteSheet/material/faceInfo/UpdateLookupTables/GetSpriteIndexFromName`, `TMP_Settings.instance/defaultSpriteAsset/defaultFontAsset`, `FaceInfo` property setters (`pointSize, scale, ascentLine, descentLine, baseline, lineHeight`), `TMP_FontAsset.CreateFontAsset(Font,int,int,GlyphRenderMode,int,int,AtlasPopulationMode,bool)`, `fontWeightTable[7].regularTypeface = …`, `fallbackFontAssetTable` set — all as in TMP 3.x / uGUI 2.0; if `m_Version` or `m_defaultSpriteAsset` are named differently in his TMP the step logs an error saying what to set by hand.
- `CinemachineBrain.IsBlending` (CM 3.1.7: used by ControllerChecks already).
- The sprite asset's `faceInfo.pointSize > 0` path is what makes TMP use my metrics; if his TMP ignores it, icons draw at the font's ascender height instead (still icons, slightly larger).
- Texts made by `RepairOverlayUI.Text` have rich text off; I turned it on where an icon can appear in my files. Any other file that puts a `ControlHints` verb into such a text must set `richText = true` or it will show the tag.
- The phone's bigger type lengthens every card; layouts flow from `Bottom()` so nothing should overlap, but the 6-tab bar at 16 px bold ("Franchise") is tight on the recap's five tabs.
- The cup-stack glow assumes URP Lit materials (`_EmissionColor`, `_EMISSION`); on another shader it does nothing (guarded by `HasProperty`).
- With a drink on a ticket the fault line is one wrapped line in 41 px: a long fault ends in an ellipsis (the details panel still has it in full).
- Not done: nothing. The spec's "world bubble 30 px with the icon" is the §4 CustomerBrain edit, not mine to make.