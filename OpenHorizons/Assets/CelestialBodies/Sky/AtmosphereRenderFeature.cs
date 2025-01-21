using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEngine.Profiling;


#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace CelestialBodies.Sky
{
    public class AtmosphereRenderPass : ScriptableRenderPass
    {
        struct SortedEffect
        {
            public IAtmosphereEffect effect;
        }


        private static Shader atmosphereShader;


        public AtmosphereRenderPass(Shader atmosphereShader)
        {
            AtmosphereRenderPass.atmosphereShader = atmosphereShader;
        }


        private static IAtmosphereEffect currentActiveEffect;


        public static void RegisterEffect(IAtmosphereEffect effect)
        {
            currentActiveEffect = effect;
        }


        public static void RemoveEffect(IAtmosphereEffect effect)
        {
            if(effect == currentActiveEffect)
                currentActiveEffect = null;
        }


        private SortedEffect visibleEffects;

        private Plane[] cameraPlanes;
        
        private void CullAndSortEffects(Camera camera)
        {
            Profiler.BeginSample("Cull atmosphere");
            if(currentActiveEffect == null)
                return;
            if (cameraPlanes == null || cameraPlanes.Length != 6)
            {
                cameraPlanes = new Plane[6];
            }
            // Perform culling of active effects
            GeometryUtility.CalculateFrustumPlanes(camera, cameraPlanes);
            
            if (currentActiveEffect.IsVisible(cameraPlanes))
            {
                visibleEffects = new SortedEffect
                {
                    effect = currentActiveEffect,
                };
            }
            Profiler.EndSample();
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            BlitUtility.SetupBlitTargets(cmd, renderingData.cameraData.cameraTargetDescriptor);
            CullAndSortEffects(renderingData.cameraData.camera);
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Profiler.BeginSample("Execute Atmophere Effect");
            if(currentActiveEffect == null)
                return;
            CommandBuffer cmd = CommandBufferPool.Get("Atmosphere Effects");
            cmd.Clear();

            var cameraData = renderingData.cameraData;
            Camera camera = cameraData.camera;

            bool isOverlay = camera.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Overlay;


#if UNITY_EDITOR
            // Likely a bug or oversight- scene camera is considered overlay camera when it shouldn't be, so ensure it's not set as overlay.
            isOverlay = !cameraData.isSceneViewCamera && isOverlay;

            bool prefabMode = PrefabStageUtility.GetCurrentPrefabStage() == StageUtility.GetCurrentStage();

            if (cameraData.postProcessEnabled)
            {
                RenderEffects(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, prefabMode);
            }
#else
        if (cameraData.postProcessEnabled) 
        {
            RenderEffects(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, false);
        }
#endif


            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            Profiler.EndSample();
        }


        void RenderEffects(CommandBuffer cmd, RenderTargetIdentifier colorSource, bool inPrefabMode)
        {
            if(visibleEffects.effect == null)
                return;
            BlitUtility.BeginBlitLoop(cmd, colorSource);

#if UNITY_EDITOR
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(visibleEffects.effect.GameObject);
#endif
            Material blitMat = visibleEffects.effect.GetMaterial(atmosphereShader);
            BlitUtility.BlitNext(blitMat, "_Source");

            // Blit to camera target
            BlitUtility.EndBlitLoop(colorSource);
        }


        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            BlitUtility.ReleaseBlitTargets(cmd);
        }
    }


    public interface IAtmosphereEffect
    {
        /// <summary>
        /// Gets or creates a new material with the provided shader
        /// </summary>
        public Material GetMaterial(Shader atmosphereShader);


        /// <summary>
        /// Is the effect visible to the provided camer frustum planes?
        /// </summary>
        public bool IsVisible(Plane[] cameraPlanes);

        public GameObject GameObject { get; }
    }

    public class AtmosphereRenderFeature : ScriptableRendererFeature
    {
        private Shader atmosphereShader;

        AtmosphereRenderPass atmospherePass;


        public override void Create()
        {
            ValidateShader();

            atmospherePass = new AtmosphereRenderPass(atmosphereShader);

            // Effect does not work with transparents since they do not write to the depth buffer. Sorry if you wanted to have a planet made of glass.
            atmospherePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

            atmospherePass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Prevent renering in material previews.
            if (!renderingData.cameraData.isPreviewCamera)
            {
                renderer.EnqueuePass(atmospherePass);
            }
        }


        void ValidateShader()
        {
            Shader shader = AddAlwaysIncludedShader("Hidden/Atmosphere");

            if (shader == null)
            {
                Debug.LogError(
                    "Atmosphere shader could not be found! Make sure Hidden/Atmosphere is located somewhere in your project and included in 'Always Included Shaders'",
                    this);
                return;
            }

            atmosphereShader = shader;
        }


        // NOTE: Does not always immediately add the shader. If the shader was just recently imported with the project, will return null as the shader hasn't compiled yet
        public static Shader AddAlwaysIncludedShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                return null;
            }

