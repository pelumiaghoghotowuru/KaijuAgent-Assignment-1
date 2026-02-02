using System.Collections;
using UnityEngine;
using KaijuSolutions.Agents.Exercises.Cleaner;

namespace CleanerSolutions
{
    public class FloorCleanerActuator : MonoBehaviour
    {
        [SerializeField] private FloorGroundSensor groundSensor;
        [SerializeField] private float cleaningDuration = 1.0f;

        public bool IsCleaning { get; private set; }

        public bool CanClean => !IsCleaning && groundSensor != null
            && groundSensor.HasFloor && groundSensor.IsCurrentDirty;

        public void TryToClean() {
            if (!CanClean) return;
            StartCoroutine(CleanRoutine());
        }

        private IEnumerator CleanRoutine()
        {
            IsCleaning = true;

            Floor target = groundSensor.onFloor;

            yield return new WaitForSeconds(cleaningDuration);

            groundSensor.Sense();

            if (groundSensor.onFloor == target && target != null && target.Dirty)
            {
                target.Clean();
            }

            IsCleaning = false;
        }
    }
}

