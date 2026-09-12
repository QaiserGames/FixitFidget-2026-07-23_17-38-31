using UnityEngine;

// A valve physically stays depressed during its own pour.
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
