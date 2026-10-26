#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE FURNITURE RULES, IN EDIT MODE (24 Oct 2026; furniture-spec.md §5.1, with his answers of 24 Oct)
//
// Fixit Fidget > Checks > Furniture rules. Pure functions, no scene and no Play: FurnitureRules on today's café as data, the
// café as the reference scene has it and Furniture 1 will make it (the 17 pieces' shapes in their own frames, the room's 49
// fixed solids with the lounge's floor lamp among them, the four waiting spots, the queue's three places, the three stations,
// the repair bar's three stools), read from the scene by the rules' Python twin on 24 Oct and pasted here. The same numbers
// the twin found are the expected ones below; a FAIL means the C# and the twin disagree, or a rule changed.
//   1. Each of the café's 17 own pieces fits against the rest.
//   2. Today's café (28 seats) passes both guards: 38 of 38 places reached from the door, 35 of 35 served with all taken.
//   3. A square table at (1.0, 9.5), in the aisle: "Keep the way from the door clear" (mockup 2; the spec's 0.9 isn't on
//      the 25 cm grid).
//   4. The half-finished café (his answer of 24 Oct, r1: 10 seats; its sheets' boxes solid for the guards): 20 of 20, 17 of
//      17; a bistro at (-3.0, 10.25) fits, and one turned a quarter at (-2.5, 10.25) (the spec's (3.1, 9.75) and (3.05,
//      11.55) were on its 3-seat Day 1: the lounge and the window counter stand there now).
//   5. A bay on the west wall at z 2.25 fits; a second touching it (z 4.35) fits; overlapping it by 1 cm doesn't.
//   6. The cram: today's café plus six bistros, each where the twin's search put it nearest the counter, fits (40 seats,
//      50 of 50, 47 of 47), and no seventh fits anywhere on the grid, either way round (the spec's five and sixth: the lamp
//      left as the room's frees the corner by the counter).
//   7. A bistro whose stand point is 0.89 m from the waiting nook's spot (half-finished): "Too close to where people wait".
//   8. The communal table with $300 in the till: "You need $60 more".
//   9. Every verdict's words are at most seven words.
//  10. Day 1's first ghost (Arrange's search: on a sheet first, nearest the counter) at (-4.75, 10.0) turned: it fits and
//      folds sheets 2 and 3.
//  11. 26 Oct 2026 (Days and first table): today's price (FurnitureRules.PriceOn, the one price function). Day 1 with no table
//      bought: the bistro and the square table $0, the plant its $35, a Day 2 kind its price; once a table is bought, and on
//      Day 2, every price is full again; the words are "<s>$80</s> Free today" only for the free one. With $70 in the till
//      (his Mac's second Day 1), the first ghost at its free price fits; at the full $80 it says "You need $10 more". With
//      no clock (Edit mode, the steps) the price is the full one.
// 26 Oct 2026 (Mac run fix): the data is now the café as his Mac has it: a43's repair-bar stools and stand points 10 cm back,
// and the counter's box from its MeshCollider's bounds (the customers' foot rail, z 13.16), which BuildMap reads; c12's
// window-counter stools (Snap To Seat, Max Lift, Approach Side) move no stand point or collider. Items 4 and 10 moved to the
// twin's new first spots; every other number is as it was.
// Report: Logs/Furniture/rules-<time>.txt; the Console's last line "[Furniture rules] All N checks passed." or
// "[Furniture rules] N of M checks FAILED. <folder>".
// ---------------------------------------------------------------------------
internal static class FurnitureRulesCheck
{
    const string Tag = "[Furniture rules]";
    static readonly StringBuilder report = new StringBuilder();
    static int checks, failures;

