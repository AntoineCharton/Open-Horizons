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
        internal const int UpdatePerFrames = 750;
        internal static void Initialize(this ref Trees trees)
        {
            trees.chunks = new List<Chunk>();
            trees.Noise = new Noise();
            for (var i = 0; i < trees.references.Count; i++)
            {
                trees.references[i].pool.Initialize(UpdatePerFrames);
            }
        }

        internal static void GenerateInteractable(this ref Trees trees, Vector3 targetPosition, Transform parent)
        {
            if (trees.chunks.Count == 0)
                return;
            var closestPositionID = -1;
            var secondClosestPositionID = -1;
            var referenceID = -1;
            var closestPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var secondClosestPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var secondReferenceID = -1;
            for (var i = 0; i < trees.chunks.Count; i++)
            {
                var referencesIDs = trees.chunks[i].GetReferenceIDs();
                var positionsIDs = trees.chunks[i].GetPositions();


                for (var j = 0; j < referencesIDs.Length; j++)
                {
                    if (referencesIDs[j] > -1)
                    {
                        var positionToCheck = positionsIDs[j];
                        if (Vector3.Distance(positionToCheck, targetPosition) < Vector3.Distance(secondClosestPosition, targetPosition))
                        {
                            if (Vector3.Distance(positionToCheck, targetPosition) <
                                Vector3.Distance(closestPosition, targetPosition)) // If closer than the first closest we swap
                            {
                                secondClosestPositionID = closestPositionID;
                                secondClosestPosition = closestPosition;
                                secondReferenceID = referenceID;
                                closestPositionID = j;
                                closestPosition = positionsIDs[j];
                                referenceID = referencesIDs[j];
                            }
                            else // Otherwise we just assign
                            {
                                secondClosestPositionID = j;
                                secondClosestPosition = positionsIDs[j];
                                secondReferenceID = referencesIDs[j];
                            }
                        }
                    }
                }
            }
            
            if (closestPositionID != -1 && referenceID != -1)
            {
                var interactableReference = trees.references[referenceID].pool.GetFirstInteractable();
                if (interactableReference != null)
                {
                    PlaceGameObject(interactableReference, parent, closestPosition,
                        trees.Noise.Evaluate(closestPosition));
                }
            }
            
            if (secondClosestPositionID != -1 && secondReferenceID != -1)
            {
                var interactableReference = trees.references[secondReferenceID].pool.GetSecondInteractable();
                if (interactableReference != null)
                {
                    PlaceGameObject(interactableReference, parent, secondClosestPosition,
                        trees.Noise.Evaluate(secondClosestPosition));
                }
            }
        }
        
        internal static void PlaceGameObject(GameObject gameObject, Transform parent, Vector3 position, float rotation)
        {
            gameObject.transform.parent = parent;
            gameObject.transform.localPosition = position;
            gameObject.transform.LookAt(parent.position, Vector3.back);
            gameObject.transform.Rotate(Vector3.up,  -90, Space.Self);
            gameObject.transform.Rotate(Vector3.right, rotation * 360, Space.Self);
        }
        
        internal static void UpdateDetails(this ref Trees trees, Transform transform)
        {
            if (trees.chunks.Count == 0)
                return;
            
            var currentPositions = trees.chunks[trees.CurrentChunkEvaluated].GetPositions();
            var currentColor = trees.chunks[trees.CurrentChunkEvaluated].GetVertexColor();
            if (currentPositions.Length == 0)
                return;
            var loopedCurrentIDs = false;
            for (var i = 0; i < trees.references.Count; i++)
            {
                if(!trees.references[i].pool.HasFoundAvailableIndexes())
                    return;
            }

            var currentID = trees.CurrentIDEvaluated;
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
                        trees.CurrentChunkEvaluated++;
                        trees.CurrentIDEvaluated = 0;
                        loopedCurrentIDs = true;
                        if (trees.chunks.Count == trees.CurrentChunkEvaluated)
                        {
                            trees.CurrentChunkEvaluated = 0;
                        }

                        stopLooping = true;
                        break;
                    }
    
                    foundDetail = trees.EvaluateDetail(currentID,currentPositions[currentID], currentColor[currentID], transform);
                    if (!foundDetail)
                    {
                        currentID++;
                    }

                    iterations++;
                    if(iterations > 20)
                        break;
                }
                
                if(stopLooping)
                    break;
            }

            if (!loopedCurrentIDs)
            {
                trees.CurrentIDEvaluated = currentID;
            }

            for (var i = 0; i < trees.references.Count; i++)
            {
                trees.references[i].pool.FindNewIndexes();
            }
        }
        
        

        internal static bool EvaluateDetail(this ref Trees trees, int currentID, Vector3 currentPosition, Color vertexColor, Transform transform)
        {
            var sum = 0f;
            var foundDetail = false;
            for (var i = 0; i < trees.references.Count; i++)
            {
                sum += trees.references[i].frequence;
            }
            if (!trees.chunks[trees.CurrentChunkEvaluated].IsAssigned(currentID) &&
                !trees.chunks[trees.CurrentChunkEvaluated].IsAvailable() && 
                (trees.Noise.Evaluate(currentPosition) + 1) / 2 < trees.plantsDensity &&
                vertexColor.r < trees.StepThreshold)
            {
                var targetValue = 0f;
                for (int i = 0; i < trees.references.Count; i++)
                {
                    targetValue += (trees.references[i].frequence / sum);
                    //We offset the noise so the blank spots don't affect the noise
                    if ((trees.Noise.Evaluate(currentPosition + new Vector3(1000,0,0))+ 1) / 2 <= targetValue || 
                        i == trees.references.Count -1)
                    {
                        if (trees.references[i].MinAlitude < Vector3.Distance(Vector3.zero, currentPosition) &&
                            !(trees.references[i].MaxAltitude < Vector3.Distance(Vector3.zero, currentPosition)))
                        {
                            var poolID = trees.references[i].pool.Instantiate(trees.references[i].reference,
                                trees.references[i].interactableReference, transform,
                                currentPosition, trees.Noise.Evaluate(currentPosition), trees.CurrentChunkEvaluated,
                                currentID);
                            trees.chunks[trees.CurrentChunkEvaluated].AssignPool(currentID, i, poolID);
                        }

                        foundDetail = true;
                        break;
                    }
                }
            }

            return foundDetail;
        }

        static bool AlreadyContainsID(this ref Trees trees, int id)
        {
            for (var i = 0; i < trees.chunks.Count; i++)
            {
                if (trees.chunks[i].MatchesID(id))
                    return true;
            }

            return false;
        }

        internal static bool HighDefinition(this ref Trees trees, Vector3[] positions, Color [] vertexColors, int id, float stepThreshold, MinMax minMax)
        {
            if (trees.AlreadyContainsID(id))
                return true;

            for (var i = 0; i < trees.references.Count; i++)
            {
                trees.references[i].MinAlitude = minMax.Min + trees.references[i].minAltitudeOffset;
                trees.references[i].MaxAltitude = minMax.Max - trees.references[i].maxAltitudeOffset;
            }
            trees.StepThreshold = stepThreshold;
            var foundChunk = false;
            for (int i = 0; i < trees.chunks.Count; i++)
            {
                if (trees.chunks[i].IsAvailable())
                {
                    trees.chunks[i].SetPositions(positions, vertexColors, id);
                    foundChunk = true;
                    break;
                }
            }

            if (!foundChunk)
            {

                var newChunk = new Chunk();
                newChunk.SetPositions(positions, vertexColors, id);
                trees.chunks.Add(newChunk);
            }

            return true;
        }

        internal static bool LowDefinition(this ref Trees trees, int id)
        {
            for (var i = 0; i < trees.chunks.Count; i++)
            {
                trees.chunks[i].ResetIfIDMatches(id, trees.references);
            }

            return true;
        }

        internal static void CleanUp(this ref Trees trees)
        {
            for (var i = 0; i < trees.references.Count; i++)
            {
                trees.references[i].pool.DisposeThreads();
            }
        }
    }
    
    [Serializable]
    struct Trees
    {
        [SerializeField] internal bool isEnabled;
        internal Noise Noise;
        internal int CurrentIDEvaluated;
        internal int CurrentChunkEvaluated;
        [SerializeField] internal List<Reference> references;
        [SerializeField, HideInInspector] internal List<Chunk> chunks;
        [SerializeField] internal float plantsDensity;
        //internal float MinAlitude;
        //internal float MaxAltitude;
        internal float StepThreshold;

        public static Trees Default()
        {
            var newTerrainDetail = new Trees();
            newTerrainDetail.plantsDensity = 0.5f;
            newTerrainDetail.isEnabled = false;
            return newTerrainDetail;
        }
    }

    [Serializable]
    class Reference
    {
        [SerializeField] internal GameObject reference;
        [SerializeField] internal GameObject interactableReference;
        [SerializeField] internal Pool pool;
        [SerializeField] internal float frequence = 0.5f;
        [SerializeField] internal float minAltitudeOffset;
        [SerializeField] internal float maxAltitudeOffset;
        [SerializeField] internal float MinAlitude;
        [SerializeField] internal float MaxAltitude;
    }

    [Serializable]
    class Pool
    {
        private List<GameObject> _pooledGameObjects;
        private List<PoolTarget> _poolTargets;
        private int _availableGameObjects;
        private GameObject _interactableReference;
        private GameObject _secondInteractableReference;
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

        internal GameObject GetFirstInteractable()
        {
            return _interactableReference;
        }
        
        internal GameObject GetSecondInteractable()
        {
            return _secondInteractableReference;
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

        internal int Instantiate(GameObject reference, GameObject interactableReference, Transform parent, Vector3 position, float rotation, int chunkID, int id)
        {
            if (_pooledGameObjects == null)
            {
                _pooledGameObjects = new List<GameObject>();
                _poolTargets = new List<PoolTarget>();
            }
            
            if (interactableReference != null && _interactableReference == null)
            {
                _interactableReference = GameObject.Instantiate(interactableReference, parent, true);
                _secondInteractableReference = GameObject.Instantiate(interactableReference, parent, true);
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
                DetailBuilder.PlaceGameObject(newGameObject, parent, position, rotation);
                _pooledGameObjects.Add(newGameObject);
                _poolTargets.Add(new PoolTarget(chunkID, id));
                return _poolTargets.Count - 1;
            }

            _availableGameObjects--;
            newGameObject = _pooledGameObjects[targetId];
            _poolTargets[targetId] = new PoolTarget(chunkID, id);
            DetailBuilder.PlaceGameObject(newGameObject, parent, position, rotation);
            return targetId;
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
        private float[] _rotations;
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

        internal int[] GetReferenceIDs()
        {
            return _referenceID;
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