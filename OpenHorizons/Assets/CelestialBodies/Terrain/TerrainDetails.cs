using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CelestialBodies.Terrain;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace CelestialBodies
{
    public class TerrainDetails : MonoBehaviour
    {
        [SerializeField] private List<GameObject> references;
        private List<int> _currentDetailsIDs;
        [SerializeField] private List<DetailsGroup> _detailsGroups;
        [SerializeField] private List<PooledDetail>[] _pooledDetails;
        [SerializeField] private float minAltitudeOffset;
        [SerializeField] private float maxAltitudeOffset;
        private float _minAlitude;
        private float _maxAltitude;
        private int _currentPoolDetailCount;
        private int _currentGroupCount;
        private const int UpdatePerFrames = 100;
        private Noise _noise;
        public bool canUpdate;
        private ThreadDetailsBuilder _threadDetailsBuilder;
        private ThreadDetailSorter _threadDetailSorter;
        private bool isAvailableReady;
        private bool lookForAvailablePool;

        private void Awake()
        {
            isAvailableReady = false;
            lookForAvailablePool = true;
            _threadDetailSorter = new ThreadDetailSorter(UpdatePerFrames, references.Count);
            _noise = new Noise();
            _threadDetailsBuilder = new ThreadDetailsBuilder();
            canUpdate = true;
            _detailsGroups = new List<DetailsGroup>();
            _currentDetailsIDs = new List<int>();
            _pooledDetails = new List<PooledDetail>[references.Count];
            for (var i = 0; i < _pooledDetails.Length; i++)
            {
                _pooledDetails[i] = new List<PooledDetail>();
            }

            Thread thread = new Thread(UpdateDetail);
            Thread lookForAvailableTreeThread = new Thread(LookForAvailableTree);
            _threadDetailsBuilder.IsActive = true;
            lookForAvailableTreeThread.Start();
            thread.Start();
        }

        private void LookForAvailableTree()
        {
            while (_threadDetailSorter.LookForAvailableTree)
            {
                if (lookForAvailablePool)
                {
                    isAvailableReady = false;
                    for (var k = 0; k < _pooledDetails.Length; k++)
                    {
                        for (var i = 0; i < _threadDetailSorter.AvailableIndices.Length; i++)
                        {
                            AddPooledObjectToAvailableIndexes(_threadDetailSorter.AvailableIndices[i], k);
                        }
                    }

                    lookForAvailablePool = false;
                }

                Thread.Sleep(1);
                isAvailableReady = true;
            }

            void AddPooledObjectToAvailableIndexes(ThreadDetailSorter.AvailableIndex availableIndices, int pooledID)
            {
                for (var i = 0; i < availableIndices.AvailableIndexes.Length; i++)
                {
                    if (availableIndices.AvailableIndexes[i] == -1)
                    {
                        for (int j = 0; j < _pooledDetails[pooledID].Count; j++)
                        {
                            if (_pooledDetails[pooledID][j].ID == -1)
                            {
                                availableIndices.AvailableIndexes[i] = j;
                                break;
                            }
                        }
                    }
                }
            }
        }

        internal bool HighDefinition(Vector3[] positions, int id, MinMax minMax)
        {
            if (!canUpdate)
                return false;

            if (_currentDetailsIDs.Contains(id))
                return true;

            _minAlitude = minMax.Min + minAltitudeOffset;
            _maxAltitude = minMax.Max - maxAltitudeOffset;
            canUpdate = false;
            _threadDetailsBuilder.Positions = positions;
            _threadDetailsBuilder.ID = id;
            _threadDetailsBuilder.IsCalculating = true;
            return false;
        }

        void UpdateDetail()
        {
            while (_threadDetailsBuilder.IsActive)
            {
                if (_threadDetailsBuilder.IsCalculating && !_threadDetailsBuilder.Lock)
                {
                    Stopwatch time = new Stopwatch();
                    time.Start();
                    var currentGroup = -1;
                    for (var i = 0; i < _detailsGroups.Count; i++)
                    {
                        if (_detailsGroups[i].id == -1)
                        {
                            currentGroup = i;
                            break;
                        }
                    }

                    if (currentGroup == -1)
                    {
                        var newGroup = new DetailsGroup();
                        newGroup.id = _threadDetailsBuilder.ID;
                        _detailsGroups.Add(newGroup);
                        currentGroup = _detailsGroups.Count - 1;
                    }
                    else
                    {
                        _detailsGroups[currentGroup].id = _threadDetailsBuilder.ID;
                    }

                    for (int i = 0; i < _threadDetailsBuilder.Positions.Length; i++)
                    {
                        if (_minAlitude < Vector3.Distance(Vector3.zero, _threadDetailsBuilder.Positions[i]) &&
                            !(_maxAltitude < Vector3.Distance(Vector3.zero, _threadDetailsBuilder.Positions[i])) &&
                            _noise.Evaluate(_threadDetailsBuilder.Positions[i]) > 0.1f)
                        {
                            PositionDetail(_threadDetailsBuilder.Positions[i], _threadDetailsBuilder.ID, currentGroup,
                                _noise.Evaluate(_threadDetailsBuilder.Positions[i]) > 0.4f ? 0 : 1);
                        }
                    }

                    _currentDetailsIDs.Add(_threadDetailsBuilder.ID);
                    canUpdate = true;
                    _threadDetailsBuilder.IsCalculating = false;
                    time.Stop();
                    TimeSpan ts = time.Elapsed;
                    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        ts.Hours, ts.Minutes, ts.Seconds,
                        ts.Milliseconds / 10);
                    UnityEngine.Debug.Log("RunTime " + elapsedTime);
                }

                Thread.Sleep(1);
            }

            Debug.Log("Dispose detail builder");
        }

        private void Update()
        {
            if (canUpdate && isAvailableReady)
                UpdatePooledDetails();
        }

        void UpdatePooledDetails()
        {
            var position = Camera.main.transform.position;
            var positionOffset = transform.position;
            if (_detailsGroups.Count == 0 || !canUpdate)
            {
                _currentGroupCount = 0;
                _currentPoolDetailCount = 0;
                return;
            }

            void PositionDetail(ref DetailData detailData)
            {
                var pooledDetailFound = false;
                var referenceID = detailData.ReferenceID;
                for (int i = 0; i < _threadDetailSorter.AvailableIndices[referenceID].AvailableIndexes.Length; i++)
                {
                    if (_threadDetailSorter.AvailableIndices[referenceID].AvailableIndexes[i] != -1)
                    {
                        var j = _threadDetailSorter.AvailableIndices[referenceID].AvailableIndexes[i];
                        
                        var pooledDetail = _pooledDetails[referenceID];
                        if (pooledDetail[j].ID == -1 && detailData.AssignedPoolID == -1)
                        {
                            _threadDetailSorter.AvailableIndices[referenceID].AvailableIndexes[i] = -1;
                            SetPosition(pooledDetail[j].GameObject, detailData.Position);
                            var currentPooledDetail = pooledDetail[j];
                            currentPooledDetail.ID = detailData.ID;
                            pooledDetail[j] = currentPooledDetail;
                            detailData.AssignedPoolID = j;
                            _pooledDetails[referenceID][j] = pooledDetail[j];
                            pooledDetailFound = true;
                            break;
                        }
                    }
                }

                if (!pooledDetailFound)
                {
                    var newPooledDetailObject = Instantiate(references[detailData.ReferenceID], transform);
                    var newPooledDetail = new PooledDetail(newPooledDetailObject, detailData.ReferenceID);
                    SetPosition(newPooledDetailObject, detailData.Position);
                    _pooledDetails[detailData.ReferenceID].Add(newPooledDetail);
                    detailData.AssignedPoolID = _pooledDetails[detailData.ReferenceID].Count - 1;
                }
            }

            var targetIndex = UpdatePerFrames + _currentPoolDetailCount;
            const float distance = 5000;
            for (var i = _currentPoolDetailCount; i < targetIndex; i++)
            {
                var currentGroup = _detailsGroups[_currentGroupCount];
                if (currentGroup.detailDatas.Count == 0)
                {
                    return;
                }

                var detailData = currentGroup.detailDatas[i];
                if (_detailsGroups[_currentGroupCount].detailDatas[i].ID != -1)
                {
                    if (Vector3.Distance(position, detailData.Position + positionOffset) < distance &&
                        detailData.AssignedPoolID == -1)
                    {
                        Profiler.BeginSample("Position");
                        PositionDetail(ref detailData);
                        Profiler.EndSample();
                    }
                    else if (detailData.AssignedPoolID != -1 &&
                             Vector3.Distance(position, detailData.Position + positionOffset) >= distance)
                    {
                        var pooledDetail = _pooledDetails[detailData.ReferenceID][detailData.AssignedPoolID];
                        pooledDetail.ID = -1;
                        _pooledDetails[detailData.ReferenceID][detailData.AssignedPoolID] = pooledDetail;
                        detailData.AssignedPoolID = -1;
                    }
                }

                _detailsGroups[_currentGroupCount].detailDatas[i] = detailData;
                _currentPoolDetailCount++;
                if (_currentPoolDetailCount >= _detailsGroups[_currentGroupCount].detailDatas.Count)
                {
                    _currentGroupCount++;
                    if (_currentGroupCount >= _detailsGroups.Count)
                    {
                        _currentGroupCount = 0;
                    }
                    else if (_detailsGroups[_currentGroupCount].detailDatas.Count == 0)
                    {
                        _currentGroupCount++;
                        if (_currentGroupCount >= _detailsGroups.Count)
                        {
                            _currentGroupCount = 0;
                        }
                    }

                    _currentPoolDetailCount = 0;
                    break;
                }
            }

            lookForAvailablePool = true;
        }

        private void OnDestroy()
        {
            _threadDetailsBuilder.IsActive = false;
            _threadDetailSorter.LookForAvailableTree = false;
        }

        void PositionDetail(Vector3 position, int id, int groupID, int referenceID)
        {
            for (var i = 0; i < _detailsGroups[groupID].detailDatas.Count; i++)
            {
                var detailData = _detailsGroups[groupID].detailDatas[i];
                if (_detailsGroups[groupID].detailDatas[i].ID == -1)
                {
                    detailData.ID = id;
                    detailData.AssignedPoolID = -1;
                    detailData.Position = position;
                    detailData.ReferenceID = referenceID;
                    _detailsGroups[groupID].detailDatas[i] = detailData;
                    return;
                }
            }

            var newDetailData = new DetailData(position, id, referenceID);
            _detailsGroups[groupID].detailDatas.Add(newDetailData);
        }

        void SetPosition(GameObject detailGameObject, Vector3 position)
        {
            detailGameObject.transform.localPosition = position;
            detailGameObject.transform.LookAt(transform.position, Vector3.back);
            detailGameObject.transform.Rotate(Vector3.up, -90, Space.Self);
            detailGameObject.transform.Rotate(Vector3.right, _noise.Evaluate(position) * 360, Space.Self);
        }

        internal bool LowDefinition(int id)
        {
            var groupID = -1;
            for (var i = 0; i < _detailsGroups.Count; i++)
            {
                if (_detailsGroups[i].id == id)
                {
                    groupID = i;
                    break;
                }
            }

            if (_detailsGroups.Count == 0 || !_currentDetailsIDs.Contains(id) || groupID == -1)
                return true;

            _threadDetailsBuilder.Lock = true;
            for (var i = 0; i < _detailsGroups[groupID].detailDatas.Count; i++)
            {
                var detail = _detailsGroups[groupID].detailDatas[i];
                if (detail.AssignedPoolID != -1)
                {
                    var objectDetail = _pooledDetails[detail.ReferenceID][detail.AssignedPoolID];
                    objectDetail.ID = -1;
                    objectDetail.GameObject.transform.localPosition = Vector3.zero;
                    _pooledDetails[detail.ReferenceID][detail.AssignedPoolID] = objectDetail;
                }

                detail.ID = -1;

                _detailsGroups[groupID].detailDatas[i] = detail;
            }

            _detailsGroups[groupID].id = -1;
            _currentDetailsIDs.Remove(id);
            _threadDetailsBuilder.Lock = false;
            return true;
        }
    }

    class ThreadDetailSorter
    {
        internal AvailableIndex[] AvailableIndices;
        internal bool LookForAvailableTree;

        internal ThreadDetailSorter(int updatePerFrames, int numberOfReferences)
        {
            AvailableIndices = new AvailableIndex[numberOfReferences];
            for (var i = 0; i < AvailableIndices.Length; i++)
            {
                AvailableIndices[i] = new AvailableIndex(updatePerFrames);
                for (var j = 0; j < AvailableIndices[i].AvailableIndexes.Length; j++)
                {
                    AvailableIndices[i].AvailableIndexes[j] = -1;
                }
            }

            LookForAvailableTree = true;
        }

        internal class AvailableIndex
        {
            internal readonly int[] AvailableIndexes;

            internal AvailableIndex(int updatePerFrame)
            {
                AvailableIndexes = new int[updatePerFrame];
            }
        }
    }

    [Serializable]
    class DetailsGroup
    {
        [SerializeField] internal int id;
        [SerializeField] internal List<DetailData> detailDatas;

        public DetailsGroup()
        {
            detailDatas = new List<DetailData>();
            id = -1;
        }
    }

    class ThreadDetailsBuilder
    {
        internal bool IsCalculating;
        internal Vector3[] Positions;
        internal int ID;
        internal bool IsActive;
        internal bool Lock;
    }

    [Serializable]
    struct PooledDetail
    {
        [SerializeField] internal GameObject GameObject;
        internal int ID;

        public PooledDetail(GameObject gameObject, int id)
        {
            GameObject = gameObject;
            ID = id;
        }
    }

    [Serializable]
    struct DetailData
    {
        internal Vector3 Position;
        internal int ID;
        internal int ReferenceID;
        internal int AssignedPoolID;

        public DetailData(Vector3 position, int id, int referenceID)
        {
            Position = position;
            ID = id;
            AssignedPoolID = -1;
            ReferenceID = referenceID;
        }
    }
}