    static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
    }

    static void Note(string what) => report.AppendLine("  note  " + what);

    sealed class Kind
    {
        public readonly Bounds[] boxes;
        public readonly string[] parts;
        public readonly Vector3[] stands;
        public Kind(Bounds[] b, string[] p, Vector3[] s) { boxes = b; parts = p; stands = s; }

        public FurnitureRules.Shape At(Vector3 pivot, float yaw, string name, bool wall = false) => new FurnitureRules.Shape
        {
            name = name,
            solids = boxes.Select(b => PlacedPiece.ToWorld(b, pivot, yaw)).ToList(),
            stands = stands.Select(p => PlacedPiece.ToWorld(p, pivot, yaw)).ToList(),
            parts = parts.ToList(),
            againstAWall = wall,
        };
    }

    sealed class Piece
    {
        public readonly string name, kind;
        public readonly Vector3 pivot;
        public readonly float yaw;
        public readonly bool wall, open;
        public readonly Kind shape;
        public Piece(string n, string k, Vector3 p, float y, bool w, bool o, Kind s) { name = n; kind = k; pivot = p; yaw = y; wall = w; open = o; shape = s; }
        public FurnitureRules.Shape Shape => shape.At(pivot, yaw, kind, wall);
    }

    static Bounds B(float x0, float x1, float y0, float y1, float z0, float z1)
    {
        var b = new Bounds();
        b.SetMinMax(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));
        return b;
    }

    // 49 fixed solids (x0, x1, y0, y1, z0, z1): the room's walls, the counters, the bins, the repair bar and its stools, the lamp...
    static readonly (Bounds box, string name)[] FixedSolids =
    {
        (B(3.6098f, 3.7915f, 1.008f, 1.188f, 17.1886f, 17.4598f), "Discard basin"),
        (B(1.6f, 4.6f, 0f, 1f, 16.865f, 17.865f), "KitchenCounter"),
        (B(2.7531f, 2.9054f, 1.0715f, 1.4065f, 17.128f, 17.329f), "Cup and nozzle area"),
        (B(-3.3925f, -3.0075f, 0f, 1.039f, 12.55f, 12.951f), "CK_Stool_Bar (repair bar, east)"),   // 26 Oct 2026 (Mac run fix): a43, 10 cm back
        (B(-5.26f, -4.74f, 0f, 0.86f, -2.08f, -1.62f), "the wall"),
        (B(-7.64f, -7.4f, 0f, 0.76f, 0f, 12.7f), "the wall"),
        (B(1.0512f, 1.3792f, 0.03f, 2.51f, -1.1783f, -0.1186f), "the wall"),
        (B(2.5286f, 3.5999f, 1.038f, 1.628f, 17.4265f, 17.6336f), "Dispenser body collider"),
        (B(3.2564f, 3.3497f, 1.412f, 1.504f, 17.2403f, 17.2789f), "Dispense paddle"),
        (B(3.0984f, 3.1917f, 1.412f, 1.504f, 17.2393f, 17.2779f), "Dispense paddle"),
        (B(2.6245f, 2.7177f, 1.412f, 1.504f, 17.2361f, 17.2748f), "Dispense paddle"),
        (B(-2f, 2f, -0f, 1f, 13.1f, 14.1f), "Counter"),
        (B(-4.4139f, -3.6861f, 0f, 0.748f, -2.2139f, -1.4861f), "the wall"),
        (B(-7.4f, 4.9f, 0f, 1f, 16.9f, 18f), "CK_BackBar (the back bar run)"),
        (B(-3.36f, -2.84f, 0f, 0.86f, -2.08f, -1.62f), "the wall"),
        (B(2.3088f, 3.2648f, 0f, 0.851f, 0.14f, 0.572f), "LI_BinStation"),
        (B(-7.4f, -3.8f, 1f, 1.75f, 17.7f, 18f), "CK_PartsWall (72 drawers, the workshop end)"),
        (B(-5.334f, -4.734f, 1.006f, 1.011f, 13.818f, 14.178f), "BM_RepairMat"),
        (B(1.2f, 7.4f, 0f, 0.76f, -0.24f, 0f), "the wall"),
        (B(-4.3925f, -4.0075f, 0f, 1.039f, 12.55f, 12.951f), "CK_Stool_Bar (repair bar, middle)"),   // 26 Oct 2026 (Mac run fix): a43, 10 cm back
        (B(-1.3792f, -1.0512f, 0.03f, 2.51f, -1.1783f, -0.1186f), "the wall"),
        (B(-7.3003f, -4.9003f, 0f, 0.453f, -0.69f, -0.25f), "the wall"),
        (B(4.762f, 5.438f, 0f, 0.998f, -0.959f, -0.26f), "the wall"),
        (B(4.13f, 4.37f, 1f, 1.6f, 17.295f, 17.705f), "CK_Grinder (back bar, beside the dispenser)"),
        (B(-7.64f, -7.4f, 0f, 3.2f, 12.675f, 18.125f), "the wall"),
        (B(7.4f, 7.64f, 0f, 3.2f, -0.125f, 18.125f), "the wall"),
        (B(3.4144f, 3.5077f, 1.412f, 1.504f, 17.2413f, 17.28f), "Dispense paddle"),
        (B(2.7825f, 2.8757f, 1.412f, 1.504f, 17.2372f, 17.2758f), "Dispense paddle"),
        (B(-5.8f, -2.6f, 0f, 1.006f, 13.66f, 14.45f), "Workbench"),
        (B(3.0691f, 3.2214f, 1.0715f, 1.4065f, 17.1301f, 17.3311f), "Cup and nozzle area"),
        (B(2.9404f, 3.0337f, 1.412f, 1.504f, 17.2382f, 17.2768f), "Dispense paddle"),
        (B(3.1665f, 4.0335f, -0f, 0.9337f, -2.4384f, -1.5616f), "the wall"),
        (B(3.3851f, 3.5374f, 1.0715f, 1.4065f, 17.1322f, 17.3331f), "Cup and nozzle area"),
        (B(2.9111f, 3.0634f, 1.0715f, 1.4065f, 17.129f, 17.33f), "Cup and nozzle area"),
        (B(-3.8f, 4.9f, 1.75f, 1.81f, 17.7f, 18f), "CK_BackBarShelf (the long shelf)"),
        (B(2.5951f, 2.7474f, 1.0715f, 1.4065f, 17.127f, 17.3279f), "Cup and nozzle area"),
        (B(2.3448f, 2.5164f, 1.003f, 1.323f, 17.2023f, 17.4434f), "Cup stack"),
        (B(-7.4f, -1.2f, 0f, 0.76f, -0.24f, 0f), "the wall"),
        (B(-7.61f, -7.43f, 0f, 3.3f, 0f, 12.7f), "the wall"),
        (B(-7.4f, -1.2f, 0f, 3.3f, -0.21f, -0.03f), "the wall"),
        (B(1.2f, 7.4f, 0f, 3.3f, -0.21f, -0.03f), "the wall"),
        (B(-7.6f, 7.6f, 0f, 3.2f, -4.11f, -3.99f), "the wall"),
        (B(-7.63f, -7.51f, 0f, 3.2f, -4f, 0f), "the wall"),
        (B(7.51f, 7.63f, 0f, 3.2f, -4f, 0f), "the wall"),
        (B(3.2271f, 3.3794f, 1.0715f, 1.4065f, 17.1311f, 17.3321f), "Cup and nozzle area"),
        (B(-5.3925f, -5.0075f, 0f, 1.039f, 12.55f, 12.951f), "CK_Stool_Bar (repair bar, west)"),   // 26 Oct 2026 (Mac run fix): a43, 10 cm back
        (B(-7.4f, 4.87f, 0f, 1.1f, 13.16f, 14.24f), "the counter"),   // 26 Oct 2026 (Mac run fix): its MeshCollider's bounds reach the foot rail (z 13.16), not the stone's edge (13.30)
        (B(-7.4f, 4.9f, 0f, 1.2f, 16.9f, 18f), "the back bar"),
        (B(5.58f, 5.92f, 0f, 1.8f, 11.18f, 11.52f), "the lamp"),
    };
    static readonly Vector3[] Waiting = { new Vector3(-6.2f, 0.06f, 10.9f), new Vector3(6.2f, 0.06f, 0.75f), new Vector3(6.2f, 0.06f, 2.4f), new Vector3(6.2f, 0.06f, 4f) };
    static readonly Vector3[] Queue = { new Vector3(1.6f, 0f, 12.5f), new Vector3(0f, 0f, 12.5f), new Vector3(-1.6f, 0f, 12.5f) };
    static readonly Vector3[] Stations = { new Vector3(-0.26f, 0f, 14.54f), new Vector3(3.071f, 0f, 16.48f), new Vector3(-4.2f, 0f, 15.3f) };
    // 26 Oct 2026 (Mac run fix): a43 (Unity art fixes 4) moved the repair bar's stools and their stand points 10 cm back from the bar.
    static readonly Vector3[] RepairBarStands = { new Vector3(-5.2f, 0.04f, 12.0865f), new Vector3(-4.2f, 0.04f, 12.0865f), new Vector3(-3.2f, 0.04f, 12.0865f) };
    static readonly Vector3 Door = new Vector3(0f, 0.06f, -1.2f), AceStart = new Vector3(0f, 1f, 15.15f);
    // The café's own 17 pieces as Furniture 1 makes them: (root, kind, pivot, yaw, a wall piece, on in a new game, its shape in its own frame).
    static readonly Piece[] Own =
    {
        new Piece("Bistro table, south", "Bistro table", new Vector3(3f, 0f, 4.6f), 0f, false, false, new Kind(new[] { B(-0.36f, 0.36f, 0f, 0.75f, -0.36f, 0.36f), B(-0.862f, -0.378f, 0f, 0.863f, -0.212f, 0.212f), B(0.378f, 0.862f, 0f, 0.863f, -0.212f, 0.212f) }, new[] { "CK_Table_BistroRound (bistro 2-top, south)", "CK_Chair_Spindle (bistro 2-top, south, west)", "CK_Chair_Spindle (bistro 2-top, south, east)" }, new[] { new Vector3(-1.3397f, 0.04f, -0f), new Vector3(1.3397f, 0.04f, -0f) })),
        new Piece("Bistro table, north", "Bistro table", new Vector3(3f, 0f, 7.8f), 0f, false, true, new Kind(new[] { B(-0.36f, 0.36f, 0f, 0.75f, -0.36f, 0.36f), B(-0.862f, -0.378f, 0f, 0.863f, -0.212f, 0.212f), B(0.378f, 0.862f, 0f, 0.863f, -0.212f, 0.212f) }, new[] { "CK_Table_BistroRound (bistro 2-top, north)", "CK_Chair_Spindle (bistro 2-top, north, west)", "CK_Chair_Spindle (bistro 2-top, north, east)" }, new[] { new Vector3(-1.3397f, 0.04f, -0f), new Vector3(1.3397f, 0.04f, -0f) })),
        new Piece("Banquette bay, south", "Banquette bay", new Vector3(-7.4f, 0f, 4.69f), 90f, true, false, new Kind(new[] { B(-1.05f, 1.05f, 0f, 0.765f, -0f, 0.554f), B(-0.36f, 0.36f, 0f, 0.75f, 0.74f, 1.46f), B(-0.2235f, 0.2235f, 0f, 0.895f, 1.539f, 2.06f) }, new[] { "bay", "CK_Table_Square2 (banquette 2-top, south)", "CK_Chair_Cafe (banquette 2-top, south)" }, new[] { new Vector3(1.33f, 0.04f, 1.16f), new Vector3(-0.8f, 0.04f, 2.114f) })),
        new Piece("Banquette bay, middle", "Banquette bay", new Vector3(-7.4f, 0f, 6.79f), 90f, true, false, new Kind(new[] { B(-1.05f, 1.05f, 0f, 0.765f, -0f, 0.554f), B(-0.36f, 0.36f, 0f, 0.75f, 0.74f, 1.46f), B(-0.2235f, 0.2235f, 0f, 0.895f, 1.539f, 2.06f) }, new[] { "bay", "CK_Table_Square2 (banquette 2-top, middle)", "CK_Chair_Cafe (banquette 2-top, middle)" }, new[] { new Vector3(-0.98f, 0.04f, 1.31f), new Vector3(-0.7f, 0.04f, 2.064f) })),
        new Piece("Banquette bay, north", "Banquette bay", new Vector3(-7.4f, 0f, 8.89f), 90f, true, false, new Kind(new[] { B(-1.05f, 1.05f, 0f, 0.765f, -0f, 0.554f), B(-0.36f, 0.36f, 0f, 0.75f, 0.74f, 1.46f), B(-0.2235f, 0.2235f, 0f, 0.895f, 1.539f, 2.06f) }, new[] { "bay", "CK_Table_Square2 (banquette 2-top, north)", "CK_Chair_Cafe (banquette 2-top, north)" }, new[] { new Vector3(-0.83f, 0.04f, 1.06f), new Vector3(-1.05f, 0.04f, 1.864f) })),
        new Piece("Communal table, west of the aisle", "Communal table", new Vector3(-2.95f, 0f, 6.2f), 0f, false, false, new Kind(new[] { B(-0.45f, 0.45f, 0f, 0.75f, -1.8f, 1.8f), B(-0.963f, -0.637f, 0f, 0.452f, -1.5205f, -1.1795f), B(0.637f, 0.963f, 0f, 0.452f, -1.5205f, -1.1795f), B(-0.963f, -0.637f, 0f, 0.452f, -0.6205f, -0.2795f), B(0.637f, 0.963f, 0f, 0.452f, -0.6205f, -0.2795f), B(-0.963f, -0.637f, 0f, 0.452f, 0.2795f, 0.6205f), B(0.637f, 0.963f, 0f, 0.452f, 0.2795f, 0.6205f), B(-0.963f, -0.637f, 0f, 0.452f, 1.1795f, 1.5205f), B(0.637f, 0.963f, 0f, 0.452f, 1.1795f, 1.5205f) }, new[] { "CK_CommunalTable (communal table)", "CK_Stool_Low (communal table, west 1)", "CK_Stool_Low (communal table, east 1)", "CK_Stool_Low (communal table, west 2)", "CK_Stool_Low (communal table, east 2)", "CK_Stool_Low (communal table, west 3)", "CK_Stool_Low (communal table, east 3)", "CK_Stool_Low (communal table, west 4)", "CK_Stool_Low (communal table, east 4)" }, new[] { new Vector3(-1.65f, 0.04f, -1.112f), new Vector3(1.47f, 0.04f, -1.3385f), new Vector3(-1.45f, 0.04f, -0.412f), new Vector3(1.47f, 0.04f, -0.4385f), new Vector3(-1.4f, 0.04f, 0.439f), new Vector3(1.47f, 0.04f, 0.4615f), new Vector3(-1.65f, 0.04f, 1.138f), new Vector3(1.47f, 0.04f, 1.3615f) })),
        new Piece("Window counter, east wall", "Window counter", new Vector3(7.4f, 0f, 7.2f), 270f, true, true, new Kind(new[] { B(-2f, 2f, 0.34f, 1.05f, -0f, 0.45f), B(-1.3925f, -1.0075f, 0f, 1.039f, 0.699f, 1.1f), B(-0.1925f, 0.1925f, 0f, 1.039f, 0.699f, 1.1f), B(1.0075f, 1.3925f, 0f, 1.039f, 0.699f, 1.1f) }, new[] { "CK_WindowCounter (window counter)", "CK_Stool_Bar (window counter, south)", "CK_Stool_Bar (window counter, middle)", "CK_Stool_Bar (window counter, north)" }, new[] { new Vector3(-1.2f, 0.04f, 1.5635f), new Vector3(-0f, 0.04f, 1.5635f), new Vector3(1.2f, 0.04f, 1.5635f) })),
        new Piece("Lounge corner, the reading corner", "Lounge corner", new Vector3(-6f, 0f, 1.4f), 0f, false, false, new Kind(new[] { B(-0.3719f, 0.3719f, -0f, 0.735f, -1.175f, -0.485f), B(-0.45f, 0.45f, 0.01f, 0.4311f, -0.25f, 0.25f), B(-0.97f, -0.63f, 0f, 1.8f, 0.73f, 1.07f), B(-0.3719f, 0.3719f, -0f, 0.735f, 0.485f, 1.175f) }, new[] { "Tub chair", "Lounge table", "Floor lamp (front sofa)", "Tub chair (reading corner, north)" }, new[] { new Vector3(0.92f, 0.04f, -0.88f), new Vector3(0.95f, 0.04f, 0.55f) })),
        new Piece("Lounge corner, by the counter", "Lounge corner", new Vector3(4.85f, 0f, 10.55f), 0f, false, true, new Kind(new[] { B(-1.175f, -0.485f, -0f, 0.735f, -0.3719f, 0.3719f), B(0.485f, 1.175f, -0f, 0.735f, -0.3719f, 0.3719f), B(-0.25f, 0.25f, 0.01f, 0.4311f, -0.45f, 0.45f) }, new[] { "Tub chair (lounge, A)", "Tub chair (lounge, B)", "Lounge table (lounge)" }, new[] { new Vector3(-0.88f, 0.04f, -0.92f), new Vector3(0.55f, 0.04f, -0.95f) })),
        new Piece("Plant, fig in the front-left corner", "Plant", new Vector3(-6.85f, 0f, 0.75f), 0f, false, true, new Kind(new[] { B(-0.21f, 0.21f, -0f, 0.42f, -0.21f, 0.21f) }, new[] { "CK_FiddleLeafFig (front-left corner)" }, new Vector3[0])),
        new Piece("Plant, monstera at the counter's west end", "Plant", new Vector3(-6.8f, 0f, 12.6f), 0f, false, true, new Kind(new[] { B(-0.2f, 0.2f, 0f, 0.4f, -0.2f, 0.2f) }, new[] { "CK_Monstera (the counter's west end)" }, new Vector3[0])),
        new Piece("Plant, olive by the door (east)", "Plant", new Vector3(1.6f, 0f, 0.5644f), 0f, false, true, new Kind(new[] { B(-0.22f, 0.22f, -0f, 0.44f, -0.22f, 0.22f) }, new[] { "CK_OliveTree (door, east)" }, new Vector3[0])),
        new Piece("Plant, olive by the door (west)", "Plant", new Vector3(-1.6f, 0f, 0.5f), 0f, false, true, new Kind(new[] { B(-0.22f, 0.22f, 0f, 0.44f, -0.22f, 0.22f) }, new[] { "CK_OliveTree (door, west)" }, new Vector3[0])),
        new Piece("Plant, rubber plant between the bistros", "Plant", new Vector3(3f, 0f, 6.2f), 0f, false, false, new Kind(new[] { B(-0.17f, 0.17f, 0f, 0.34f, -0.17f, 0.17f) }, new[] { "CK_RubberPlant (between the bistro tables)" }, new Vector3[0])),
        new Piece("Plant, monstera by the lounge", "Plant", new Vector3(3.95f, 0f, 11.7f), 0f, false, true, new Kind(new[] { B(-0.2f, 0.2f, 0f, 0.4f, -0.2f, 0.2f) }, new[] { "CK_Monstera (lounge)" }, new Vector3[0])),
        new Piece("Plant, rubber plant front right", "Plant", new Vector3(4.05f, 0f, 1.15f), 0f, false, true, new Kind(new[] { B(-0.17f, 0.17f, 0f, 0.34f, -0.17f, 0.17f) }, new[] { "CK_RubberPlant (touch 1)" }, new Vector3[0])),
        new Piece("Plant, rubber plant front left", "Plant", new Vector3(-3.55f, 0f, 1.25f), 0f, false, false, new Kind(new[] { B(-0.17f, 0.17f, 0f, 0.34f, -0.17f, 0.17f) }, new[] { "CK_RubberPlant (touch 2)" }, new Vector3[0])),
    };
    // The kinds as Furniture 2 builds them (their prefabs' shapes in their own frames).
    static readonly Kind Bistro = new Kind(new[] { B(-0.36f, 0.36f, 0f, 0.75f, -0.36f, 0.36f), B(-0.862f, -0.378f, 0f, 0.863f, -0.212f, 0.212f), B(0.378f, 0.862f, 0f, 0.863f, -0.212f, 0.212f) }, new[] { "CK_Table_BistroRound (bistro 2-top, south)", "CK_Chair_Spindle (bistro 2-top, south, west)", "CK_Chair_Spindle (bistro 2-top, south, east)" }, new[] { new Vector3(-1.3397f, 0.04f, -0f), new Vector3(1.3397f, 0.04f, -0f) });
    static readonly Kind Square = new Kind(new[] { B(-0.36f, 0.36f, 0f, 0.75f, -0.36f, 0.36f), B(0.439f, 0.96f, 0f, 0.895f, -0.224f, 0.224f), B(-0.96f, -0.439f, 0f, 0.895f, -0.224f, 0.224f) }, new[] { "CK_Table_Square2 (square table)", "CK_Chair_Cafe (square table, east)", "CK_Chair_Cafe (square table, west)" }, new[] { new Vector3(1.4144f, 0.04f, 0f), new Vector3(-1.4144f, 0.04f, 0f) });
    static readonly Kind Communal = new Kind(new[] { B(-0.45f, 0.45f, 0f, 0.75f, -1.8f, 1.8f), B(-0.963f, -0.637f, 0f, 0.452f, -1.5205f, -1.1795f), B(0.637f, 0.963f, 0f, 0.452f, -1.5205f, -1.1795f), B(-0.963f, -0.637f, 0f, 0.452f, -0.6205f, -0.2795f), B(0.637f, 0.963f, 0f, 0.452f, -0.6205f, -0.2795f), B(-0.963f, -0.637f, 0f, 0.452f, 0.2795f, 0.6205f), B(0.637f, 0.963f, 0f, 0.452f, 0.2795f, 0.6205f), B(-0.963f, -0.637f, 0f, 0.452f, 1.1795f, 1.5205f), B(0.637f, 0.963f, 0f, 0.452f, 1.1795f, 1.5205f) }, new[] { "CK_CommunalTable (communal table)", "CK_Stool_Low (communal table, west 1)", "CK_Stool_Low (communal table, east 1)", "CK_Stool_Low (communal table, west 2)", "CK_Stool_Low (communal table, east 2)", "CK_Stool_Low (communal table, west 3)", "CK_Stool_Low (communal table, east 3)", "CK_Stool_Low (communal table, west 4)", "CK_Stool_Low (communal table, east 4)" }, new[] { new Vector3(-1.65f, 0.04f, -1.112f), new Vector3(1.47f, 0.04f, -1.3385f), new Vector3(-1.45f, 0.04f, -0.412f), new Vector3(1.47f, 0.04f, -0.4385f), new Vector3(-1.4f, 0.04f, 0.439f), new Vector3(1.47f, 0.04f, 0.4615f), new Vector3(-1.65f, 0.04f, 1.138f), new Vector3(1.47f, 0.04f, 1.3615f) });
    static readonly Kind Bay = new Kind(new[] { B(-1.05f, 1.05f, 0f, 0.765f, -0f, 0.554f), B(-0.36f, 0.36f, 0f, 0.75f, 0.74f, 1.46f), B(-0.2235f, 0.2235f, 0f, 0.895f, 1.539f, 2.06f) }, new[] { "bay", "CK_Table_Square2 (banquette 2-top, middle)", "CK_Chair_Cafe (banquette 2-top, middle)" }, new[] { new Vector3(-0.98f, 0.04f, 1.31f), new Vector3(-0.7f, 0.04f, 2.064f) });
    static readonly Kind Monstera = new Kind(new[] { B(-0.2f, 0.2f, 0f, 0.4f, -0.2f, 0.2f) }, new[] { "CK_Monstera" }, new Vector3[0]);
    // The five dust sheets (x0, x1, z0, z1) and their boxes' boxes (Furniture 3's plan).
    static readonly (Rect area, Bounds boxes)[] Sheets =
    {
        (Rect.MinMaxRect(-7.3f, 3.3f, -4.95f, 6.75f), B(-6.8836f, -6.2164f, 0.006f, 1.146f, 4.155f, 4.745f)),
        (Rect.MinMaxRect(-7.3f, 6.85f, -4.95f, 10f), B(-7.0206f, -6.3794f, 0.006f, 1.146f, 8.672f, 9.228f)),
        (Rect.MinMaxRect(-4.4f, 4.15f, -1.75f, 8.25f), B(-3.4546f, -2.7454f, 0.006f, 1.146f, 4.9753f, 5.6247f)),
        (Rect.MinMaxRect(-6.5f, 0.3f, -4.4f, 2.85f), B(-6.1922f, -5.5078f, 0.006f, 1.146f, 1.2431f, 1.8569f)),
        (Rect.MinMaxRect(1.55f, 3.4f, 3.75f, 6.65f), B(2.3865f, 3.0135f, 0.006f, 1.146f, 4.531f, 5.069f)),
    };

    // The cram: six bistros where the twin's search (nearest the counter, one after another) put them.
    static readonly Vector3[] Cram =
    {
        new Vector3(-3.25f, 0f, 10f), new Vector3(2.75f, 0f, 8.75f), new Vector3(3f, 0f, 5.5f),
        new Vector3(3f, 0f, 3.75f), new Vector3(-2.5f, 0f, 2.75f), new Vector3(-2.75f, 0f, 1.75f),
    };

    [MenuItem("Fixit Fidget/Checks/Furniture rules")]
    static void Run()
    {
        report.Clear();
        checks = failures = 0;
        report.AppendLine(Tag + " FurnitureRules on today's café as data (furniture-spec.md §5.1; his answers of 24 Oct)");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        report.AppendLine();
        try { Checks(); }
        catch (Exception e) { Check(false, "the check ran to its end (it stopped: " + e.Message + ")"); Debug.LogException(e); }
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Furniture"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "rules-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        string verdict = failures == 0 ? $"{Tag} All {checks} checks passed." : $"{Tag} {failures} of {checks} checks FAILED. {folder}";
        report.AppendLine();
        report.AppendLine(verdict);
        try { File.WriteAllText(file, report.ToString()); }
        catch (Exception e) { Debug.LogWarning(Tag + " Couldn't write the report: " + e.Message); }
        if (failures == 0) Debug.Log(report + "\n" + verdict);
        else Debug.LogError(report + "\n" + verdict);
    }

    static FurnitureRules.FloorMap Map(bool sheets) => FurnitureRules.MapFromData(
        FixedSolids.Select(f => f.box).ToList(), Waiting, Queue, Stations, Door, AceStart, RepairBarStands, FixedSolids.Select(f => f.name).ToList(),
        sheets ? Sheets.Select(s => s.boxes).ToList() : null, sheets ? Sheets.Select(s => s.area).ToList() : null);

    static string Say(FurnitureRules.Result r) => r.Fits ? "fits" : $"{r.verdict} (\"{r.words}\")";

    static void Checks()
    {
        FurnitureRules.FloorMap today = Map(false), half = Map(true);
        Note($"the map: {today.FixedCount} fixed solids, {today.waiting.Count} waiting spots, {today.queue.Count} queue places, {today.stations.Count} stations, " +
             $"{today.fixedSeatStands.Count} repair-bar stools; the half-finished café's {half.sheetBoxes.Count} box stacks");
        List<FurnitureRules.Shape> all = Own.Select(p => p.Shape).ToList();

        // 1. Each own piece against the rest.
        int fit = 0;
        for (int i = 0; i < all.Count; i++)
        {
            FurnitureRules.Result r = FurnitureRules.Check(today, all.Where((s, j) => j != i).ToList(), all[i]);
            if (r.Fits) fit++;
            else Note($"{Own[i].name}: {Say(r)}");
        }
        Check(fit == Own.Length && Own.Length == 17, $"1. each of the café's {Own.Length} own pieces fits against the rest ({fit} of {Own.Length})");

        // 2. Today's café, both guards.
        FurnitureRules.Result whole = FurnitureRules.CheckWholeLayout(today, all);
        int seats = all.Sum(s => s.stands.Count) + RepairBarStands.Length;
        Check(whole.Fits && seats == 28 && whole.reached == 38 && whole.places == 38 && whole.served == 35 && whole.servedOf == 35,
              $"2. today's café ({seats} seats) passes both guards: {whole.reached} of {whole.places} reached, {whole.served} of {whole.servedOf} served (38 and 35)");

        // 3. The square table in the aisle.
        FurnitureRules.Result aisle = FurnitureRules.Check(today, all, Square.At(new Vector3(1f, 0f, 9.5f), 0f, "Square table"), 90, 10000);
        Check(aisle.verdict == FurnitureRules.Verdict.Aisle && aisle.words == "Keep the way from the door clear", $"3. a square table at (1.0, 9.5): {Say(aisle)}");

        // 4. The half-finished café.
        List<FurnitureRules.Shape> open = Own.Where(p => p.open).Select(p => p.Shape).ToList();
        int halfSeats = open.Sum(s => s.stands.Count) + RepairBarStands.Length;
        FurnitureRules.Result h = FurnitureRules.CheckWholeLayout(half, open);
        Check(halfSeats == 10 && h.Fits && h.reached == 20 && h.places == 20 && h.served == 17 && h.servedOf == 17,
              $"4. the half-finished café ({halfSeats} seats, its box stacks solid): {h.reached} of {h.places} reached, {h.served} of {h.servedOf} served (20 and 17)");
        // 26 Oct 2026 (Mac run fix): (-3.0, 10.25), not (-3.0, 11.0): with a43's stools 10 cm back, the table's north edge (z 11.36) is
        // 0.73 m from the east stool's stand point, under the 0.75 m a seat keeps clear (BlocksAChair). The twin's first green square bistro now.
        FurnitureRules.Result b1 = FurnitureRules.Check(half, open, Bistro.At(new Vector3(-3f, 0f, 10.25f), 0f, "Bistro table"));
        FurnitureRules.Result b2 = FurnitureRules.Check(half, open, Bistro.At(new Vector3(-2.5f, 0f, 10.25f), 90f, "Bistro table"));
        Check(b1.Fits && b2.Fits, $"4. there a bistro at (-3.0, 10.25) {Say(b1)}, and one turned at (-2.5, 10.25) {Say(b2)}");

        // 5. Bays joining on the west wall (half-finished: the café's own bays are under the sheets).
        FurnitureRules.Shape bayA = Bay.At(new Vector3(FurnitureRules.WestWall, 0f, 2.25f), FurnitureRules.WestWallYaw, "Banquette bay", true);
        FurnitureRules.Result a = FurnitureRules.Check(half, open, bayA);
        var withA = new List<FurnitureRules.Shape>(open) { bayA };
        FurnitureRules.Result touch = FurnitureRules.Check(half, withA, Bay.At(new Vector3(FurnitureRules.WestWall, 0f, 4.35f), FurnitureRules.WestWallYaw, "Banquette bay", true));
        FurnitureRules.Result over = FurnitureRules.Check(half, withA, Bay.At(new Vector3(FurnitureRules.WestWall, 0f, 4.34f), FurnitureRules.WestWallYaw, "Banquette bay", true));
        Check(a.Fits && touch.Fits && over.verdict == FurnitureRules.Verdict.TooClose && over.words == "Too close to the banquette bay",
              $"5. a bay at z 2.25 {Say(a)}; one touching it at 4.35 {Say(touch)}; 1 cm into it at 4.34: {Say(over)}");

        // 6. The cram.
        var crammed = new List<FurnitureRules.Shape>(all);
        int placed = 0;
        foreach (Vector3 p in Cram)
        {
            FurnitureRules.Shape c = Bistro.At(p, 0f, "Bistro table");
            FurnitureRules.Result r = FurnitureRules.Check(today, crammed, c);
            if (!r.Fits) { Note($"the cram's bistro at ({p.x:0.00}, {p.z:0.00}): {Say(r)}"); break; }
            crammed.Add(c);
            placed++;
        }
        FurnitureRules.Result cram = FurnitureRules.CheckWholeLayout(today, crammed);
        int cramSeats = crammed.Sum(s => s.stands.Count) + RepairBarStands.Length;
        Check(placed == Cram.Length && cram.Fits && cramSeats == 40 && cram.reached == cram.places && cram.served == cram.servedOf,
              $"6. today's café plus {placed} bistros fits ({cramSeats} seats; {cram.reached} of {cram.places} reached, {cram.served} of {cram.servedOf} served)");
        int tried = 0, local = 0;
        string seventh = "";
        for (float x = -7.25f; x <= 7.25f && seventh.Length == 0; x += FurnitureRules.Grid)
            for (float z = FurnitureRules.Grid; z < FurnitureRules.CounterFront && seventh.Length == 0; z += FurnitureRules.Grid)
                foreach (float y in new[] { 0f, 90f })
                {
                    tried++;
                    FurnitureRules.Shape c = Bistro.At(new Vector3(x, 0f, z), y, "Bistro table");
                    if (!FurnitureRules.CheckLocal(today, crammed, c).Fits) continue;
                    local++;
                    if (FurnitureRules.Check(today, crammed, c).Fits) { seventh = $"({x:0.00}, {z:0.00}) at {y:0}"; break; }
                }
        Check(seventh.Length == 0, $"6. ...and no seventh bistro fits anywhere ({tried} cells and turns tried, {local} passing the quick rules, " +
                                   (seventh.Length == 0 ? "none both guards" : "one fits at " + seventh) + ")");

        // 7. Too close to where people wait.
        FurnitureRules.Shape nook = Bistro.At(new Vector3(-4.25f, 0f, 10.25f), 0f, "Bistro table");
        float toNook = nook.stands.Min(s => Waiting.Min(w => Vector2.Distance(new Vector2(s.x, s.z), new Vector2(w.x, w.z))));
        FurnitureRules.Result wait = FurnitureRules.Check(half, open, nook);
        Check(wait.verdict == FurnitureRules.Verdict.NearWaiting && wait.words == "Too close to where people wait",
              $"7. a bistro at (-4.25, 10.25), its stand point {toNook:0.00} m from a waiting spot: {Say(wait)}");

        // 8. Money.
        FurnitureRules.Result money = FurnitureRules.Check(half, open, Communal.At(new Vector3(-3f, 0f, 8.75f), 0f, "Communal table"), 360, 300);
        Check(money.verdict == FurnitureRules.Verdict.NotEnoughMoney && money.words == "You need $60 more", $"8. the communal table with $300 in the till: {Say(money)}");

        // 9. Words.
        List<string> longWords = FurnitureRules.AllWords().Where(w => w.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 7).ToList();
        Check(longWords.Count == 0, $"9. every verdict's words are at most seven words ({FurnitureRules.AllWords().Count()} of them" +
                                    (longWords.Count > 0 ? "; too long: " + string.Join(" | ", longWords) : "") + ")");

        // 10. Day 1's first ghost.
        // 26 Oct 2026 (Mac run fix): (-4.75, 10.0), not 10.25: with a43's stools 10 cm back, the north chair's stand point at 11.59 was
        // 0.67 m from the west stool's, under the 0.70 m a seat claims (NearSeat). The twin's first ghost now folds sheets 2 and 3.
        FurnitureRules.Shape first = Bistro.At(new Vector3(-4.75f, 0f, 10f), 90f, "Bistro table");
        FurnitureRules.Result firstFits = FurnitureRules.Check(half, open, first);
        List<int> folded = Enumerable.Range(0, Sheets.Length).Where(i => FurnitureRules.FoldsSheet(first, Sheets[i].area)).Select(i => i + 1).ToList();
        Check(firstFits.Fits && folded.Count == 2 && folded[0] == 2 && folded[1] == 3, $"10. Day 1's first ghost at (-4.75, 10.00) turned {Say(firstFits)}, folding sheet(s) {string.Join(", ", folded)}");

        // 11. Day 1's first table is free (26 Oct 2026, Days and first table).
        FurnitureDefinition bistroKind = TempKind("Bistro table", 80, 2, 1), squareKind = TempKind("Square table", 90, 2, 1);
        FurnitureDefinition plantKind = TempKind("Plant", 35, 0, 1), counterKind = TempKind("Window counter", 120, 3, 2);
        try
        {
            bool freeDay1 = FurnitureRules.PriceOn(bistroKind, 1, false) == 0 && FurnitureRules.PriceOn(squareKind, 1, false) == 0
                            && FurnitureRules.PriceOn(plantKind, 1, false) == 35 && FurnitureRules.PriceOn(counterKind, 1, false) == 120;
            bool fullAfter = FurnitureRules.PriceOn(bistroKind, 1, true) == 80 && FurnitureRules.PriceOn(squareKind, 1, true) == 90
                             && FurnitureRules.PriceOn(bistroKind, 2, false) == 80 && FurnitureRules.PriceOn(squareKind, 3, false) == 90
                             && FurnitureRules.PriceOn(plantKind, 2, false) == 35;
            bool noClock = FurnitureRules.PriceToday(bistroKind) == 80 && FurnitureRules.PriceWords(bistroKind) == "$80" && !FurnitureRules.FreeToday(bistroKind);
            Check(freeDay1, $"11. Day 1, no table bought yet: the bistro ${FurnitureRules.PriceOn(bistroKind, 1, false)}, the square ${FurnitureRules.PriceOn(squareKind, 1, false)} ($0 each), " +
                            $"the plant ${FurnitureRules.PriceOn(plantKind, 1, false)} ($35), a Day 2 kind ${FurnitureRules.PriceOn(counterKind, 1, false)} ($120)");
            Check(fullAfter, $"...after one table, and on Days 2 and 3, full price: the bistro ${FurnitureRules.PriceOn(bistroKind, 1, true)} / ${FurnitureRules.PriceOn(bistroKind, 2, false)} ($80), " +
                             $"the square ${FurnitureRules.PriceOn(squareKind, 1, true)} / ${FurnitureRules.PriceOn(squareKind, 3, false)} ($90)");
            Check(noClock, $"...with no clock (Edit mode) the full price: ${FurnitureRules.PriceToday(bistroKind)}, \"{FurnitureRules.PriceWords(bistroKind)}\" ($80)");
            FurnitureRules.Result freeWith70 = FurnitureRules.Check(half, open, first, FurnitureRules.PriceOn(bistroKind, 1, false), 70);
            FurnitureRules.Result fullWith70 = FurnitureRules.Check(half, open, first, FurnitureRules.PriceOn(bistroKind, 1, true), 70);
            Check(freeWith70.Fits && fullWith70.verdict == FurnitureRules.Verdict.NotEnoughMoney && fullWith70.words == "You need $10 more",
                  $"...with $70 in the till (his Mac's Day 1 without the thrift watch): the free first ghost {Say(freeWith70)}; at $80 {Say(fullWith70)}");
        }
        finally
        {
            foreach (FurnitureDefinition k in new[] { bistroKind, squareKind, plantKind, counterKind }) if (k != null) UnityEngine.Object.DestroyImmediate(k);
        }
    }

    static FurnitureDefinition TempKind(string displayName, int price, int seats, int fromDay)
    {
        FurnitureDefinition k = ScriptableObject.CreateInstance<FurnitureDefinition>();
        k.hideFlags = HideFlags.HideAndDontSave;
        k.name = displayName;
        k.displayName = displayName;
        k.price = price;
        k.seats = seats;
        k.forSaleFromDay = fromDay;
        return k;
    }
}
#endif
