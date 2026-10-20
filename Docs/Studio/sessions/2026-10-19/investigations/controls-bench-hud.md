# Investigation: controls, text, bench highlights, HUD and words

This is a read-only look at the snapshot in `/root/code` (Fix It Fiasco, branch playtest-2, files up to 16 Oct 2026). It covers what his girlfriend ran into on Days 1–3 on 19 Oct 2026. References are `file:line`. A bare file name is in `Assets/Scripts/`; labs are in `Assets/Scripts/Diagnostics/` and editor steps in `Assets/Editor/`. Sizes are reference pixels on a 1920 x 1080 canvas unless a line says otherwise.

**What the snapshot can't show:** the café scene, the prefabs (customers, devices, the HUD canvas), the fonts and the TextMesh Pro folder. Where an answer depends on them, I say so. For the pocket watch I used the Devices 2 bake report of 15 Oct (`/root/review/pack-2026-10-19/Bench__devices2/report.txt`). It lists every object in the watch prefab with its place and size.

## The short version
- **Button names are written as words everywhere.** On a PS pad that means "Square", "R2" and "[Triangle] Flip it over". One helper picks the word (`ControlHints`), but about 17 files put it into text, in four different styles. There is no glyph art and no TMP sprite asset.
- **Most of what she reads is 14–21 px.** The bench card is 19 and 17 px. The day and time are 16 px, and can shrink to 12. The Day 1 card is 21 or 18, the tabs 11–17, the pins 21–22 and the controls strip 19. The font is still TextMesh Pro's default (LiberationSans), and its bold is synthesized, not a real weight.
- **The watch's cloth step is broken for a newcomer.** The checklist's "is this on the face turned down?" test misses the crystal smudge, so the card says "Wipe" instead of "Flip it over" while the smudge faces the mat.
  - The cloth can't touch the smudge in that position.
  - What the cursor does find is one of four empty screw holes. They stay floating where the back plate's dome was, and the hover reads "Screw hole · The cover goes back on first".
  - If she closes the watch anyway, the job has 2 tasks with 1 done, which grades Passable.
- **The mint highlight has almost no contrast where it matters.** It is 1.01:1 against the pale new-part pad and 1.04:1 against the yellow cloth. Red would vanish in brightness on the slate mat (1.03:1). No single flat colour works on every surface.
- **The seated aim point floats at the top of the head.** When a customer sits, the code lowers their patience bar and speech bubble, but never their look target.

---

## 1. Button names in text

**Where the word comes from**
- **`PadInput.Label(PadButton)`** (PadInput.cs:288-314) returns what the pad's face prints:
  - PlayStation: Cross / Circle / Square / Triangle, L1 / R1 / L2 / R2, Options, Share or Create, L3 / R3.
  - Xbox: A / B / X / Y, LB / RB / LT / RT, Menu / View, LS / RS.
  - Nintendo: B / A / Y / X, L / R / ZL / ZR, Plus / Minus.
  - The D-pad is spelled out on every pad: "D-pad up", "D-pad down" and so on (308-311).
- **Which pad family** (`PadInput.Kind`, 270-286). It reads the pad's layout, product and maker names:
  - "dualshock", "dualsense", "playstation" or "sony" → PlayStation.
  - "switch", "pro controller" or "nintendo" → Nintendo.
  - Anything else, or no pad at all → Xbox.
  - DualSense against DualShock only changes Create/Share (292-293).
  - A PS pad routed through Steam Input or DS4Windows can arrive as an Xbox pad and get Xbox words (the comment at 58-63).
- **Keyboard or pad** (`PadInput.UsingPad`, 55, set in `Track` 94-130). The last device touched wins. Any pad button, or a stick pushed past 0.35, means pad. Any key, click, scroll, or a mouse move of more than 4 px means keyboard.
- **`ControlHints`** (ControlHints.cs:7-61) turns each verb into its word. Pad word first, keyboard word second:

  | Verb | Pad / keyboard | Line |
  |---|---|---|
  | Interact | Cross / "E" | 14 |
  | Refuse | Triangle / "Q" | 18 |
  | Back | Circle / "Esc" | 19 |
  | Use | R2 / "Click" | 27 |
  | Cancel | Circle / "Right-click" | 29 |
  | Flip | Triangle / "R" | 33 |
  | Torch | Square / "F" | 36 |
  | TakeFromBench | Square / "E" | 46 |
  | BenchUse | "Cross / R2" / "Hold click" | 48 |
  | BenchHelp | Create or Share / "Tab" | 52 |
  | Orbit | "Right stick" / "Middle-drag" | 34 |
  | BenchZoom | "D-pad up/down" / "Scroll" | 50 |
  | The bench sticks | "Right stick" / "Left stick" | 57-60 |

**Every place a button name is written into text**

No literal "Square", "Triangle", "Cross", "Circle", "R2", "L2", "R1", "L1" or "Options" appears outside `PadInput.Label`: every pad word goes through it. The keyboard words are literals in ControlHints and in the keyboard branches listed below.

- **As a bracketed chip, "[word] verb":**
  - **The HUD prompt line.** `ShopUI.Key` writes "[word]" in BrandBright (ShopUI.cs:122-123). It is used at 193 (night), 302 (E) and 304 (Use).
  - **The controls strip, bottom middle.** "Left stick Move / Cross Use" (ShopUI.cs:129-133), the night extras (161-163), and the view's own line (CafeViewMode.cs:157-175).
  - **The bench cards.** `K` writes the word in BrandBright on the dark band; `KP` writes it in Brand bold on the paper card (BenchCards.cs:142-146).
    - The first repair's controls card: 171-181.
    - The full card while held: 184-205, with the literals "D-pad left/right" at 193 and "Right-click" at 179 and 202.
    - "[Create] Help" at 208, "[Triangle] Flip it over" at 211, "[Square] Take it to Grace" at 218-222.
  - **Conversation replies.** "[Cross]" on the lit reply and "[Triangle]" on a refusal, at 78 % size in #CFC27D (ConversationController.cs:577, 596-598). The footer reads "[D-pad] Choose [Circle] Step away" (604-610).
  - **Night and the morning.**
    - "Hidden in … [Cross] Come out" (GraceAtHome.cs:1693).
    - Ace's reply chips, "Square" and "Triangle" (Barks.cs:888-889).
    - "[Cross] Go on" (MorningFace.cs:164) and the stop key "Square" or "Space" (190).
    - "[Triangle] Retry (-1 point)" on the circuit (CircuitPuzzle.cs:403).
  - **The impatience card at the bench.** "[Circle] Step back, then [Cross]." (DayOneGuideUI.cs:586-587).
- **Inside a sentence, no brackets:**
  - **The Day 1 / featured-visit card.** It has 35 sentences such as "Press Cross to pick up Grace's camera from the intake tray." The helpers are at DayOneGuideUI.cs:545-547. Drinks sentences are at 388-484, repairs at 499-540, others at 565-579.
  - **The circuit.** "Aim at a wire and press R2…" (CircuitPuzzle.cs:387).
  - **Night notes.** NightZero.cs:352 ("D-pad up to read it") and 440 ("Square torch · D-pad up notebook · Cross at a café door: home").
