using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CelestialBodies
{
    [ExecuteInEditMode]
    public class Planet : MonoBehaviour
    {
        [SerializeField]
        private PlanetSettings planetSettings;
        
        private void OnValidate()
        {
            planetSettings.Dirty();
        }

        private void Update()
        {
            planetSettings.LazyUpdate(transform);
        }

        private void OnDestroy()
        {
            planetSettings.Cleanup();
        }
    }

    static class PlanetGeneration
    {
        private static readonly int ElevationMinMax = Shader.PropertyToID("_ElevationMinMax");
        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private static readonly int OceanColor = Shader.PropertyToID("_BaseColor");
        private static readonly int PlanetRadius = Shader.PropertyToID("_PlanetRadius");
        private static readonly int AtmosphereRadius = Shader.PropertyToID("_AtmosphereRadius");
        private static readonly int LightColor = Shader.PropertyToID("_LightColor");

        internal static void Cleanup(this PlanetSettings planetSettings)
        {
            if (planetSettings.meshFilters != null && !Application.isPlaying)
            {
                for (var i = 0; i < planetSettings.meshFilters.Length; i++)
                {
                    Object.DestroyImmediate(planetSettings.meshFilters[i].gameObject);
                }
            }
        }
        
        internal static void LazyUpdate(this ref PlanetSettings planetSettings, Transform transform)
        {
            if (planetSettings.terrain.Material.mainTexture == null) // When we loose serialization we want to regenerate the texture
            {
                planetSettings.Dirty = true;
            }
            if (planetSettings.Dirty)
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                planetSettings.Initialize(transform);
                planetSettings.GenerateMesh();
                planetSettings.GenerateColor();
                planetSettings.ConstructAtmosphere(transform);
                planetSettings.Dirty = false;
                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed; string elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}"; UnityEngine.Debug.Log("RunTime " + elapsedTime);
            }
        }
        
        internal static void Dirty(this ref PlanetSettings planetSettings)
        {
            planetSettings.Dirty = true;
        }
        
        internal static void GenerateColor(this PlanetSettings planetSettings)
        {
            planetSettings.ColorGenerator.GenerateColor();
        }
        
        internal static void GenerateMesh(this ref PlanetSettings planetSettings)
        {
            for (var i = 0; i < planetSettings.TerrainFaces.Length; i++)
            {
                planetSettings.TerrainFaces[i].ConstructPlanet();
            }
            
            planetSettings.ColorGenerator.UpdateElevation(planetSettings.ShapeGenerator.ElevationMinMax);
        }
        
        internal static void Initialize(this ref PlanetSettings planetSettings, Transform transform)
        {
            planetSettings.ShapeGenerator = new ShapeGenerator(planetSettings.shape);
            planetSettings.ColorGenerator = new ColorGenerator(planetSettings.terrain);
            if (planetSettings.meshFilters == null || planetSettings.meshFilters.Length == 0)
            {
                planetSettings.meshFilters = new MeshFilter[6];
            }

            planetSettings.TerrainFaces = new TerrainFace[6];

            Vector3[] direction =
                { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

            var initialPosition = transform.position;
            var initialRotation = transform.rotation;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            planetSettings.LazyMeshInitialization(transform);
            transform.position = initialPosition;
            transform.rotation = initialRotation;

            for (int i = 0; i < 6; i++)
            {
                planetSettings.MeshRenderers[i].sharedMaterial = planetSettings.terrain.Material;
                planetSettings.TerrainFaces[i] = new TerrainFace(planetSettings.ShapeGenerator, planetSettings.meshFilters[i].sharedMesh, planetSettings.Resolution, direction[i]);
            }
        }

        internal static void LazyMeshInitialization(this PlanetSettings planetSettings, Transform transform)
        {
            for (int i = 0; i < 6; i++)
            {
                if (planetSettings.meshFilters[i] == null)
                {
                    GameObject planetObject = new GameObject("mesh");
                    planetObject.transform.parent = transform;
                    planetObject.AddComponent<MeshRenderer>();
                    planetSettings.meshFilters[i] = planetObject.AddComponent<MeshFilter>();
                    planetSettings.meshFilters[i].sharedMesh = new Mesh();
                }
            }
        }
        
        internal static void GenerateColor(this ColorGenerator colorGenerator)
        {
            colorGenerator.UpdateColors();
        }
        
        internal static void ConstructPlanet(this TerrainFace terrainFace)
        {
            Vector3[] vertices = new Vector3[terrainFace.Resolution * terrainFace.Resolution];
            int[] triangles = new int[(terrainFace.Resolution - 1) * (terrainFace.Resolution - 1) * 6];
           
            Parallel.For(0, terrainFace.Resolution, y =>
            {
                int triIndex = y * (terrainFace.Resolution - 1) * 6;
                for (int x = 0; x < terrainFace.Resolution; x++)
                {
                    int i = x + y * terrainFace.Resolution;
                    Vector2 percent = new Vector2(x, y) / (terrainFace.Resolution - 1);
                    Vector3 pointOnUnitCube = terrainFace.LocalUp + (percent.x - 0.5f) * 2 * terrainFace.AxisA +
                                              (percent.y - 0.5f) * 2 * terrainFace.AxisB;
                    Vector3 pointOnUnitSphere = pointOnUnitCube.normalized;
                    vertices[i] = terrainFace.ShapeGenerator.CalculatePointOnPlanet(pointOnUnitSphere);
                    if (x != terrainFace.Resolution - 1 && y != terrainFace.Resolution - 1)
                    {
                        triangles[triIndex] = i;
                        triangles[triIndex + 1] = i + terrainFace.Resolution + 1;
                        triangles[triIndex + 2] = i + terrainFace.Resolution;

                        triangles[triIndex + 3] = i;
                        triangles[triIndex + 4] = i + 1;
                        triangles[triIndex + 5] = i + terrainFace.Resolution + 1;
                        triIndex += 6;
                    }
                }
            });

            terrainFace.Mesh.Clear();
            terrainFace.Mesh.vertices = vertices;
            terrainFace.Mesh.triangles = triangles;
            terrainFace.Mesh.RecalculateNormals();
        }

        internal static void ConstructAtmosphere(this ref PlanetSettings planetSettings, Transform parent)
        {
            if (!planetSettings.atmosphere.HasAtmosphere)
            {
                if (planetSettings.atmosphereMeshFilter != null)
                    Object.DestroyImmediate(planetSettings.atmosphereMeshFilter.gameObject);
                
                planetSettings.atmosphereMaterial = null;
                return;
            }
            
            if (planetSettings.atmosphereMeshRenderer == null)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.parent = parent;
                planetSettings.atmosphereMeshFilter = sphere.GetComponent<MeshFilter>();
                planetSettings.atmosphereMeshRenderer = sphere.GetComponent<MeshRenderer>();
                
            }
            
            if (planetSettings.atmosphereMaterial == null)
            {
                var material = new Material(Shader.Find("Atmosphere/Atmospheric Scattering"));
                planetSettings.atmosphereMeshRenderer.material = material;
                planetSettings.atmosphereMaterial = material;
            }

            var sharedMesh = planetSettings.atmosphereMeshFilter.sharedMesh;
            var vertices = sharedMesh.vertices;
            var triangles = sharedMesh.triangles;
            var mesh = sharedMesh;
            
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Normalize(vertices[i]);
                vertices[i] *= (planetSettings.shape.Radius + planetSettings.atmosphere.Size);
            }
            planetSettings.atmosphereMaterial.SetFloat(PlanetRadius, planetSettings.shape.Radius);
            planetSettings.atmosphereMaterial.SetFloat(AtmosphereRadius, planetSettings.shape.Radius + planetSettings.atmosphere.Size);
            planetSettings.atmosphereMaterial.SetColor(LightColor, planetSettings.atmosphere.Color);
           
            
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
        }

        internal static float EvaluateRigidNoise(this NoiseSettings noiseSettings, Noise noise, Vector3 point)
        {
            var noiseValue = 0f;
            var frequency = noiseSettings.BaseRoughness;
            var amplitude = 1f;
            var weight = 1f;

            for (int i = 0; i < noiseSettings.LayersCount; i++)
            {
                var v = 1 - Mathf.Abs(noise.Evaluate(point * frequency + noiseSettings.Center));
                v *= v;
                v *= weight;
                weight = Mathf.Clamp01(v * noiseSettings.WeightMultiplier);
                noiseValue += v * amplitude;
                frequency *= noiseSettings.Roughness;
                amplitude *= noiseSettings.Persistence;
            }

            noiseValue = Mathf.Max(0, noiseValue - noiseSettings.MinValue);
            return noiseValue * noiseSettings.Strength;
        }

        internal static float EvaluateSimpleNoise(NoiseSettings noiseSettings, Noise noise, Vector3 point)
        {
            float noiseValue = 0;
            float frequency = noiseSettings.BaseRoughness;
            float amplitude = 1;
            for (int i = 0; i < noiseSettings.LayersCount; i++)
            {
                float v = noise.Evaluate(point * frequency + noiseSettings.Center);
                noiseValue += (v + 1) * 0.5f * amplitude;
                frequency *= noiseSettings.Roughness;
                amplitude *= noiseSettings.Persistence;
            }

            noiseValue = Mathf.Max(0, noiseValue - noiseSettings.MinValue);
            return noiseValue * noiseSettings.Strength;
        }

        internal static float Evaluate(this NoiseFilter noiseFilter, Vector3 position)
        {
            switch (noiseFilter.NoiseSettings.Filter)
            {
                case NoiseSettings.FilterType.Simple:
                    return EvaluateSimpleNoise(noiseFilter.NoiseSettings, noiseFilter.Noise, position);
                case NoiseSettings.FilterType.Rigid:
                    return EvaluateRigidNoise(noiseFilter.NoiseSettings, noiseFilter.Noise, position);
                default:
                    return -100000000;
            }
        }

        internal static Vector3 CalculatePointOnPlanet(this ref ShapeGenerator shapeGenerator, Vector3 pointOnUnitSphere)
        {
            float firstLayerValue = 0;
            float elevation = 0;

            if (shapeGenerator.NoiseFilters.Length > 0)
            {
                firstLayerValue = shapeGenerator.NoiseFilters[0].Evaluate(pointOnUnitSphere);
                if (shapeGenerator.Settings.NoiseSettings[0].Enabled)
                {
                    elevation = firstLayerValue;
                }
            }

            for (int i = 1; i < shapeGenerator.NoiseFilters.Length; i++)
            {
                if (shapeGenerator.Settings.NoiseSettings[i].Enabled)
                {
                    float mask = (shapeGenerator.Settings.NoiseSettings[i].UseFirstLayerAsMask) ? firstLayerValue : 1;
                    elevation += shapeGenerator.NoiseFilters[i].Evaluate(pointOnUnitSphere) * mask;
                }
            }

            elevation = shapeGenerator.Settings.Radius * (1 + elevation);
            shapeGenerator.ElevationMinMax.AddValue(elevation);
            return pointOnUnitSphere * elevation;
        }
        
        internal static void UpdateElevation(this ref ColorGenerator colorGenerator, MinMax elevationMinMax)
        {
            colorGenerator.Settings.Material.SetVector(ElevationMinMax, new Vector4(elevationMinMax.Min, elevationMinMax.Max));
            colorGenerator.Settings.Material.SetColor(OceanColor, colorGenerator.Settings.OceanColor);
        }

        internal static void UpdateColors(this ref ColorGenerator colorGenerator)
        {
            Color[] colors = new Color[colorGenerator.TextureResolution];
            for (int i = 0; i < colorGenerator.TextureResolution; i++)
            {
                colors[i] = colorGenerator.Settings.Color.Evaluate(i / (colorGenerator.TextureResolution - 1f));
            }
            colorGenerator.Texture.SetPixels(colors);
            colorGenerator.Texture.Apply();
            colorGenerator.Settings.Material.SetTexture(MainTexture, colorGenerator.Texture);
        }
    }

    [Serializable]
    struct PlanetSettings
    {
        [SerializeField, Range(3, 1024)] private int resolution;
        public int Resolution
        {
            get { return Mathf.Max(resolution, 3); }
        }
        
        internal ShapeGenerator ShapeGenerator;
        internal ColorGenerator ColorGenerator;

        [SerializeField] internal MeshFilter[] meshFilters;
        [SerializeField] private MeshRenderer[] meshRenderers;
        [SerializeField, HideInInspector] internal MeshFilter atmosphereMeshFilter;
        [SerializeField, HideInInspector] internal MeshRenderer atmosphereMeshRenderer;
        [SerializeField, HideInInspector] internal Material atmosphereMaterial;

        public MeshRenderer[] MeshRenderers
        {
            get
            {
                if (!(meshRenderers != null && meshRenderers.Length != 0))
                {
                    meshRenderers = new MeshRenderer[meshFilters.Length];
                    for (var i = 0; i < meshFilters.Length; i++)
                    {
                        meshRenderers[i] = meshFilters[i].GetComponent<MeshRenderer>();
                    }
                }
                
                return meshRenderers;
            }
        }
        [SerializeField] internal Terrain terrain;
        [SerializeField] internal Shape shape;
        [SerializeField] internal Atmosphere atmosphere;

        internal TerrainFace[] TerrainFaces;

        internal bool Dirty;
    }

    struct TerrainFace
    {
        internal ShapeGenerator ShapeGenerator;
        internal readonly Mesh Mesh;
        internal readonly int Resolution;
        internal readonly Vector3 LocalUp;
        internal readonly Vector3 AxisA;
        internal readonly Vector3 AxisB;

        public TerrainFace(ShapeGenerator shapeGenerator, Mesh mesh, int resolution, Vector3 localUp)
        {
            ShapeGenerator = shapeGenerator;
            Mesh = mesh;
            Mesh.indexFormat = IndexFormat.UInt32;
            Resolution = resolution;
            LocalUp = localUp;
            AxisA = new Vector3(localUp.y, localUp.z, localUp.x);
            AxisB = Vector3.Cross(localUp, AxisA);
        }
    }


    public struct ShapeGenerator
    {
        internal Shape Settings;
        internal readonly NoiseFilter[] NoiseFilters;
        internal MinMax ElevationMinMax;

        public ShapeGenerator(Shape settings)
        {
            Settings = settings;
            NoiseFilters = new NoiseFilter[settings.NoiseSettings.Length];
            for (int i = 0; i < NoiseFilters.Length; i++)
            {
                NoiseFilters[i] = new NoiseFilter(settings.NoiseSettings[i].NoiseSettings);
            }

            ElevationMinMax = new MinMax();
        }
    }

    [Serializable]
    public struct Shape
    {
        [SerializeField] private float radius;
        public float Radius => radius;
        [SerializeField] private NoiseLayer[] noiseSettings;

        public NoiseLayer[] NoiseSettings
        {
            get
            {
                if (noiseSettings == null)
                    noiseSettings = new NoiseLayer[0];
                return noiseSettings;
            }
        }
    }

    [Serializable]
    public struct NoiseLayer
    {
        [SerializeField] private bool enabled;
        public bool Enabled => enabled;

        [SerializeField] private bool useFirstLayerAsMask;
        public bool UseFirstLayerAsMask => useFirstLayerAsMask;

        [SerializeField] private NoiseSettings noiseSettings;
        public NoiseSettings NoiseSettings => noiseSettings;
    }

    public class MinMax
    {
        internal float Min { get; private set; }
        internal float Max { get; private set; }

        public MinMax()
        {
            Min = float.MaxValue;
            Max = float.MinValue;
        }

        internal void AddValue(float value)
        {
            if (value > Max)
            {
                Max = value;
            }
            if(value < Min)
            {
                Min = value;
            }
        }
    }

    [Serializable]
    public struct NoiseSettings
    {
        public enum FilterType
        {
            Simple,
            Rigid
        }

        [SerializeField] private FilterType filter;
        public FilterType Filter => filter;

        [SerializeField] private float strength;
        public float Strength => strength;
        [Range(1, 8)] [SerializeField] private int layersCount;

        public int LayersCount
        {
            get { return Mathf.Max(layersCount, 1); }
        }

        [SerializeField] private float baseRoughness;
        public float BaseRoughness => baseRoughness;
        [SerializeField] private float roughness;
        public float Roughness => roughness;
        [SerializeField] private float persistence;
        public float Persistence => persistence;
        [SerializeField] private Vector3 center;
        public Vector3 Center => center;
        [SerializeField] private float minValue;
        public float MinValue => minValue;
        [SerializeField] private float weightMultiplier;
        public float WeightMultiplier => weightMultiplier;
    }
    
    [Serializable]
    public struct Atmosphere
    {
        [SerializeField] private bool hasAtmosphere;
        public bool HasAtmosphere => hasAtmosphere;
        [SerializeField] private float size;
        public float Size => size;
        [SerializeField] private Color color;
        public Color Color => color;
    }
    
    public struct ColorGenerator
    {
        internal Terrain Settings;
        internal Texture2D Texture;
        internal int TextureResolution;

        public ColorGenerator(Terrain settings)
        {
            Settings = settings;
            TextureResolution = 50;
            Texture = new Texture2D(TextureResolution, 1);
        }
    }

    [Serializable]
    public struct Terrain
    {
        [SerializeField] private Gradient color;
        public Gradient Color
        {
            get
            {
                if (color == null)
                    color = new Gradient();
                return color;
            }
        }

        [SerializeField, HideInInspector] private Material material;
        [SerializeField] private Color oceanColor;
        public Color OceanColor => oceanColor;

        public Material Material
        {
            get
            {
                if (material == null)
                {
                    material = new Material(Shader.Find("Shader Graphs/Planet"));
                }
                return material;
            }
        }
    }

    public struct NoiseFilter
    {
        internal NoiseSettings NoiseSettings;
        internal readonly Noise Noise;

        public NoiseFilter(NoiseSettings settings)
        {
            Noise = new Noise();
            NoiseSettings = settings;
        }
    }
}

//Original code https://github.com/SebLague/Geographical-Adventures
//
//MIT License
//
//Copyright (c) 2024 Sebastian Lague
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.