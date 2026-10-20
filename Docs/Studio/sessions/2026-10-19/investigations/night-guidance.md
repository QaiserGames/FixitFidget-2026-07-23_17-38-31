# Investigation: what tells the player what to do (day guide, night, notebook, franchise goal, stock)

Read-only, code only. All paths are under `/root/code`. Two things are not in the snapshot: the café scene (`Assets/Playtests/AcesCafeLayoutPlaytest.unity`) and the Day 1 and Day 2 assets the scene plays (`Assets/GraceShowcase/Day_01_GraceCamera.asset`, `Day_02_GraceReturn.asset`). Where the scene could change a value, I give the code's default and say so. For Days 1–2 I read the editor step that writes and checks those assets.

**How the nights are named.** Night 0 is the start of the first night (after Day 1's recap): the bins and the deal. Night 1 is the rest of that same night: Barnaby. Night 2 is the night after Day 2: the cups. Day 1 is a Monday, so the cards say "Monday night", "Tuesday night".

**One transcript line is from the day, not the night.** "Wait for Grace to reach the counter" is the Day 1 guide card (`Assets/Scripts/DayOneGuideUI.cs:393`). No night text says it.

---

## 1. The night's flow, step by step

### 1.0 What is on screen all night
- **Top right:** the night clock, e.g. "Monday night   11:04 PM" (`ShopUI.cs:180-187`).
- **Bottom middle:** the prompt line, e.g. "[Cross]  Take the bins out" (`ShopUI.cs:189-196`). The scene's prompt text is about 36 px (`ShopUI.cs:270-272`, comment).
- **Under the prompt:** the controls strip, 19 px (`ShopUI.cs:104-113`). On a pad at night it reads: "Share  First person    Right stick  Orbit    L2 / R2  Zoom    Options  Pause    L3  Sneak    Square  Torch    D-pad up  Notebook" (`CafeViewMode.cs:166-172` + `ShopUI.cs:161-162`).
- **Notes:** one at a time, bottom middle, 250 px up, 25 px text (`NightCycle.cs:586-600`). The default is 4.5 s (`NightCycle.cs:157`). A new `Note()` replaces the one on screen at once (`NightCycle.cs:541-553`). `NoteThen()` waits up to 10 s for it to finish (`NightCycle.cs:174-187`).
- **Barks:** 30 px lines pinned over the speaker's head, with no name (`Barks.cs:15-17, 51`). Each line stays up for its reading time: 0.6 s + 1/18 s a character, between 1.4 and 4.5 s (`BarkRules.cs:27, 47-51`), then a 0.25 s beat (`Barks.cs:611-615`). In a scene that holds Ace, Cross or E moves the line on after 0.45 s (`Barks.cs:68-69, 527-529`).
- **Nothing else.** At night there are no pins (`UiPins.cs:380`), no guide card (`DayOneGuideUI.cs:185-191`) and no HUD corners (`HudCorners.cs:344-346`).
- **The clock:** 11 PM to 4 AM in 4 real minutes, 48 s an hour (`NightWalk.cs:141-150`; no editor step changes it). It is held during the bins and starts when the man's scene ends (`NightZero.cs:137-138, 432-433`). At 4 AM the night ends by itself (`NightCycle.cs:299`).

### 1.1 Nightfall (every night)
The recap's button "Close up for the night" (`RecapUI.cs:60`) runs `NightCycle.Dusk` (`NightCycle.cs:217-252`):
- **A card.** The title is 76 px and the line 30 px (`NightCycle.cs:581, 584`). It reads "Monday night" with "The café is closed. One last job: the bins." on Night 0, or "The café is closed. The bins first, then the night is yours." later (`NightCycle.cs:225-227`). It is readable for about 1.4 s plus the fades (`NightCycle.cs:221, 243-245`).
- **A note, 6 s:** "Last job: the bins. Out the back." It shows every night that opens at the bins, not only Night 0 (`NightCycle.cs:250`).
- **Ace's start:** just inside the café's back door, facing it, with the bin bag in hand (`NightZero.cs:139-141`; `Editor/NightZeroSteps.cs:257-258, 580-581`). Only the bins' things are offered (`NightInteractable.cs:16-18`). Ace can't call it a night until the man's scene is over (`NightCycle.cs:94`).
- **The morning before:** nothing mentions the bins. I found no text in the recap or the morning that does (grep of `RecapUI`, `RecapPhone`, `DayIntro`, `MorningFace`, `LodgerDay`).

### 1.2 Night 0: the deal
**How the game knows the step:** `NightZero.Step { AtTheDoor, Outside, Binning, Reveal, Deal, Done }` (`NightZero.cs:49, 74`). While Ace holds the bag, the step follows where Ace is (`NightZero.cs:152-153`). `NightZero.Pending` means not Done (`NightZero.cs:72`). The man is `Lodger` (`Up`, `Hidden`, `Returning`: `Lodger.cs:74-82`).

1. **AtTheDoor.** The prompt "Take the bins out" shows within 1.6 m of the back door's inside place (`NightBackDoor.cs:22-24, 46, 52-53`). Cross gives a 0.4 s dip to black, and Ace is outside on Back Street facing the dumpster (`NightCycle.cs:484-502`; `NightZeroSteps.cs:259`). Going out the front door also counts as Outside (`NightZero.cs:31-34`).
2. **Outside.** The prompt "Bin it" shows in front of the dumpster's near half (`NightBins.cs:13-16`). Within 8 m of the bins the overhead turns to "the bins view" (§4).
3. **Binning, then Reveal.** Ace is held for about 4–5 s while the lid lifts, the bag goes in, the lamp flickers, the reveal camera comes on, and he stands up and looks round (`NightZero.cs:191-269`).
4. **Deal.** This is the scene `night0.deal`. It holds Ace (`Night lines.asset:386-415`; `NightZero.cs:273-275`). In order:
   - "You didn't see me." Then **Ace answers:** "...I didn't see you." (warmth +1) → "Good. Quick." / "You were in my bin." (warmth −1) → "And now I'm not. Keep up." (asset `:40, 44, 192, 196, 200`)
   - "You open at nine. You burn the second batch." (`:48`)
   - "Grace likes you. Café owners who talk don't last." (`:56`). On a day her visit went wrong it says "Grace walked out." instead (`NightZero.cs:319-337`; asset `:384`).
   - "But I like you. So here's the thing." (`:60`)
   - "Everything I know about this street. Learn the rest." (`:64`). On this line his pages go into the notebook, and a **note shows for 6 s: "His notebook is yours now. D-pad up to read it."** (`NightZero.cs:343-353`).
   - "A gnome on the corner step. Bring it here." Then **Ace answers:** "Fine." → "Good. Don't drop him." / "Why?" → "Don't ask why. Ask how." (`:72, 204, 208, 76, 80`)
   - "That cop who drinks your coffee? He's looking for me." (`:260`)
   - "Keep him happy. And don't get caught." (`:264`)
5. **Done** (`NightZero.cs:427-437`). The ledger records the deal: Favour = gnome, asked tonight (`NightLedger.cs:72-80`). The clock starts, and he ducks back into the bin 0.7 s later. Then (`NightZero.cs:443-458`):
   - At +0.6 s, Ace's bark: "I own a café. A man lives in my bins." (asset `:144`)
   - At +3.2 s, a **note for 7 s:** "Barnaby: Grace's front step, the saffron house on the corner." If Grace never told Ace about him by day, it starts "The garden gnome: …" (`LodgerStory.cs:148, 240-245`).
   - At +7.5 s after that, a **note for 6 s:** "Square torch · D-pad up notebook · Cross at a café door: home." (`NightZero.cs:440, 457`)
   - **After about 17 s, nothing on screen says what to do.**

### 1.3 Night 1: Barnaby (the same night)
**State:**
- `NightLedger.Errand` holds the gnome while he has asked for it and not been given it (`NightLedger.cs:105`).
- `HasTrophy` means taken, `HasGiven` means given, and `OnShelf` means taken but not given (`NightLedger.cs:55, 197, 214`).
- `NightCarry.Current.HeldId` is what is in Ace's hand (`NightCarry.cs:20`).
- `Lodger.Up` means he is out of the bin (`Lodger.cs:76`).

1. **Find Grace's step** (§5). The prompt there is "Take Barnaby" or "Take the garden gnome" (`NightTrophy.cs:118-128`). A small light comes on over him while he is the thing Cross would take (`NightTrophy.cs:164-179`).
2. **Take him.** Cross puts him in Ace's hand. A **note shows for 4.5 s:** "Bring Barnaby back to the man at the bins." Ace barks "It's a gnome. It's just a gnome." (`NightTrophy.cs:199-206`; asset `:148`).
3. **Go back to the bins.** Within 4.5 m of the man's half with Barnaby in hand, he pops up (`Lodger.cs:42-43, 421-429`). He may say "Over here." / "Bring it here." / "That's the one." (asset `:245-256`).
4. **Give him Barnaby.** The prompt is "Give him Barnaby" (`NightGive.cs:22-33`). Then the scene `night1.return` plays and holds Ace (asset `:442-459`):
   - "There he is." Then **Ace answers:** "Here. Happy?" → "Very." / "What's he even for?" → "Watch."
   - "It was watching me. Now it watches for me." He turns the gnome to face the street (`Lodger.cs:560-566, 639-653`).
   - "You kept your nerve. Keep it when she asks." A **note shows for 6 s:** "Learned: Nerve. When Grace asks, your face will give less away." (`Lodger.cs:573-580`; `LodgerStory.cs:271`)
   - "A page for you. Grace's Thursdays." The page goes into the notebook, with no note (`Lodger.cs:582-590`).
   - "We've both got something on each other now. Mine's bigger."
5. **Go home.** "Call it a night" is offered inside the café's front door or outside the back door (`NightDoorway.cs:29`; `NightBackDoor.cs:52-54`). With Barnaby still in hand it reads "Call it a night (Barnaby goes on the shelf)" (`NightCycle.cs:100-113`). The card then says "Home" / "Ace calls it a night.", or at 4 AM "Dawn" / "The sky pales. Ace hurries home before the street wakes." (`NightCycle.cs:331-332`).

**If the night ends before he has Barnaby, it counts as a skip** (`NightCycle.cs:334-337`; `LodgerStory.cs:18-24`):
- The next morning he takes a table, with the note "The man at the bins took a table, waiting on his favour." (`LodgerStory.cs:98`; `LodgerDay.cs:149-154`).
- Night 2 then asks for the gnome again, colder, with no reply (`NightZero.cs:382-399`). The hint comes again, or reads "Barnaby is on Ace's shelf, behind the counter." (`NightZero.cs:465-468`).
- The shelf then offers "Take Barnaby off the shelf" (`NightShelfTake.cs:34-41`), with the note "Bring Barnaby to the man at the bins." (`NightShelfTake.cs:61`).

**When Ace passes him empty-handed** (within 7 m, at most once every 20 s), he speaks from inside the bin: "Not yet." / "You're going the wrong way." / "I'm not going anywhere." (`Lodger.cs:474-498`; asset `:117-128`; `BarkRules.cs:29`). **"You're going the wrong way." is picked at random.** It does not depend on where Ace is going, so a lost player is told something that may not be true.

### 1.4 Night 2: the cups
1. **The bins.** Nightfall and the bins are as in 1.1 and 1.2 steps 1–2. After "Bin it" he pops up; on Night 2 he peeks over the rim first (`Lodger.cs:70-71`).
   - If something happened that morning, he gives a verdict first, e.g. "She asked. You held. Good." (`LodgerStory.cs:226-234`; asset `:341-364`).
   - Then the scene `night2.ask` plays and holds Ace (asset `:416-430`). He says "Grace has a box of cups. Get me a sleeve." **Ace answers:** "One sleeve. Fine." → "Good. Don't wake her." / "Cups? Why cups?" → "A man needs cups." Then he says "Her kitchen. A box on the worktop."
   - When he is warm, the scene is `night2.ask.warm`, which adds "Before you ask: old times. You'll see." (asset `:277-288, 460-475`).
   - The notebook learns the cups on his first line (`NightZero.cs:419-425`).
2. **A note for 7 s:** "The reunion cups: Grace's kitchen worktop. Her front door opens." (`LodgerStory.cs:160`; `NightZero.cs:469`)
3. **Grace's house.**
   - On her stoop the prompt is "Let yourself in" (`GraceHouse.cs:175-180`; `GraceDoorZone.cs:31`). Inside, the camera turns to look in from the street: yaw 258°, tilt 52°, 13 m (`GraceHouse.cs:102-104, 492`).
   - **Grace is home** (`GraceNight.cs:7-12`): her armchair and the TV from 11 PM, the kettle at 11:35, her front window 11:49–11:59, bed at 1:20, lamp off at 1:40.
   - **Her "?" mark** fills as she sees or hears Ace. She says "Hm?" / "Is someone there?" / "Hello?" (`GraceAtHome.cs:1822`; asset `:161-172`). When it is full: "Ace?! What on earth—", then a card: "Caught." / "Grace saw Ace in her house. Whatever Ace took tonight goes back. For now, that's all." (`GraceAtHome.cs:2232`; `NightCycle.cs:372-373`).
   - Her bedroom doors offer "Open the doors" (`BedroomDoorsZone.cs:25`).
   - **Nothing on screen says to wait until she is asleep.** Only his page in the notebook says it: "Grace: the saffron house on the corner. Asleep by 1:45." (`LodgerStory.cs:310-311`).
4. **The worktop.** The prompt is "Take the reunion cups", offered only when Ace can see them (`NightTrophy.cs:54, 82-95`). Taking them gives a **note for 4.5 s:** "Bring the reunion cups back to the man at the bins." Ace barks "If she wakes, I'm a burglar. Otherwise, a guest." (asset `:152`).
5. **Back at the bins.** The prompt is "Give him the reunion cups". The scene `night2.return` plays (asset `:476-491`):
   - **Ace answers:** "One sleeve, as asked." / "So what are they for?" → "Watch."
   - "Four cups. There used to be four of us."
   - "You walk like a cop. I can fix that." with a **note for 6 s:** "Learned: Soft feet. Your steps carry half as far." (`LodgerStory.cs:272`)
   - No page is paid, because her photos aren't in the game yet (`LodgerStory.cs:156-159, 329`).
6. **Go home**, as in 1.3 step 5.

**Night 3** (after Day 3) has nothing to do. The cones aren't in the game, so he says "Nothing tonight. I'm still thinking." and the note reads "Nothing tonight. The street is yours." (`LodgerStory.cs:210-216`; `NightZero.cs:400-404, 465-466`; asset `:328`).

### 1.5 Every `NightCycle.Note` call

| Where | Text | Seconds | When |
|---|---|---|---|
| `NightCycle.cs:250` | Last job: the bins. Out the back. | 6 | every nightfall with the bins |
| `NightCycle.cs:251` | (the keys note) | 8 | first night, only when a lab skips the bins |
| `NightZero.cs:352` | His notebook is yours now. D-pad up to read it. | 6 | Night 0, on the deal's 5th line |
| `NightZero.cs:456` | Barnaby: Grace's front step, the saffron house on the corner. | 7 | Night 0, 3.8 s after the deal |
| `NightZero.cs:457` | Square torch · D-pad up notebook · Cross at a café door: home. | 6 | 7.5 s after that |
| `NightZero.cs:469` | where the thing is / "…is on Ace's shelf, behind the counter." / "Night off. The street is yours." / "Nothing tonight. The street is yours." | 7 | every later night, after his scene |
| `NightTrophy.cs:196` | Barnaby is coming home with Ace. He'll go on the shelf. (not his errand) | 4.5 | on taking it |
| `NightTrophy.cs:201` | Bring Barnaby back to the man at the bins. | 4.5 | on taking his errand |
| `NightShelfTake.cs:61` | Bring Barnaby to the man at the bins. | 4.5 | taking it off Ace's shelf |
| `Lodger.cs:579` | Learned: Nerve. … / Learned: Soft feet. … | 6 | the return's lesson line |
| `GraceHouse.cs:413` | Locked. Her door doesn't open for Ace tonight. | 4.5 | only with break-ins off (labs) |
| `GraceAtHome.cs:1693` | Hidden in … [Cross] Come out | until out | nothing makes a hiding place now (`GraceAtHome.cs:71-76`) |

By day, `NightCycle.Note` is also used for the closing note when a conversation ends, e.g. Grace's print (`ConversationController.cs:265`), and for the man's morning notes (`LodgerDay.cs:154, 202, 233`).

### 1.6 Is there one place that knows the current goal at night?
**No.** It is spread across seven places:
- `NightZero.Now` (the bins)
- `Barks.ScenePlaying` / `Barks.Choosing` (a scene is up)
- `NightLedger` `Favour` / `AskedOn` / `Errand` / `HasTrophy` / `OnShelf` / `HasGiven`
- `NightCarry.Current.HeldId`
- `Lodger.Up` / `Returning`
- `GraceHouse` (Ace on her stoop or inside)
- `NightCycle.CanCallItANight`

The words are worked out only once, at the moment they show (`NightZero.cs:460-469`; `NightTrophy.cs:193-201`; `NightShelfTake.cs:61`), and then thrown away. The `Describe()` methods already sum the state up for the reports (`NightCycle.cs:633-636`; `NightZero.cs:474-478`).

**Least invasive place:** a read-only property beside `NightCycle.CallItANightPrompt` (`NightCycle.cs:100-113`). That property already builds a line on demand from `NightCarry` and the notebook, and caches it by name. The new property would apply these rules in order, using only existing public state:
1. Bins pending, AtTheDoor: take the bag out the back door.
2. Outside: put the bag in the dumpster.
3. A scene is playing: show nothing.
4. The errand's thing is in hand: bring it to the bins.
5. The thing is on the shelf: it is on your shelf.
6. There is an errand: `LodgerStory.Hint`.
7. Otherwise: go home, Cross at a café door.

---

## 2. The notebook

**How it opens.**
- It is not an Input System action. It is a direct read in `NightNotebook.Update`, at night only: the N key or D-pad up toggles it (`NightNotebook.cs:88-94`):
  ```csharp
  bool pressed = Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame || PadInput.Pressed(PadButton.DpadUp);
  ```
- `NightWalk` adds it at nightfall and removes it at dawn (`NightWalk.cs:372-375, 403-404`).
- It doesn't open while paused (`NightNotebook.cs:90`). Esc closes it (`PausePhone.cs:85-87`).
- Ace can keep walking with it open (`NightNotebook.cs:12`).
- While it is open, every bark is hidden and the barks' clock stops (`Barks.cs:476-486`), so the man's lines and Ace's reply chips vanish until it closes.
- At night the pause phone (Options) also opens on Notes (`RecapPhone.cs:80, 97`), but nothing says so.
- By day there is no notebook page, only the Notes app on the pause phone and the recap phone (`RecapPhone.cs:47-49, 73-77, 96`).

**What it shows.**
- A dark page at the right middle of the screen, 540 × 720 px: the title "Notebook" (30 px), the facts (23 px) and the footer "D-pad up  Close" (18 px) (`NightNotebook.cs:53-69, 103`).
- It is laid out by `NotebookRecap.Page` (`NotebookRecap.cs:87-103`). **His pages come first, in italics, under "His pages":**
  - "Grace: the saffron house on the corner. Asleep by 1:45."
  - "A gnome on her step. It faces the alley."
  - "The street's windows go dark one by one. All by three." (`LodgerStory.cs:301-319`)
- Then each person, address first. For Grace that can be "Came out of / Went into the saffron house at 12 West Street. (hunch)" (`NotebookEntries.cs:93-111`), then her camera facts and the Barnaby mention.
- **Nothing on the page says "your current errand".**

**How entries are added.**
- `Notebook.Learn`: the same id is the same fact, and Ace only ever becomes surer (`Notebook.cs:72-92`).
- The day writes through `NotebookHooks`:
  - Grace's intake and thanks (`NotebookHooks.cs:18-58`)
  - a regular seen at their own door (`NotebookHooks.cs:65-76`, via `HomeSightings.CanSee`)
  - a mention or a complaint (`NotebookHooks.cs:81-97`)
  - his ask (`NotebookHooks.cs:103-109`)
  - the officer (`NotebookHooks.cs:112-122`)
  - his note (`NotebookHooks.cs:125-130`)
  - taken at night (`NotebookHooks.cs:133-138`)
  - she suspects Ace (`NotebookHooks.cs:141-146`)
- The texts are in `NotebookEntries` and `NightThings` (`NightThings.cs:89-102, 131-135`).
- His pages go in on the deal's hand-over line (`NightZero.cs:343-353`). A page goes in on each return (`Lodger.cs:582-590`).

**Is the player told it exists, or how to open it?** Four times, none of them lasting, none by day, and none repeated when the player is stuck:
1. "His notebook is yours now. D-pad up to read it." for 6 s, in the middle of the deal (`NightZero.cs:352`). It is up while she is reading 30 px barks over two heads.
2. The keys note for 6 s, about 11 s after the deal (`NightZero.cs:440, 457`).
3. "D-pad up  Notebook" at the far right end of the 19 px controls strip, all night (`ShopUI.cs:161-162`).
4. The page's own footer, which shows only once the page is open (`NightNotebook.cs:103`).

---

## 3. Choices at night vs by day

### Night (`Barks`)
**Where choices come up.** A reply comes up only in a scene from the Night lines that holds Ace and has a choice after one of its lines (`Barks.cs:617-623`). In Nights 0–2, every scene with choices holds Ace: `night0.deal`, `night1.return`, `night2.ask`, `night2.ask.warm`, `night2.return` (asset `:386-491`).

**How they are offered.**
- **Timing:** the chips come up min(reading time, 0.9 s) after the line, or sooner on a Cross / E / click (`Barks.cs:549-555, 622`).
- **Look:** two chips at 27 px (`Barks.cs:861`), beside the man's line, to its right or left (`Barks.cs:708-726`).
- **Words:** each chip starts with its key in Ace's green, e.g. "**Square**   ...I didn't see you." / "**Triangle**   You were in my bin." (`Barks.cs:888-889`).
- **Nothing else:** no highlight, no ► and no "choose" hint.

**How a reply is taken.**
- 1 / 2 on the keyboard, or West / North on a pad, which is **Square / Triangle** on PlayStation (`Barks.cs:559-560`; labels `PadInput.cs:298-299`). Picks count only 0.2 s after the chips appear (`Barks.cs:556`).
- Cross / E never picks; that was a deliberate rule (`Barks.cs:34-39`: "a press meant to move a line on can't answer for Ace").
- **Can Ace move while choosing? No.** A held scene stops walking (`Barks.cs:518`; `PlayerMovement.cs:323-329`), stops Cross from reaching interactables (`PlayerInteractor.cs:464-469`), the torch (`NightTorch.cs:82-83`) and the pause phone (`PausePhone.cs:112`).
- **But the camera stays free:** the right stick orbits, L2 / R2 zoom, Share switches view (`PlayerMovement.cs:147`; `CafeViewMode.cs:396-422`).
- **D-pad up still opens the notebook,** which hides the chips and stops the scene's clock (`NightNotebook.cs:91`; `Barks.cs:482`).

### Day (`ConversationController` / `ConversationUI`)
- **Look:** their line is 32 px under their face. Ace's replies are 32 px, each on its own plate, in a column to the right. The highlighted reply is light green with "►" and "[Cross]". The turn-away reply carries "[Triangle]" (`ConversationUI.cs:21-31`; `ConversationController.cs:584-602`).
- **Footer:** "[D-pad]  Choose      [Circle]  Step away" (`ConversationController.cs:604-610`).
- **Input:**
  - up / down moves the highlight: W/S, the arrows, the wheel, D-pad up / down, or the left stick, one step a push (`ConversationController.cs:538-564`)
  - Cross / E / Enter / click takes it (`ConversationController.cs:530-536, 347`)
  - 1–4 take a reply directly (`ConversationController.cs:566-575`)
  - Triangle / Q refuses (`ConversationController.cs:345-352`)
  - Circle / Esc / Tab steps away (`ConversationController.cs:320-323`)
- **Gating:** an early press only brings the replies up (`ConversationController.cs:325-331`).
- **Movement and camera:** walking stops (`PlayerMovement.cs:315-320`), and the conversation camera frames their face, so look input does nothing (`ConversationController.cs:196-217`; `CafeViewMode.cs:236-238`).

### Making the night's choices work like the day's
Walking already stops (held scenes). What is missing is in `Barks` alone:
1. A highlighted index, starting on reply 0.
2. The day's `Move()` inputs in `StepChoice` (D-pad up / down, the stick, W/S, the arrows).
3. Cross / E picking the highlighted reply in `StepChoice`. The existing 0.2 s guard (`Barks.cs:556`) already stops the press that brought the chips up from also picking, as by day.
4. `ShowChips` drawing ► and "[Cross]" on the lit chip, and the day's footer under the chips.

1 / 2 can stay as direct picks. The labs press 1 / 2 or call `Barks.Choose` (`Diagnostics/NightZeroCheck.cs:483-498`; `Diagnostics/NightTwoCheck.cs:759-761`), so they keep passing.

**Conflicts to handle:**
- **D-pad up toggles the notebook** (`NightNotebook.cs:91`). It must skip while `Barks.Choosing`, or "up" opens the page and hides the chips.
- **The mouse wheel zooms the overhead at night** (`CafeViewMode.cs:407-408`). Either leave the wheel out at night or skip the zoom while choosing.
- **Square is the torch,** but the torch is already blocked while Ace is held (`NightTorch.cs:83`), so dropping Square / Triangle has no side effect.
- **Reversing "E never picks one" is a design call** (`Barks.cs:34-39`): his to make.

---

## 4. Meeting the man at the bins (Night 0)

**Where things are** (`Editor/NightZeroSteps.cs`; world metres; confidence medium, because the scene can be moved by hand):
- **The back wall:** inside face z 18.0, outside face z 18.24 (`NightZeroSteps.cs:57`).
- **The back door:** 0.30 m from the east corner, so about x 6.6 after "Bins 2" (`NightZeroSteps.cs:403, 484`).
- **The dumpster:** against the wall, middle x 4.4, reaching 1.2 m each way (`NightZeroSteps.cs:62, 137-141`). The near lid, nearer the door, takes the bag; the far lid is his.
- **The man:** stands on the dumpster's floor in the middle of the far half, about x 3.8, facing the street, +z (`NightZeroSteps.cs:152-154`). That is about 2.8 m west of the door.
- **Ace's places:** Ace comes out about 0.65 m from the door, facing west, 270°, toward the dumpster (`NightZeroSteps.cs:233-236, 259`). Ace stands 0.56 m in front of the near half to "Bin it" and in front of the far half to give him things (`NightZeroSteps.cs:237-245, 260-261`).

**Markers, highlights, pins: none.**
- `UiPins` is off at night (`UiPins.cs:380`).
- The door and the dumpster are "zones" with no glow (`NightBackDoor.cs`, `NightBins.cs`). Only takeable things get a light (`NightTrophy.cs:164-179`).
- What there is: the lamp over the bins, a warm spot light (intensity 9, range 7 m: `NightZeroSteps.cs:267-280`), and the prompt line.

**The camera.**
- **Inside the café:** the overhead follows Ace at the day's angle (§5).
- **Outside, within 8 m of the bins:** the overhead turns to the "bins view": yaw 205°, tilt 46°, 14 m from Ace (`NightZeroSet.cs:48-51`; `Lodger.cs:451-468`; `CafeViewMode.cs:514-528`). It looks from the street toward the café wall, because from the day's angle the café's back wall hides the dumpster (`Lodger.cs:31-32`). The focus stays on Ace, not on the man.
- **The reveal:** a fixed Cinemachine camera with a 40° lens, priority 50. It stands 2 m east of the dumpster's middle, 2.2 m up and 3 m in front, looking at his half 1.6 m up (`NightZeroSteps.cs:191-203`). It is on only from the flicker to his look round, about 2.5 s, **and switched off before the deal starts** (`NightZero.cs:257, 266`).

**Is there any automatic look-at? No camera look-at during the deal.**
- Ace's body turns to him and he turns to Ace (`NightZero.cs:234-237, 267-268`). Neither changes the camera.
- In first person (Share / V), the view's yaw is set by the view itself every frame (`CafeViewMode.cs:394`), so turning Ace's body doesn't turn it. `CafeViewMode.LookTo` exists (`CafeViewMode.cs:460-466`) but only labs use it.
- **First person ("Look up").** He stands on the dumpster's floor, about 1.7 m from Ace. His head is about 0.7 m above Ace's eyes at 1.65 m (`CafeViewMode.cs:50`), and his line sits 0.32 m above his head (`Barks.cs:61`). So with a level view, his head and line are near the top edge or clamped to it. This matches "Look up" (confidence: medium; estimated from the set-up's numbers).
- **Overhead.** In the bins view he is on screen, about 2 m "up the screen" from Ace.
- **By day, the conversation camera does exactly what she asked for** (`ConversationController.cs:196-217`); the night has no equivalent.

---

## 5. Finding a house at night

**Where her house is named: five names for one place.**
- **By day, Grace's mention (the topic "Big plans tonight?"):** "…Twenty years on the front step of the saffron house, on the corner." (`NightThings.cs:88-90`). The notebook keeps "Barnaby, her gnome: twenty years on the saffron house's front step." (`NightThings.cs:98`).
- **His page:** "Grace: the saffron house on the corner. Asleep by 1:45." (`LodgerStory.cs:310-311`)
- **The deal:** "A gnome on the corner step." (asset `:72`). The ask-again reminder: "The gnome. Grace's step." (asset `:332`).
- **The hint note, 7 s:** "Barnaby: Grace's front step, the saffron house on the corner." (`LodgerStory.cs:148`). Night 2's hint names no house: "…Grace's kitchen worktop. Her front door opens." (`LodgerStory.cs:160`).
- **The notebook's address line,** only if Ace saw her at her door by day: "Went into the saffron house at 12 West Street." It is built from `HomeDoor` (looks "the saffron house", number "12", street "west": `HomeDoor.cs:19-25`) and `NotebookEntries.cs:93-111`. **The night's hint never says "West Street" or "12".**

**Two saffron houses.**
- West Street has six bay-window houses. Two of them are saffron: #1, Grace's, by the Front Street corner (z −4), and #6, by the Back Street end (z 25) (`Editor/CafeStreetUpgrade.cs:39-40`; `Editor/StreetDoorSteps.cs:62-67`).
- The bins are on Back Street. Walking west from the bins reaches West Street at house #6 first, which is also "on a corner" and carries the plaque "22". Grace's house is about 29 m further south (my reading of the coordinates; confidence: medium).
- The studio has already heard "at night the saffron house looks brown" (`Diagnostics/NightOneCheck.cs:215`; `GraceAtHome.cs:23-24`).

**How she is meant to find it.**
- **Her porch light,** on all night every night (`GraceAtHome.cs:23-28, 105-108`), and her windows' light on the pavement.
- **Barnaby's small light** when he is in reach (`NightTrophy.cs:164-179`).
- **The plaque and the corner signs.**
- **No map, marker or compass exists.** `GraceHouseMap` is Grace's own walking graph inside her house, not a map for the player (`GraceHouseMap.cs:4-14`). A search of `Assets/Scripts` finds no minimap, waypoint or compass. Pins are off at night (`UiPins.cs:380`).
- **The notebook spec's rule is "curiosity, never markers"** (`Notebook.cs:6-8`).

**Sizes.**
- **House numbers:** a plaque 0.30 × 0.20 m, 1.95 m up, by each bay house's door. Its lettering box is 0.26 × 0.16 m, bold cream, auto-sized (`Editor/NightLivedIn.cs:419-425, 450-463, 568-588`). The numbers run 12, 14, 16 … from south to north; Grace's door supplies "12" (`NightLivedIn.cs:441-467`; `HouseNumber.cs:22-31`).
- **Street signs:** two blades, 1.3 × 0.22 m, at 3.08 m and 3.34 m on the signal post at each of the four corners of the café's block. The lettering box is 1.2 × 0.17 m (`NightLivedIn.cs:426-429, 469-512, 527-547`).
- **Both letterings dim at night.** They are lit by the scene: dark unless a lamp, her porch light or the torch is on them (`HouseNumber.cs:15-16`; `NightLivedIn.cs:636-657`).

**The night camera and readability.**
- The overhead follows Ace at the day's framing: tilt 50°, yaw 45°, 34 m, 41° lens (`Editor/CafeStreetUpgrade.cs:404-409`; `CafeViewMode.cs:84-85, 122-123, 469-497`). Outside at night it zooms between 8 and 48 m (`CafeViewMode.cs:99-102, 225-231`). L2 / R2 zoom at 22 m/s (`CafeViewMode.cs:64-65, 419-420`).
- At 34 m with a 41° lens, a metre is about 42 px tall on a 1080p screen; at 8 m, about 180 px. **So the "12" is at most about 7 px tall at the default zoom, and at most about 29 px fully zoomed in. The street names are about the same.** These are estimates before foreshortening.
- **At yaw 45° the camera looks north-east, and West Street's house fronts face east** (each house is turned 90°: `CafeStreetUpgrade.cs:171-175`). From the default view the camera sees the houses' backs and roofs, so the plaque on the front wall can't be seen at all.
- A house between the camera and Ace fades to a quarter of its dots (`NightSeeThrough.cs:6-20`). The studio noted the same: "from the overhead her street wall is seen edge on" (`GraceAtHome.cs:23-26`).
- **To read "12" the player must orbit the camera about 180° and zoom in.**

---

## 6. Barnaby: is his purpose ever stated?
**Before the errand, no.**
- The deal says "A gnome on the corner step. Bring it here." If she picks "Why?", he answers **"Don't ask why. Ask how."** (asset `:72, 76, 80`).
- His page says "A gnome on her step. It faces the alley." (`LodgerStory.cs:312-313`).

**Only at the return, and only by hint.**
- If she picks "What's he even for?", he says "Watch." Then "It was watching me. Now it watches for me." as he turns the gnome from the alley to face the street (asset `:217-232`; `Lodger.cs:639-653`).
- Then "We've both got something on each other now. Mine's bigger." (asset `:244`).

So the reason comes only after the work is done, and it is implied (a lookout, and now she is involved). By day Grace says what he is and where he stands, never why the man wants him (`NightThings.cs:88-91`). All of these words are placeholders for Mansoor and his sister (`LodgerStory.cs:29-30`; `NightThings.cs:19-23`).

---

## 7. The day guide

**Which days.**
- The card guides a day's featured visit when the day asset says so (`DayDefinition.cs:160-163`).
- **Day 1:** Grace, then the Officer's first coffee (`DayOneGuideUI.cs:285-317`; `Editor/DaysOneToThreeSteps.cs:527`).
- **Day 2:** Grace again (`DaysOneToThreeSteps.cs:562`).
- **Day 3: nothing,** because hints stay off (`DaysOneToThreeSteps.cs:587, 599`).
- The scheduled Day 1 has no guided opening (`Editor/DayOneOnboardingChecks.cs:157-162`), so the drink-then-repair lesson in `DayOneOpening` (`DayOneOpening.cs:1-36`) is not played. It belongs to the unscheduled `Day_01_LearnTheShop`.
- **Any day:** one "GETTING IMPATIENT" card, the first time a waiting customer gets frustrated, until Ace has reassured someone once (`DayOneGuideUI.cs:719-745`).
- Once a featured visit is over, the card says so once and goes. **The walk-ins are never guided** (`DayOneGuideUI.cs:346-354, 559-562`).

**The words** (pad wording):

| Step | Words | Where |
|---|---|---|
| First card | "You're Ace. Your café's first day: Grace is on the way with a repair." (adds "Go behind the counter." when Ace isn't behind it) | `DayOneGuideUI.cs:555-557` |
| Not behind the counter | "Walk behind the service counter to take orders." | `:386` |
| She is walking in | "Wait for Grace to reach the counter." | `:393` |
| Talk | "Cross talks to Grace; when your replies appear, Cross takes the job." (first person: "Look at Grace. Cross talks; …") | `:565-571` |
| The device | "Grace's reunion film camera will appear on the intake tray." / "Press Cross to pick up Grace's reunion film camera from the intake tray." | `:492, 574` |
| Carry | "Carry the reunion film camera to the repair bench. Cross sets it down." | `:527` |
| On the bench | "Placed. At the repair bench, press Cross to work on the reunion film camera." | `:538-540` |
| At the bench | (no card; the bench's own card instead) | `:532-533, 881` |
| Fixed, in hand | "Perfect repair. Take it to Grace; Cross hands it back." | `:511-513` |
| Drinks (Officer Day 1; her latte Day 2) | "Walk to the drinks machine on the back bar. Cross brings it into view." → "Aim at the cup stack. L1 takes a cup in your left hand, R1 in your right." → "Aim under the {drink} nozzle and press Cross…" → "Cup placed. Aim at the {drink} paddle and press Cross to pour." → "…ready. Aim at the cup and press Cross to take it." → "Walk to {name}; Cross serves the {drink}." | `:400-450` |
| Out of stock | "No stock: Cross to talk, then Triangle to apologise." / "No stock. Cross near {name} apologises; restock after closing." | `:388, 413` |
| Day 2's first card | "Grace is visiting again. Talk at the counter to hear how things went." | `:354` |
| Visit over | "Grace's visit is over. Closing time brings the day's recap." | `:560-562` |
| Impatience | "GETTING IMPATIENT" / "{name} is getting impatient. Get in reach and press Cross to reassure." | `:115, 579` |

**Where the card is drawn.**
- Top left, 24 px in from the left edge, 16 px under the bottom of the tabs, or 24 px from the top when there are no tabs (`DayOneGuideUI.cs:861-867, 959-972`; `TicketRailUI.cs:36-42`).
- **In full:** 460 px wide, a bold green title and the step at **21 px**. **Compact:** the step alone at **18 px**, up to 640 px wide (`DayOneGuideUI.cs:862-864, 927-957`).
- It shows in full for 6 s, then compact. If she is still stuck 10 s later, it comes back in full once, with one pulse (`DayOneGuideUI.cs:1044-1100`).
- The guide's green pin names the step's target over it, 21 px. "You" shows over Ace for 15 s or 1 m on Day 1 (`DayOneGuideUI.cs:776-848`; `UiPins.cs:25-27, 64-76`).
- **It hides at night,** in conversations, the recap, the day intro, and **at the bench**, where only the impatience card shows, bottom middle (`DayOneGuideUI.cs:185-202, 869-898`).

**What the bench shows instead** (`BenchCards`):
- **A paper card top right, under the takings** (`BenchCards.cs:18-29, 40-47`):
  - her words at 19 px: Grace: "The lens and film path are dirty."
  - one icon per step, the current one big, with its verb under it at 17 px ("Wipe")
  - when fixed, "[Square] Take it to Grace" (`BenchCards.cs:218-222`)
- **A glow** on the next thing to touch.
- **On the first repair only, a controls card bottom right** at 18 px: "Right stick Move / Cross Use / Circle Step back / Share What's left" (`BenchCards.cs:171-181`). After that only "[Share] Help" (`BenchCards.cs:208`).
- **Nothing on the bench says where to go after the bench** beyond "Take it to Grace".

---

## 8. The franchise goal

**Every mention:**
- `ReputationRules.cs:15`: "Five stars and no scandal brings the franchise offer." (a code comment). The star thresholds are 5 / 30 / 120 / 300 / 580 reputation (`ReputationRules.cs:94`).
- The recap phone's **Franchise app**, titled "Franchise HQ / Your café's standing". It shows stars, the level name, "N / M reputation", "Next: …", then under "REQUESTS FROM HQ": "Reach N reputation for your Nth star.", "Stay out of the papers: no scandal running." and **"At five stars, HQ sends the franchise offer." / "With a fee to save for"** (`RecapPhone.cs:1342-1409`).
- That app is one of the recap's tabs, reached from the closing screen's "More" (`RecapPhone.cs:95, 1080-1084`). It gets a dot on a day a star is earned (`RecapPhone.cs:894-899`).
- The Franchise app's "WHAT CHANGED TODAY" line (`ReputationLedger.cs:378-395`).

**Stars:**
- **Never on the day's HUD.** The corners are cash, the sign, the tabs and stock (`HudCorners.cs:10-23`).
- **By day,** only on the pause phone's "Today" tab: five 20 px stars and a 15 px line, e.g. "3 to your first star" or "a new star at closing" (`RecapPhone.cs:1686-1692, 1317-1334`).
- **At closing,** on the Tonight screen: stars and "N to your first star" (`RecapPhone.cs:972-992`).

**Does Day 1 say the goal? No.**
- The intro line is "Fix what they bring. Pour what they order. Make the rent." (`DayIntro.cs:955`).
- The first card is "You're Ace. Your café's first day: …" (`DayOneGuideUI.cs:555-557`).
- Tonight shows stars but not the word franchise.
- **The word "franchise" first appears inside the Franchise app.**

---

## 9. Stock

**The numbers:**
- 20 cups and 20 beans to start, the scene's serialized defaults (`ShopInventory.cs:7-8`).
- "Low" is under 10 (`ShopInventory.cs:23-26`).
- A restock adds 20 of each for $30 (`ShopInventory.cs:10-12, 82-93`).

**What the player sees:**
- **By day:** a stock chip bottom right, 18 px bold on a 36 px chip, "Low on beans · 3 left" or "Out of beans" (`HudCorners.cs:20-23, 331, 691-733`). **It doesn't say where or when to buy.**
- **At closing:** the Tonight screen shows "Low on beans. Restock in Shop." (15.5 px; tapping it opens Shop: `RecapPhone.cs:1067-1077`).
- **In the Shop app:** the hint "Low on beans. Restock here before tomorrow." and the button "Restock cups & beans / +20 each, ready for tomorrow / $30" (`RecapPhone.cs:1465-1477`). The tab shows a "!" (`RecapPhone.cs:895, 900`).

**Can the player buy during the day? No.**
- The phone by day opens with Esc, or Start (Options on PlayStation) (`PausePhone.cs:11, 75-90`; the strip says "Options  Pause": `CafeViewMode.cs:166`). Its apps are Today, Notes and Settings, with no Shop (`RecapPhone.cs:96`).
- Buying works only once the day is over (`UpgradeShopUI.cs:55-74`: `if (!DayClock.Instance.DayOver) return false;`).

**What tells her where to buy:**
- By day, only the guide card's drink steps during a guided visit: "…restock after closing." / "Not enough beans. Restock after closing." / "No cups. Collect a spare or restock after closing." (`DayOneGuideUI.cs:413, 477-483`).
- At closing, the Tonight line and the Shop tab.
- **Nothing ties "Out of beans" on the HUD to "the phone at closing".**

---

## 10. A single persistent "Now: …" line

**Day sources:**
- **The guide's step** (`DayOneGuideUI.GuideStep`, `:240-283`; `VisitAction` / `NextAction` / `DrinkAction` / `RepairAction`, `:362-541`). This already turns a customer's state into one next step.
- **The impatience card** (`:721-745`).
- **What Cross does now** (`PlayerInteractor.CurrentPrompt` / `WorkPrompt`, `:500-501`) and "In hand" (`ShopUI.cs:290-299`).
- **Who Cross talks to first** (`PlayerInteractor.QueueHead`, used at `DayOneGuideUI.cs:322-328`).
- **The customers with a tab, in order** (`TicketRailUI.InService`, `:44-50`), and each `CustomerBrain`'s state (`CanHearIntake`, `CanDecide`, `WasAccepted`, `JobReady`, `CanReceiveDrink`, `HasDrinkOrder`, `CanReassure`, `JobNeedsAttention`).
- **The bench:** `BenchCards.CurrentVerb` / `FinishedText` / `JobLine` (`BenchCards.cs:74-86`).
- **Stock:** `ShopInventory.CupsLow` / `BeansLow`.
- **Closing:** `DayClock` (open, last orders, closing).

**Night sources:** the rules in §1.6.

**What already does part of this:**
- `DayOneGuideUI`: one next step, persistent, with a compact look and a stuck reminder. It runs on Days 1–2 only and is off at night.
- `BenchCards`: the bench only.
- `NightCycle` notes: they vanish after 4.5–8 s.
- The prompt line: only once something is in reach.
- `NightCycle.CallItANightPrompt`: night words worked out on demand.

**The natural home: `DayOneGuideUI`'s card.**
- Its panel sits on the HUD canvas that holds ShopUI's prompt (`DayOneGuideUI.cs:974-988`), and that canvas is live by day and at night (`ShopUI.cs:177-199`).
- **By day,** top left under the tabs is already the guide's slot. Each of the other corners is taken:
  - top right: the cash stack, plus the bench card under it at the bench
  - bottom left: the sign
  - bottom right: the stock chips, plus the bench controls at the bench
  - bottom middle: the prompt and the controls strip
- **At night the top left is empty:** the corners are hidden (`HudCorners.cs:346`), there are no tabs, the clock is top right (`ShopUI.cs:88-102`), the notebook is at the right middle and the notes are bottom middle.
- **So one place, top left, works day and night.**
- Its type is 21 / 18 px today. For her it should be at least the night note's 25 px.
- Any new line has to fit the 12-word note budget (`WordBudget.cs:26`).

---

## Least-invasive plan

No code here; each item is the smallest change that fixes what she hit, with the files it touches.

1. **Night flow.** Add one read-only "night goal" built from the existing state (§1.6). Show it as the Now line (item 10). Replace the random "You're going the wrong way." with a line that can't mislead. Files: `Assets/Scripts/NightCycle.cs` (or `LodgerStory.cs` for the words), `NightZero.cs` (reuse its hint chain), `DayOneGuideUI.cs`, and `Assets/Editor/BarkSteps.cs:94` + `Assets/Data/Resources/Night lines.asset:124`.
2. **Notebook.** Keep "D-pad up: notebook" inside the Now line until the page has been opened once; `NightNotebook.Opened` already counts this (`NightNotebook.cs:35, 83`). Move "D-pad up Notebook" to the front of the night strip. Files: `NightZero.cs:352`, `NightNotebook.cs`, `ShopUI.cs:161-162`, `DayOneGuideUI.cs`.
3. **Choices.** In `Barks`, add a highlighted reply moved by up / down, taken with Cross / E. Draw ► and [Cross] on it, with the day's "[D-pad] Choose" footer. Keep 1 / 2 as direct picks. Make the notebook ignore D-pad up while choosing. Files: `Barks.cs` (`StepChoice`, `ShowChips`, `Pick`), `NightNotebook.cs:91`, and `CafeViewMode.cs:407-408` only if the wheel moves the highlight.
4. **Bins.** The Now line covers "Carry the bag out the back door" and "Put the bag in the dumpster". Keep the reveal camera on until the deal ends, so the view frames him in both views. That means moving the switch-off from `NightZero.cs:266` to `SceneDone` (`:427-437`); it needs a framing check. The alternative is one `CafeViewMode.LookTo` toward his head at `NightZero.cs:268`, for first person. Files: `NightZero.cs`.
5. **Houses.**
   - Use one name everywhere, in words she can see, e.g. "number 12, West Street, the yellow house with the porch light". Files: `LodgerStory.cs:148, 311`; `NightThings.cs:89-90, 98`; the scene's `HomeDoor.looks` (set by `Assets/Editor/HomeSetup.cs:124`).
   - Repaint house #6 so only Grace's is saffron. Files: `Assets/Editor/CafeStreetUpgrade.cs:39`, or the scene material by hand.
   - Show the street Ace is on beside the night clock, using the four street lines now in `Assets/Editor/NightLivedIn.cs:427`. Bigger plaques don't help, because they face away from the camera. Files: `ShopUI.cs:186`.
6. **Barnaby.** Give the man one short "why" at the deal, in his own words, in place of "Don't ask why. Ask how.". Files: `Assets/Editor/BarkSteps.cs:81` + `Night lines.asset:80`. The Now line already says what to do with him ("Bring Barnaby to the bins").
7. **Day guide.** When no featured visit is guided (Day 3, and Days 1–2 after the visits), use the existing `VisitAction` for the most urgent customer as a compact line. Raise the sizes from 21 / 18 to about 24 / 22. Files: `DayOneGuideUI.cs` (`GuideStep`, the sizes at `:864`).
8. **Franchise.** Say it once on Day 1 (the intro line or the first card), and keep "5 stars = franchise" under the stars on Tonight and on Today. Files: `DayIntro.cs:955` or `DayOneGuideUI.cs:555-557`; `RecapPhone.cs:986-992, 1686-1692`.
9. **Stock.** Make the chip say when and where: "Out of beans · restock at closing". Buying by day would change a rule, which is his call. Files: `HudCorners.cs:716` (12-word alert budget, `WordBudget.cs:43`).
10. **The Now line.** One always-on compact line on `DayOneGuideUI`'s card, top left under the tabs, day and night, 24–26 px. By day it is fed by the guide steps or the fallback; at the bench, nothing extra, because `BenchCards` is the guide there; at night, by the night goal. Files: `DayOneGuideUI.cs` (stop hiding at night, add the night source), `NightCycle.cs` (the goal), `BenchCards.cs` (read only).

**Decisions his to overrule:**
- Cross picking night replies reverses `Barks.cs:34-39`.
- Any night pin or marker goes against "curiosity, never markers" (`Notebook.cs:6-8`); the Now line avoids it.
- Buying by day goes against "only at closing, saved at once" (`RecapPhone.cs:45-46`; `UpgradeShopUI.cs:55-74`).
- All the words above are placeholders for him and his sister.
