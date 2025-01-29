using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#pragma warning disable CS0672 
namespace CelestialBodies
{
    public sealed class VolumetricFogPass : CustomRenderPass
    {
        /// <summary>
        /// List of fog volumes to render.
        /// </summary>
        private static readonly List<Fog> FogVolumes = new List<Fog>();

        private static int VolumeID;
        /// <summary>
        /// Black with 0 alpha.
        /// </summary>
        private static readonly Color ColorNothing;
        
        /// <summary>
        /// Are there any fog volumes to render this frame?
        /// </summary>
        private static bool ShouldRender;

        private static bool IsPropertiesDirty;

        /// <summary>
        /// The fog volume material instance being used.
        /// </summary>
        private Material FogMaterialInstance;

        /// <summary>
        /// The per-render property block.
        /// </summary>
        private MaterialPropertyBlock FogMaterialProperties;

        /// <summary>
        /// The double-buffered render target. Is this needed anymore (to be double-buffered)?
        /// </summary>
        private BufferedRenderTargetReference BufferedFogRenderTarget;

        public VolumetricFogPass(VolumetricFogFeature.VolumetricFogSettings settings)
        {
            renderPassEvent = settings.Event;
            FogMaterialInstance = (settings.InstantiateMaterial ? GameObject.Instantiate(settings.VolumetricFogMaterial) : settings.VolumetricFogMaterial);
            FogMaterialProperties = new MaterialPropertyBlock();
            BufferedFogRenderTarget = null;
        }

        // ---------------------------------------------------------------------------------
        // Rendering
        // ---------------------------------------------------------------------------------

        public override void OnCameraSetup(CommandBuffer commandBuffer, ref RenderingData renderingData)
        {
            ShouldRender = false;
            for (var i = 0; i < FogVolumes.Count; i++)
            {
                if (FogVolumes[i].gameObject.activeInHierarchy)
                {
                    ShouldRender = true;
                    break;
                }
            }

            if (!ShouldRender)
            {
                return;
            }

            if (HasCameraResized(ref renderingData))
            {
                BufferedFogRenderTarget = BufferedFogRenderTarget ?? new BufferedRenderTargetReference("_BufferedVolumetricFogRenderTarget");
                BufferedFogRenderTarget.SetRenderTextureDescriptor(new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height,
                    RenderTextureFormat.ARGB32, 0, 1), FilterMode.Bilinear, TextureWrapMode.Clamp);
            }

            BufferedFogRenderTarget.Clear(commandBuffer, ColorNothing);

            FogMaterialProperties.SetMatrix(ShaderIds.CameraNearPlaneCorners, renderingData.cameraData.camera.GetNearClipPlaneCornersMatrix());
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!ShouldRender)
            {
                return;
            }

            CommandBuffer commandBuffer = CommandBufferPool.Get("VolumetricFogPass");

            using (new ProfilingScope(commandBuffer, new ProfilingSampler("VolumetricFogPass")))
            {
                for (var i = 0; i < FogVolumes.Count; i++)
                {
                    if (!FogVolumes[i].gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    var fogVolume = FogVolumes[i];
                    if(IsPropertiesDirty)
                        fogVolume.Apply(FogMaterialProperties);
                    FogVolumes[i] = fogVolume;

                    RasterizeColorToTarget(commandBuffer, BufferedFogRenderTarget.BackBuffer.Handle, FogMaterialInstance, BlitGeometry.Quad, 0, FogMaterialProperties);
                }

                IsPropertiesDirty = false;

                BlitBlendOntoCamera(commandBuffer, BufferedFogRenderTarget.BackBuffer.Handle, ref renderingData);
            }

            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();

            BufferedFogRenderTarget.Swap();

            CommandBufferPool.Release(commandBuffer);
        }

        // ---------------------------------------------------------------------------------
        // Misc
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Adds a <see cref="DepthFog"/> to the render list.
        /// </summary>
        /// <param name="volume"></param>
        public static int AddFogVolume(Fog volume)
        {
            VolumeID++;
            volume.id = VolumeID;
            RemoveFogVolume(volume);
            FogVolumes.Add(volume);
            IsPropertiesDirty = true;
            return VolumeID;
        }

        /// <summary>
        /// Removes a <see cref="DepthFog"/> from the render list.
        /// </summary>
        /// <param name="volume"></param>
        public static void RemoveFogVolume(Fog volume)
        {
            FogVolumes.RemoveAll(f => f.id == volume.id);
        }
        
        internal static void UpdateFogVolume(Fog newVolume)
        {
            for (var i = 0; i < FogVolumes.Count; i++)
            {
                if (FogVolumes[i].id == newVolume.id)
                {
                    FogVolumes[i] = newVolume;
                    IsPropertiesDirty = true;
                    return;
                }
            }
            
            Debug.Log("Couldn't update volume");
        }
    }
}
#pragma warning restore CS0672