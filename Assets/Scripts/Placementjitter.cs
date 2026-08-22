using System.Runtime.CompilerServices;
using UnityEngine;

// Items laid out on exactly the same yaw at exactly even spacing read as a
// spawner, not as a shop. A few degrees of rotation and a centimetre or two of
// offset is the whole difference between "placed by a person" and "instanced".
//
// The jitter is SEEDED off the item itself, so it's stable: pick a watch up and
// put it back down and it lands the way it was, instead of hopping to a new
// random angle every time. It also restores the global Random state afterwards,
// so seeding here can't disturb anything else that's rolling dice this frame.
public static class PlacementJitter
{
    public static void Apply(JobBase item, Transform spot, float yawDegrees, float offsetMetres)
    {
        if (item == null || spot == null) return;

        Vector3 basePos = spot.position + Vector3.up * item.restHeight;

        if (yawDegrees <= 0f && offsetMetres <= 0f)
        {
            item.transform.position = basePos;
            item.transform.rotation = spot.rotation;
            return;
        }

        Random.State previous = Random.state;

        // Identity hash of the object reference: unique per instance, stable
        // for its lifetime, and not a Unity API — so it can't be deprecated out
        // from under us the way GetInstanceID() was.
        Random.InitState(RuntimeHelpers.GetHashCode(item));

        float yaw = Random.Range(-yawDegrees, yawDegrees);
        Vector3 offset = new Vector3(
            Random.Range(-offsetMetres, offsetMetres), 0f,
            Random.Range(-offsetMetres, offsetMetres));

        Random.state = previous;

        // Offset in the spot's own space, so a rotated shelf still scatters
        // along its own surface rather than along world X/Z.
        item.transform.position = basePos + spot.rotation * offset;
        item.transform.rotation = spot.rotation * Quaternion.Euler(0f, yaw, 0f);
    }
}