- **As "word · verb" lines:**
  - The drinks card: "L1 · left hand R1 · right hand Cross · use Circle or walk · step back" (BeverageStation.cs:94-97).
  - The paddle: "Take a cup · L1 / R1 chooses that hand" (BeverageControl.cs:24).
  - The cup return (BeverageCupSupply.cs:17).
  - The counter phone (CounterRepairView.cs:36-43).
- **As "word  verb" footers:**
  - The pause phone and recap: "Options Back L1 / R1 Apps D-pad Move, adjust Cross Press" (RecapPhone.cs:709-719).
  - The notebook: "D-pad up Close" (NightNotebook.cs:103).
  - The day intro: "Skip any button" (DayIntro.cs:805).
  - A settings row: "Left stick moves your hand…" (RecapPhone.cs:1749).

**Is there a TMP Sprite Asset for glyphs?** No.
- No script references a `TMP_SpriteAsset` or uses a `<sprite>` tag (searched Assets, Tools and Docs).
- The only icon images the game ships are the 13 bench tool and part PNGs in `Assets/Data/Resources/UI/BenchIcons/`.
- Whether TMP's sample emoji sprite asset came in with TMP Essentials can't be seen here. It has no button glyphs anyway.

**One helper or scattered?** Both.
- The word comes from one place: ControlHints, which calls PadInput.Label.
- How it is set into text is scattered over about 17 files in four styles: a bracketed chip, a bracketed chip at 78 % in conversations, a bare word inside a sentence, and "word · verb".
- The key colour differs too: BrandBright on the HUD and the band cards, Brand on the paper card, #CFC27D in conversations.
- Labs compare these exact strings: ControllerChecks.cs:121, 268, 322; BenchViewProbe.cs:369-370; BenchWordsCheck.cs:364, 428.

## 2. Text sizes and fonts

**UiSkin** (UiSkin.cs)
- **Colours (35-70):**
  - Paper #F7F3EE, Ink #1F1B17, InkSoft #6B645C, InkFaint #A39B91.
  - Brand #2E7D5B, BrandBright #7FD3A8, BrandSoft #DCEFE1, BrandDeep #1F5A41.
  - Accent #C9702D, Gold #F2A61F, GoldSoft #FDEBD0, GoldInk #A8620F, Red #B8483C, RedSoft #F6D9D4.
  - Band is Ink at 86 %; BandText #F7EFE6; BandFaint #CBBFB2.
- **Sizes:** only Radius 12 and Margin 24 (77-80). The skin has no type scale and no weights; every screen sets its own sizes.
- **The font** comes from one swap point (84-120): a TMP font asset at `Resources/UI/UI font`, else the HUD's scene text's font, else `TMP_Settings.defaultFontAsset`.
  - There is no "UI font" in this snapshot: `Assets/Data/Resources/UI/` holds only BenchIcons.
  - The studio docs say the font is still LiberationSans (Docs/Studio/agents/ui-ux.md:24; BIBLE.md:224), which is TMP's default.
- **Bold:** `UiSkin.Text` defaults to Normal (134-150), and Bold is TMP's `FontStyles.Bold`. No bold or semibold font asset is referenced anywhere in code. With TMP's LiberationSans SDF alone, Bold is drawn thicker by the shader rather than as a true weight (medium confidence: the TMP folder isn't in the snapshot).
- **Shadow:** text over the world gets one shared underlay (`Shadowed`, 157-195).
- **"Baloo 2"** is not in the project. It appears only as a proposal waiting for his verdict (ui-ux.md:24, 59; BIBLE.md:223).

**Canvas scalers**
- **`UiSkin.ScreenCanvas`:** Scale With Screen Size, 1920 x 1080, match 0.5 (UiSkin.cs:273-288). Used by HudCorners (160), BenchCards (849), BenchGlow (453), UiPins (243) and DayIntro (787).
- **The same settings, written by hand:** Juice (639-641), Barks (816-818), NightCycle (568-570), NightNotebook (48-50), StraightFaceUI (136-138), CircuitPuzzle (309-310), NoticeMark (95-97).
- **The pause phone and recap** (`RecapPhone`): 1920 x 1080, match 1, so it follows the screen's height (RecapPhone.cs:229-231).
- **`RepairOverlayUI.Canvas`:** Constant Pixel Size (RepairOverlayUI.cs:15-26), scaled by hand.
  - The drinks card: × clamp(min(W/1440, H/900), .6, 1.4), which is 1.2 at 1080p (BeverageStation.cs:66).
  - The counter phone: × clamp(min(W/1600, H/900), .8, 1.6), also 1.2 at 1080p (CounterRepairView.cs:161-162).
- **The scene's own canvases** (ShopUI's prompt, clock and strip; the conversation; the hover card; the tab rail; the Day 1 card) are set up in the scene, which isn't in the snapshot. The code assumes 1920 x 1080 with the height near 1080 (ConversationUI.cs:316).

**Every runtime script that sets a text size.** ⚑ marks a size under 24 px.

