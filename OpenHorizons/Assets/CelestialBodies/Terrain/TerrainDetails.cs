using System;
using System.Collections.Generic;
using System.Threading;
using CelestialBodies.Terrain;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CelestialBodies
{
    public class TerrainDetails : MonoBehaviour
    {
        private const int UpdatePerFrames = 500;
        private Noise _noise;
        private int currentIDEvaluated;
        private int currentChunkEvaluated;
        [SerializeField] private List<Reference> _references;
        [SerializeField, HideInInspector] private List<Chunk> _chunks;
        [SerializeField] private float minAltitudeOffset;
        [SerializeField] private float maxAltitudeOffset;
        [SerializeField] private float plantsDensity = 0.5f;
        private float _minAlitude;
        private float _maxAltitude;

        private void Awake()
        {
            _chunks = new List<Chunk>();
            _noise = new Noise();
            for (var i = 0; i < _references.Count; i++)
            {
                _references[i].pool.Initialize(UpdatePerFrames);
            }
        }

        private void Update()
        {
            UpdateDetails();
        }

        void UpdateDetails()
        {
            if (_chunks.Count == 0)
                return;
            var currentPositions = _chunks[currentChunkEvaluated].GetPositions();
            if (currentPositions.Length == 0)
                return;
            var loopedCurrentIDs = false;
            for (var i = 0; i < _references.Count; i++)
            {
                if(!_references[i].pool.HasFoundAvailableIndexes())
                    return;
            }
                
            for (var i = 0; i < UpdatePerFrames; i++)
            {
                var currentID = i + currentIDEvaluated;
                if (currentPositions.Length == currentID)
                {
                    currentChunkEvaluated++;
                    currentIDEvaluated = 0;
                    loopedCurrentIDs = true;
                    if (_chunks.Count == currentChunkEvaluated)
                    {
                        currentChunkEvaluated = 0;
                    }
                    break;
                }

                EvaluateDetail(currentID,currentPositions[currentID]);
            }

            if (!loopedCurrentIDs)
            {
                currentIDEvaluated += UpdatePerFrames;
            }

            for (var i = 0; i < _references.Count; i++)
            {
                _references[i].pool.FindNewIndexes();
            }
        }

        void EvaluateDetail(int currentID, Vector3 currentPosition)
        {
            var sum = 0f;
            for (var i = 0; i < _references.Count; i++)
            {
                sum += _references[i].frequence;
            }
            if (!_chunks[currentChunkEvaluated].IsAssigned(currentID) &&
                !_chunks[currentChunkEvaluated].IsAvailable() && 
                _minAlitude < Vector3.Distance(Vector3.zero, currentPosition) &&
                !(_maxAltitude < Vector3.Distance(Vector3.zero, currentPosition)) &&
                (_noise.Evaluate(currentPosition) + 1) / 2 < plantsDensity)
            {
                var targetValue = 0f;
                for (int i = 0; i < _references.Count; i++)
                {
                    targetValue += (_references[i].frequence / sum);
                    //We offset the noise so the blank spots don't affect the noise
                    if ((_noise.Evaluate(currentPosition + new Vector3(1000,0,0))+ 1) / 2 <= targetValue || i == _references.Count -1)
                    {
                        var poolID = _references[i].pool.Instantiate(_references[i].reference, transform,
                            currentPosition, _noise.Evaluate(currentPosition), currentChunkEvaluated, currentID);
                        _chunks[currentChunkEvaluated].AssignPool(currentID, i, poolID);
                        break;
                    }
                }
            }
        }

        bool AlreadyContainsID(int id)
        {
            for (var i = 0; i < _chunks.Count; i++)
            {
                if (_chunks[i].MatchesID(id))
                    return true;
            }

            return false;
        }

        internal bool HighDefinition(Vector3[] positions, int id, MinMax minMax)
        {
            if (AlreadyContainsID(id))
                return true;
            
            _minAlitude = minMax.Min + minAltitudeOffset;
            _maxAltitude = minMax.Max - maxAltitudeOffset;
            
            var foundChunk = false;
            for (int i = 0; i < _chunks.Count; i++)
            {
                if (_chunks[i].IsAvailable())
                {
                    _chunks[i].SetPositions(positions, id);
                    foundChunk = true;
                    break;
                }
            }

            if (!foundChunk)
            {
                var newChunk = new Chunk();
                newChunk.SetPositions(positions, id);
                _chunks.Add(newChunk);
            }

            return true;
        }

        internal bool LowDefinition(int id)
        {
            for (var i = 0; i < _chunks.Count; i++)
            {
                _chunks[i].ResetIfIDMatches(id, _references);
            }

            return true;
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _references.Count; i++)
            {
                _references[i].pool.DisposeThreads();
            }
        }
    }

    [Serializable]
    class Reference
    {
        [SerializeField] internal GameObject reference;
        [SerializeField] internal Pool pool;
        [SerializeField] internal float frequence = 0.5f;
    }

    [Serializable]
    class Pool
    {
        private List<GameObject> pooledGameObjects;
        private List<PoolTarget> poolTargets;
        private int availableGameObjects;
        private int[] availablePoolIndexes;
        bool findNewIndexes;
        private bool runIndexesSearch;

        internal void Initialize(int maxUpdate)
        {
            pooledGameObjects = new List<GameObject>();
            poolTargets = new List<PoolTarget>();
            findNewIndexes = false;
            runIndexesSearch = true;
            availablePoolIndexes = new int[maxUpdate];
            for (var i = 0; i < availablePoolIndexes.Length; i++)
            {
                availablePoolIndexes[i] = -1;
            }

            Thread thread = new Thread(FindAvailableIndexes);
            thread.Start();
        }

        internal bool HasFoundAvailableIndexes()
        {
            return !findNewIndexes;
        }

        internal void FindNewIndexes()
        {
            findNewIndexes = true;
        }

        void FindAvailableIndexes()
        {
            while (runIndexesSearch)
            {
                if (findNewIndexes)
                {
                    for (var i = 0; i < availablePoolIndexes.Length; i++)
                    {
                        availablePoolIndexes[i] = -1;
                    }
                    
                    for (int j = 0; j < poolTargets.Count; j++)
                    {
                        for (var i = 0; i < availablePoolIndexes.Length; i++)
                        {
                            if (availablePoolIndexes[i] == -1)
                            {
                                if (poolTargets[j].IsAvailable())
                                {
                                    availablePoolIndexes[i] = j;
                                    break;
                                }
                            }
                        }
                    }

                    findNewIndexes = false;
                }

                Thread.Sleep(1);
            }
            Debug.Log("Finished thread");
        }

        internal void DisposeThreads()
        {
            runIndexesSearch = false;
        }

        internal int Instantiate(GameObject reference, Transform parent, Vector3 position, float rotation, int chunkID, int id)
        {
            if (pooledGameObjects == null)
            {
                pooledGameObjects = new List<GameObject>();
                poolTargets = new List<PoolTarget>();
            }

            var targetId = -1;
            if (availableGameObjects > 0)
            {
                for (int i = 0; i < availablePoolIndexes.Length; i++)
                {
                    if (availablePoolIndexes[i] != -1)
                    {
                        targetId = availablePoolIndexes[i];
                        availablePoolIndexes[i] = -1;
                        break;
                    }
                }
            }

            GameObject newGameObject;
            if (targetId < 0)
            {
                newGameObject = GameObject.Instantiate(reference, position, Quaternion.identity);
                PlaceGameObject(newGameObject);
                pooledGameObjects.Add(newGameObject);
                poolTargets.Add(new PoolTarget(chunkID, id));
                return poolTargets.Count - 1;
            }

            availableGameObjects--;
            newGameObject = pooledGameObjects[targetId];
            poolTargets[targetId] = new PoolTarget(chunkID, id);
            PlaceGameObject(newGameObject);
            return targetId;

            void PlaceGameObject(GameObject gameObject)
            {
                gameObject.transform.parent = parent;
                gameObject.transform.localPosition = position;
                gameObject.transform.LookAt(parent.position, Vector3.back);
                gameObject.transform.Rotate(Vector3.up,  -90, Space.Self);
                gameObject.transform.Rotate(Vector3.right, rotation * 360, Space.Self);
            }
        }

        internal bool IsInitialized()
        {
            if (poolTargets == null || poolTargets.Count == 0)
                return false;

            return true;
        }

        internal void Destroy(int poolID)
        {
            if (poolTargets == null)
            {
                Debug.LogWarning("Pool target was null. id requested: " + poolID);
                return;
            }

            availableGameObjects++;
            poolTargets[poolID] = new PoolTarget(-1, -1);
            pooledGameObjects[poolID].transform.localPosition = Vector3.zero;
        }
    }

    [Serializable]
    struct PoolTarget
    {
        [SerializeField] private int chunkID;
        [SerializeField] private int id;

        internal PoolTarget(int chunkID, int id)
        {
            this.chunkID = chunkID;
            this.id = id;
        }

        public bool IsAvailable()
        {
            if (chunkID == -1 && id == -1)
            {
                return true;
            }

            if (chunkID == -1 || id == -1)
            {
                Debug.LogWarning("Mismatching");
            }

            return false;
        }
    }

    [Serializable]
    class Chunk
    {
        [SerializeField] private int _chunkID;
        private Vector3[] _positions;
        private int[] _referenceID;
        private int[] _pooledReferenceID;

        internal Vector3[] GetPositions()
        {
            return _positions;
        }

        internal bool IsAssigned(int id)
        {
            if (_pooledReferenceID[id] >= 0)
            {
                return true;
            }

            return false;
        }

        internal bool MatchesID(int id)
        {
            if (_chunkID == id)
                return true;

            return false;
        }

        internal void SetPositions(Vector3[] positions, int chunkID)
        {
            if (_positions == null || _positions.Length != positions.Length)
            {
                _positions = new Vector3[positions.Length];
                _referenceID = new int[positions.Length];
                _pooledReferenceID = new int[positions.Length];
            }

            ResetChunck(positions);
            _chunkID = chunkID;
        }

        void ResetChunck(Vector3[] positions)
        {
            for (var i = 0; i < _positions.Length; i++)
            {
                _positions[i] = positions[i];
                _referenceID[i] = -1;
                _pooledReferenceID[i] = -1;
            }
        }

        internal void AssignPool(int id, int referenceID, int poolID)
        {
            _referenceID[id] = referenceID;
            _pooledReferenceID[id] = poolID;
        }

        internal void ReleasePool(int id)
        {
            _referenceID[id] = -1;
            _pooledReferenceID[id] = -1;
        }

        internal bool IsAvailable()
        {
            if (_chunkID == -1)
            {
                return true;
            }

            return false;
        }

        internal bool ResetIfIDMatches(int id, List<Reference> references)
        {
            if (_chunkID == id)
            {
                for (var i = 0; i < _pooledReferenceID.Length; i++)
                {
                    if (_pooledReferenceID[i] != -1)
                    {
                        references[_referenceID[i]].pool.Destroy(_pooledReferenceID[i]);
                    }
                }

                _chunkID = -1;
                ResetChunck(_positions);
            }

            return false;
        }
    }
}