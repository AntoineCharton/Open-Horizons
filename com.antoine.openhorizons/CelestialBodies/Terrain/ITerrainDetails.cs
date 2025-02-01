using UnityEngine;

namespace CelestialBodies.Terrain
{
    /// <summary>
    /// Calls when a face is in high definition or low definition
    /// </summary>
    public interface ITerrainDetails
    {
        
        /// <summary>
        /// Face is high definition and need to be updated. This will be called every frame the mesh is updating until all the details return true.
        /// Note: There is not timer if it never returns true planet generation will not resume.
        /// </summary>
        /// <param name="chunkID">The id of the chunk. Use it to avoid duplicates</param>
        /// <param name="vertices">The vertices of the high definition mesh</param>
        /// <param name="indexes">The indexes of the high definition mesh</param>
        /// <param name="normals">The normals of the high definition mesh</param>
        /// <param name="vertexColors">The colors which stores genereation information. R is steepness</param>
        /// <param name="stepThreshold">The threshold at which the mesh is considered a hill</param>
        /// <param name="minMax">The values relative to the planet transform position representing the highest and lowest point</param>
        /// <returns>When the detail is ready return true. Return false for processes that needs multiple frame to setup.</returns>
        public bool HighDefinition(int chunkID, Vector3[] vertices, int[] indexes, Vector3[] normals,
            Color[] vertexColors, float stepThreshold, MinMax minMax);
            
        /// <summary>
        /// Face is low definition and requires cleanup. This will be called every frame the mesh is updating until all the details return true.
        /// Note: There is not timer if it never returns true planet generation will not resume.
        /// </summary>
        /// <param name="chunkID">The id of the chunk to cleanup</param>
        /// <returns>When the detail is ready return true. Return false for processes that needs multiple frame to setup.</returns>
        public bool LowDefinition(int chunkID);
    }
}