| Where | What | Size |
|---|---|---|
| ShopUI.cs:114 | the controls strip, bottom middle | 19 ⚑ |
| ShopUI.cs:299 | "In hand …" over the prompt | 72 % of the prompt. The prompt is the scene's; the comment at 270 says 36 px, so about 26 |
| HudCorners.cs:194 / 198 / 240 | takings figure / "TODAY" / "+$6" chip | 44 / 14 ⚑ / 20 ⚑ |
| HudCorners.cs:251, 254, 621-646 | the day and the time | 16 ⚑, shrinking to 12 ⚑ for a long weekday |
| HudCorners.cs:293, 586 / 305 | OPEN, LAST ORDERS / CLOSED | 40, 23 ⚑ / 38 |
| HudCorners.cs:296, 309 / 331 | "COME IN", "CLOSING AT 7 PM", "FINISHING UP" / stock chips | 12.5 ⚑ / 18 ⚑ |
| BenchCards.cs:43 (876, 885) | the card's customer line, the finished line | 19 ⚑ |
| BenchCards.cs:44 (882) | the verb under the icon, including "[Triangle] Flip it over" | 17 ⚑ (bold) |
| BenchCards.cs:50 (891) / 51 (906) | the controls card / "[Create] Help" | 18 ⚑ / 17 ⚑ |
| DayOneGuideUI.cs:864 (930-931, 1012) | the Day 1 / featured card, full / compact | 21 ⚑ / 18 ⚑; its "also" line is 86 %, about 18 ⚑ (906-907) |
| UiPins.cs:65-76 (249-251, 287) | the name over what E acts on / Day 1's pin and "You" | 22 ⚑ / 21 ⚑ |
| ConversationUI.cs:320-330, 63 | their line / earlier lines / name / replies / footer | 32 / about 27.5 / 24 / 32 / 22 ⚑ |
| ConversationController.cs:596-598 | key chips / "noted" | about 25 / about 23 ⚑ |
| DayIntro.cs:798, 839 | skip hint / day title / line | 18 ⚑ / 60 / 28 |
| Juice.cs:215, 235, 252, 267 | the pay pop (its tip about 23 ⚑) / word pops / "Fixed!" / "Back together" | 32 / 40 / 46 / 30 |
| Barks.cs:51, 861 | lines / Ace's reply chips | 30 / 27 |
| NightCycle.cs:581-598 | night title / subtitle / notes | 76 / 30 / 25 |
| NightNotebook.cs:59-69 | title / facts / footer | 30 / 23 ⚑ / 18 ⚑ |
| NoticeMark.cs:121; StraightFaceUI.cs:156 | "?" mark; the meter's title | 38; 25 |
| CircuitPuzzle.cs:318-350 | status / result / instruction / progress, boost, retry cost / retry | 25 / 20 ⚑ / 21 ⚑ / 18 ⚑ / 20 ⚑ |
| JobTicket.cs:169-176, 284-292, 340-341, 356 | tabs: initial / name / mood / fault / "+ Latte" / call / details | 22 ⚑ / 17 ⚑ / 11 ⚑ / 14 ⚑ / 14 ⚑ bold / 13 ⚑ / 16 ⚑ (on the scene's rail canvas) |
| BeverageStation.cs:34-42 | drinks card title / action / controls, as seen at 1080p | 24 / 20.4 ⚑ / 18 ⚑ |
| CounterRepairView.cs:104-106 | counter phone caption / instruction, as seen at 1080p | 27.6 / 22.8 ⚑ |
| RecapPhone.cs (the `Words` helper, 2102-2125) | phone body text / labels / hint / headings / money | 15-18 ⚑ / 12.5-14 ⚑ / 19 ⚑ / 19-26 / 24-40 (details below) |
| HoverTooltipUI | the bench hover card | set in the scene, not in code |

The phone's lines, by kind:
- Body text at 15-18 px: 1019, 1034-1060, 1266-1274, 1428-1429, 1524-1549, 1640, 1697-1707.
- Labels at 12.5-14 px: 342, 346, 950, 1166, 1680.
- The hint at 19 px: 274.
- Headings: 1168 (19 ⚑), 1368 (23 ⚑), 1963 (26).
- Money: 955 and 1682 (40), 1170 (24).

The world-space texts at CircuitPuzzle.cs:274 (2 units) and CounterPhoneModel.cs:69 (0.19 units) aren't screen sizes, so they aren't in the table.

**Bolder.** The code sets Bold on the HUD's numbers and sign words, the bench verb, the Day 1 title, tab names and pin labels. The body text she actually reads is Normal: the bench card's line, the Day 1 step, conversation lines and replies, and the controls strip and card.

## 3. Aim help

**Where the switch lives**
- `GameSettings.AimHelp` (GameSettings.cs:162-167) just passes through to `AimAssist.Enabled`.
- `AimAssist.Enabled` (AimAssist.cs:30-52) is saved in PlayerPrefs under the key **"FixitFidget.AimHelp"** (31). That is not under the "FixitFidget.Settings." prefix every other setting uses (GameSettings.cs:52).
- Its default is **on**: `GetInt(PrefKey, 1)` (43). Setting it writes the value and calls `PlayerPrefs.Save()` at once (46-51).
- It is **not part of the save file** (SaveData has no settings). It is one value per computer, shared by everyone who plays on that machine.

**Who uses it**
- `AimAssist.Active` is `Enabled && PadInput.UsingPad` (55), so it is already pad-only. It drives:
  - first-person walking (CafeViewMode.cs:377-391);
  - the drinks close-up (BeverageLook.cs:70);
  - the D-pad steps (PlayerInteractor.cs:1120).
- The bench's pad cursor reads `Enabled` directly:
  - its friction (PadCursor.cs:113);
  - its 60 px settle onto parts (PadCursor.cs:173);
  - its snap points (ItemInspector.cs:1733-1737).
- With it off, the bench cursor never settles on a screw, spot or tool. That fits what she felt when it went on ("Better? Yeah.").

**Who writes it**
- The phone's Settings row (RecapPhone.cs:1741-1742).
- The aim-help lab (AimHelpCheck.cs). It saves the old value and turns it on (94-95), turns it off (226), back on (239), and restores the old value (313).
  - A lab restores only when its steps finish: `Restore` is called only from `Finish` (PlayLab.cs:51-78, 80-82).
  - Stopping Play between lines 226 and 239 leaves **off** saved on that machine. That is one plausible way it ended up off (low confidence; someone may simply have turned it off in Settings).

**On by default for pads, without overriding a choice**
- The code already does both: the default is on, and only a pad is ever helped.
- The real problem is a stale saved "0".
- Option A: read a new key whose default is on, and let only the Settings row write it. Every machine starts on once, and every choice made after that is kept.
- Option B: keep the key, but add a "chosen by the player" marker written only by the Settings row, and ignore a saved 0 that has no marker.
- Either way, the lab should switch the help through an in-memory override instead of the saved setting. `BenchCards.LabControls` is the existing pattern (BenchCards.cs:65-69).

## 4. The seated aim point

**How the aim point is chosen**
- `PlayerInteractor.TargetOf` (PlayerInteractor.cs:1082-1095) aims at a customer's "chest": `brain.LookTarget.position - 0.2 m`.
- It uses half the smaller width of the found collider as the target's size.
- The aim help (AimAssist.cs) then does three things around that point:
  - slows the view to 40 % within the target's size + 3°;
  - draws the view toward the point within size + 7°;
  - settles onto it within size + 4° once the stick lets go.
  - The size counts for up to 8° (constants at 57-72; Steer at 152-197).

**What `LookTarget` is**
- A serialized transform on the customer prefab (CustomerBrain.cs:196, 390). The prefab isn't in the snapshot.
- Every fallback in the code treats it as the face, about 1.6 m above the feet: ConversationController.cs:424; StationsCheck.cs:986 (chest at 1.4 m); AimHelpCheck.cs:155.
- **Standing**, the aim point is therefore about 1.4 m up, the chest.
- **Seated**, nothing moves it:
  - NpcSeating puts the root at the seat's "feet" point. The seated hips sit 0.26 m behind that point and 0.5 m above it (NpcSitData.cs:18; Placement, NpcSeating.cs:511-526).
  - NpcSeating lowers only two children, "PatienceBar" and "SpeechBubble" (Awake 251-256). It lowers them by the drop from standing to seated hips, 0.85 - 0.5 = 0.35 m (338, 1102-1117).
  - On a repair-bar stool the root rises with the seat (seatLift, 295, 855), so the look target rises with it.
- So for a seated customer the aim point stays about 1.4 m above the floor on a chair (about 1.7 m on a stool). That is about 0.35 m higher than the seated chest, around the top of the head, and about 0.26 m in front of the hips toward the table. From the side, that reads as the top-left or top-right of the head.

**Why it "sticks"**
- The customer's collider isn't shrunk when they sit (nothing in NpcSeating or CustomerBrain touches it).
- So the standing-height capsule still contains the floating point. The sight test (`InSight`, PlayerInteractor.cs:1098-1113) accepts it, and the view is slowed and settled there.
- Confidence medium: the prefab, where the look target's parent is set, isn't in the snapshot. The code never lowers it, and her description matches.
- **The same point is used elsewhere:**
  - the conversation camera's tracking target (ConversationController.cs:202-203, 424);
  - the storyteller's "is she in view" check (CustomerStoryteller.cs:71-72).

## 5. Bench highlights

**The colours**
- `BenchOutline.Guide` is UiSkin.BrandBright #7FD3A8, the "next thing" glow (BenchOutline.cs:36).
- Hover is white (33). A hold under way is (1, .82, .35) (34). "What's left" is Gold #F2A61F (38).
- BrandSoft #DCEFE1 is what the guide colour breathes toward.
- The line is an inverted hull, about 0.0035 m wide per metre of distance (40). That is about 5 px at the close-up's 40° view, and more when zoomed in. The shader draws one flat, unlit colour ("Assets/Data/Resources/Bench outline.shader").

**How "next" is chosen**
- `RepairChecklist.Build` orders the steps (RepairChecklist.cs:82-205). The first step not done is current (203-204).
- `BenchCards` rebuilds the steps every 0.1 s, and at once when a target finishes (BenchCards.cs:432-553).
- `BenchGlow.Guide` reads `BenchCards.CurrentStep` (BenchGlow.cs:225-280). It waits 0.3 s after a tick (228), then:
  1. If the step's work is on the face turned down (`under`), the whole device breathes, and nothing else glows (244-250).
  2. Otherwise, if the step's tool isn't in hand, that tool glows in its stand (252-258). It picks the one of its kind nearest the device (437-449).
  3. Otherwise, each target glows:
     - a screw, a hole or a dirt spot gets a ring on screen: 3 px stroke, radius = its projected size + 10 px (at least 14), breathing ±3 px and alpha 0.7→1 (42, 231-234, 349-357);
     - a cover, a part or the fresh part gets the outline: width 1.0→1.5×, mint↔BrandSoft, every 1.6 s (41-43, 231-233);
     - a target something hides is drawn dashed (Hidden, 427-434).
- **The stuck pulse:** after 12 s with no progress there is one pulse, 2.2× wide, white to mint over 0.5 s (235-242). This is driven by BenchCards' clock.

**The fresh part**
- The press-in step's target is the `LoosePart` waiting on its pad (RepairChecklist.cs:164-169). It gets the mint outline as a FreshPart (BenchWork.cs:182).
- The pad is the mat's pale new-part pad, #B4C3D6 (Tools/Blender/bench_stage.py:45). Mint on that pad has a luminance contrast of **1.01:1**: it is highlighted but can't be seen.
- Its empty seat isn't drawn at all. It is only a hit area: a press there sends the part (ItemInspector.cs:997-1005).

**The cloth and the brush as the next tool**
- Until the tool is in hand, the outline goes round the tool's own meshes only (BenchGlow.cs:252-258 → BenchOutline.Build 183-226).
- The line is not the "dashed through things" kind (`hidden` is false).
- The folded yellow cloth lies on cork in a tray with 6 mm steel walls at the far end of the tool rest (Tools/Blender/bench_tools.py:312, 390-395, 499-511). The wall hides part of the line, and the rest is mint on yellow #D9BD5C: **1.04:1**.
- The brush's line is mint on the golden bristles (1.32:1) and the beech handle (1.26:1).
- The tray and the rest themselves are never highlighted.
- On a pad, L1/R1 pick tools, and the glow moves on once the right one is in hand (252).

How each colour reads against the bench's surfaces (WCAG luminance ratio; 3:1 is the usual floor for graphics):

| Glow colour | Slate mat #4F70A0 | New-part pad #B4C3D6 | Cloth #D9BD5C | Cork #A57B4F | Bristle #CFA253 | Beech #C9A878 |
|---|---|---|---|---|---|---|
| Mint #7FD3A8 (now) | 2.83 | **1.01** | **1.04** | 2.13 | 1.32 | 1.26 |
| BrandSoft #DCEFE1 | 4.20 | 1.49 | 1.54 | 3.15 | 1.95 | 1.87 |
| Gold #F2A61F | 2.47 | 1.14 | 1.11 | 1.85 | 1.15 | 1.10 |
| Red #B8483C | **1.03** | 2.91 | 2.82 | 1.38 | 2.22 | 2.32 |
| White | 5.05 | 1.79 | 1.84 | 3.79 | 2.35 | 2.24 |
| Ink #1F1B17 | 3.39 | 9.55 | 9.27 | 4.52 | 7.29 | 7.62 |

No single flat colour clears 3:1 on both the mat and the pad. A two-tone line does: dark outside, light inside. Hue still differs (red against blue), but luminance is what reads at a glance.

**Where to set a carried device down**
- Nothing in the world is ever highlighted while carrying.
  - The bench's slots are bare transforms (ItemSlotArea.cs:9, 78-93).
  - `DropSpot` has no visual (DropSpot.cs).
  - The bench station and the drop spot give words only: "Set down", "No room here" or "Put it on the intake tray" (StationInteractable.cs:174-184; DropInteractable.cs:87-100).
  - `SetFocused` does nothing for stations (Interactable.cs:20).
- The only pointers are on screen:
  - Day 1's green guide pin, "Repair bench", while the featured visit's card says "Carry the … to the repair bench" (DayOneGuideUI.cs:523-528, BenchPoint 694-700; the pin is UiPins.cs:249-251). It runs only for the guided or featured visit (DayOneGuideUI.cs:240-253), not for walk-ins or later days.
  - The name pin over whatever E acts on, once she is in reach (FocusNameUI → UiPins).
- `BenchInteractable` covers parts inside the close-up only. There, the device is moved to the middle of the mat by itself (ItemInspector.cs:1202-1224), so nothing needs placing.

## 6. The pocket watch's cleaning

**The watch's objects**
- Direct children of the prefab root, in this order: Cylinder (the case), Dookie, Crown, BackPlate, Screw0-3, JobMarker, Grime_Extra_Watch_1, Grime_Extra_Watch_2, Part_Extra_Mainspring, Fresh, Smudge_Crystal. Source: the Devices 2 report, which writes each object's path from the root.
- The watch's face is +Y; the case back unscrews.

| Object | Place, real size (mm) | Size | Kind and tool | Fault |
|---|---|---|---|---|
| Dookie | (10.0, -4.3, 6.7): on the movement under the back plate | 16.7 mm across | dirt → brush | "Debris Inside, Won't Tell Time" (fault 0) |
| Smudge_Crystal | (4.0, 3.8, -3.3): a thumbprint on the crystal's plane, a hair over the bezel top | 15 mm across | smudge → cloth | fault 0. Added on 9 Oct by Bench 4 to give that fault a cloth job (BenchFeelSteps.cs:18-19, 116-140) |
| Grime_Extra_Watch_1, _2 | (-16.1, -4.3, 10.8) and (-5.0, -4.3, 3.7) | 16.7 mm across each | dirt → brush | "Seized Movement" (fault 1), with the mainspring swap (FaultExpansionSetup.cs:166-210) |
| Screw0-3 | (±10.7, -6.9, ±10.7): on the back plate's dome | — | they hold the BackPlate (43.2 × 2.2 × 43.2 mm, at y -5.3) | — |

- The tool a spot needs is set by its kind: a smudge needs the cloth, dirt needs the brush (GrimeSpot.cs:61). With the wrong tool, the hover says "Needs the cloth" or "Needs the brush" (97).
- Day 3 brings the Debris watch, the cracked phone and the Seized watch (Assets/Data/Days/Day_03_TheRealGame.asset:56-64; DaysOneToThreeSteps.cs:594).
- Payout: Content step 2 set the Debris fault to $75 (FaultExpansionSetup.cs:162-163). The prefab may have changed since.

**The finish rule**
- **Clean radius** (GrimeSpot.cs:434-445; BenchRules.cs:220-259):
  - Cloth = 1.0 × the spot's radius; brush = 0.75 ×. The cap is 0.6 × the tool's contact (27 mm for the cloth, 14.4 mm for the brush). It never goes under 10 px on screen.
  - A pad's strokes reach 1.35× further (`PadRubReach`, BenchRules.cs:236-243; used at ItemInspector.cs:763).
- **For the watch:**
  - Crystal under the cloth: 7.5 mm with a mouse, 10.1 mm with a pad.
  - Dookie under the brush: 6.3 mm with a mouse, 8.5 mm with a pad.
- **Grease smears first.** The first half-pass over a fingerprint takes only 15 % and smears it (BenchRules.cs:226-228, 283-297; GrimeSpot.cs:574-637).
- **The finish:** at 90 % the spot is "armed". It then finishes after the footprint has travelled 0.6 × its diameter over it (9 mm on the crystal), or after 0.4 s of a pad rubbing in place (BenchRules.cs:229-231, 299-303; GrimeSpot.cs:490-520). At 97 % it finishes at once (48, 509).

**How the cursor picks while rubbing**
- **The picker** decides what a press starts and what the hover card says (ItemInspector.cs:905-932).
  - It sorts candidates into classes: 1 = the current step's targets the tool can act on; 2 = other targets it can act on; 3 = step targets it can't act on; 4 = everything else (BenchRules.cs:114-123).
  - Within a class, the one nearer the middle of its hit area wins.
  - Screws and holes hit within a circle of max(28 px, head + 10 px) (BenchRules.cs:25-35; ItemInspector.cs:1063-1072).
  - Spots hit within their outline + 16 px (1021-1044); parts +14, covers +12, tools +16.
  - A thing on the device that faces away (its normal more than 100° from the line to the camera) is never a candidate (ItemInspector.cs:965-966, 1024-1025, 1126-1130).
- **The rub itself** doesn't use the picker (745-789). Each frame it casts the cursor onto the plane of every spot of the tool's kind that is not covered and **faces the camera** (760-761), and it wipes there.

**Can a screw hole take the pointer or show "Screw hole" during a cloth step?** Yes, when the watch lies back-up.
- When a screw comes out, the hole it leaves is made under the screw's home parent, which is the watch root (Screw.cs:203-205; ScrewSocket.Make 337-372). It sits at the screw's seated place on the back plate's dome.
- When the back plate flies to its well, the four holes stay behind. They float about 2 mm beyond the case's back rim and about 3 mm over the open movement.
- Their face points out of the watch, so with the watch back-up they face the camera and are candidates (ItemInspector.cs:981-986).
- While the plate is off they can't act (Screw.cs:470; ScrewTarget.cs:33), so they are class 4.
- With the crystal smudge facing the mat it isn't a candidate at all. So near the middle of the back, a hole is the best hit.
- The hover card names it "Screw hole" (Screw.cs:467) and adds "The cover goes back on first" (ItemInspector.cs:1146-1158). The card shows whenever the cloth isn't actually rubbing something (HoverTooltipUI.cs:74-80).
- This is almost certainly her "It just said something about the hole".

**Why the cloth "didn't work": the checklist never says to flip for the crystal**
- **The test.** RepairChecklist decides that a spot is on the face turned down from where its middle sits relative to the middle of the device's drawn box: within 45° of straight below (RepairChecklist.cs:135, with `Under` at 219, threshold 0.7).
- **Why the crystal fails it.** The watch is thin (about 11 mm) but long. The case's bow and the crown run its drawn box from about -24.5 to +42 mm, so the box's middle sits about 9 mm toward the bow. The crystal smudge is off-centre the other way.
  - With the watch back-up, the smudge is about 5 mm below the box's middle and about 13 mm to the side: about 68° off straight down (0.37 against the 0.7 needed).
  - Even without the bow it would be about 45° (0.70, which is not over 0.70).
- **So, after the brush step:**
  - **The card** shows the cloth icon with "Wipe" (BenchCards.cs:548-551), not "[Triangle] Flip it over" with the gold badge.
  - **The glow** first rings the cloth in its tray. Once the cloth is in hand, it draws a dashed ring round the smudge as seen through the watch (BenchGlow.cs:252-279, 427-434). The device never breathes.
  - **The rub and the picker** both skip the smudge, because it faces away (ItemInspector.cs:761, 1025).
  - **The cursor helps don't reach it:** the pad's snap and the D-pad steps work only on candidates, and the smudge isn't one (1716-1754). Tab ("what's left") lights the smudge gold, under the watch (GrimeSpot.cs:708-717).
  - **The stuck helps leave out the flip.** After 20 s stuck, the first repair's short controls card comes back (BenchCards.cs:585, 593-595), but it has no flip line (171-181). Only the full card, held 0.35 s, has one (192).
- **The comment is out of date.** RepairChecklist.cs:121-125 says it avoids the patch's own "up" because the watch's debris was authored facing the dial. But `GrimeSpot.Normal` already points to the side the dirt shows on (GrimeSpot.cs:110, 145-151). The picker and the rub use exactly that.
- **No lab covers this job.** The clumsy-hand lab runs Camera clean, Camera swap, Phone screen and Watch seized; not Watch debris and not Phone gunked (ClumsyBenchLab.cs:76-77). The editor's reachability check only asks whether each spot faces the camera at some orientation (DeviceCleaningReachabilityChecks.cs:16-19, 99), and the crystal does when the watch is face up.
- **Once flipped, the crystal cleans normally.** It is flat (no dome), nothing covers it, and no part or hand sits over it.
- **Other things checked:**
  - **No watch spot needs a tool it can't get.** Dookie is under the back plate until the plate is off (GrimeSpot.cs:655-699: by geometry, the plate's box grown inward holds it), and is then on the visible movement.
  - **The hover card says "Dookie".** It shows that object's name, because `PartName` only strips words like "grime" and "smudge" (GrimeSpot.cs:68-88). It is player-visible placeholder text.
  - **The cloth "looked like a glob".** The held cloth pad is 40 × 45 mm, and 50 × 52 mm when pressed (Tools/Blender/export_bench_tools.py:55-60). That is as wide as the whole 50 mm watch and three times the 15 mm smudge. The code that keeps the smudge visible (ToolPickup.cs:28-36, 312-339) leads with the cloth's far edge 4 mm under the cursor, so most of the cloth covers the glass.
