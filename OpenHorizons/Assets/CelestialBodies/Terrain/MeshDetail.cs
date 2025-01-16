using System;
using System.Collections.Generic;
using System.Threading;
using CelestialBodies.Terrain;
using UnityEngine;

public class MeshDetail : MonoBehaviour
{
    private List<DetailMesh> _detailMeshes;
    [SerializeField] private Material Material;
    [SerializeField] private float MinAltitude;
    [SerializeField] private float MaxAltitude;
    private float _minAltitude;
    private float _maxAltitude;
    private int currentUpdatedMesh;

    private void Awake()
    {
        _detailMeshes = new List<DetailMesh>();
    }

    void Update()
    {
        var camera = Camera.main;
        if (currentUpdatedMesh > _detailMeshes.Count - 1)
        {
            currentUpdatedMesh = 0;
        }
        if(_detailMeshes.Count > 0)
        {
        _detailMeshes[currentUpdatedMesh].UpdateMesh();
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

    public bool HighDefinition(int chunkID, Vector3[] vertices, int[] indexes, Vector3[] normals, MinMax minMax)
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
                foundDetailMesh = true;
                break;
            }
        }

        if (!foundDetailMesh)
        {
            var newDetailMesh = new DetailMesh(gameObject, chunkID, Material, vertices, indexes, normals, _minAltitude, _maxAltitude);
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

    internal DetailMesh(GameObject parent, int id, Material material, Vector3[] vertices, int[] indexes,
        Vector3[] normals, float minAltitude, float maxAltitude)
    {
        this.vertices = vertices;
        this.indexes = indexes;
        this.normals = normals;
        _minAltitude = minAltitude;
        _maxAltitude = maxAltitude;
        var newMesh = new GameObject("Detail Mesh");
        newMesh.transform.parent = parent.transform;
        newMesh.transform.localPosition = Vector3.zero;
        newMesh.transform.rotation = Quaternion.identity;
        _meshRenderer = newMesh.AddComponent<MeshRenderer>();
        _meshRenderer.material = material;
        _meshFilter = newMesh.AddComponent<MeshFilter>();
        this.id = id;
        runCalculationThread = true;
        Thread thread = new Thread(CalculateTriangles);
        thread.Start();
    }

    internal void DisposeThread()
    {
        runCalculationThread = false;
    }

    internal void UpdateMesh()
    {
        
        if (finishedCalculation)
        {
            AssignTriangles();
            finishedCalculation = false;
        }
        else if (!submitedCalculation)
        {
            SumbitCalculation();
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
                    if (distance < 500  && _minAltitude < Vector3.Distance(Vector3.zero, vertices[triangles[j]]) &&
                        !(_maxAltitude < Vector3.Distance(Vector3.zero, vertices[triangles[j]])))
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
                int count = Mathf.Min(500, numberOfTrianglesDrawn);
                if (trianglesIndexes == null || trianglesIndexes.Length != count * 3)
                    trianglesIndexes = new int[count * 3];
                for (int j = 0; j < count; j++)
                {
                    var triangle = closestTriangles[j];
                    trianglesIndexes[(j * 3)] = triangles[triangle.triangleIndex * 3];
                    trianglesIndexes[(j * 3) + 1] = triangles[(triangle.triangleIndex * 3) + 1];
                    trianglesIndexes[(j * 3) + 2] = triangles[(triangle.triangleIndex * 3) + 2];
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
        mesh.triangles = trianglesIndexes;
        mesh.RecalculateNormals();
        _meshFilter.mesh = mesh;
    }
}

public class MeshHelper
{
    static List<Vector3> vertices;
    static List<Vector3> normals;
    // [... all other vertex data arrays you need]

    static List<int> indices;
    static Dictionary<uint, int> newVectices;

    static int GetNewVertex(int i1, int i2)
    {
        // We have to test both directions since the edge
        // could be reversed in another triangle
        uint t1 = ((uint)i1 << 16) | (uint)i2;
        uint t2 = ((uint)i2 << 16) | (uint)i1;
        if (newVectices.ContainsKey(t2))
            return newVectices[t2];
        if (newVectices.ContainsKey(t1))
            return newVectices[t1];
        // generate vertex:
        int newIndex = vertices.Count;
        newVectices.Add(t1, newIndex);

        // calculate new vertex
        vertices.Add((vertices[i1] + vertices[i2]) * 0.5f);
        normals.Add((normals[i1] + normals[i2]).normalized);
        // [... all other vertex data arrays]

        return newIndex;
    }

    public static void RemoveUnusedVertices(Mesh aMesh)
    {
        Vector3[] vertices = aMesh.vertices;
        Vector3[] normals = aMesh.normals;
        Vector4[] tangents = aMesh.tangents;
        Vector2[] uv = aMesh.uv;
        Vector2[] uv2 = aMesh.uv2;
        List<int> indices = new List<int>();

        Dictionary<int, int> vertMap = new Dictionary<int, int>(vertices.Length);

        List<Vector3> newVerts = new List<Vector3>(vertices.Length);
        List<Vector3> newNormals = new List<Vector3>(vertices.Length);
        List<Vector4> newTangents = new List<Vector4>(vertices.Length);
        List<Vector2> newUV = new List<Vector2>(vertices.Length);
        List<Vector2> newUV2 = new List<Vector2>(vertices.Length);

        System.Func<int, int> remap = (int aIndex) =>
        {
            int res = -1;
            if (!vertMap.TryGetValue(aIndex, out res))
            {
                res = newVerts.Count;
                vertMap.Add(aIndex, res);
                newVerts.Add(vertices[aIndex]);
                if (normals != null && normals.Length > 0)
                    newNormals.Add(normals[aIndex]);
                if (tangents != null && tangents.Length > 0)
                    newTangents.Add(tangents[aIndex]);
                if (uv != null && uv.Length > 0)
                    newUV.Add(uv[aIndex]);
                if (uv2 != null && uv2.Length > 0)
                    newUV2.Add(uv2[aIndex]);
            }

            return res;
        };
        for (int subMeshIndex = 0; subMeshIndex < aMesh.subMeshCount; subMeshIndex++)
        {
            var topology = aMesh.GetTopology(subMeshIndex);
            indices.Clear();
            aMesh.GetIndices(indices, subMeshIndex);
            for (int i = 0; i < indices.Count; i++)
            {
                indices[i] = remap(indices[i]);
            }

            aMesh.SetIndices(indices, topology, subMeshIndex);
        }

        aMesh.SetVertices(newVerts);
        if (newNormals.Count > 0)
            aMesh.SetNormals(newNormals);
        if (newTangents.Count > 0)
            aMesh.SetTangents(newTangents);
        if (newUV.Count > 0)
            aMesh.SetUVs(0, newUV);
        if (newUV2.Count > 0)
            aMesh.SetUVs(1, newUV2);
    }


    public static void Subdivide(Mesh mesh)
    {
        newVectices = new Dictionary<uint, int>();

        vertices = new List<Vector3>(mesh.vertices);
        normals = new List<Vector3>(mesh.normals);
        // [... all other vertex data arrays]
        indices = new List<int>();

        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i1 = triangles[i + 0];
            int i2 = triangles[i + 1];
            int i3 = triangles[i + 2];

            int a = GetNewVertex(i1, i2);
            int b = GetNewVertex(i2, i3);
            int c = GetNewVertex(i3, i1);
            indices.Add(i1);
            indices.Add(a);
            indices.Add(c);
            indices.Add(i2);
            indices.Add(b);
            indices.Add(a);
            indices.Add(i3);
            indices.Add(c);
            indices.Add(b);
            indices.Add(a);
            indices.Add(b);
            indices.Add(c); // center triangle
        }

        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        // [... all other vertex data arrays]
        mesh.triangles = indices.ToArray();

        // since this is a static function and it uses static variables
        // we should erase the arrays to free them:
        newVectices = null;
        vertices = null;
        normals = null;
        // [... all other vertex data arrays]

        indices = null;
    }
}