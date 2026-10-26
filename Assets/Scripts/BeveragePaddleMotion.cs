using UnityEngine;

// A valve physically stays depressed during its own pour.
// 26 Oct 2026 (Drink buttons): the paddle's visible face is now the picture cap DB_Cap_<Name> parented under this transform
// (Fixit Fidget > Drinks > Buttons 1), so it rides the same press; the old tile's renderer is off. That step sets
// pressedOffset to 9 mm (a third of the cap's 30 mm; the installer gave the old 26 mm tile 6).
public sealed class BeveragePaddleMotion : MonoBehaviour
{
    public BeverageSlot slot;
    public Vector3 pressedOffset;
    private Vector3 rest;
    private float depression;
    private void Awake() { rest = transform.localPosition; }
    private void Update()
    {
        depression = Mathf.MoveTowards(depression, slot != null && slot.IsPouring ? 1 : 0, Time.deltaTime * 9);
        transform.localPosition = rest + pressedOffset * depression;
    }
}
