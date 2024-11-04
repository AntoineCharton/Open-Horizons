using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;


namespace CelestialBodies.Terrain
{
    public static class SurfaceBuilder
    {
        
        private static readonly int ElevationMinMax = Shader.PropertyToID("_ElevationMinMax");
        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private static readonly int OceanColor = Shader.PropertyToID("_BaseColor");
        
        internal static void Cleanup(this Surface surface)
        {
            if (surface.meshFilters != null && !Application.isPlaying)
            {
                for (var i = 0; i < surface.meshFilters.Length; i++)
                {
                    Object.DestroyImmediate(surface.meshFilters[i].gameObject);
                }
            }
        }
        
        internal static void LazyUpdate(this ref Surface surface, Transform transform)
        {
            if (surface.terrain.Material.mainTexture == null) // When we loose serialization we want to regenerate the texture
            {
                surface.Dirty = true;
            }
            if (surface.Dirty)
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                surface.InitializeSurface(transform);
                surface.GenerateMesh();
                surface.GenerateColor();
                surface.Dirty = false;
                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed; string elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}"; UnityEngine.Debug.Log("RunTime " + elapsedTime);
            }
        }
        
        internal static void Dirty(this ref Surface surface)
        {
            surface.Dirty = true;
        }
        
        internal static void GenerateColor(this Surface surface)
        {
            surface.ColorGenerator.GenerateColor();
        }
        
        internal static void GenerateMesh(this ref Surface surface)
        {
            for (var i = 0; i < surface.TerrainFaces.Length; i++)
            {
                surface.TerrainFaces[i].GeneratePlanet();
            }
            
            surface.ColorGenerator.UpdateElevation(surface.ShapeGenerator.ElevationMinMax);
        }
        
        internal static void InitializeSurface(this ref Surface surface, Transform transform)
        {
            surface.ShapeGenerator = new ShapeGenerator(surface.shape);
            surface.ColorGenerator = new ColorGenerator(surface.terrain);
            if (surface.meshFilters == null || surface.meshFilters.Length == 0)
            {
                surface.meshFilters = new MeshFilter[6];
            }

            surface.TerrainFaces = new TerrainFace[6];

            Vector3[] direction =
                { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

            var initialPosition = transform.position;
            var initialRotation = transform.rotation;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            surface.LazyMeshInitialization(transform);
            transform.position = initialPosition;
            transform.rotation = initialRotation;

            for (int i = 0; i < 6; i++)
            {
                surface.MeshRenderers[i].sharedMaterial = surface.terrain.Material;
                surface.TerrainFaces[i] = new TerrainFace(surface.ShapeGenerator, surface.meshFilters[i].sharedMesh, surface.Resolution, direction[i]);
            }
        }

        internal static void LazyMeshInitialization(this Surface surface, Transform transform)
        {
            for (int i = 0; i < 6; i++)
            {
                if (surface.meshFilters[i] == null)
                {
                    GameObject planetObject = new GameObject("mesh");
                    planetObject.transform.parent = transform;
                    planetObject.AddComponent<MeshRenderer>();
                    surface.meshFilters[i] = planetObject.AddComponent<MeshFilter>();
                    surface.meshFilters[i].sharedMesh = new Mesh();
                }
            }
        }
        
        internal static void GenerateColor(this ColorGenerator colorGenerator)
        {
            colorGenerator.UpdateColors();
        }
        
        internal static void GeneratePlanet(this TerrainFace terrainFace)
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
                    Vector3 pointOnUnitSphere = pointOnUnitCube.normalized;
                    vertices[i] = terrainFace.ShapeGenerator.CalculatePointOnPlanet(pointOnUnitSphere);
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
    struct Surface
    {
        [SerializeField, Range(3, 1024)] private int resolution;

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

        [SerializeField] internal Terrain terrain;
        [SerializeField] internal Shape shape;

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

            if (value < Min)
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