- **If she closed the watch anyway,** it had 1 of 2 tasks done: Quality 0.5, which is Passable (section 7). "Back together · Passable" pops (Juice.cs:819-820). The card stays on "Wipe" and never folds into "Take it to …", because the job never finishes (BenchCards.cs:498-507).

**Is there a cloth step right after a brush step on the watch?**
- Yes, on "Debris Inside" only.
  - The brush comes first because Dookie, a brush spot, is the watch's first dirty spot in the prefab's order. The rule is RepairChecklist.cs:121-150 (`smudgesFirst`).
  - Brush: wipe the grit off the movement, inside the open back. That is the fault itself: "there's grit inside; it won't tick" (FaultSpeech.cs:43).
  - Cloth: wipe a thumbprint off the crystal on the other face. It has nothing to do with the fault, which is why it felt pointless, and it needs a flip.
- "Seized Movement" has two brush spots and the mainspring, and no cloth.
- **Elsewhere:**
  - **The "Gunked Up" phone** (a Day 1 and Day 2 walk-in) has dirt, smudge, dirt in that order on the same glass (BenchFeelSteps.cs:60-73). Because the order rule is re-run as spots clear, its card goes brush, then wipe, then brush again. After the first dirt spot is clean, the "first dirty spot" is the smudge (RepairChecklist.cs:140-142).
  - **Grace's camera** wipes the lens first, then brushes the film path. The lens spot is made first (GraceShowcaseSetup.cs:155-160).

