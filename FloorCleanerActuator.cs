using System.Collections;
using UnityEngine;
using KaijuSolutions.Agents.Exercises.Cleaner;

namespace CleanerSolutions
{
    /// <summary>
    /// The Actuator is responsible for cleaning the dirty floor tiles. It is not an instant action with a cleaning duration of 1sec
    /// and validates that the agent is still on the same tile, performing the cleaning action, so if the agent moves away then the tile remains
    /// dirty and is still stored in the ObservableCollection of Dirty Tiles. 
    /// </summary>
    public class FloorCleanerActuator : MonoBehaviour
    {
        [SerializeField] private FloorGroundSensor groundSensor;
        [SerializeField] private float cleaningDuration = 0.6f;

        //indicates whether the actuator is performing a cleaning
        public bool IsCleaning { get; private set; }

        //there are conditions in place for if we can clean the floor tile: is it dirty, is our agent abpoe the tile and has it already been cleaned
        public bool CanClean => !IsCleaning && groundSensor != null
            && groundSensor.HasFloor && groundSensor.IsCurrentDirty;

        //we request that our tile begins cleaning
        public void TryToClean() {
            if (!CanClean) return;
            StartCoroutine(CleanRoutine());
        }

        //cleaning action is performed on the current Floor tile we are on, so that if we move away then the action is not completed and the tile returns to Dirty.
        private IEnumerator CleanRoutine()
        {
            IsCleaning = true;

            Floor target = groundSensor.onFloor;

            yield return new WaitForSeconds(cleaningDuration);

            groundSensor.Sense();

            if (groundSensor.onFloor == target && target != null && target.Dirty)
            {
                //floor successfully cleaned
                target.Clean();
            }
            //cleaning complete
            IsCleaning = false;
        }
    }
}

