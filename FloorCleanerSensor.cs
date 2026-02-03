using System.Collections.Generic;
using System.Linq;
using KaijuSolutions.Agents.Exercises.Cleaner;
using KaijuSolutions.Agents.Sensors;
using UnityEngine;

namespace CleanerSolutions
{
    /// <summary>
    /// Vision sensor specialized for detecting Floor tiles in the Cleaner exercise.
    /// 
    /// This extends Kaiju's generic vision sensor (KaijuVisionSensor<T>) and sets T = Floor.
    /// That means:
    /// - The Kaiju base class handles the "human-like" vision constraints (distance, FOV angle,
    ///   optional line-of-sight, etc.).
    /// - This class focuses on higher-level helper logic useful to the controller, such as
    ///   "which observed tiles are dirty" and "what is the nearest dirty tile I can see."
    /// </summary>
    public class FloorCleanerSensor : KaijuVisionSensor<Floor>
    {
        public IEnumerable<Floor> observeDirtyTiles => Observed.Where(f => f != null && f.Dirty);

        /// <summary>
        /// We iterate through the Collection of observed dirty tiles from the Vision Sensor stored in the memory and
        /// use the squared distance so we always have the nearest tile stored. There are checks against a clean tile or if the floor has been destroyed
        /// </summary>
        /// <returns></returns>
        public Floor NearestDirty()
        {
            Floor best = null;
            float bestSqr = float.MaxValue;
            foreach (var tile in observeDirtyTiles)
            {
                if (tile == null || !tile.Dirty) continue;

                float d = (tile.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = tile;
                }
            }
            return best;
        }

        /// <summary>
        /// Defines the default set of objects that this sensor is allowed to "see"
        /// if Observables was not explicitly provided.
        /// 
        /// By returning all Floor objects in the scene, we allow the base Kaiju vision logic
        /// to apply visibility constraints (distance/angle/LOS) to those candidates.
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<Floor> DefaultObservables()
        {
            return FindObjectsByType<Floor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        //private float _nextLog;

        //private void Update()
        //{
        //    if (Time.time >= _nextLog)
        //    {
        //        _nextLog = Time.time + 1f;
        //        UnityEngine.Debug.Log($"[Vision] Observed: {ObservedCount}, Dirty seen: {observeDirtyTiles.Count()}");
        //    }
        //}

    }
}

