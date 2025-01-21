using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;


namespace CelestialBodies.Terrain
{
    public static class SurfaceBuilder
    {
        
        private static readonly int ElevationMinMax = Shader.PropertyToID("_ElevationMinMax");
        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private static readonly int GroundTex = Shader.PropertyToID("_GroundTex");
        private static readonly int GroundTexBlend = Shader.PropertyToID("_GroundTexBlend");
        private static readonly int GroundNorm = Shader.PropertyToID("_GroundNorm");
        private static readonly int GroundSmooth = Shader.PropertyToID("_GroundSmooth");
        private static readonly int StepThreshold = Shader.PropertyToID("_StepThreshold");
        private static readonly int StepColor = Shader.PropertyToID("_StepTint");
        private static readonly int TextureStep = Shader.PropertyToID("_TextureStep");
        private static readonly int TextureNormalStep = Shader.PropertyToID("_TextureNormalStep");
        private const int highDefinitionFaces = 4; 

        internal static void Cleanup(this Terrain terrain)
        {
            if(terrain.Surface.UpdateAsync)
                terrain.Surface.TerrainMeshThreadCalculation.TerminateThread();
            
            if (terrain.Surface.meshFilters != null && !terrain.Surface.UpdateAsync)
            {
                for (var i = 0; i < terrain.Surface.meshFilters.Length; i++)
                {
                    Object.DestroyImmediate(terrain.Surface.meshFilters[i].gameObject);
                }
            }
        }

        internal static Bounds GetBounds(this Surface surface, Transform transform)
        {
            Quaternion currentRotation = transform.rotation;
            transform.rotation = Quaternion.Euler(0f,0f,0f);
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            if (surface.MeshRenderers != null)
            {
                foreach (Renderer renderer in surface.MeshRenderers)
                {
                    if (renderer != null)
                        bounds.Encapsulate(renderer.bounds);
                }
            }

            Vector3 localCenter = bounds.center - transform.position;
            bounds.center = localCenter;
            transform.rotation = currentRotation;
            return bounds;
        }

        private static void CleanUpMeshes(this ref Surface surface)
        {
            if (surface.MeshRenderers != null)
            {
                for (var i = 0; i < surface.MeshRenderers.Length; i++)
                {
                    if (surface.MeshRenderers[i] != null)
                    {
                        if (Application.isPlaying)
                            Object.Destroy(surface.MeshRenderers[i].gameObject);
                        else
                        {
                            Object.DestroyImmediate(surface.MeshRenderers[i].gameObject);
                        }
                    }
                }
            }

            surface.ResetRenderers();
            surface.TerrainFaces = null;
        }

