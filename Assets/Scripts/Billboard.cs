using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera cam;

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Face away from the camera so text reads the right way round.
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}