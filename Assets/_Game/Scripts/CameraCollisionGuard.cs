using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Stops the camera from poking through walls/geometry when it has an
    /// offset from the player's collision capsule (headbob, lean, etc.) - the
    /// CharacterController only protects its own body, not anything offset
    /// from it, so without this a camera lean/bob can clip straight through
    /// a nearby wall.
    ///
    /// Runs in LateUpdate, AFTER any headbob/lean script has applied its
    /// offset for the frame (Unity runs LateUpdate after all Update calls),
    /// so it always corrects the final camera position. If your headbob/lean
    /// script also uses LateUpdate, make sure this component's script executes
    /// AFTER it via Edit > Project Settings > Script Execution Order.
    ///
    /// Setup: attach to the CAMERA itself (or a rig it sits under). "Anchor"
    /// should be a transform that is always safely inside the player's own
    /// collision (e.g. the player body/root, or wherever the camera would sit
    /// with zero offset) - the camera is cast for FROM there.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraCollisionGuard : MonoBehaviour
    {
        [Tooltip("A point guaranteed to be inside the player's own collision (e.g. the player root/body transform). If left empty, this object's parent is used.")]
        [SerializeField] private Transform anchor;
        [Tooltip("Small buffer kept between the camera and any wall it is pulled back from, so it doesn't sit exactly on the surface.")]
        [SerializeField, Min(0f)] private float skinWidth = 0.05f;
        [Tooltip("Radius of the sphere cast used to detect walls - a small value approximates the camera as a point, a larger one avoids tight corner clipping.")]
        [SerializeField, Min(0f)] private float castRadius = 0.1f;
        [Tooltip("Layers considered solid for this check. Should match whatever the level geometry (walls, doors, etc.) is on.")]
        [SerializeField] private LayerMask obstructionMask = ~0;

        private void LateUpdate()
        {
            Transform safeAnchor = anchor != null ? anchor : transform.parent;
            if (safeAnchor == null)
                return;

            // The first LateUpdate after any offset (headbob/lean) has been
            // applied this frame is what we correct against - capture it once
            // per frame before adjusting.
            Vector3 desiredWorldPosition = transform.position;

            // Cast from the camera's own HEIGHT, using the anchor only for a
            // stable horizontal (X/Z) reference point. If the anchor sits at
            // feet/ground level (common for a CharacterController's pivot),
            // starting the cast there would begin right at/inside the floor
            // collider, registering it as a false "obstruction" at distance
            // ~0 and yanking the camera down to the floor.
            Vector3 castOrigin = new Vector3(safeAnchor.position.x, transform.position.y, safeAnchor.position.z);
            Vector3 fromAnchor = desiredWorldPosition - castOrigin;
            float distance = fromAnchor.magnitude;
            if (distance <= 0.0001f)
                return;

            Vector3 direction = fromAnchor / distance;

            // SphereCastAll (not SphereCast) so we can skip any hit that
            // belongs to the player's own body/CharacterController - without
            // this, the cast immediately self-intersects with the player's
            // own capsule and yanks the camera down to the anchor point.
            RaycastHit[] hits = Physics.SphereCastAll(
                castOrigin,
                castRadius,
                direction,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore);

            float closestDistance = distance;
            bool foundObstruction = false;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;
                if (hit.collider.transform.IsChildOf(safeAnchor.root) ||
                    hit.collider.transform.root == transform.root)
                {
                    continue; // Part of the player/camera rig itself - not a real wall.
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    foundObstruction = true;
                }
            }

            if (foundObstruction)
            {
                float safeDistance = Mathf.Max(0f, closestDistance - skinWidth);
                transform.position = castOrigin + direction * safeDistance;
            }
        }
    }
}