        internal static void UpdateMeshResolution(this ref Terrain terrain)
        {
            if(terrain.Surface.AllFacesGenerated == false)
                return;
            
            var closestFaces = new int[highDefinitionFaces];
            var closestDistances = new float[highDefinitionFaces];
            Array.Fill(closestDistances, float.MaxValue);
            Array.Fill(closestFaces, -1);
            var cameraPosition = Camera.main.transform.position;
            for (var i = 0; i < terrain.Surface.TerrainFaces.Length; i++)
            {
                var currentDistance = Vector3.Distance(cameraPosition, terrain.Surface.MeshRenderers[i].bounds.center);
                // Check if the current number is smaller than the largest number in resultArray
                for (int j = 0; j < highDefinitionFaces; j++)
                {
                    if (currentDistance < closestDistances[j])
                    {
                        // Shift the larger numbers to the right to make room
                        for (int k = 3; k > j; k--)
                        {
                            closestDistances[k] = closestDistances[k - 1];
                            closestFaces[k] = closestFaces[k - 1];
                        }
                        closestDistances[j] = currentDistance;
                        closestFaces[j] = i;
                        break;
                    }
                }
            }

            var newSurface = terrain.Surface;
            if (IsClosestFacesChanged(ref newSurface) && !terrain.Surface.DirtyFaceResolution)
            {
                newSurface.PreviousClosestFaces = terrain.Surface.ClosestFaces;
                newSurface.ClosestFaces = closestFaces;
                newSurface.DirtySurfaceResolution();
            }
            terrain.SetSurface(newSurface);
            
            bool IsClosestFacesChanged(ref Surface surface)
            {
                for (var i = 0; i < surface.ClosestFaces.Length; i++)
                {
                    if (surface.ClosestFaces[i] != closestFaces[i])
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private static void StartTerrainThread(this ref Surface surface)
        {
            var meshThreadCalculation = new TerrainMeshThreadCalculation();
            surface.TerrainMeshThreadCalculation = meshThreadCalculation;
            Thread thread = new Thread(() =>
            {
                meshThreadCalculation.MeshThread();
            });
            thread.Name = "Terrain Mesh Calculation";
            thread.Start();
        }

        internal static void SmartUpdate(this ref Terrain terrain, Transform transform, ref Trees trees, TerrainGrass terrainGrass)
        {
            var updatedSurface = terrain.Surface;
            if (terrain.Material.mainTexture == null || updatedSurface.TerrainFaces == null) // When we loose serialization we want to regenerate the texture
            { 
                updatedSurface.Dirty = true;
            }
            
            if (updatedSurface.Dirty)
            {
                if (updatedSurface.UpdateAsync && updatedSurface.TerrainMeshThreadCalculation == null)
                {
                    updatedSurface.CleanUpMeshes();
                    updatedSurface.StartTerrainThread();
                }

                if (updatedSurface.CurrentSurface == 0 || updatedSurface.TerrainFaces == null)
                {
                    updatedSurface.InitializeSurface(transform);
                    terrain.InitializeMaterial(ref terrain.ColorGenerator, updatedSurface);
                }

                var terrainSurfaceValue = terrain;
                var calculatedSurface = updatedSurface.GenerateMesh(max => terrainSurfaceValue.UpdateElevation(max), updatedSurface.TerrainMeshThreadCalculation);
                updatedSurface.GenerateColor(terrain);
                if(updatedSurface.UpdateAsync)
                    updatedSurface.Dirty = !calculatedSurface;
                else
                {
                    updatedSurface.AllFacesGenerated = true;
                    updatedSurface.Dirty = false;
                }
            } else if (terrain.Surface.DirtyFaceResolution)
            {
                updatedSurface.GenerateMeshLod(updatedSurface.TerrainMeshThreadCalculation);
            }
            updatedSurface.AssignMesh(ref trees, terrainGrass);
            terrain.SetSurface(updatedSurface);
        }
        
        internal static void SmartUpdate(this ref Ocean oceanFaces, Transform transform)
        {
            var newSurface = oceanFaces.surface;
            if (newSurface.TerrainFaces == null) // When we loose serialization we want to regenerate the texture
            { 
                newSurface.Dirty = true;
            }
            
            if (newSurface.Dirty)
            {
                if (newSurface.UpdateAsync && newSurface.TerrainMeshThreadCalculation == null)
                {
                    newSurface.CleanUpMeshes();
                    newSurface.StartTerrainThread();
                }

                if (newSurface.CurrentSurface == 0 || newSurface.TerrainFaces == null)
                {
                    newSurface.InitializeSurface(transform);
                    oceanFaces.InitializeMaterial(newSurface);
                }

                var calculatedSurface = newSurface.GenerateMesh(max => { }, newSurface.TerrainMeshThreadCalculation);
                //newSurface.GenerateColor(oceanFaces);
                if(newSurface.UpdateAsync)
                    newSurface.Dirty = !calculatedSurface;
                else
                {
                    newSurface.AllFacesGenerated = true;
                    newSurface.Dirty = false;
                }
            } else if (oceanFaces.surface.DirtyFaceResolution)
            {
                newSurface.GenerateMeshLod(newSurface.TerrainMeshThreadCalculation);
            }

            var disabledDetails = Trees.Default();
            newSurface.AssignMesh(ref disabledDetails);
            oceanFaces.SetSurface(newSurface);
        }

        internal static void Dirty(this ref Terrain terrain)
        {
            var newSurface = terrain.Surface;
            newSurface.Dirty();
            terrain.SetSurface(newSurface);
        }
        
        internal static void Dirty(this ref Ocean terrainSurface)
        {
            var newSurface = terrainSurface.surface;
            newSurface.Dirty();
            terrainSurface.SetSurface(newSurface);
        }
        
        internal static void Dirty(this ref Surface surface)
        {
            surface.Dirty = true;
            surface.CurrentSurface = 0;
        }
        
        private static void DirtySurfaceResolution(this ref Surface surface)
        {
            surface.DirtyFaceResolution = true;
            surface.CurrentSurface = surface.ClosestFaces[0];
            surface.CurrentClosestFaceUpdate = 0;
        }
        
        private static void GenerateColor(this Surface surface, Terrain terrain)
        {
            terrain.GenerateColor();
        }

        private static void GenerateMeshLod(this ref Surface surface, TerrainMeshThreadCalculation terrainCalculator)
        {
            if(surface.CurrentPreviousClosestFaceUpdate < surface.PreviousClosestFaces.Length)
            {
                var keepHighResolution = false;
                for (var i = 0; i < surface.ClosestFaces.Length; i++)
                {
                    if (surface.ClosestFaces[i] ==
                        surface.PreviousClosestFaces[surface.CurrentPreviousClosestFaceUpdate])
                        keepHighResolution = true;
                }

                if (!keepHighResolution)
                {
                    surface.CurrentSurface = surface.PreviousClosestFaces[surface.CurrentPreviousClosestFaceUpdate];
                    if (surface.TerrainFaces[surface.CurrentSurface].GeneratePlanet(terrainCalculator, false, surface.UpdateAsync))
                        surface.CurrentPreviousClosestFaceUpdate++;
                }
                else
                {
                    surface.CurrentPreviousClosestFaceUpdate++;
                }
            }
            else if (surface.CurrentClosestFaceUpdate < surface.ClosestFaces.Length)
            {
                surface.CurrentSurface = surface.ClosestFaces[surface.CurrentClosestFaceUpdate];
                if (surface.TerrainFaces[surface.CurrentSurface].GeneratePlanet(terrainCalculator, true, surface.UpdateAsync))
                    surface.CurrentClosestFaceUpdate++;
            }
            else
            {
                surface.DirtyFaceResolution = false;
                surface.CurrentClosestFaceUpdate = 0;
                surface.CurrentPreviousClosestFaceUpdate = 0;
            }
        }
        
        private static bool GenerateMesh(this ref Surface surface, Action<MinMax> UpdateElevation, TerrainMeshThreadCalculation terrainCalculator)
        {
            if (surface.CurrentSurface == -1)
                return false;
            
            var iterations = 1; // In play mode we only want to do one face at a time to save perfs.
            if (!surface.UpdateAsync)
            {
                iterations = surface.TerrainFaces.Length;
                surface.CurrentSurface = 0;
            }
            
            for (var i = 0; i < iterations; i++)
            {
                if (surface.CurrentSurface > surface.TerrainFaces.Length - 1)
                {
                    surface.AllFacesGenerated = true;
                    surface.CurrentSurface = -1;
                    return true;
                }

                if (!surface.TerrainFaces[surface.CurrentSurface].GeneratePlanet(terrainCalculator, false, surface.UpdateAsync))
                    return false;
                
                surface.CurrentSurface++;
                UpdateElevation.Invoke(surface.ShapeGenerator.ElevationMinMax);
                //colorGenerator.UpdateElevation(surface.ShapeGenerator.ElevationMinMax);
            }

            return false;
        }
        
        private static void AssignMesh(this ref Surface surface, ref Trees trees, TerrainGrass terrainGrass = null)
        {
            for (var i = 0; i < surface.TerrainFaces.Length; i++)
            {
                if (surface.TerrainFaces[i].Mesh == null)
                    Debug.Log("Mesh Shouldn't be null");
                else
                {
                    if (!surface.TerrainFaces[i].TerrainMeshData.IsGeneratingVertex &&
                        surface.TerrainFaces[i].TerrainMeshData.IsDoneGeneratingVertex)
                    {
                        
                        surface.TerrainFaces[i].Mesh.Clear();
                        surface.TerrainFaces[i].Mesh.vertices = surface.TerrainFaces[i].TerrainMeshData.Vertices;
                        surface.TerrainFaces[i].Mesh.triangles = surface.TerrainFaces[i].TerrainMeshData.Triangles;
                        surface.TerrainFaces[i].Mesh.normals = surface.TerrainFaces[i].TerrainMeshData.Normals;
                        surface.TerrainFaces[i].Mesh.colors = surface.TerrainFaces[i].TerrainMeshData.VertexColor;
                        var terrainDetailReady = true;
                        var meshDetailReady = true;
                        if (surface.TerrainFaces[i].TerrainMeshData.IsHighDefinition && surface.UpdateAsync)
                        {
                            if (trees.isEnabled)
                                terrainDetailReady = trees.HighDefinition(
                                    surface.TerrainFaces[i].TerrainMeshData.Vertices,
                                    surface.TerrainFaces[i].TerrainMeshData.VertexColor,
                                    i,
                                    surface.TerrainFaces[i].StepThreshold,
                                    surface.ShapeGenerator.ElevationMinMax);

                            if (terrainGrass != null)
                                meshDetailReady = terrainGrass.HighDefinition(i,
                                    surface.TerrainFaces[i].TerrainMeshData.Vertices,
                                    surface.TerrainFaces[i].TerrainMeshData.Triangles,
                                    surface.TerrainFaces[i].TerrainMeshData.Normals,
                                    surface.TerrainFaces[i].TerrainMeshData.VertexColor,
                                    surface.TerrainFaces[i].StepThreshold,
                                    surface.ShapeGenerator.ElevationMinMax);
                            
                            if (terrainDetailReady && meshDetailReady)
                            {
                                surface.TerrainFaces[i].TerrainMeshData.IsDoneGeneratingVertex = false;
                            }
                            
                        }
                        else if(surface.UpdateAsync)
                        {
                            
                            if (trees.isEnabled)
                                terrainDetailReady = trees.LowDefinition(i);

                            
                            if (terrainGrass != null)
                                meshDetailReady = terrainGrass.LowDefinition(i);
                            
                            
                            if (terrainDetailReady && meshDetailReady)
                            {
                                surface.TerrainFaces[i].TerrainMeshData.IsDoneGeneratingVertex = false;
                            }
                        }
                        else
                        {
                            surface.TerrainFaces[i].TerrainMeshData.IsDoneGeneratingVertex = false;
                        }
                        if(surface.UpdateAsync)
                            return; //We only want to do that once per frame to avoid lags
                    }
                }
            }
        }
        
        private static void InitializeMaterial(this ref Ocean terrainSurface, Surface surface)
        {
            var subdivision = CapSubdivision(surface.subdivisions);
            for (int i = 0; i < 6; i++)
            {
                var tilesNumber = (subdivision * subdivision);
                for (int j = 0; j < tilesNumber; j++)
                {
                    var id = (i * tilesNumber) + j;
                    surface.MeshRenderers[id].sharedMaterial = terrainSurface.oceanMaterial;
                }
            }
        }

        private static void InitializeMaterial(this ref Terrain terrain, ref ColorGenerator colorGenerator, Surface surface)
        {
            colorGenerator = new ColorGenerator(terrain);
            var subdivision = CapSubdivision(surface.subdivisions);
            for (int i = 0; i < 6; i++)
            {
                var tilesNumber = (subdivision * subdivision);
                for (int j = 0; j < tilesNumber; j++)
                {
                    var id = (i * tilesNumber) + j;
                    surface.MeshRenderers[id].sharedMaterial = terrain.Material;
                }
            }
        }

        static int CapSubdivision(int defaultSubdivision)
        {
            if (!Application.isPlaying)
            {
                return Mathf.Min(4, defaultSubdivision);
            }

            return defaultSubdivision;
        }
        
        private static void InitializeSurface(this ref Surface surface, Transform transform)
        {
            surface.ShapeGenerator = new ShapeGenerator(surface.shape);
            surface.ClosestFaces = new int[highDefinitionFaces];
            surface.PreviousClosestFaces = new int[highDefinitionFaces];
            var subdivision = CapSubdivision(surface.subdivisions);
            var subdivisionCount = 6 * (subdivision * subdivision);
            if (surface.meshFilters == null || surface.meshFilters.Length == 0 || surface.meshFilters.Length != subdivisionCount)
            {
                surface.CleanUpMeshes();
                surface.meshFilters = new MeshFilter[subdivisionCount];
            }

            if (surface.MeshRenderers == null || surface.MeshRenderers.Length == 0)
            {
                surface.MeshRenderers = new MeshRenderer[subdivisionCount];
            }
            
            surface.TerrainFaces = new TerrainFace[subdivisionCount];

            Vector3[] direction = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

            var initialPosition = transform.position;
            var initialRotation = transform.rotation;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            surface.LazyMeshInitialization(transform);
            transform.position = initialPosition;
            transform.rotation = initialRotation;

            for (int i = 0; i < 6; i++)
            {
                var tilesNumber = (subdivision * subdivision);
                for (int j = 0; j < tilesNumber; j++)
                {
                    var id = (i * tilesNumber) + j;
                    int lineID = j / subdivision;
                    int rowID = j % subdivision;
                    surface.TerrainFaces[id] = new TerrainFace(surface.ShapeGenerator, surface.meshFilters[id].sharedMesh,
                        surface.minResolution, surface.highResolution,rowID, lineID, subdivision, surface.stepThreshold, direction[i]);
                }
            }
            
            Debug.Log("Initialize");
        }

        private static void LazyMeshInitialization(this Surface surface, Transform transform)
        {
            var subdivision = surface.subdivisions;
            if(!Application.isPlaying)
                subdivision = Mathf.Min(subdivision, 4);
            for (int i = 0; i < 6 * (subdivision * subdivision); i++)
            {
                if (surface.meshFilters[i] == null)
                {
                    GameObject planetObject = new GameObject("mesh");
                    planetObject.transform.parent = transform;
                    surface.MeshRenderers[i] = planetObject.AddComponent<MeshRenderer>();
                    surface.meshFilters[i] = planetObject.AddComponent<MeshFilter>();
                    surface.meshFilters[i].sharedMesh = new Mesh();
                }
            }
        }
        
        private static void GenerateColor(this Terrain colorGenerator)
        {
            colorGenerator.UpdateColors();
        }
        
        private static bool GeneratePlanet(this ref TerrainFace terrainFace, TerrainMeshThreadCalculation terrainCalculator, bool isHighResolution, bool isParallel)
        {
            var resolution = terrainFace.MinResolution;
            if (isHighResolution)
                resolution = terrainFace.HighResolution;

            if (!Application.isPlaying)
            {
                resolution = Mathf.Min(resolution, 32);
            }

            terrainFace.TerrainMeshData.IsHighDefinition = isHighResolution;
            
            if (isParallel)
            {
                Profiler.BeginSample("Update Planet Mesh");
                if (!terrainCalculator.SubmitCalculation(terrainFace, isHighResolution))
                    return false;
                Profiler.EndSample();
            }
            else
            {
                CalculateFaceParallel(terrainFace, resolution);
            }

            return true;
        }

        private static void CalculateFace(ref TerrainFace terrainFace, int resolution, int y)
        {
            
            int triIndex = y * (resolution - 1) * 6;
            for (int x = 0; x < resolution; x++)
            {
                int i = x + y * resolution;
                var sizeX = ((resolution - 1) * terrainFace.NumberOfSubdivisions);
                var sizeY = ((resolution - 1) * terrainFace.NumberOfSubdivisions);
                Vector2 percent = new Vector2(((float)x / sizeX), ((float)y / sizeY));
                Vector3 pointOnUnitCube = Vector3.zero;
                var line = 0.5f - (((0.5f / terrainFace.NumberOfSubdivisions) * terrainFace.LineID)*2);
                var row = 0.5f - (((0.5f / terrainFace.NumberOfSubdivisions) * terrainFace.RowID)*2);
                pointOnUnitCube = terrainFace.LocalUp + (percent.x - line) * 2 * terrainFace.AxisA +
                                  (percent.y - row) * 2 * terrainFace.AxisB;
                    
                if (x != resolution - 1 && y != resolution - 1)
                {
                    terrainFace.TerrainMeshData.Triangles[triIndex] = i;
                    terrainFace.TerrainMeshData.Triangles[triIndex + 1] = i + resolution + 1;
                    terrainFace.TerrainMeshData.Triangles[triIndex + 2] = i + resolution;

                    terrainFace.TerrainMeshData.Triangles[triIndex + 3] = i;
                    terrainFace.TerrainMeshData.Triangles[triIndex + 4] = i + 1;
                    terrainFace.TerrainMeshData.Triangles[triIndex + 5] = i + resolution + 1;
                    
                    triIndex += 6;
                }
                Vector3 pointOnUnitSphere = PointOnCubeToPointOnSphere(pointOnUnitCube);
                terrainFace.TerrainMeshData.Vertices[i] = terrainFace.ShapeGenerator.CalculatePointOnPlanet(pointOnUnitSphere);
            }

            static Vector3 PointOnCubeToPointOnSphere(Vector3 p)
            {
                float x2 = p.x * p.x;
                float y2 = p.y * p.y;
                float z2 = p.z * p.z;
                float x = p.x * (float) Math.Sqrt(1 - (y2 + z2) / 2 + (y2 * z2) / 3);
                float y = p.y * (float)  Math.Sqrt(1 - (z2 + x2) / 2 + (z2 * x2) / 3);
                float z = p.z * (float)  Math.Sqrt(1 - (x2 + y2) / 2 + (x2 * y2) / 3);
                return new Vector3(x, y, z);

            }
        }

        private static void InitializeFace(ref TerrainFace terrainFace, int resolution)
        {
            if (terrainFace.TerrainMeshData.Vertices == null ||
                terrainFace.TerrainMeshData.Vertices.Length != resolution * resolution)
            {
                terrainFace.TerrainMeshData.Vertices = new Vector3[resolution * resolution];
                terrainFace.TerrainMeshData.Normals = new Vector3[resolution * resolution];
                terrainFace.TerrainMeshData.Triangles = new int[(resolution - 1) * (resolution - 1) * 6];
                terrainFace.TerrainMeshData.VertexColor = new Color[resolution * resolution];
            }

            terrainFace.TerrainMeshData.IsGeneratingVertex = true;
            terrainFace.TerrainMeshData.IsDoneGeneratingVertex = false;
        }

        private static void FinishFaceCalculation(ref TerrainFace terrainFace)
        {
            terrainFace.TerrainMeshData.Normals = RecalculateNormals(terrainFace.TerrainMeshData.Vertices, terrainFace.TerrainMeshData.Triangles);
            for (var i = 0; i < terrainFace.TerrainMeshData.Vertices.Length; i++)
            {
                var vertexPosition = terrainFace.TerrainMeshData.Vertices[i];
                var normal = terrainFace.TerrainMeshData.Normals[i];
                Vector3 localUpDir =  vertexPosition.normalized;
                float steepness = 1 - Vector3.Dot(normal, localUpDir);
                steepness = Math.Clamp(steepness, 0.0f, 1.0f) / 0.6f;
                terrainFace.TerrainMeshData.VertexColor[i] = new Color(steepness,  0, 0);
            }
            terrainFace.TerrainMeshData.IsDoneGeneratingVertex = true;
            terrainFace.TerrainMeshData.IsGeneratingVertex = false;
        }

        private static void CalculateFaceSynchronous(ref TerrainFace terrainFace, int resolution)
        {
            InitializeFace(ref terrainFace, resolution);
            for(var y = 0; y < resolution; y ++)
            {
                CalculateFace(ref terrainFace, resolution, y);
            }

            FinishFaceCalculation(ref terrainFace);
        }

        private static void CalculateFaceParallel(TerrainFace terrainFace, int resolution)
        {
            InitializeFace(ref terrainFace, resolution);
            Parallel.For(0, resolution, y =>
            {
                CalculateFace(ref terrainFace, resolution, y);
            });
            FinishFaceCalculation(ref terrainFace);
        }
        
        private static Vector3[] RecalculateNormals(Vector3[] vertices, int[] triangles)
        {
            Vector3[] normals = new Vector3[vertices.Length];

            // Calculate normals per triangle
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int v0 = triangles[i];
                int v1 = triangles[i + 1];
                int v2 = triangles[i + 2];

                Vector3 edge1 = vertices[v1] - vertices[v0];
                Vector3 edge2 = vertices[v2] - vertices[v0];

                Vector3 normal = Vector3.Cross(edge1, edge2).normalized;

                normals[v0] += normal;
                normals[v1] += normal;
                normals[v2] += normal;
            }

            // Normalize the normals
            for (int i = 0; i < normals.Length; i++)
                normals[i] = normals[i].normalized;

            return normals;
        }

        private static float EvaluateRigidNoise(this NoiseSettings noiseSettings, Noise noise, Vector3 point)
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

        private static float EvaluateSimpleNoise(NoiseSettings noiseSettings, Noise noise, Vector3 point)
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

        private static float Evaluate(this NoiseFilter noiseFilter, Vector3 position)
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

        private static Vector3 CalculatePointOnPlanet(this ref ShapeGenerator shapeGenerator, Vector3 pointOnUnitSphere)
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
        
        private static void UpdateElevation(this ref Terrain terrain, MinMax elevationMinMax)
        {
            terrain.Material.SetVector(ElevationMinMax, new Vector4(elevationMinMax.Min, elevationMinMax.Max));
        }

        private static void UpdateColors(this ref Terrain terrain)
        {
            Color[] colors = new Color[terrain.ColorGenerator.TextureResolution];
            for (int i = 0; i < terrain.ColorGenerator.TextureResolution; i++)
            {
                colors[i] = terrain.Tint.Evaluate(i / (terrain.ColorGenerator.TextureResolution - 1f));
            }
            terrain.ColorGenerator.TintTexture.SetPixels(colors);
            terrain.ColorGenerator.TintTexture.Apply();
            terrain.Material.SetTexture(MainTexture, terrain.ColorGenerator.TintTexture);
            terrain.Material.SetTexture(GroundTex, terrain.ColorGenerator.GroundTexture);
            terrain.Material.SetTexture(GroundNorm, terrain.ColorGenerator.GroundTextureNormals);
            terrain.Material.SetFloat(GroundTexBlend, terrain.ColorGenerator.GroundTextureBlend);
            terrain.Material.SetFloat(GroundSmooth, terrain.GroundSmooth);
            terrain.Material.SetFloat(StepThreshold, terrain.Surface.stepThreshold);
            terrain.Material.SetColor(StepColor, terrain.StepColor);
            terrain.Material.SetTexture(TextureStep, terrain.TextureStep);
            terrain.Material.SetTexture(TextureNormalStep, terrain.TextureNormalStep);
        }

        internal static void SwitchToAsyncUpdate(this ref Terrain terrain)
        {
            if(!Application.isPlaying || terrain.Surface.UpdateAsync)
                return;
            var newSurface = terrain.Surface;
            newSurface.UpdateAsync = true;
            newSurface.StartTerrainThread();
            Debug.Log("Switch terrain to thread");
            terrain.SetSurface(newSurface);
        }

        internal static void SwitchToParrallelUpdate(this ref Terrain terrain)
        {
            var newSurface = terrain.Surface;
            newSurface.UpdateAsync = false;
            terrain.SetSurface(newSurface);
        }
        
        private static void MeshThread(this TerrainMeshThreadCalculation terrainMeshThreadCalculation)
        {
            if (terrainMeshThreadCalculation == null)
            {
                Debug.LogError("Thread Aborted terrain mesh is null");
                return;
            }

            while (!terrainMeshThreadCalculation.IsTerminated)
            {
                Profiler.BeginSample("Test");
                if (terrainMeshThreadCalculation.SubmitedNewCalculation)
                {
                    terrainMeshThreadCalculation.SubmitedNewCalculation = false;
                    terrainMeshThreadCalculation.IsCalculating = true;
                    CalculateFaceSynchronous(ref terrainMeshThreadCalculation.TerrainFace, terrainMeshThreadCalculation.Resolution);
                }
                Profiler.EndSample();
                Thread.Sleep(1);
                terrainMeshThreadCalculation.IsCalculating = false;
            }
            
            Debug.Log("Thread succesfully disposed");
        }
    }
    
    [Serializable]
    struct Surface
    {
        [SerializeField, Range(3, 127)] internal int minResolution;
        [SerializeField, Range(3, 4096)] internal int highResolution;
        [SerializeField, Range(1, 100)] internal int subdivisions;
        [SerializeField, Range(0, 1)] internal float stepThreshold;
        internal bool UpdateAsync;
        internal int CurrentSurface;
        internal ShapeGenerator ShapeGenerator;
        internal int CurrentClosestFaceUpdate;
        internal int[] ClosestFaces;
        internal int CurrentPreviousClosestFaceUpdate;
        internal int [] PreviousClosestFaces;
        internal bool AllFacesGenerated;
        internal TerrainMeshThreadCalculation TerrainMeshThreadCalculation;
        [SerializeField, HideInInspector] internal MeshFilter[] meshFilters;
        [SerializeField, HideInInspector] private MeshRenderer[] meshRenderers;
        //[SerializeField] internal TerrainMaterial terrainMaterial;
        [SerializeField] internal Shape shape;
        internal TerrainFace[] TerrainFaces;
        internal bool Dirty;
        internal bool DirtyFaceResolution;
        
        public MeshRenderer[] MeshRenderers
        {
            get
            {
                return meshRenderers;
            }
            set
            {
                meshRenderers = value;
            }
        }

        public void ResetRenderers()
        {
            meshFilters = null;
            meshRenderers = null;
        }

        internal static Surface Default()
        {
            var surface = new Surface();
            surface.minResolution = 16;
            surface.highResolution = 64;
            surface.subdivisions = 15;
            surface.stepThreshold = 0.4f;
            //surface.terrainMaterial = TerrainMaterial.Default();
            surface.shape = Shape.Default();
            
            return surface;
        }
    }

    class TerrainMeshData
    {
        internal Vector3[] Vertices;
        internal Vector3[] Normals;
        internal Color[] VertexColor;
        internal int[] Triangles;
        internal bool IsGeneratingVertex;
        internal bool IsDoneGeneratingVertex;
        internal bool IsCalculatedMeshAssigned;
        internal bool IsHighDefinition;
    }

    struct TerrainFace {
        internal ShapeGenerator ShapeGenerator;
        internal readonly Mesh Mesh;
        internal readonly int RowID;
        internal readonly int LineID;
        internal readonly int NumberOfSubdivisions;
        internal readonly int MinResolution;
        internal readonly int HighResolution;
        internal readonly float StepThreshold;
        internal readonly Vector3 LocalUp;
        internal readonly Vector3 AxisA;
        internal readonly Vector3 AxisB;
        internal TerrainMeshData TerrainMeshData;

        public TerrainFace(ShapeGenerator shapeGenerator, Mesh mesh, int minResolution, int highResolution, int rowID, int lineID, int numberOfSubdivisions, float stepThreshold, Vector3 localUp)
        {
            ShapeGenerator = shapeGenerator;
            Mesh = mesh;
            Mesh.indexFormat = IndexFormat.UInt32;
            MinResolution = minResolution;
            HighResolution = highResolution;
            LocalUp = localUp;
            AxisA = new Vector3(localUp.y, localUp.z, localUp.x);
            AxisB = Vector3.Cross(localUp, AxisA);
            RowID = rowID;
            LineID = lineID;
            NumberOfSubdivisions = numberOfSubdivisions;
            TerrainMeshData = new TerrainMeshData();
            StepThreshold = stepThreshold;
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

        public static Shape Default()
        {
            var newShape = new Shape();
            newShape.radius = 30000;
            newShape.noiseSettings = new NoiseLayer[1];
            newShape.noiseSettings[0] = NoiseLayer.Default();

            return newShape;
        }
        
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

        public static NoiseLayer Default()
        {
            var newNoiseLayer = new NoiseLayer();
            newNoiseLayer.enabled = true;
            var newNoiseSettings = CelestialBodies.Terrain.NoiseSettings.Default();
            newNoiseLayer.noiseSettings = newNoiseSettings;
            return newNoiseLayer;
        }
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

        public static NoiseSettings Default()
        {
            var newNoiseSettings = new NoiseSettings();
            newNoiseSettings.filter = FilterType.Simple;
            newNoiseSettings.strength = 0.04f;
            newNoiseSettings.layersCount = 4;
            newNoiseSettings.baseRoughness = 1.43f;
            newNoiseSettings.roughness = 2.53f;
            newNoiseSettings.persistence = 0.61f;
            newNoiseSettings.minValue = 1.02f;
            return newNoiseSettings;
        }
    }


    [Serializable]
    public struct Terrain
    {
        [SerializeField] private Surface surface;
        [SerializeField] private Gradient tint;

        internal Surface Surface
        {
            get => surface;
        }

        internal void SetSurface(Surface newSurface)
        {
            surface = newSurface;
        }
        
        public Gradient Tint
        {
            get
            {
                if (tint == null)
                    tint = new Gradient();
                return tint;
            }
        }

        [SerializeField] private Texture2D texture;
        public Texture2D Texture
        {
            get => texture;
        }
        
        [SerializeField] private Texture2D textureNormal;
        public Texture2D TextureNormal
        {
            get => textureNormal;
        }

        [SerializeField] private Color stepColor;

        public Color StepColor
        {
            get => stepColor;
        }
        
        [SerializeField] private Texture2D textureStep;
        public Texture2D TextureStep
        {
            get => textureStep;
        }
        
        [SerializeField] private Texture2D textureNormalStep;
        public Texture2D TextureNormalStep
        {
            get => textureNormalStep;
        }
        
        [SerializeField] private float groundSmooth;
        public float GroundSmooth
        {
            get => groundSmooth;
        }
        
        [SerializeField] private float textureBlend;
        public float TextureBlend
        {
            get => textureBlend;
        }

        [SerializeField, HideInInspector] private Material material;
        
        internal ColorGenerator ColorGenerator;

        public static Terrain Default()
        {
            var newTerrain = new Terrain();
            newTerrain.groundSmooth = 0.1f;
            
            var newTint = new Gradient();
            var colors = new GradientColorKey[2];
            colors[0] = new GradientColorKey(Color.green, 0.0f);
            colors[1] = new GradientColorKey(Color.grey, 1.0f);
            var alphas = new GradientAlphaKey[2];
            alphas[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphas[1] = new GradientAlphaKey(0.0f, 1.0f);
            newTint.SetKeys(colors, alphas);
            newTerrain.tint = newTint;
            newTerrain.textureBlend = 0.3f;
            newTerrain.surface = Surface.Default();
            newTerrain.stepColor = Color.grey;
            return newTerrain;
        }
        
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
        internal Texture2D TintTexture;
        internal int TextureResolution;
        internal Texture2D GroundTexture;
        internal float GroundTextureBlend;
        internal Texture2D GroundTextureNormals;

        public ColorGenerator(Terrain settings)
        {
            TextureResolution = 50;
            TintTexture = new Texture2D(TextureResolution, 1);
            GroundTexture = settings.Texture;
            GroundTextureBlend = settings.TextureBlend;
            GroundTextureNormals = settings.TextureNormal;
        }
    }
    
    class TerrainMeshThreadCalculation
    {
        public bool IsCalculating;
        public bool SubmitedNewCalculation;
        public bool IsTerminated;
        public TerrainFace TerrainFace;
        internal int Resolution;

        internal void TerminateThread()
        {
            IsTerminated = true;
        }

        internal bool SubmitCalculation(TerrainFace terrainFace, bool isHighResolution)
        {
            if (IsCalculating || SubmitedNewCalculation || terrainFace.TerrainMeshData.IsDoneGeneratingVertex)
            {
                return false;
            }
            TerrainFace = terrainFace;
            Resolution = isHighResolution? terrainFace.HighResolution : terrainFace.MinResolution;
            SubmitedNewCalculation = true;
            return true;
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