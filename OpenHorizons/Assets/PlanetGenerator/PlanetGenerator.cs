using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GenerativePlanet
{
    [ExecuteInEditMode]
    public class PlanetGenerator : MonoBehaviour
    {
        [SerializeField]
        private Planet planet;
        
        private void OnValidate()
        {
            planet.Dirty();
        }

        private void Update()
        {
            planet.LazyUpdate(transform);
        }

        private void OnDestroy()
        {
            planet.Cleanup();
        }
    }

    static class PlanetGeneration
    {
        private static readonly int ElevationMinMax = Shader.PropertyToID("_elevationMinMax");
        private static readonly int MainTexture = Shader.PropertyToID("_texture");

        internal static void Cleanup(this Planet planet)
        {
            if (planet.meshFilters != null && !Application.isPlaying)
            {
                for (var i = 0; i < planet.meshFilters.Length; i++)
                {
                    Object.DestroyImmediate(planet.meshFilters[i].gameObject);
                }
            }
        }
        
        internal static void LazyUpdate(this ref Planet planet, Transform transform)
        {
            if (planet.Dirty)
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                planet.Initialize(transform);
                planet.GenerateMesh();
                planet.GenerateColor();
                planet.Dirty = false;
                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed; string elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}"; UnityEngine.Debug.Log("RunTime " + elapsedTime);
            }
        }
        
        internal static void Dirty(this ref Planet planet)
        {
            planet.Dirty = true;
        }
        
        internal static void GenerateColor(this Planet planet)
        {
            planet.ColorGenerator.GenerateColor();
        }
        
        internal static void GenerateMesh(this ref Planet planet)
        {
            for (var i = 0; i < planet.TerrainFaces.Length; i++)
            {
                planet.TerrainFaces[i].ConstructMesh();
            }
            
            planet.ColorGenerator.UpdateElevation(planet.ShapeGenerator.ElevationMinMax);
        }
        
        internal static void Initialize(this ref Planet planet, Transform transform)
        {
            planet.ShapeGenerator = new ShapeGenerator(planet.shapeSettings);
            planet.ColorGenerator = new ColorGenerator(planet.colorSettings);
            if (planet.meshFilters == null || planet.meshFilters.Length == 0)
            {
                planet.meshFilters = new MeshFilter[6];
            }

            planet.TerrainFaces = new TerrainFace[6];

            Vector3[] direction =
                { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

            for (int i = 0; i < 6; i++)
            {
                if (planet.meshFilters[i] == null)
                {
                    GameObject planetObject = new GameObject("mesh");
                    planetObject.transform.parent = transform;
                    planetObject.AddComponent<MeshRenderer>();
                    planet.meshFilters[i] = planetObject.AddComponent<MeshFilter>();
                    planet.meshFilters[i].sharedMesh = new Mesh();
                }

                planet.MeshRenderers[i].sharedMaterial = planet.colorSettings.Material;
                planet.TerrainFaces[i] = new TerrainFace(planet.ShapeGenerator, planet.meshFilters[i].sharedMesh, planet.Resolution, direction[i]);
            }
        }
        
        internal static void GenerateColor(this ColorGenerator colorGenerator)
        {
            colorGenerator.UpdateColors();
        }
        
        internal static void ConstructMesh(this TerrainFace terrainFace)
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

            elevation = shapeGenerator.Settings.PlanetRadius * (1 + elevation);
            shapeGenerator.ElevationMinMax.AddValue(elevation);
            return pointOnUnitSphere * elevation;
        }
        
        internal static void UpdateElevation(this ref ColorGenerator colorGenerator, MinMax elevationMinMax)
        {
            colorGenerator.Settings.Material.SetVector(ElevationMinMax, new Vector4(elevationMinMax.Min, elevationMinMax.Max));
        }

        internal static void UpdateColors(this ref ColorGenerator colorGenerator)
        {
            Color[] colors = new Color[colorGenerator.TextureResolution];
            for (int i = 0; i < colorGenerator.TextureResolution; i++)
            {
                colors[i] = colorGenerator.Settings.PlanetColor.Evaluate(i / (colorGenerator.TextureResolution - 1f));
            }
            colorGenerator.Texture.SetPixels(colors);
            colorGenerator.Texture.Apply();
            colorGenerator.Settings.Material.SetTexture(MainTexture, colorGenerator.Texture);
        }
    }

    [Serializable]
    struct Planet
    {
        [SerializeField, Range(3, 256)] private int resolution;
        public int Resolution
        {
            get { return Mathf.Max(resolution, 3); }
        }

        internal ShapeGenerator ShapeGenerator;
        internal ColorGenerator ColorGenerator;

        [SerializeField, HideInInspector] internal MeshFilter[] meshFilters;
        [SerializeField, HideInInspector] private MeshRenderer[] meshRenderers;

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
        [SerializeField] internal ColorSettings colorSettings;
        [SerializeField] internal ShapeSettings shapeSettings;

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
            Resolution = resolution;
            LocalUp = localUp;
            AxisA = new Vector3(localUp.y, localUp.z, localUp.x);
            AxisB = Vector3.Cross(localUp, AxisA);
        }
    }


    public struct ShapeGenerator
    {
        internal ShapeSettings Settings;
        internal readonly NoiseFilter[] NoiseFilters;
        internal MinMax ElevationMinMax;

        public ShapeGenerator(ShapeSettings settings)
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
    public struct ShapeSettings
    {
        [SerializeField] private float planetRadius;
        public float PlanetRadius => planetRadius;
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

    public struct ColorGenerator
    {
        internal ColorSettings Settings;
        internal Texture2D Texture;
        internal int TextureResolution;

        public ColorGenerator(ColorSettings settings)
        {
            Settings = settings;
            TextureResolution = 50;
            Texture = new Texture2D(TextureResolution, 1);
        }
    }

    [Serializable]
    public struct ColorSettings
    {
        [SerializeField] private Gradient planetColor;
        public Gradient PlanetColor => planetColor;
        [SerializeField] private Material material;

        public Material Material
        {
            get
            {
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
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