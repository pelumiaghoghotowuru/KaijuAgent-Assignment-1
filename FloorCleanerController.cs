using System.Collections.Generic;
using UnityEngine;
using KaijuSolutions.Agents;
using KaijuSolutions.Agents.Movement;              // for kaiju.Seek(...)
using KaijuSolutions.Agents.Exercises.Cleaner;     // for Floor
using KaijuSolutions.Agents.Actuators;
using KaijuSolutions.Agents.Sensors;

namespace CleanerSolutions {
    /// <summary>
    /// Controller (brain) of the Kaiju Agent
    /// 
    /// It reads the vision and ground sensors, maintaining a short memory of which tiles are dirty
    /// and what is the nearest dirty tile. Once a dirty tile is found, the CleanerActuator is triggered. If no dirty tiles are found,
    /// there is a circular pattern initiated for the Kaiju to look from the outer to the inner corners for dirty tiles.
    /// 
    /// There is no "all-kmowing" behaviour with either of the Search patterns but rather as the Kaiju wanders and finds, it keeps the momory and perform the clean operation.
    /// </summary>
    public class FloorCleanerController : KaijuController
    {
        /// <summary>
        /// behavioural states of the Kaiju
        /// Search -> search for tiles
        /// GoToDirty -> Find target tile and move towards it
        /// Clean: clean the tile you are standing on.
        /// </summary>
        private enum FloorStates { Search, GoToDirty, Clean}

        [SerializeField] private KaijuAgent kaiju;
        [SerializeField] private FloorCleanerSensor visionSensor;
        [SerializeField] private FloorGroundSensor groundSensor;
        [SerializeField] private FloorCleanerActuator cleanActuator;
        [SerializeField] private float thinkInterval = 0.12f; //how often we update our decisions
        [SerializeField] private float arriveDistance = 0.55f; //within the distance, we say we have arrived
        [SerializeField] private float seekWeight = 1.0f;
        [SerializeField] private float wanderRadius = 6f; //random wander settings if systemic pattern fails
        [SerializeField] private float wanderRetargetSeconds = 1.0f; //random wander settings if systemic pattern fails
        [SerializeField] private float forgetDirtyAfterSeconds = 10f; //dirtyTile remembered only when seen, shortMemory to avoid past information
        [SerializeField] private float sweepStep = 2f; //match tile spacing to cover floor efficiently
        [SerializeField] private float sweepInset = 2f; //ring moves inward from boundary


        //search route
        private readonly List<Vector3> sweepPoints = new();
        private int sweepIndex = 0;

        /// <summary>
        /// Memory: dirty tiles the agent has actually seen, along with the time last observed.
        /// Key: Floor tile, Value: Time.time when last confirmed dirty via vision.
        /// </summary>
        private readonly Dictionary<Floor, float> knownDirtyLastSeen = new Dictionary<Floor, float>();

        private FloorStates state = FloorStates.Search;

        //current Floor target to move toward
        private Floor currentFloor;

        /// <summary>
        /// A hidden transform used as the target for Kaiju's Seek movement.
        /// Kaiju Seek expects a Transform, so we move this object around instead of the floor itself.
        /// </summary>
        private Transform seekTarget;
        private float nextWanderPickTime;
        private Vector3 wanderCenter;

        private void Awake()
        {
            //auto fetch if the inspector is not set
            if (kaiju == null) kaiju = GetComponent<KaijuAgent>();
            wanderCenter = transform.position;

            // Create the hidden target object once and reuse it (avoids allocations per tick).
            var go = new GameObject("SeekingTarget_Clean");
            go.hideFlags = HideFlags.HideAndDontSave;
            seekTarget = go.transform;

            // Build our sweep route once using floor layout (NOT dirt state).
            BuildCornerSpiralSweep();
        }

        /// <summary>
        /// Builds a deterministic "inspection route":
        /// - corners first (so the agent checks edges like a human would)
        /// - then walks the perimeter
        /// - then repeats with an inset rectangle, moving inward ring by ring
        /// 
        /// This avoids random wandering and increases coverage of the entire floor.
        /// </summary>
        private void BuildCornerSpiralSweep()
        {
            //we know the floor layout, just not the state of the tiles re: Dirty or Clean tiles
            Floor[] floors = FindObjectsByType<Floor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            if (floors.Length == 0) return;

            // Compute bounds of the tile grid in X/Z.
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var f in floors)
            {
                Vector3 p = f.transform.position;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }

            sweepPoints.Clear();
            float inset = 0f;
            float y = transform.position.y;

            // Generate rings until the inset rectangle collapses.
            while (minX + inset <= maxX - inset && minZ + inset <= maxZ - inset)
            {
                float left = minX + inset;
                float right = maxX - inset;
                float bottom = minZ + inset;
                float top = maxZ - inset;

                // corners first
                sweepPoints.Add(new Vector3(left, y, bottom));
                sweepPoints.Add(new Vector3(right, y, bottom));
                sweepPoints.Add(new Vector3(right, y, top));
                sweepPoints.Add(new Vector3(left, y, top));

                // perimeter
                for (float x = left; x <= right; x += sweepStep)
                    sweepPoints.Add(new Vector3(x, y, bottom));

                for (float z = bottom; z <= top; z += sweepStep)
                    sweepPoints.Add(new Vector3(right, y, z));

                for (float x = right; x >= left; x -= sweepStep)
                    sweepPoints.Add(new Vector3(x, y, top));

                for (float z = top; z >= bottom; z -= sweepStep)
                    sweepPoints.Add(new Vector3(left, y, z));

                //move towards next inward ring
                inset += sweepInset;
            }

