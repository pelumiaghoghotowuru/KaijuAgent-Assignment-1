using UnityEngine;
using KaijuSolutions.Agents.Exercises.Cleaner;

namespace CleanerSolutions
{
    /// <summary>
    /// Detects which Floor tiles the kaijuAgent is currently standing on using a downward cast ray
    /// 
    /// The rayOffset is defined to start from outside of the agent's collider.
    /// The rayDistance is the maximum distance that the raycast checks downward with a height slightly greater than our KaijuAgent's height. 
    /// The floorMask filters objects which the RayCast can hit and is focused primarily on the sloor tiles only.
    /// </summary>
    public class FloorGroundSensor : MonoBehaviour
    {
        [SerializeField] private float rayStartOffset = 0.5f;
        [SerializeField] private float rayDistance = 2.0f;
        [SerializeField] private LayerMask floorMask = ~0;

        //set up for debugging to visualize ground detection
        [SerializeField] private bool drawDebugRay = true;

        //gets and sets the FloorTile beneath the agent and updated through the Sense() method
        public Floor onFloor {  get; private set; }

        //detects if we are currently on the FloorTile
        public bool HasFloor => onFloor != null;
        //checks if the FloorTile is currently dirty and stores the bool reference to use for triggering cleaning
        public bool IsCurrentDirty => onFloor != null && onFloor.Dirty;

        /// <summary>
        /// The Sense Method implements the Physics.Raycast and checks if we have hit a Floor object
        /// </summary>
        public void Sense()
        {
            //compute to avoid self-collision
            Vector3 origin = transform.position + Vector3.up * rayStartOffset;

            if (drawDebugRay)
            {
                Debug.DrawRay(origin, Vector3.down * rayDistance, Color.yellow);
            }

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hitInfo, rayDistance, floorMask))
            {
                //attempt to retrieve the Floor component from the object hit
                onFloor = hitInfo.collider.GetComponent<Floor>() ?? hitInfo.collider.GetComponentInParent<Floor>();
            }
            else
            {
                //no floor on agent
                onFloor = null;
            }
        }
    }
}


