using System;
using System.Collections.Generic;
using System.Threading;
using CelestialBodies;
using CelestialBodies.Terrain;
using UnityEngine;

public class MeshDetail : MonoBehaviour
{
    private List<DetailMesh> _detailMeshes;
    [SerializeField] private Material Material;
    [SerializeField] private float MinAltitude;
    [SerializeField] private float MaxAltitude;
    [SerializeField] private GameObject reference;
    private float _minAltitude;
    private float _maxAltitude;
    private float _stepThreshold;
    private int currentUpdatedMesh;

    private void Awake()
    {
        _detailMeshes = new List<DetailMesh>();
    }

    void Update()
    {
        if (currentUpdatedMesh > _detailMeshes.Count - 1)
        {
            currentUpdatedMesh = 0;
        }
        if(_detailMeshes.Count > 0)
        {
            _detailMeshes[currentUpdatedMesh].UpdateMesh(transform);
            currentUpdatedMesh++;
        }
    }

    public bool LowDefinition(int chunkID)
    {
        for (var i = 0; i < _detailMeshes.Count; i++)
        {
            if (_detailMeshes[i].id == chunkID)
            {
                _detailMeshes[i].id = -1;
            }
        }

        return true;
    }

    private void OnDestroy()
    {
        for (var i = 0; i < _detailMeshes.Count; i++)
        {
            _detailMeshes[i].DisposeThread();
        }
    }

    public bool HighDefinition(int chunkID, Vector3[] vertices, int[] indexes, Vector3[] normals, Color [] vertexColors, float stepThreshold, MinMax minMax)
    {
        _minAltitude = minMax.Min + MinAltitude;
        _maxAltitude = minMax.Max - MaxAltitude;
        if (ContainsID(chunkID))
            return true;
        var foundDetailMesh = false;
        for (var i = 0; i < _detailMeshes.Count; i++)
        {
            if (_detailMeshes[i].id == -1)
            {
                _detailMeshes[i].id = chunkID;
                _detailMeshes[i].vertices = vertices;
                _detailMeshes[i].indexes = indexes;
                _detailMeshes[i].normals = normals;
                _detailMeshes[i].CopyVertexColors(vertexColors);
                foundDetailMesh = true;
                break;
            }
        }

        if (!foundDetailMesh)
        {
            _stepThreshold = stepThreshold;
            var newDetailMesh = new DetailMesh(gameObject, chunkID, Material, vertices, vertexColors, indexes, normals, _minAltitude, _maxAltitude, stepThreshold, reference);
            _detailMeshes.Add(newDetailMesh);
        }

        return true;
    }

    bool ContainsID(int id)
    {
        for (var i = 0; i < _detailMeshes.Count; i++)
        {
            if (_detailMeshes[i].id == id)
                return true;
        }
        return false;
    }
}

class DetailMesh
{
    internal int id;
    internal Vector3[] vertices;
    internal int[] indexes;
    internal Color[] vertexColor;
    internal Vector3[] normals;
    internal MeshRenderer _meshRenderer;
    internal MeshFilter _meshFilter;
    private List<(float distance, Vector3 faceCenter, int triangleIndex)> closestTriangles;

    private int[] trianglesIndexes;
    private Vector3 meshTransformPosition;
    private Quaternion meshTransformRotation;
    private Vector3 meshTransformScale;
    private Vector3 targetPosition;
    private bool submitedCalculation;
    private bool finishedCalculation;
    private bool runCalculationThread;
    private float _minAltitude;
    private float _maxAltitude;
    private float _stepThreshold;
    private GameObject _reference;
    private List<GameObject> pool;
    private Noise _noise;

    internal DetailMesh(GameObject parent, int id, Material material, Vector3[] vertices, Color [] vertexColor, int[] indexes,
        Vector3[] normals, float minAltitude, float maxAltitude, float stepThreshold, GameObject reference)
    {
        _noise = new Noise();
        this.vertices = vertices;
        CopyVertexColors(vertexColor);
        this.indexes = indexes;
        this.normals = normals;
        _minAltitude = minAltitude;
        _maxAltitude = maxAltitude;
        _stepThreshold = stepThreshold;
        var newMesh = new GameObject("Detail Mesh");
        newMesh.transform.parent = parent.transform;
        newMesh.transform.localPosition = Vector3.zero;
        newMesh.transform.rotation = Quaternion.identity;
        _meshRenderer = newMesh.AddComponent<MeshRenderer>();
        _meshRenderer.material = material;
        _meshRenderer.sharedMaterial.SetFloat("_GrassSupression", stepThreshold);;
        _meshFilter = newMesh.AddComponent<MeshFilter>();
        this.id = id;
        runCalculationThread = true;
        Thread thread = new Thread(CalculateTriangles);
        thread.Start();
        _reference = reference;
    }

    internal void CopyVertexColors(Color [] vertexColorToCopy)
    {
        if (vertexColor == null || vertexColor.Length != vertexColorToCopy.Length)
        {
            vertexColor = new Color[vertexColorToCopy.Length];
        }

        for (var i = 0; i < vertexColorToCopy.Length; i++)
        {
            vertexColor[i] = vertexColorToCopy[i];
        }
    }

    internal void DisposeThread()
    {
        runCalculationThread = false;
    }

    internal void UpdateMesh(Transform parent)
    {
        
        if (finishedCalculation)
        {
            AssignTriangles();
            AddDetails(parent);
            finishedCalculation = false;
        }
        else if (!submitedCalculation)
        {
            SumbitCalculation();
        }

    }
    
