using System;
using System.Collections.Generic;
using System.Threading;
using CelestialBodies.Terrain;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CelestialBodies
{
    static class DetailBuilder
    {
        internal const int UpdatePerFrames = 1000;
        internal static void Initialize(this ref TerrainDetails terrainDetails)
        {
            terrainDetails.chunks = new List<Chunk>();
            terrainDetails.Noise = new Noise();
            for (var i = 0; i < terrainDetails.references.Count; i++)
            {
                terrainDetails.references[i].pool.Initialize(UpdatePerFrames);
            }
        }

        
        internal static void UpdateDetails(this ref TerrainDetails terrainDetails, Transform transform)
        {
            if (terrainDetails.chunks.Count == 0)
                return;
            var currentPositions = terrainDetails.chunks[terrainDetails.CurrentChunkEvaluated].GetPositions();
            var currentColor = terrainDetails.chunks[terrainDetails.CurrentChunkEvaluated].GetVertexColor();
            if (currentPositions.Length == 0)
                return;
            var loopedCurrentIDs = false;
            for (var i = 0; i < terrainDetails.references.Count; i++)
            {
                if(!terrainDetails.references[i].pool.HasFoundAvailableIndexes())
                    return;
            }

            var currentID = terrainDetails.CurrentIDEvaluated;
            for (var i = 0; i < UpdatePerFrames; i++)
            {
                currentID ++;
                var foundDetail = false;
                var stopLooping = false;
                var iterations = 0;
                while (foundDetail == false)
                {
                    if (currentPositions.Length <= currentID)
                    {
                        terrainDetails.CurrentChunkEvaluated++;
                        terrainDetails.CurrentIDEvaluated = 0;
                        loopedCurrentIDs = true;
                        if (terrainDetails.chunks.Count == terrainDetails.CurrentChunkEvaluated)
                        {
                            terrainDetails.CurrentChunkEvaluated = 0;
                        }

                        stopLooping = true;
                        break;
                    }
    
                    foundDetail = terrainDetails.EvaluateDetail(currentID,currentPositions[currentID], currentColor[currentID], transform);
                    if (!foundDetail)
                    {
                        currentID++;
                    }

                    iterations++;
                    if(iterations > 10)
                        break;
                }
                
                if(stopLooping)
                    break;
            }

            if (!loopedCurrentIDs)
            {
                terrainDetails.CurrentIDEvaluated = currentID;
            }

            for (var i = 0; i < terrainDetails.references.Count; i++)
            {
                terrainDetails.references[i].pool.FindNewIndexes();
            }
        }
        
        

        internal static bool EvaluateDetail(this ref TerrainDetails terrainDetails, int currentID, Vector3 currentPosition, Color vertexColor, Transform transform)
        {
            var sum = 0f;
            var foundDetail = false;
            for (var i = 0; i < terrainDetails.references.Count; i++)
            {
                sum += terrainDetails.references[i].frequence;
            }
            if (!terrainDetails.chunks[terrainDetails.CurrentChunkEvaluated].IsAssigned(currentID) &&
                !terrainDetails.chunks[terrainDetails.CurrentChunkEvaluated].IsAvailable() && 
                terrainDetails.MinAlitude < Vector3.Distance(Vector3.zero, currentPosition) &&
                !(terrainDetails.MaxAltitude < Vector3.Distance(Vector3.zero, currentPosition)) &&
                (terrainDetails.Noise.Evaluate(currentPosition) + 1) / 2 < terrainDetails.plantsDensity &&
                vertexColor.r < terrainDetails.StepThreshold)
            {
                var targetValue = 0f;
                for (int i = 0; i < terrainDetails.references.Count; i++)
                {
                    targetValue += (terrainDetails.references[i].frequence / sum);
                    //We offset the noise so the blank spots don't affect the noise
                    if ((terrainDetails.Noise.Evaluate(currentPosition + new Vector3(1000,0,0))+ 1) / 2 <= targetValue|| i == terrainDetails.references.Count -1)
                    {
                        var poolID = terrainDetails.references[i].pool.Instantiate(terrainDetails.references[i].reference, transform,
                            currentPosition, terrainDetails.Noise.Evaluate(currentPosition), terrainDetails.CurrentChunkEvaluated, currentID);
                        terrainDetails.chunks[terrainDetails.CurrentChunkEvaluated].AssignPool(currentID, i, poolID);
                        foundDetail = true;
                        break;
                    }
                }
            }

            return foundDetail;
        }

        static bool AlreadyContainsID(this ref TerrainDetails terrainDetails, int id)
        {
            for (var i = 0; i < terrainDetails.chunks.Count; i++)
            {
                if (terrainDetails.chunks[i].MatchesID(id))
                    return true;
            }

            return false;
        }

        internal static bool HighDefinition(this ref TerrainDetails terrainDetails, Vector3[] positions, Color [] vertexColors, int id, float stepThreshold, MinMax minMax)
        {
            if (terrainDetails.AlreadyContainsID(id))
                return true;
            
            terrainDetails.MinAlitude = minMax.Min + terrainDetails.minAltitudeOffset;
            terrainDetails.MaxAltitude = minMax.Max - terrainDetails.maxAltitudeOffset;
            terrainDetails.StepThreshold = stepThreshold;
            var foundChunk = false;
            for (int i = 0; i < terrainDetails.chunks.Count; i++)
            {
                if (terrainDetails.chunks[i].IsAvailable())
                {
                    terrainDetails.chunks[i].SetPositions(positions, vertexColors, id);
                    foundChunk = true;
                    break;
                }
            }

            if (!foundChunk)
            {

                var newChunk = new Chunk();
                newChunk.SetPositions(positions, vertexColors, id);
                terrainDetails.chunks.Add(newChunk);
            }

            return true;
        }

        internal static bool LowDefinition(this ref TerrainDetails terrainDetails, int id)
        {
            for (var i = 0; i < terrainDetails.chunks.Count; i++)
            {
                terrainDetails.chunks[i].ResetIfIDMatches(id, terrainDetails.references);
            }

            return true;
        }

        internal static void CleanUp(this ref TerrainDetails terrainDetails)
        {
            for (var i = 0; i < terrainDetails.references.Count; i++)
            {
                terrainDetails.references[i].pool.DisposeThreads();
            }
        }
    }
    
    [Serializable]
    struct TerrainDetails
    {
        [SerializeField] internal bool isEnabled;
        internal Noise Noise;
        internal int CurrentIDEvaluated;
        internal int CurrentChunkEvaluated;
        [SerializeField] internal List<Reference> references;
        [SerializeField, HideInInspector] internal List<Chunk> chunks;
        [SerializeField] internal float minAltitudeOffset;
        [SerializeField] internal float maxAltitudeOffset;
        [SerializeField] internal float plantsDensity;
        internal float MinAlitude;
        internal float MaxAltitude;
        internal float StepThreshold;

        public static TerrainDetails Default()
        {
            var newTerrainDetail = new TerrainDetails();
            newTerrainDetail.plantsDensity = 0.5f;
            newTerrainDetail.isEnabled = false;
            return newTerrainDetail;
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
        private List<GameObject> _pooledGameObjects;
        private List<PoolTarget> _poolTargets;
        private int _availableGameObjects;
        private int[] _availablePoolIndexes;
        bool _findNewIndexes;
        private bool _runIndexesSearch;

        internal void Initialize(int maxUpdate)
        {
            _pooledGameObjects = new List<GameObject>();
            _poolTargets = new List<PoolTarget>();
            _findNewIndexes = false;
            _runIndexesSearch = true;
            _availablePoolIndexes = new int[maxUpdate];
            for (var i = 0; i < _availablePoolIndexes.Length; i++)
            {
                _availablePoolIndexes[i] = -1;
            }

            Thread thread = new Thread(FindAvailableIndexes);
            thread.Start();
        }

        internal bool HasFoundAvailableIndexes()
        {
            return !_findNewIndexes;
        }

        internal void FindNewIndexes()
        {
            _findNewIndexes = true;
        }

        void FindAvailableIndexes()
        {
            while (_runIndexesSearch)
            {
                if (_findNewIndexes)
                {
                    for (var i = 0; i < _availablePoolIndexes.Length; i++)
                    {
                        _availablePoolIndexes[i] = -1;
                    }
                    
                    for (int j = 0; j < _poolTargets.Count; j++)
                    {
                        for (var i = 0; i < _availablePoolIndexes.Length; i++)
                        {
                            if (_availablePoolIndexes[i] == -1)
                            {
                                if (_poolTargets[j].IsAvailable())
                                {
                                    _availablePoolIndexes[i] = j;
                                    break;
                                }
                            }
                        }
                    }

                    _findNewIndexes = false;
                }

                Thread.Sleep(1);
            }
            Debug.Log("Finished thread");
        }

        internal void DisposeThreads()
        {
            _runIndexesSearch = false;
        }

        internal int Instantiate(GameObject reference, Transform parent, Vector3 position, float rotation, int chunkID, int id)
        {
            if (_pooledGameObjects == null)
            {
                _pooledGameObjects = new List<GameObject>();
                _poolTargets = new List<PoolTarget>();
            }

            var targetId = -1;
            if (_availableGameObjects > 0)
            {
                for (int i = 0; i < _availablePoolIndexes.Length; i++)
                {
                    if (_availablePoolIndexes[i] != -1)
                    {
                        targetId = _availablePoolIndexes[i];
                        _availablePoolIndexes[i] = -1;
                        break;
                    }
                }
            }

            GameObject newGameObject;
            if (targetId < 0)
            {
                newGameObject = GameObject.Instantiate(reference, position, Quaternion.identity);
                PlaceGameObject(newGameObject);
                _pooledGameObjects.Add(newGameObject);
                _poolTargets.Add(new PoolTarget(chunkID, id));
                return _poolTargets.Count - 1;
            }

            _availableGameObjects--;
            newGameObject = _pooledGameObjects[targetId];
            _poolTargets[targetId] = new PoolTarget(chunkID, id);
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
            if (_poolTargets == null || _poolTargets.Count == 0)
                return false;

            return true;
        }

        internal void Destroy(int poolID)
        {
            if (_poolTargets == null)
            {
                Debug.LogWarning("Pool target was null. id requested: " + poolID);
                return;
            }

            _availableGameObjects++;
            _poolTargets[poolID] = new PoolTarget(-1, -1);
            _pooledGameObjects[poolID].transform.localPosition = Vector3.zero;
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
        [SerializeField] private int chunkID;
        private Vector3[] _positions;
        private Color[] _colors;
        private int[] _referenceID;
        private int[] _pooledReferenceID;

        internal Vector3[] GetPositions()
        {
            return _positions;
        }

        internal Color[] GetVertexColor()
        {
            return _colors;
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
            if (chunkID == id)
                return true;

            return false;
        }

        internal void SetPositions(Vector3[] positions, Color [] colors, int chunkID)
        {
            if (_positions == null || _positions.Length != positions.Length)
            {
                _positions = new Vector3[positions.Length];
                _referenceID = new int[positions.Length];
                _pooledReferenceID = new int[positions.Length];
                _colors = new Color[positions.Length];
            }

            ResetChunck(positions, colors);
            this.chunkID = chunkID;
        }

        void ResetChunck(Vector3[] positions, Color[] colors)
        {
            for (var i = 0; i < _positions.Length; i++)
            {
                _positions[i] = positions[i];
                _referenceID[i] = -1;
                _pooledReferenceID[i] = -1;
                _colors[i] = colors[i];
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
            if (chunkID == -1)
            {
                return true;
            }

            return false;
        }

        internal bool ResetIfIDMatches(int id, List<Reference> references)
        {
            if (chunkID == id)
            {
                for (var i = 0; i < _pooledReferenceID.Length; i++)
                {
                    if (_pooledReferenceID[i] != -1)
                    {
                        references[_referenceID[i]].pool.Destroy(_pooledReferenceID[i]);
                    }
                }

                chunkID = -1;
                ResetChunck(_positions, _colors);
            }

            return false;
        }
    }
}