#if UNITY_EDITOR
            var graphicsSettingsObj =
                AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var serializedObject = new SerializedObject(graphicsSettingsObj);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
            bool hasShader = false;

            for (int i = 0; i < arrayProp.arraySize; ++i)
            {
                var arrayElem = arrayProp.GetArrayElementAtIndex(i);
                if (shader == arrayElem.objectReferenceValue)
                {
                    hasShader = true;
                    break;
                }
            }

            if (!hasShader)
            {
                int arrayIndex = arrayProp.arraySize;
                arrayProp.InsertArrayElementAtIndex(arrayIndex);
                var arrayElem = arrayProp.GetArrayElementAtIndex(arrayIndex);
                arrayElem.objectReferenceValue = shader;

                serializedObject.ApplyModifiedProperties();

                AssetDatabase.SaveAssets();
            }
#endif

            return shader;
        }
    }

    public static class BlitUtility
    {
        static readonly int blitTargetA = Shader.PropertyToID("_BlitA");
        static readonly int blitTargetB = Shader.PropertyToID("_BlitB");


        static RenderTargetIdentifier destinationA = new(blitTargetA);
        static RenderTargetIdentifier destinationB = new(blitTargetB);


        static RenderTargetIdentifier latestDest;
        static CommandBuffer blitCommandBuffer;


        /// <summary>
        /// Call SetupBlitTargets before calling BeginBlitLoop/BlitNext.
        /// </summary>
        /// <param name="cmd">The command buffer used to blit.</param>
        /// <param name="blitSourceDescriptor">The source texture information to use.</param>
        public static void SetupBlitTargets(CommandBuffer cmd, RenderTextureDescriptor blitSourceDescriptor)
        {
            ReleaseBlitTargets(cmd);

            if (cmd == null)
            {
                Debug.LogError("Blit Command Buffer is null, cannot set up blit targets.");
            }

            RenderTextureDescriptor descriptor = blitSourceDescriptor;
            descriptor.depthBufferBits = 0;

            cmd.GetTemporaryRT(blitTargetA, descriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(blitTargetB, descriptor, FilterMode.Bilinear);
        }


        /// <summary>
        /// Assigns the initial texture used in the blit loop.
        /// </summary>
        /// <param name="source">The source texture to use.</param>
        public static void BeginBlitLoop(CommandBuffer cmd, RenderTargetIdentifier source)
        {
            blitCommandBuffer = cmd;
            latestDest = source;
        }


        /// <summary>
        /// Blits back and forth between two temporary textures until EndBlitLoop is called.
        /// </summary>
        /// <param name="material">The material to blit with.</param>
        /// <param name="shaderProperty">The shader property to assign the source texture to.</param>
        /// <param name="pass">The material pass to use.</param>
        public static void BlitNext(Material material, string shaderProperty, int pass = 0)
        {
            if (blitCommandBuffer == null)
            {
                throw new System.Exception(
                    "No CommandBuffer has been passed in before beginning the blit loop! Make sure BeginBlitLoop() is called before calling BlitNext(), and make sure CommandBuffer is not disposed of prematurely!");
            }

            var first = latestDest;
            var last = first == destinationA ? destinationB : destinationA;

            blitCommandBuffer.SetGlobalTexture(shaderProperty, first);

            blitCommandBuffer.Blit(first, last, material, pass);
            latestDest = last;
        }


        /// <summary>
        /// Writes the final blit loop result into the destination texture.
        /// </summary>
        /// <param name="destination">The texture to write the blit loop output into.</param>
        public static void EndBlitLoop(RenderTargetIdentifier destination)
        {
            blitCommandBuffer.Blit(latestDest, destination);
            blitCommandBuffer = null;
        }


        /// <summary>
        /// Call ReleaseBlitTargets after finishing any blit loops performed during rendering.
        /// </summary>
        /// <param name="cmd">The command buffer used to release allocated textures.</param>
        public static void ReleaseBlitTargets(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(blitTargetA);
            cmd.ReleaseTemporaryRT(blitTargetB);
        }
    }
}

//MIT License
//Copyright (c) 2023 Kai Angulo
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.