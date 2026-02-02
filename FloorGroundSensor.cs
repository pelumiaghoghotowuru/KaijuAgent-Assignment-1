using UnityEngine;
using KaijuSolutions.Agents.Exercises.Cleaner;

namespace CleanerSolutions
{
    public class FloorGroundSensor : MonoBehaviour
    {
        [SerializeField] private float rayStartOffset = 0.5f;
        [SerializeField] private float rayDistance = 2.0f;
        [SerializeField] private LayerMask floorMask = ~0;

        [SerializeField] private bool drawDebugRay = true;

        public Floor onFloor {  get; private set; }

        public bool HasFloor => onFloor != null;
        public bool IsCurrentDirty => onFloor != null && onFloor.Dirty;

        public void Sense()
        {
            Vector3 origin = transform.position + Vector3.up * rayStartOffset;

            if (drawDebugRay)
            {
                Debug.DrawRay(origin, Vector3.down * rayDistance, Color.yellow);
            }

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hitInfo, rayDistance, floorMask))
            {
                onFloor = hitInfo.collider.GetComponent<Floor>() ?? hitInfo.collider.GetComponentInParent<Floor>();
            }
            else
            {
                onFloor = null;
            }
        }
    }
}


