using System.Collections.Generic;
using System.Threading;
using CelestialBodies;
using CelestialBodies.Terrain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public class TerrainGrass : MonoBehaviour
{
    private List<DetailMesh> _detailMeshes;
    [FormerlySerializedAs("Material")] [SerializeField] private Material material;
    [FormerlySerializedAs("MinAltitude")] [SerializeField] private float minAltitude;
    [FormerlySerializedAs("MaxAltitude")] [SerializeField] private float maxAltitude;
    [SerializeField] private GameObject reference;
    [SerializeField] private GameObject stepReference;
    [SerializeField] private GameObject bellowAltitudeReference;
    [SerializeField] private GameObject aboveAltitudeReference;
    private float _minAltitude;
    private float _maxAltitude;
    private int _currentUpdatedMesh;

    private void Awake()
    {
        _detailMeshes = new List<DetailMesh>();
    }

    void Update()
    {
        if (_currentUpdatedMesh > _detailMeshes.Count - 1)
        {
            _currentUpdatedMesh = 0;
        }
        
        if(_detailMeshes.Count > 0)
        {
            _detailMeshes[_currentUpdatedMesh].UpdateMesh(transform);
            _currentUpdatedMesh++;
        }
    }

    public bool LowDefinition(int chunkID)
    {
        for (var i = 0; i < _detailMeshes.Count; i++)
        {
            if (_detailMeshes[i].ID == chunkID)
            {
                _detailMeshes[i].ID = -1;
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
        _minAltitude = minMax.Min + minAltitude;
        _maxAltitude = minMax.Max - maxAltitude;
        if (ContainsID(chunkID))
            return true;
        var foundDetailMesh = false;
        for (var i = 0; i < _detailMeshes.Count; i++)
        {
            if (_detailMeshes[i].ID == -1)
            {
                _detailMeshes[i].ID = chunkID;
                _detailMeshes[i].Vertices = vertices;
                _detailMeshes[i].Indexes = indexes;
                _detailMeshes[i].CopyVertexColors(vertexColors);
                foundDetailMesh = true;
                break;
            }
        }

        if (!foundDetailMesh)
        {
            var newDetailMesh = new DetailMesh(gameObject, chunkID, material, vertices, vertexColors, indexes, _minAltitude, _maxAltitude, stepThreshold, reference, 
                stepReference, bellowAltitudeReference, aboveAltitudeReference);
            _detailMeshes.Add(newDetailMesh);
        }

        return true;
    }

    bool ContainsID(int id)
    {
        for (var i = 0; i < _detailMeshes.Count; i++)
        {
            if (_detailMeshes[i].ID == id)
                return true;
        }
        return false;
    }
}

class DetailMesh
{
    internal int ID;
    internal Vector3[] Vertices;
    internal int[] Indexes;
    internal Color[] VertexColor;
    internal readonly MeshRenderer MeshRenderer;
    internal readonly MeshFilter MeshFilter;
    internal readonly MeshCollider MeshCollider;
    private List<(float distance, Vector3 faceCenter, int triangleIndex)> _closestTriangles;
    private int[] _trianglesIndexes;
    
    private Vector3 _meshTransformPosition;
    private Quaternion _meshTransformRotation;
    private Vector3 _meshTransformScale;
    private Vector3 _targetPosition;
    private bool _submitedCalculation;
    private bool _finishedCalculation;
    private bool _hasAtLeastOneTriangle;
    private bool _runCalculationThread;
    private readonly float _minAltitude;
    private readonly float _maxAltitude;
    private readonly float _stepThreshold;
    private readonly GameObject _reference;
    private readonly GameObject _stepReference;
    private readonly GameObject _bellowMinimumReference;
    private readonly GameObject _aboveMaximumReference;
    private List<GameObject> _pool;
    private List<GameObject> _stepPool;
    private List<GameObject> _bellowMinimumPool;
    private List<GameObject> _aboveMaximumPool;
    private readonly Noise _noise;
    
    //Mesh Subdivision
    private Vector3[] _newVertices;
    private int[] _newTriangles;
    private Color[] _newColors;
    private int _currentCount;
    private static readonly int GrassSupression = Shader.PropertyToID("_GrassSupression");
    private Mesh _mesh;

    internal DetailMesh(GameObject parent, int id, Material material, Vector3[] vertices, Color [] vertexColor, int[] indexes,
        float minAltitude, float maxAltitude, float stepThreshold, 
        GameObject reference, GameObject stepReference, GameObject bellowMinimumReference, GameObject aboveMaximumReference)
    {
        _noise = new Noise();
        Vertices = vertices;
        CopyVertexColors(vertexColor);
        Indexes = indexes;
        _minAltitude = minAltitude;
        _maxAltitude = maxAltitude;
        _stepThreshold = stepThreshold;
        var newMesh = new GameObject("Detail Mesh");
        newMesh.transform.parent = parent.transform;
        newMesh.transform.localPosition = Vector3.zero;
        newMesh.transform.rotation = Quaternion.identity;
        MeshRenderer = newMesh.AddComponent<MeshRenderer>();
        MeshRenderer.material = material;
        MeshRenderer.sharedMaterial.SetFloat(GrassSupression, stepThreshold);
        MeshFilter = newMesh.AddComponent<MeshFilter>();
        MeshCollider = newMesh.AddComponent<MeshCollider>();
        ID = id;
        _runCalculationThread = true;
        Thread thread = new Thread(CalculateTriangles);
        thread.Start();
        _reference = reference;
        _stepReference = stepReference;
        _bellowMinimumReference = bellowMinimumReference;
        _aboveMaximumReference = aboveMaximumReference;

    }

    internal void CopyVertexColors(Color [] vertexColorToCopy)
    {
        if (VertexColor == null || VertexColor.Length != vertexColorToCopy.Length)
        {
            VertexColor = new Color[vertexColorToCopy.Length];
        }

        for (var i = 0; i < vertexColorToCopy.Length; i++)
        {
            VertexColor[i] = vertexColorToCopy[i];
        }
    }

    internal void DisposeThread()
    {
        _runCalculationThread = false;
    }

    internal void UpdateMesh(Transform parent)
    {
        
        if (_finishedCalculation)
        {
            AssignTriangles();
            AddDetails(parent);
            _finishedCalculation = false;
        }
        else if (!_submitedCalculation)
        {
            SumbitCalculation();
        }

    }

    void InitializeDetailPool()
    {
        if (_pool == null)
            _pool = new List<GameObject>();

        if (_stepPool == null)
            _stepPool = new List<GameObject>();

        if (_bellowMinimumPool == null)
            _bellowMinimumPool = new List<GameObject>();

        if (_aboveMaximumPool == null)
            _aboveMaximumPool = new List<GameObject>();
        
        for (var i = 0; i < _pool.Count; i++)
        {
            _pool[i].transform.position = Vector3.one * 10000;
        }
        
        for (var i = 0; i < _stepPool.Count; i++)
        {
            _stepPool[i].transform.position = Vector3.one * 10000;
        }

        for (int i = 0; i < _bellowMinimumPool.Count; i++)
        {
            _bellowMinimumPool[i].transform.position = Vector3.one * 10000;
        }
        
        for (int i = 0; i < _aboveMaximumPool.Count; i++)
        {
            _aboveMaximumPool[i].transform.position = Vector3.one * 10000;
        }
    }

    void PlaceDetail(GameObject gameObject, Transform parent, Vector3 position)
    {
        gameObject.transform.parent = parent;
        gameObject.transform.localPosition = position;
        gameObject.transform.LookAt(parent.position, Vector3.back);
        gameObject.transform.Rotate(Vector3.up, -90, Space.Self);
        gameObject.transform.Rotate(Vector3.right, _noise.Evaluate(position) * 360, Space.Self);
    }
    
    void AddDetails(Transform parent) // Ugly but works 
    {
        InitializeDetailPool();
        
        for (var i = 0; i < _trianglesIndexes.Length; i = i + 3)
        {
            var first = _trianglesIndexes[i];
            var second = _trianglesIndexes[i + 1];
            var third = _trianglesIndexes[i + 2];
            var position =  Vector3.Lerp(Vertices[first], Vertices[second], (_noise.Evaluate(Vertices[first]) + 1) / 2);
            position = Vector3.Lerp(position, Vertices[third], (_noise.Evaluate(Vertices[first] + new Vector3(1000, 0 ,0)) + 1) / 2);
            GameObject gameObject;
            if (VertexColor[first].r < _stepThreshold)
            {
                if (_reference != null)
                {
                    if (_pool.Count - 1 < i / 3)
                    {
                        gameObject = Object.Instantiate(_reference, position, Quaternion.identity);
                        _pool.Add(gameObject);
                    }
                    else
                    {
                        gameObject = _pool[i / 3];
                    }
                    
                    PlaceDetail(gameObject, parent, position);
                }
            }
            else if(VertexColor[first].r != 0.99f && VertexColor[first].r != 0.98f && VertexColor[first].r != 0.97f)
            {
                if (_stepReference != null)
                {
                    if (_stepPool.Count - 1 < i / 3)
                    {
                        gameObject = Object.Instantiate(_stepReference, position, Quaternion.identity);
                        _stepPool.Add(gameObject);
                    }
                    else
                    {
                        gameObject = _stepPool[i / 3];
                    }

                    PlaceDetail(gameObject, parent, position);
                }
            }
            else if(VertexColor[first].r == 0.98f)
            {
                if (_bellowMinimumReference != null)
                {
                    if (_bellowMinimumPool.Count - 1 < i / 3)
                    {
                        gameObject = Object.Instantiate(_bellowMinimumReference, position, Quaternion.identity);
                        _bellowMinimumPool.Add(gameObject);
                    }
                    else
                    {
                        gameObject = _bellowMinimumPool[i / 3];
                    }

                    PlaceDetail(gameObject, parent, position);
                }
            }else if (VertexColor[first].r == 0.97f)
            {
                if (_aboveMaximumReference != null)
                {
                    if (_aboveMaximumPool.Count - 1 < i / 3)
                    {
                        gameObject = Object.Instantiate(_aboveMaximumReference, position, Quaternion.identity);
                        _aboveMaximumPool.Add(gameObject);
                    }
                    else
                    {
                        gameObject = _aboveMaximumPool[i / 3];
                    }

                    PlaceDetail(gameObject, parent, position);
                }
            }
        }
    }
    
    void SumbitCalculation()
    {
        _submitedCalculation = true;
        _finishedCalculation = false;
        _targetPosition = Camera.main.transform.position;
        Transform meshTransform = MeshFilter.transform;
        // Get the vertices of the triangle
        _meshTransformPosition = meshTransform.transform.position;
        _meshTransformRotation = meshTransform.rotation;
        _meshTransformScale = meshTransform.localScale;
    }

    internal void CalculateTriangles()
    {
        while (_runCalculationThread)
        {
            if (_submitedCalculation)
            {
                _submitedCalculation = false;
                int[] triangles = Indexes;
                
                // Priority queue to store the closest triangles
                if (_closestTriangles == null)
                    _closestTriangles = new();
                var numberOfTrianglesDrawn = 0;
                for (int j = 0; j < triangles.Length; j += 3)
                {
                    Vector3 v0 = CustomTransformPoint(Vertices[triangles[j]], _meshTransformPosition,
                        _meshTransformRotation,
                        _meshTransformScale);
                    Vector3 v1 = CustomTransformPoint(Vertices[triangles[j + 1]], _meshTransformPosition,
                        _meshTransformRotation, _meshTransformScale);
                    Vector3 v2 = CustomTransformPoint(Vertices[triangles[j + 2]], _meshTransformPosition,
                        _meshTransformRotation, _meshTransformScale);

                    // Calculate the face center
                    Vector3 faceCenter = (v0 + v1 + v2) / 3f;

                    // Calculate the distance from the camera to the face center
                    float distance = Vector3.Distance(_targetPosition, faceCenter);
                    if(_minAltitude > Vector3.Distance(Vector3.zero, Vertices[triangles[j]]) || _maxAltitude < Vector3.Distance(Vector3.zero, Vertices[triangles[j]]))
                    {
                        if(_minAltitude > Vector3.Distance(Vector3.zero, Vertices[triangles[j]]))
                            VertexColor[triangles[j]] = new Color(0.98f, VertexColor[triangles[j]].g,
                                VertexColor[triangles[j]].b, VertexColor[triangles[j]].a);
                        else
                        {
                            VertexColor[triangles[j]] = new Color(0.97f, VertexColor[triangles[j]].g,
                                VertexColor[triangles[j]].b, VertexColor[triangles[j]].a);
                        }
                    }
                    
                    if (distance < 400)
                    {
                        numberOfTrianglesDrawn++;
                    } else
                    {
                        distance = float.MaxValue;
                    }
                    
                    if (_closestTriangles.Count - 1 < j)
                        _closestTriangles.Add((distance, faceCenter, j / 3));
                    else
                    {
                        _closestTriangles[j] = (distance, faceCenter, j / 3);
                    }
                }

                // Sort the list by distance
                _closestTriangles.Sort((a, b) => a.distance.CompareTo(b.distance));
                _hasAtLeastOneTriangle = false;
                int count = Mathf.Min(400, numberOfTrianglesDrawn);
                if (_trianglesIndexes == null || _trianglesIndexes.Length != count * 3)
                    _trianglesIndexes = new int[count * 3];
                for (int j = 0; j < count; j++)
                {
                    var triangle = _closestTriangles[j];
                    if (triangle.distance < float.MaxValue -1)
                    {
                        _trianglesIndexes[(j * 3)] = triangles[triangle.triangleIndex * 3];
                        _trianglesIndexes[(j * 3) + 1] = triangles[(triangle.triangleIndex * 3) + 1];
                        _trianglesIndexes[(j * 3) + 2] = triangles[(triangle.triangleIndex * 3) + 2];
                        _hasAtLeastOneTriangle = true;
                    }
                    else
                    {
                        _trianglesIndexes[(j * 3)] = 0;
                        _trianglesIndexes[(j * 3) + 1] = 0;
                        _trianglesIndexes[(j * 3) + 2] = 0;
                    }
                }

                _finishedCalculation = true;
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
        if(_mesh == null)
            _mesh = new Mesh();
        else
        {
            _mesh.Clear();
        }
        if (_hasAtLeastOneTriangle)
        {
            _mesh.vertices = Vertices;
            _mesh.colors = VertexColor;
            _mesh.triangles = _trianglesIndexes;
            MeshCollider.sharedMesh = _mesh;
            _mesh = SubdivideMesh(Vertices, _trianglesIndexes, VertexColor, _mesh,1f);
            MeshFilter.mesh = _mesh;
            if (!MeshFilter.gameObject.activeInHierarchy)
            {
                MeshFilter.gameObject.SetActive(true);
            }
        } else if (MeshFilter.gameObject.activeInHierarchy)
        {
            MeshFilter.gameObject.SetActive(false);
        }
    }
    
    Mesh SubdivideMesh(Vector3 [] originalVertices, int [] originalTriangles, Color [] originalColors,Mesh mesh, float randomise = -1f)
    {

        var currentVertexCount = originalVertices.Length;
        var currentTriangleCount = originalTriangles.Length;
        var hasCorrectLength = false;
        
        List<Vector3> verticesBuilder = null;
        List<int> trianglesBuilder = null;
        List<Color> colorsBuilder = null;
        
        
        if (_newVertices == null || _currentCount != originalTriangles.Length)
        {
            verticesBuilder = new List<Vector3>(originalVertices);
            trianglesBuilder = new List<int>(originalTriangles);
            colorsBuilder = new List<Color>(originalColors);
            _currentCount = originalTriangles.Length;
        }
        else
        {
            hasCorrectLength = true;
            for (var i = 0; i < originalVertices.Length; i++)
            {
                _newVertices[i] =  originalVertices[i];
                _newColors[i] = originalColors[i];
            }

            for (var i = 0; i < originalTriangles.Length; i++)
            {
                _newTriangles[i] = originalTriangles[i];
            }
        }
        
        for (int i = 0; i < originalTriangles.Length; i += 3)
        {
            int triangle0 = originalTriangles[i + 0];
            int triangle1 = originalTriangles[i + 1];
            int triangle2 = originalTriangles[i + 2];

            Vector3 vertexA = originalVertices[triangle0];
            Vector3 vertexB = originalVertices[triangle1];
            Vector3 vertexC = originalVertices[triangle2];

            Color colorA = originalColors[triangle0];
            Color colorB = originalColors[triangle1];
            Color colorC = originalColors[triangle2];

            // Calculate midpoints
            Vector3 midPointAb = Vector3.Lerp(vertexA, vertexB, 0.5f);
            Vector3 midPointBc = Vector3.Lerp(vertexB, vertexC, 0.5f);
            Vector3 midPointCa = Vector3.Lerp(vertexC, vertexA, 0.5f);
            
            if (randomise > 0)
            {
                var offset = _noise.Evaluate(midPointAb) * randomise;
                midPointAb = new Vector3(midPointAb.x + offset, midPointAb.y + offset, midPointAb.z + offset);
                offset = _noise.Evaluate(midPointBc);
                midPointBc = new Vector3(midPointBc.x + offset, midPointBc.y + offset, midPointBc.z + offset);
                offset = _noise.Evaluate(midPointCa);
                midPointCa = new Vector3(midPointCa.x + offset, midPointCa.y + offset, midPointCa.z + offset);
            }

            // Calculate midpoint colors
            Color colorAb = Color.Lerp(colorA, colorB, 0.5f);
            Color colorBc = Color.Lerp(colorB, colorC, 0.5f);
            Color colorCa = Color.Lerp(colorC, colorA, 0.5f);

            // Add new vertices with their respective colors
            int abIndex = currentVertexCount;
            if (!hasCorrectLength)
            {
                verticesBuilder.Add(midPointAb);
                colorsBuilder.Add(colorAb);
            }
            else
            {
                _newVertices[currentVertexCount] = midPointAb;
                _newColors[currentVertexCount] = colorAb;
            }
            currentVertexCount++;
            
            int bcIndex = currentVertexCount;
            if (!hasCorrectLength)
            {
                verticesBuilder.Add(midPointBc);
                colorsBuilder.Add(colorBc);
            }
            else
            {
                _newVertices[currentVertexCount] = midPointBc;
                _newColors[currentVertexCount] = colorBc;
            }
            currentVertexCount++;
            
            int caIndex = currentVertexCount;
            if (!hasCorrectLength)
            {
                verticesBuilder.Add(midPointCa);
                colorsBuilder.Add(colorCa);
            }
            else
            {
                _newVertices[currentVertexCount] = midPointCa;
                _newColors[currentVertexCount] = colorCa;
            }
            
            currentVertexCount++;

            if (!hasCorrectLength)
            {
                trianglesBuilder.AddRange(new[] { triangle0, abIndex, caIndex });
            }
            else
            {
                _newTriangles[currentTriangleCount] = triangle0;
                _newTriangles[currentTriangleCount + 1] = abIndex;
                _newTriangles[currentTriangleCount + 2] = caIndex;
            }

            currentTriangleCount += 3;
            if (!hasCorrectLength)
            {
                trianglesBuilder.AddRange(new[] { abIndex, triangle1, bcIndex });
            }
            else
            {
                _newTriangles[currentTriangleCount] = abIndex;
                _newTriangles[currentTriangleCount + 1] = triangle1;
                _newTriangles[currentTriangleCount + 2] = bcIndex;
            }

            currentTriangleCount += 3;
            if (!hasCorrectLength)
            {
                trianglesBuilder.AddRange(new[] { caIndex, bcIndex, triangle2 });
            }
            else
            {
                _newTriangles[currentTriangleCount] = caIndex;
                _newTriangles[currentTriangleCount + 1] = bcIndex;
                _newTriangles[currentTriangleCount + 2] = triangle2;
            }

            currentTriangleCount += 3;
            if (!hasCorrectLength)
            {
                trianglesBuilder.AddRange(new[] { abIndex, bcIndex, caIndex });
            }
            else
            {
                _newTriangles[currentTriangleCount] = abIndex;
                _newTriangles[currentTriangleCount + 1] = bcIndex;
                _newTriangles[currentTriangleCount + 2] = caIndex;
            }

            currentTriangleCount += 3;
        }
        if(mesh == null)
            mesh = new Mesh();

        if (!hasCorrectLength)
        {
            _newVertices = verticesBuilder.ToArray();
            _newTriangles = trianglesBuilder.ToArray();
            _newColors = colorsBuilder.ToArray();
        }
        else 
        {
            mesh.vertices = _newVertices;
            mesh.triangles = _newTriangles;
            mesh.colors = _newColors;
        }

        // Recalculate normals and other mesh properties
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return mesh;
    }
}