## 7. The checklist card and the grade

**The card** (BenchCards.cs; RepairChecklist sets no sizes)
- **Place and size:** top right, 24 px from the edge and 132 px down, under the takings (41). It is 360-480 px wide (42).
- **Text:** the customer's line is 19 px Normal (43, 876). The verb under the current icon is 17 px bold (44, 882).
- **The flip text** is `FlipWords()`, "[Triangle] Flip it over" (211). It is shown in that same 17 px verb slot (548-549), with an 18 px gold badge with an arrow on the icon (45, 787-789, 963-968).
- **Icons:** 56 px for the current step, 36 px for the rest, on a 64 px disc (44).
- **The locked decision** reads "the checklist as one small paper card on screen, under 25 words" (Docs/Studio/BIBLE.md:72). Bigger type keeps the word budget (WordBudget.cs:30-41), but "small" is his word.

**The grade**
- **Quality** = tasks done ÷ tasks (RepairJob.cs:24-34, 38-58, 62-70). Tasks are grime spots, parts, circuit tiles and human tasks.
- **Grade:** Perfect at 0.999 or more, Good at 0.66 or more, Passable above 0, Rejected at 0 (JobBase.cs:49-59).
- **Hand-back gate:** the device must be in one piece (`CanHandBack`, 39). The grade is not a gate.

