# Brief — build the playtest journal page (Opus): one HTML file, not published by you

## What it is
Mansoor (solo dev of Fix It Fiasco, a repair-café sim in Unity) asked for "a playtest notes for me that … are in general curious towards the progress of the completion of this game." It's a **phone-first page** he and his girlfriend fill in after each playtest, in about five minutes, that keeps every entry and shows how "finished" each part of the game feels over time. The studio's agents read the entries later (through the page's database). The Producer will review your file and publish it as a claude.ai artifact with `capabilities: {db: {}, user: {}}`. **Do not publish anything yourself.**

## Read first
1. The Artifact page contract and design guidance: run nothing; read the file `~/cafe/brief/artifact-page-rules.md` (the Producer saved the platform's rules there) — follow every rule in it (no `<!doctype>`/`<html>`/`<head>`/`<body>` tags; a `<title>` and `<meta name="claude:viewport-fit-sides" content="cover">` at the top; theme tokens for light and dark exactly in the shape given; the 16px safe-area side gutter; Google Fonts only for fonts; scripts only from the allowed CDNs pinned to exact versions, though you shouldn't need any; no alert/confirm/prompt; no downloads; no `window.print`).
2. The runtime API you'll use — read these type files with the Read tool: `/tmp/claude-0/bundled-skills/2.1.299/e4e5a8fd37c6fb65bb2a07c83853ae09/artifact-capabilities/0.2.77/claude.d.ts`, `…/0.2.77/db.d.ts`, `…/0.2.77/user.d.ts`. Key rules: `const db = await claude.use("db")` resolves the namespace or `null` (handle null: show a read-only "can't save here" state); `window.claude` carries only `use`; subscribe once per query (`onSnapshot`), never in render; one write at a time per doc; handle error codes per the d.ts; **never hardcode seed records in the page** (the Producer seeds after publishing); render a designed empty state first.
3. The game's look (to borrow the world, not copy screens): the café's signs use **League Gothic** (condensed, all caps) and **Yellowtail** (script) — both on Google Fonts; the brand green is `#2E7D5B` (bright `#5FBF8F`-ish for dark mode, your call), warm greys, a repair bench's paper job tickets. Pick a readable body face that is not Inter or Space Grotesk, and a mono for numbers if you like.

## Content (all of it, in this order)
**Top line** (one compact card): playtest number (auto-suggest the next number from existing entries; editable), date (default today), who played (text with suggestions from names used before; examples "Mansoor", "Guest"), machine (Mac / PC chips), input (mouse & keyboard / controller chips), how far they got (free text, e.g. "Day 1, didn't finish"), recording (optional text: a file name or link).

**How finished does each part feel?** A 1–5 rating for each of ten areas, each as five big tap targets (≥ 44 px) labelled 1–5 with the scale under the first row: 1 placeholder · 2 rough · 3 works · 4 nearly · 5 ship it; a "skip" state (unrated). Areas, in this order: The bench (repairs) · Drinks · Serving people · The café as a place · The people in it · The night · The story · Sound & music · Words & menus · Overall look.

**Eight questions** (short textareas; the first is a three-way choice plus "why" text):
1. Would a stranger pay $12.99 for what you just played? (Yes / Not yet / No) — why, in one line
2. Where did it stop — or start — being fun?
3. Where did you think "what am I supposed to do?"
4. What felt the most unfinished?
5. What felt finished enough to show a friend?
6. What did you want to do that the game didn't let you?
7. Did you want to play the next day? Why or why not?
8. The one thing to fix before the next playtest.

**Free notes** (a big textarea: "Anything, in your own words.").

**If a guest played** (a toggle that reveals): "In one sentence, what is this game?" (their words) and "Where did they get stuck, and what did they say out loud?" (the observer's notes), plus a reminder line: "Watch, don't help."

**Save entry** (sticky at the bottom on phones; disabled with a clear reason when the db is unavailable; on success, a quiet confirmation and the form resets; keep an unsent draft in `localStorage` wrapped in try/catch so a closed tab doesn't lose it).

**The progress view** (above or beside the form; on phones, a tab or a section above): 
- A headline readout: the latest playtest's average "how finished" (one decimal, 1–5) and its change from the previous one, plus the latest "Would a stranger pay?" answer.
- For each of the ten areas, one row: the area name, the latest score as five pips, and a small inline sparkline of its scores across playtest numbers (SVG drawn to scale, from theme tokens; gaps for unrated). An entry with no scores contributes nothing to charts (playtest 5's seeded entry will have notes only).
- Rows are sorted by the latest score ascending (what needs work first), with a toggle to keep the fixed order.
- When there are no scored entries yet: a designed empty state that says scores appear after the first rated entry.

**The entries list**: newest first, each a collapsed card (playtest number, date, who, machine/input, how far, the pay answer, a short preview of notes); tap to expand everything; an **Edit** button on each entry (loads it into the form; saving updates the same doc) and **Delete** with an in-page confirm step (two taps, no `confirm()`).

## Data model (the agents will read it — keep it exactly)
Collection `entries`; one document per entry at `entries/<id>` where id = `<yyyymmdd>-<6 random base36 chars>`. Document fields:
```
{ "playtest": 6, "date": "2026-10-20", "who": "Mansoor", "machine": "Mac", "input": "controller",
  "howFar": "Day 3 and Night 2", "recording": "",
  "scores": { "bench": 3, "drinks": 4, "serving": 3, "cafe": 3, "people": 2, "night": 3, "story": 3, "sound": 1, "words": 3, "look": 3 },
  "pay": "not_yet", "payWhy": "…",
  "answers": { "fun": "…", "lost": "…", "unfinished": "…", "showFriend": "…", "wanted": "…", "nextDay": "…", "oneFix": "…" },
  "notes": "…", "guest": { "oneSentence": "…", "stuck": "…" } | null,
  "authorId": "<user.id() or null>", "createdAt": "<ISO>", "updatedAt": "<ISO>", "v": 1 }
```
Unrated scores are `null`. `machine` ∈ "Mac" | "PC" | ""; `input` ∈ "mouse" | "controller" | ""; `pay` ∈ "yes" | "not_yet" | "no" | "". Subscribe to the whole collection with `onSnapshot` (it will stay small); sort client-side by playtest number then date. Use `user.id()` (from `claude.use("user")`, may be null) only to stamp `authorId`.

## Quality bar
- Phone at 390 px first; two columns (form | progress) at ≥ 960 px. No horizontal scroll ever. Big tap targets. Visible focus states. `prefers-reduced-motion` respected.
- Light and dark both designed (tokens, as the rules say). The body has an explicit background.
- Plain, warm, specific copy; no exclamation marks; no emoji.
- One file: `~/cafe/journal/journal.html`. Inline CSS and JS.
- **Test it yourself** with Playwright and the pre-installed Chromium (`PLAYWRIGHT_BROWSERS_PATH=/opt/pw-browsers`; don't run `playwright install`): wrap the file in a minimal skeleton for the test (doctype, charset, viewport meta) and inject a **mock** `window.claude = { use: async (n) => … }` that implements the db calls you use in memory (and a mock user) — the mock lives only in your test harness, never in the page. Exercise: empty state; save three entries (one unscored, two scored); edit one; delete one with the two-step confirm; the progress view; db `null` (read-only state); reload with a draft in localStorage. Take screenshots at 390 × 844 and 1280 × 900, light and dark, and look at them with the Read tool; fix what's wrong once. Report any console errors.

Your final message: what you built, the file path, the screenshots' paths, what you tested and how it went, anything the Producer must know before publishing (the capabilities, any caveat).
