using System.Collections.Generic;
using UnityEngine;
using KaijuSolutions.Agents;
using KaijuSolutions.Agents.Movement;              // for agent.Seek(...)
using KaijuSolutions.Agents.Exercises.Cleaner;     // for Floor
using KaijuSolutions.Agents.Actuators;
using KaijuSolutions.Agents.Sensors;

namespace CleanerSolutions {
    public class FloorCleanerController : KaijuController
    {
        private enum FloorStates { Search, GoToDirty, Clean}

        [SerializeField] private KaijuAgent kaiju;
        [SerializeField] private FloorCleanerSensor visionSensor;
        [SerializeField] private FloorGroundSensor groundSensor;
        [SerializeField] private FloorCleanerActuator cleanActuator;
        [SerializeField] private float thinkInterval = 0.12f;
        [SerializeField] private float arriveDistance = 0.55f;
        [SerializeField] private float seekWeight = 1.0f;
        [SerializeField] private float wanderRadius = 6f;
        [SerializeField] private float wanderRetargetSeconds = 1.0f;
        [SerializeField] private float forgetDirtyAfterSeconds = 10f;
        [SerializeField] private float sweepStep = 2f;
        [SerializeField] private float sweepInset = 2f;

        private readonly List<Vector3> sweepPoints = new();
        private int sweepIndex = 0;

        private readonly Dictionary<Floor, float> knownDirtyLastSeen = new Dictionary<Floor, float>();

        private FloorStates state = FloorStates.Search;
        private Floor currentFloor;

        private Transform seekTarget;
        private float nextWanderPickTime;
        private Vector3 wanderCenter;

        private void Awake()
        {
            if (kaiju == null) kaiju = GetComponent<KaijuAgent>();
            wanderCenter = transform.position;

            var go = new GameObject("SeekingTarget_Clean");
            go.hideFlags = HideFlags.HideAndDontSave;
            seekTarget = go.transform;

            BuildCornerSpiralSweep();
        }

        private void BuildCornerSpiralSweep()
        {
            Floor[] floors = FindObjectsByType<Floor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            if (floors.Length == 0) return;

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

                inset += sweepInset;
            }

            sweepIndex = 0;
        }

        private void SweepSearch()
        {
            if (sweepPoints.Count == 0)
            {
                Wander();
                return;
            }

            Vector3 target = sweepPoints[sweepIndex];
            seekTarget.position = target;

            if (Vector3.Distance(transform.position, target) <= arriveDistance)
                sweepIndex = (sweepIndex + 1) % sweepPoints.Count;

            // USE YOUR EXISTING movement call here
            kaiju.Seek(seekTarget, arriveDistance, seekWeight * 0.7f, clear: true);
        }


        private void OnDestroy()
        {
            if (seekTarget != null) Destroy(seekTarget.gameObject);
        }

        private void Start()
        {
            if (kaiju == null) Debug.LogError("CleanerController: Missing KaijuAgent on Agent GameObject.");
            if (visionSensor == null) Debug.LogError("CleanerController: Assign your FloorCleanerVisionSensor in Inspector.");
            if (groundSensor == null) Debug.LogError("CleanerController: Assign your FloorGroundSensor in Inspector.");
            if (cleanActuator == null) Debug.LogError("CleanerController: Assign your CleanFloorActuator in Inspector.");

            InvokeRepeating(nameof(Think), 0f, thinkInterval);
        }

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
                    cleanActuator.TryToClean();
                    break;
            }            
        }

        private void UpdateKnownDirtyFromVision()
        {
            foreach (Floor floor in visionSensor.Observed)
            {
                if (floor == null) continue;

                if (floor.Dirty) knownDirtyLastSeen[floor] = Time.time;
                else knownDirtyLastSeen.Remove(floor);
            }

            var toRemove = new List<Floor>();
            foreach (var kv in knownDirtyLastSeen)
            {
                Floor f = kv.Key;
                if (f == null || !f.Dirty) { toRemove.Add(f); continue; }
                if (Time.time - kv.Value > forgetDirtyAfterSeconds) toRemove.Add(f);
            }
            foreach (var f in toRemove) knownDirtyLastSeen.Remove(f);
        }

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

        private void SeekTo(Vector3 position)
        {
            if (Vector3.Distance(transform.position, position) <= arriveDistance) return;

            seekTarget.position = position;

            kaiju.Seek(seekTarget, arriveDistance, seekWeight, clear: true);
        }

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