**What a missed step costs**
- **Base pay:** the payout × 1.25 (Perfect), 1.0 (Good), 0.6 (Passable) or 0 (Rejected) (JobBase.cs:61-67; CustomerBrain.cs:2097-2103).
- **Tip:** base × 0.6 × speed × the customer's tip factor × the reassure penalty (CustomerBrain.cs:22, 2100-2107). So the tip shrinks with the base.
- **Review** (ReputationRules.cs:107-147):
  - Perfect or Good: "Loved it" (+2 reputation).
  - Passable: "Liked it" (+1), or "Fine" (0) if they also waited a long time.
  - Rejected: "Never again" (-2).
  - Note that Good costs nothing in reputation; only pay tells it from Perfect.
- **Stars:** they come from total reputation, at 5 / 30 / 120 / 300 / 580 (94, 157-162). A review card on the recap shows 1-5 stars (175).
- **The recap** counts Perfect, Good and Passable (DayClock.cs:451-462).
- **The watch's Debris job** has 2 tasks, so one miss means Passable:
  - Base $45 instead of $94 (×0.6 instead of ×1.25 on $75).
  - The tip is $27 instead of $56, at full patience with a tip factor of 1.
  - The review is "Liked it" instead of "Loved it".
- So **he's right: it already costs.**

**What she is told**
- **Closing up with faults left:** a quiet "Back together · Passable" pop at 30 px for 1.6 s (Juice.cs:257-269, 819-820).
- **The prompt before she commits:** "Hand it back (Passable)"; for Perfect it's just "Hand it back" (CustomerInteractable.cs:71-85).
- **On hand-back:**
  - the "+$45 +$27 tip" pop (Juice.cs:205-224);
  - the customer's line, e.g. "It works, but it could use more care…", with a worried face (CustomerIdentity.cs:436-448);
  - a "dots" badge for Passable (NpcBeats.cs:589-593, 661-662).
- **On Day 1's featured visit only:** "Passable repair. Take it to …; Cross hands it back. You can keep repairing for a higher grade." (DayOneGuideUI.cs:505-513).
- **Nothing says what was missed or what it cost.**

## 8. The HUD (HudCorners.cs; line ranges for a rewrite)

**The pieces**
- **Header and layout notes:** 6-31.
- **Size constants:** 43-47 (bill 70 × 31, edges, SignWidth 232, SignHeight 124, Border 3, BarHeight 12).
- **Build:** 158-178. The edge glow is built at 163-168. The top and bottom groups are at 170-172; the cash is in "top" and the sign in "bottom". The quiet group is at 177.
- **The cash stack:** `BuildCash` 189-243.
  - The box: 192 (top right, (-24, -18), 300 × 92).
  - The figure: 194-197 (44 px bold, right-aligned in a 196 px box).
  - "TODAY": 198-202.
  - The stack: 204.
  - The glow: 205-209; the edges: 211-223; the bill: 225-231; the paid chip: 233-242.
  - Live: `UpdateCash` 392-470, `Paid` 472-479, `SetEdges` 481-488, `StackDepth` 493.
- **The gap.** The stack's x is fixed at -196 - 14 - 40 (204), which is the figure box's full width, not the figure's.
  - So the bill's right edge is always 215 px from the cash box's right edge, while the number is right-aligned against that edge.
  - The empty space between bill and number is about 215 px minus the number's width. At about 25-28 px per character at 44 px bold, that is about 160 px for "$0", 135 px for "$24" and 105 px for "$142".
- **The sign:** `BuildSign` 245-315.
  - The box: 248 (bottom left, (24, 20), 232 × 196).
  - The strings: 269-277; the sway and flip pieces: 279-282.
  - The OPEN face: 284-298 (Brand border, Paper face, cup mark, the word at 40 px, "COME IN" at 12.5 px).
  - The CLOSED face: 300-313 (Red border, Ink face, the word at 38 px RedSoft).
  - The states: `ShowState` 580-608 (LAST ORDERS on GoldSoft, word 23 px); `ClosingWords` 610-614.
  - Live: `UpdateSign` 497-578. The state is 500-513: LAST ORDERS at the closing hour minus 1 (501-502), with the "hud.lastorders" bell at 511. The flip is 515-529. The sway is 531-537: 2.8° every 1.15 s at last orders, against 1° every 5.5 s when open.
- **The day bar:** the track 257-259 (232 × 12, Ink at 62 %) and the fill 260-267 (Brand green). Its colour by state (green, Gold, Red) is set at 539-544.
- **The day and time line:** 250-256 (16 px). The text is written at 562-570 (`Weekdays.Label`: "Wednesday · Day 3", Weekdays.cs:39-43). It is shrunk to 12 px when a long weekday doesn't fit (616-679).
- **The edge glow:**
  - It is a full-screen image (163-168) with a radial texture (894-904).
  - At last orders it pulses amber (UiSkin.Gold) at alpha 0.10-0.17 every 2.2 s. At closing a red (UiSkin.Red) glow peaks at 0.22 and fades over 1.8 s (546-560).
- **The stock chips:** `BuildChips` 317-336, `UpdateChips` and `ChipFor` 689-733.
- **Quiet at the bench:** "TODAY", the sign's under-line and the day line fade (101-109, 366-370). The sign and bar stay.
- **The bottom group fades** while a conversation or the counter phone is open (`BottomBusy` 377-388; 363-365).

**What depends on these places**
- The bench card sits at 132 px, under the takings (BenchCards.cs:41).
- The tab rail keeps 380 px clear on the right, and 512 px at the bench (TicketRailUI.cs:29, 36).
- The pins keep 150 px clear at the top (UiPins.cs:84).
- The night's clock takes the top-right corner at night, when the corners are hidden (ShopUI.cs:89-102).
- The pause lab reads the cash and sign rects (Diagnostics/PauseCheck.cs:108-135, 217, 569).

