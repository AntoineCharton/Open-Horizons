using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CelestialBodies.Terrain;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CelestialBodies
{
    public class TerrainDetails : MonoBehaviour
    {
        [SerializeField] private GameObject _reference;
        private List<int> _currentDetailsIDs;
        private List<DetailData> _detailDatas;
        private List<PooledDetail> _pooledDetail;
        [SerializeField]
        private float minAltitudeOffset;
        private float MinAlitude;
        private int currentPoolDetailCount;
        private const int updatePerFrames = 100;
        private Noise Noise;
        public bool canUpdate = false;
        private ThreadDetailsBuilder _threadDetailsBuilder;

        private void Awake()
        {
            Noise = new Noise();
            _threadDetailsBuilder = new ThreadDetailsBuilder();
            canUpdate = true;
            _currentDetailsIDs = new List<int>();
            _detailDatas = new List<DetailData>();
            _pooledDetail = new List<PooledDetail>();
            Thread thread = new Thread(UpdateDetail);
            _threadDetailsBuilder.IsActive = true;
            thread.Start();
        }

        internal bool HighDefinition(Vector3[] positions, int id, MinMax minMax)
        {
            if(!canUpdate)
                return false;
            
            if (_currentDetailsIDs.Contains(id))
                return true;

            MinAlitude = minMax.Min + minAltitudeOffset;
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
                if (_threadDetailsBuilder.IsCalculating)
                {
                    Stopwatch time = new Stopwatch();
                    time.Start();
                    for (int i = 0; i < _threadDetailsBuilder.Positions.Length; i++)
                    {
                        if(MinAlitude < Vector3.Distance(Vector3.zero, _threadDetailsBuilder.Positions[i]) && Noise.Evaluate(_threadDetailsBuilder.Positions[i]) > 0.1f)
                            PositionDetail(_threadDetailsBuilder.Positions[i], _threadDetailsBuilder.ID);
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
            if(canUpdate)
                UpdatePooledDetails();
        }

        void UpdatePooledDetails()
        {
            var position = Camera.main.transform.position;
            var positionOffset = transform.position;
            if (_detailDatas.Count == 0 || !canUpdate)
            {
                currentPoolDetailCount = 0;
                return;
            }

            var targetIndex = updatePerFrames + currentPoolDetailCount;
            const float distance = 2500;
            for (var i = currentPoolDetailCount; i < targetIndex; i++)
            {
                var detailData = _detailDatas[i];
                if (_detailDatas[i].ID != -1)
                {
                    if (Vector3.Distance(position, detailData.Position + positionOffset) < distance && detailData.AssignedPoolID == -1)
                    {
                        var pooledDetailFound = false;
                        for (int j = 0; j < _pooledDetail.Count; j++)
                        {
                            var pooledDetail = _pooledDetail[j];
                            if (pooledDetail.ID == -1 && detailData.AssignedPoolID == -1)
                            {
                                SetPosition(pooledDetail.GameObject, detailData.Position);
                                pooledDetail.ID = detailData.ID;
                                detailData.AssignedPoolID = j;
                                _pooledDetail[j] = pooledDetail;
                                pooledDetailFound = true;
                                break;
                            }
                        }

                        if (!pooledDetailFound)
                        {
                            var newPooledDetailObject = Instantiate(_reference, transform);
                            var newPooledDetail = new PooledDetail(newPooledDetailObject, i);
                            SetPosition(newPooledDetailObject, detailData.Position);
                            _pooledDetail.Add(newPooledDetail);
                            detailData.AssignedPoolID = _pooledDetail.Count - 1;
                        }
                    }
                    else if(detailData.AssignedPoolID != -1 && Vector3.Distance(position, detailData.Position + positionOffset) >= distance)
                    {
                        var pooledDetail = _pooledDetail[detailData.AssignedPoolID];
                        pooledDetail.ID = -1;
                        _pooledDetail[detailData.AssignedPoolID] = pooledDetail;
                        detailData.AssignedPoolID = -1;
                    }
                }

                _detailDatas[i] = detailData;
                currentPoolDetailCount++;
                if (currentPoolDetailCount >= _detailDatas.Count)
                {
                    currentPoolDetailCount = 0;
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            _threadDetailsBuilder.IsActive = false;
        }

        void PositionDetail(Vector3 position, int id)
        {
            for (var i = 0; i < _detailDatas.Count; i++)
            {
                var detailData = _detailDatas[i];
                if (_detailDatas[i].ID == -1)
                {
                    detailData.ID = id;
                    detailData.AssignedPoolID = -1;
                    detailData.Position = position;
                    _detailDatas[i] = detailData;
                    return;
                }
            }
            var newDetailData = new DetailData(position, id);
            _detailDatas.Add(newDetailData);
        }

        void SetPosition(GameObject gameObject, Vector3 position)
        {
            gameObject.transform.localPosition = position;
            gameObject.transform.LookAt(transform.position, Vector3.back);
            gameObject.transform.localEulerAngles += new Vector3(-90, 0, 0);
        }

        internal bool LowDefinition(int id)
        {
            if (_detailDatas.Count > 0 || !_currentDetailsIDs.Contains(id))
                return true;
            
            for (var i = 0; i < _detailDatas.Count; i++)
            {
                var detail = _detailDatas[i];
                if (detail.ID == id)
                {
                    if (detail.AssignedPoolID != -1)
                    {
                        var objectDetail = _pooledDetail[detail.AssignedPoolID];
                        objectDetail.ID = -1;
                        _pooledDetail[detail.AssignedPoolID] = objectDetail;
                    }

                    detail.ID = -1;
                }

                _detailDatas[i] = detail;
            }
            _currentDetailsIDs.Remove(id);
            return true;
        }
        
    }

    class DetailsGroup
    {
        private int ID;
    }

    class ThreadDetailsBuilder
    {
        internal bool IsCalculating;
        internal Vector3[] Positions;
        internal int ID;
        internal bool IsActive;
    }

    [Serializable]
    struct PooledDetail
    {
        public GameObject GameObject;
        public int ID;

        public PooledDetail(GameObject gameObject, int id)
        {
            GameObject = gameObject;
            ID = id;
        }
    }

    [Serializable]
    struct DetailData
    {
        public Vector3 Position;
        public int ID;
        public int AssignedPoolID;

        public DetailData(Vector3 position, int id)
        {
            Position = position;
            ID = id;
            AssignedPoolID = -1;
        }
    }
}