            sweepIndex = 0;
        }

        /// <summary>
        /// Search behavior when no dirty tiles are currently known:
        /// follow the corner->inward sweep route.
        /// 
        /// This ensures the agent eventually visits all areas and naturally discovers dirt
        /// through its vision sensor (human-like exploration).
        /// </summary>
        private void SweepSearch()
        {
            // Fallback in case sweep route could not be built
            if (sweepPoints.Count == 0)
            {
                Wander();
                return;
            }

            // Advance the route once we reach the current sweep point
            Vector3 target = sweepPoints[sweepIndex];
            seekTarget.position = target;

            if (Vector3.Distance(transform.position, target) <= arriveDistance)
                sweepIndex = (sweepIndex + 1) % sweepPoints.Count;

            kaiju.Seek(seekTarget, arriveDistance, seekWeight * 0.7f, clear: true);
        }

        //cleanup hidden target object
        private void OnDestroy()
        {
            if (seekTarget != null) Destroy(seekTarget.gameObject);
        }

        //decision making periodically
        private void Start()
        {
            if (kaiju == null) Debug.LogError("CleanerController: Missing KaijuAgent on Agent GameObject.");
            if (visionSensor == null) Debug.LogError("CleanerController: Assign your FloorCleanerVisionSensor in Inspector.");
            if (groundSensor == null) Debug.LogError("CleanerController: Assign your FloorGroundSensor in Inspector.");
            if (cleanActuator == null) Debug.LogError("CleanerController: Assign your CleanFloorActuator in Inspector.");

            InvokeRepeating(nameof(Think), 0f, thinkInterval);
        }

        /// <summary>
        /// Our decision cysle is based on sensing the ground, updating the memory from vision
        /// if (we are on dirty) clean tiles;
        /// else pick the nearest dirty and seek to it
        /// else call sweep search route
        /// </summary>
        private void Think()
        {
            if (kaiju == null || visionSensor == null || groundSensor == null ||  cleanActuator == null) { return; }

            if (cleanActuator.IsCleaning) return;

            groundSensor.Sense();

            UpdateKnownDirtyFromVision();

            if (groundSensor.IsCurrentDirty)
            {
                state = FloorStates.Clean;
                Act();
                return;
            }

            if (currentFloor != null && !currentFloor.Dirty)
            {
                knownDirtyLastSeen.Remove(currentFloor);
                currentFloor = null;
            }

            currentFloor = NearestDirty();

            state = (currentFloor != null) ? FloorStates.GoToDirty : FloorStates.Search;
            Act();
        }

        private void Act()
        {
            switch (state)
            {
                case FloorStates.GoToDirty:
                    if (currentFloor != null)
                    {
                        SeekTo(currentFloor.transform.position);
                    }
                    else
                        SweepSearch();
                    break;
                case FloorStates.Search:
                    SweepSearch();
                    break;
                case FloorStates.Clean:
                    //triggers the actuator to clean tiles
                    cleanActuator.TryToClean();
                    break;
            }            
        }

        //update memory of the dirty tiles based on agent's current observed vision. prevents stale memory
        private void UpdateKnownDirtyFromVision()
        {
            //update vision memory
            foreach (Floor floor in visionSensor.Observed)
            {
                if (floor == null) continue;

                if (floor.Dirty) knownDirtyLastSeen[floor] = Time.time;
                else knownDirtyLastSeen.Remove(floor);
            }

            //remove stale entries
            var toRemove = new List<Floor>();
            foreach (var kv in knownDirtyLastSeen)
            {
                Floor f = kv.Key;
                if (f == null || !f.Dirty) { toRemove.Add(f); continue; }
                if (Time.time - kv.Value > forgetDirtyAfterSeconds) toRemove.Add(f);
            }
            foreach (var f in toRemove) knownDirtyLastSeen.Remove(f);
        }
        
        //selects closest remembered dirty tile from the candidates stored in memory
        public Floor NearestDirty()
        {
            Floor best = null;
            float bestSqr = float.MaxValue;
            Vector3 vector3 = transform.position;

            foreach (var kv in knownDirtyLastSeen)
            {
                Floor tile = kv.Key;
                if (tile == null || !tile.Dirty) continue;

                float d = (tile.transform.position - vector3).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = tile;
                }
            }
            return best;
        }

        //Kaiju Seek Movement to move toward world position
        private void SeekTo(Vector3 position)
        {
            if (Vector3.Distance(transform.position, position) <= arriveDistance) return;

            seekTarget.position = position;

            kaiju.Seek(seekTarget, arriveDistance, seekWeight, clear: true);
        }

        //if SweepSearch fails, we fallback on just wondering around randomly
        private void Wander()
        {
            if (Time.time >= nextWanderPickTime)
            {
                nextWanderPickTime = Time.time + wanderRetargetSeconds;

                Vector2 r = Random.insideUnitCircle * wanderRadius;
                seekTarget.position = new Vector3(
                    wanderCenter.x + r.x,
                    transform.position.y,
                    wanderCenter.z + r.y);
            }
            kaiju.Seek(seekTarget, arriveDistance, seekWeight * 0.6f, clear: true);
        }
    }
}