**UiClock** (UiClock.cs:16-48) is the screen's own clock. It stops while the phone pauses the game; the HUD's animation times run on it. It is not the day's time.

**DayClock**
- The day is 420 s (DayClock.cs:28-32). The scene holds its own value.
- Hours: opening 9, closing 20 (35-39); the scene may override them.
- The café time runs from opening to closing across the day (61-66).
- At 0 s left: `IsOpen` becomes false, the "day.lastorders" sound plays, and no new arrivals come (238-248). It then waits for everyone still inside (251-296) and ends the day (298+).
- "LAST ORDERS" on the sign is the HUD's own rule: closing minus 1 hour, so 7-8 PM, the last 38 s or so of the day (HudCorners.cs:500-502).

**CafeDaylight.** Yes, the light changes over the day, and there is a sunset (CafeDaylight.cs:140-221).
- **The sun:**
  - It swings from 110° to 255° in azimuth across the opening hours (181-182).
  - It warms to orange (1, .42, .18) over dusk, 16:24 to 19:00 (144, 156). That begins about 283 s into the 420 s day.
- **The sky and ambient** shift to evening colours (158-162); night comes in from 18:48 (143).
- **The café's warm lamps** go from 40 % by day to full from 17:00 to about 19:18 (215-218).
- **Window and lamp glow:** emissive materials glow up over the same span (219-220; `eveningGlow`).
  - Which windows and lamps glow is scene data: `emissiveRenderers` and `emissiveMaterials`, filled by CafeStreetUpgrade.cs:933-937.
  - The warm lights are added by CafeRoomSteps.cs:1306 and CafeLivedInSteps.cs:2425.
- So the world already says "evening" before last orders, warmly.

## 9. Conversation double text

**How it draws**
- `ConversationUI.Render` (ConversationUI.cs:279-294) shows up to `MostLinesOnScreen` = 3 lines (64): the newest in full, and up to two earlier ones in `EarlierLine`, "<size=86%><color=#B9B3A8>" (63). That is the grey line above the new one, at about 27.5 px.
- A speech is split on "\n" (170-185, 302-312).
- Each line waits 1.4-4.5 s, at 18 characters a second (44, 296-297).
- E, through `SkipReveal` (237-251), shows every line at once, earlier ones dimmed, and brings up the replies.

**What removing the earlier lines would affect**
- The reason they're kept is that what the customer asked for stays on screen while Ace picks a reply (13).
  - With one line, the replies come up under the last line only. Grace's two-line intake (`GraceCameraEpisode.Intake`) would lose its first line by then.
- **E would skip unread lines.** SkipReveal jumps to the last line. With one line on screen, E would hide the lines it skipped, so E should step one line at a time instead.
- **A slow reader gets less time.** The 1.4-4.5 s read time becomes the only chance to read each line.
- **Layout adjusts itself.** The name card and the replies are laid out from the line's height (PlaceNameCard and LayOutReplies, 292-293).
- **The labs still pass.** The conversation lab checks counts, not text (`LinesShown` and `LineCount` stay right: ConversationCheck.cs:131-137).
- **The morning face uses the same panel** (MorningFace.cs:114, 139, 160, 229).

## 10. Grace's latte request

**How the order arrives.** Grace's data has `drinkWishChance: 1` and her usual latte (Regular_Grace.asset:31-33). Her lines are "My usual latte, please. No sugar." and the like (64-65, 83-84, 103-104). The Day 1 suppression is in CustomerSpawner.cs:760-766 and is intentional.

**Where it is shown, and how big**
1. **Her call across the room.** Once she has settled to wait, she beckons (NpcBeats.cs:544-547) and says the line in her own speech bubble (CustomerBrain.cs:1800-1841, `Say` 2494-2531).
   - The bubble is a world-space TMP text in the customer prefab, so its size is the prefab's (not in the snapshot) and it shrinks as the overhead camera pulls out.
   - It reveals at 45 characters a second and stays 2.5 s after (CustomerBrain.cs:25-27, 2472-2476), so about 3.2 s for her line.
   - When she is seated, it is lowered with her (NpcSeating.cs:251-256).
2. **Her tab, top left.** "+ Latte" at 14 px bold (JobTicket.cs:175-178, 105-106, 131-133, 237-242). Her name is 17 px, the device and fault 14 px, the mood 11 px. All are hidden at the bench (compact chips).
3. **The featured visit's guide card,** at 21 or 18 px: "Walk to the drinks machine… Cross brings it into view", "Take the latte to Grace. Cross serves it." (DayOneGuideUI.cs:400-452).
4. **At the machine,** the drinks card's title is the paddle's drink name, about 24 px on screen (BeverageStation.cs:34-36, 66, 78-79).
5. **The tab's detail panel** is 16 px, and only for a mouse (JobTicket.cs:347-377).

**Pictures available**
- There are no drink sprites or icons. Resources holds only the bench icons.
- Drawn in code:
  - Juice's round cup badge (`Juice.Icon.Cup`: a brown cup with steam on a paper disc, 58 px; Juice.cs:61, 1014, 1153-1163). Today it shows only on Served (NpcBeats.cs:659), through `Juice.Emote` (Juice.cs:177-198), for 1.5 s.
  - The sign's cup mark (HudCorners.cs:862-880).
- Per drink:
  - a liquid colour (`DrinkLiquidVisual.LiquidColor`: latte .65/.40/.18, tea, chocolate, espresso; DrinkLiquidVisual.cs:76-84);
  - `DrinkDefinition.cupColor` and `cupPrefab` (DrinkDefinition.cs:19-21);
  - the 3D paper cup built in code (DrinkLiquidVisual.cs:24-51).
- A real "latte" picture would be new art. The bench icons' Blender pipeline (Tools/Blender/render_bench_icons.py) could render one.

## 11. The pouring percentage

**Where it is drawn**
- The drinks machine's action card: `"Pouring " + 63 + "% · prepare another cup while it fills"` (BeverageStation.cs:80-81). It is 17 px, about 20 px on screen.
- When the crosshair is on a paddle, the same card shows the paddle's prompt instead: `"Pouring latte · 63%"` (BeverageControl.cs:15, shown through BeverageStation.cs:82-83).
- `BeverageSlot.PourPrompt` already says "Pouring latte…" without a number (BeverageSlot.cs:33).
- No lab reads the percentage.

**A fill visual already exists**
- The pour calls `cup.ShowPourProgress`, which runs `DrinkLiquidVisual.SetFill`: a rising liquid surface in the drink's colour, with ripples while pouring (BeverageSlot.cs:101-120; DrinkJob.cs:123-126; DrinkLiquidVisual.cs:53-66).
- A coloured stream also runs from the nozzle to the liquid's surface.
- The Day 1 card says "Latte is filling. You can prepare another cup while it pours." with no number (DayOneGuideUI.cs:435).

---

## Least-invasive plan

None of these adds a mechanic. Every change is to words, sizes, colours or placement, plus two bug fixes (6a and 6b).