    void AddDetails(Transform parent)
    {
        if (pool == null)
            pool = new List<GameObject>();
        
        for (var i = 0; i < pool.Count; i++)
        {
            pool[i].transform.position = Vector3.one * 10000;
        }
        
        for (var i = 0; i < trianglesIndexes.Length; i = i + 3)
        {
            var first = trianglesIndexes[i];
            var second = trianglesIndexes[i + 1];
            var third = trianglesIndexes[i + 2];
            var position =  Vector3.Lerp(vertices[first], vertices[second], (_noise.Evaluate(vertices[first]) + 1) / 2);
            position = Vector3.Lerp(position, vertices[third], (_noise.Evaluate(vertices[first] + new Vector3(1000, 0 ,0)) + 1) / 2);
            GameObject gameObject;
            if (vertexColor[first].r < _stepThreshold)
            {
                if (pool.Count - 1 < i / 3)
                {
                    gameObject = GameObject.Instantiate(_reference, position, Quaternion.identity);
                    pool.Add(gameObject);
                }
                else
                {
                    gameObject = pool[i / 3];
                }

                gameObject.transform.parent = parent;
                gameObject.transform.localPosition = position;
                gameObject.transform.LookAt(parent.position, Vector3.back);
                gameObject.transform.Rotate(Vector3.up, -90, Space.Self);
                gameObject.transform.Rotate(Vector3.right, 0, Space.Self);
            }
        }
    }
    
    void SumbitCalculation()
    {
        submitedCalculation = true;
        finishedCalculation = false;
        targetPosition = Camera.main.transform.position;
        Transform meshTransform = _meshFilter.transform;
        // Get the vertices of the triangle
        meshTransformPosition = meshTransform.transform.position;
        meshTransformRotation = meshTransform.rotation;
        meshTransformScale = meshTransform.localScale;
    }

    internal void CalculateTriangles()
    {
        while (runCalculationThread)
        {
            if (submitedCalculation)
            {
                submitedCalculation = false;
                int[] triangles = indexes;
                
                // Priority queue to store the closest triangles
                if (closestTriangles == null)
                    closestTriangles = new();
                var numberOfTrianglesDrawn = 0;
                for (int j = 0; j < triangles.Length; j += 3)
                {
                    Vector3 v0 = CustomTransformPoint(vertices[triangles[j]], meshTransformPosition,
                        meshTransformRotation,
                        meshTransformScale);
                    Vector3 v1 = CustomTransformPoint(vertices[triangles[j + 1]], meshTransformPosition,
                        meshTransformRotation, meshTransformScale);
                    Vector3 v2 = CustomTransformPoint(vertices[triangles[j + 2]], meshTransformPosition,
                        meshTransformRotation, meshTransformScale);

                    // Calculate the face center
                    Vector3 faceCenter = (v0 + v1 + v2) / 3f;

                    // Calculate the distance from the camera to the face center
                    float distance = Vector3.Distance(targetPosition, faceCenter);
                    if(_minAltitude > Vector3.Distance(Vector3.zero, vertices[triangles[j]]) || _maxAltitude < Vector3.Distance(Vector3.zero, vertices[triangles[j]]))
                    {
                        vertexColor[triangles[j]] = new Color(1, vertexColor[triangles[j]].g,
                            vertexColor[triangles[j]].b, vertexColor[triangles[j]].a);
                    }
                    
                    if (distance < 500 )
                    {
                        numberOfTrianglesDrawn++;
                    } else
                    {
                        distance = float.MaxValue;
                    }
                    
                    if (closestTriangles.Count - 1 < j)
                        closestTriangles.Add((distance, faceCenter, j / 3));
                    else
                    {
                        closestTriangles[j] = (distance, faceCenter, j / 3);
                    }
                }

                // Sort the list by distance
                closestTriangles.Sort((a, b) => a.distance.CompareTo(b.distance));

                // Get the top 10 closest triangles
                int count = Mathf.Min(750, numberOfTrianglesDrawn);
                if (trianglesIndexes == null || trianglesIndexes.Length != count * 3)
                    trianglesIndexes = new int[count * 3];
                for (int j = 0; j < count; j++)
                {
                    var triangle = closestTriangles[j];
                    if (triangle.distance < float.MaxValue -1)
                    {
                        trianglesIndexes[(j * 3)] = triangles[triangle.triangleIndex * 3];
                        trianglesIndexes[(j * 3) + 1] = triangles[(triangle.triangleIndex * 3) + 1];
                        trianglesIndexes[(j * 3) + 2] = triangles[(triangle.triangleIndex * 3) + 2];
                    }
                    else
                    {
                        trianglesIndexes[(j * 3)] = 0;
                        trianglesIndexes[(j * 3) + 1] = 0;
                        trianglesIndexes[(j * 3) + 2] = 0;
                    }
                }

                finishedCalculation = true;
            }
            
            Thread.Sleep(1);
        }

        Debug.Log("Finished Calculation thread");
    }

    public static Vector3 CustomTransformPoint(Vector3 localPoint, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        // Apply scaling
        Vector3 scaledPoint = Vector3.Scale(localPoint, scale);

        // Apply rotation
        Vector3 rotatedPoint = rotation * scaledPoint;

        // Apply translation
        Vector3 worldPoint = position + rotatedPoint;

        return worldPoint;
    }

    void AssignTriangles()
    {
        var mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.colors = vertexColor;
        mesh.triangles = trianglesIndexes;
        mesh.RecalculateNormals();
        _meshFilter.mesh = mesh;
    }
}