1. **Button icons instead of names.**
   - **New art:** a TMP Sprite Asset of button glyphs: PS shapes, Xbox and Switch letters, keyboard keys. A CC0 input-prompt set would do, with no platform logos.
   - **One place makes the glyph:** have `PadInput.Label` (or ControlHints) return a sprite tag for pads, so every caller gets the icon with no other change.
   - **One place lets text show it:** assign the sprite asset in `UiSkin.UseFont`, which nearly every text already passes through.
   - **Drop the brackets around a glyph** in the four composers: ShopUI.Key, BenchCards.K/KP, ConversationController's chips and footer, Barks' chips.
   - **Turn rich text on** where it's off: RepairOverlayUI.Text for the drinks and counter-phone lines, and the phone's hint (RecapPhone.cs:274).
   - **Update the labs' string checks.**
   - **Files:** PadInput.cs or ControlHints.cs, UiSkin.cs, ShopUI.cs, BenchCards.cs, ConversationController.cs, Barks.cs, RepairOverlayUI.cs (or BeverageStation.cs and CounterRepairView.cs), RecapPhone.cs. Labs: Editor/ControllerChecks.cs, Diagnostics/BenchViewProbe.cs, Diagnostics/BenchWordsCheck.cs. New: the sprite asset and its PNGs.
2. **Bigger, bolder text.**
   - **Set a floor of 24 px** for anything she must read. In order:
     - the bench card's line, verb and flip badge (BenchCards.cs:43-51);
     - the day and time (HudCorners.cs:251-256, 621);
     - the Day 1 card (DayOneGuideUI.cs:864);
     - the controls strip (ShopUI.cs:114);
     - the tabs (JobTicket.cs:169-176, 284-292);
     - the pins (UiPins.cs:65);
     - the conversation footer (ConversationUI.cs:330);
     - then the phone's body text (RecapPhone.cs).
   - **For weight:** drop a font with real Bold and SemiBold into the existing swap point, `Resources/UI/UI font`. No code is needed (Baloo 2 is already proposed). Or thicken the shared font material (a face-dilate setting), and set Bold on the body texts above.
   - **Files:** the scripts named above, plus one font asset.
3. **Aim help on.**
   - Read the switch from a new key, default on, so a stale "0" is forgotten once.
   - Let only the Settings row write it.
   - Make the aim-help lab use an in-memory override instead of the saved setting.
   - **Files:** AimAssist.cs, Diagnostics/AimHelpCheck.cs.
4. **Seated aim point.**
   - **Smallest fix:** in `PlayerInteractor.TargetOf`, aim a seated customer from their head bone instead of the look target. The code already finds it: `Barks.HeadPointOf`, less its clearance and about 0.25 m.
   - **Broader fix:** lower the look target with the patience bar and speech bubble when someone sits. That also fixes the conversation camera and the storyteller's check.
   - **Files:** PlayerInteractor.cs; or NpcSeating.cs plus the look-target child's name in the customer prefab.
5. **Bench highlights.**
   - **(a) Colour.** Draw the guide line in two tones, dark ink outside and a light core, so it reads on the pale pad, the yellow cloth and the slate mat. Breathe it harder (width and brightness) so it pulses rather than simmers. Red alone would disappear on the mat.
     - **Files:** BenchOutline.cs (the guide colour and a second hull) and BenchGlow.cs (breath and width constants, 41-43, 231-234).
   - **(b) The tool to pick up.** Also put a pulsing on-screen ring round the tool in its stand, using the ring the glow already draws for screws, so the tray wall can't hide it. If he wants it, outline the tray too.
     - **File:** BenchGlow.cs (252-258).
   - **(c) The fresh part.** It is covered by (a). Optionally ring its empty seat too.
     - **File:** BenchGlow.cs.
   - **(d) Where to set the device down.** Show the existing green "Repair bench" pin on every day, whenever Ace carries a device not yet on the bench, not only on Day 1's featured carry.
     - **Files:** DayOneGuideUI.cs (523-528), or UiPins.cs.
     - An in-world ring on the next free slot would also need a read-only "next free slot" in ItemSlotArea.cs.
6. **The watch.**
   - **(a) Make the checklist test the spot's visible side.** It should use `GrimeSpot.Normal`, the same test the picker uses. Then the crystal step says "[Triangle] Flip it over" and the whole watch breathes.
     - **File:** RepairChecklist.cs:135 (and its comment at 121-125).
   - **(b) Hide the empty screw holes while their cover is off,** and leave them out of the picker, so no "Screw hole" card can appear over the open back.
     - **File:** Screw.cs (ScrewSocket, 337-487).
   - **(c) His call: drop or move the crystal smudge.** Dropping it from the Debris fault makes the job a single task, so Perfect or nothing. Moving it onto the movement side means no flip.
     - **Files:** the PocketWatch prefab's fault 0 (made by Editor/BenchFeelSteps.cs:116-140).
   - **(d) Add the watch's Debris job and the phone's Gunked Up job to the clumsy-hand lab.**
     - **File:** Diagnostics/ClumsyBenchLab.cs:76-77.
   - **(e) Rename "Dookie"** in the watch prefab.
7. **The card and the grade.**
   - **Size:** raise the card's sizes (BenchCards.cs:43-51), keeping it one card and under 25 words.
   - **Say what was missed:** the costs already exist. Add the missed task to the existing pop, e.g. "Back together · Passable · 1 spot left" (Juice.cs:257-269), and to the hand-back prompt (CustomerInteractable.cs:71-85).
   - **Files:** BenchCards.cs, Juice.cs, CustomerInteractable.cs.
8. **The HUD.**
   - **Move the sign to the top right and put the cash under it.** That means re-anchoring the sign box (248), the cash box (192) and their group (170-176). Then:
     - move the bench card down (BenchCards.cs:41);
     - check the tab rail's reserve (TicketRailUI.cs:29, 36) and the pins' top reserve (UiPins.cs:84);
     - update the pause lab (PauseCheck.cs).
   - **Close the gap:** set the bill's place from the number's measured width each time the number changes (204, 402, 425).
   - **Restyle or remove the day bar** (257-267, 539-544).
   - **Remove the edge glow** (163-168, 546-560). The LAST ORDERS sign, its bell and CafeDaylight's existing sunset, lamps and window glow already say "evening".
   - **Raise the day and time to 24 px or more** (250-256, 616-679), and widen the sign if needed (46).
   - **Files:** HudCorners.cs, BenchCards.cs, TicketRailUI.cs, UiPins.cs, Diagnostics/PauseCheck.cs.
9. **Double text.**
   - Show only the current line (ConversationUI.cs:279-294, or `MostLinesOnScreen` at 64 set to 1).
   - Make E step one line at a time (SkipReveal, 241-251), so nothing is skipped unread.
   - Consider a longer minimum read time (296-297).
   - **File:** ConversationUI.cs; check Diagnostics/ConversationCheck.cs.
10. **Grace's latte.** Show the order as a picture that stays.
    - Put the existing cup badge, tinted with the drink's colour, beside the patience bar over her head while the drink is owed. UiPins already draws the job's dot there.
    - On the tab, put "+ Latte" at 24 px or more, with the same cup badge.
    - Optionally send her call through the screen-space barks (30 px, the same size at any zoom) instead of the world-space bubble.
    - **Files:** UiPins.cs, JobTicket.cs, Juice.cs (to expose a tinted cup sprite); optionally CustomerBrain.cs (1825-1826).
    - Drink-specific pictures (latte against tea) would be new art.
11. **Pouring %.**
    - Remove the number from the two strings: "Pouring latte · prepare another cup while it fills". The cup's rising liquid already shows how far it has got.
    - **Files:** BeverageStation.cs:81, BeverageControl.cs:15.
