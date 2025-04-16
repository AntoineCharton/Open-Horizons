using UnityEngine;

namespace CelestialBodies.Terrain
{

public class UniqueTerrainElement : MonoBehaviour
{
    [SerializeField] private Planet planet;
    [SerializeField] private TerrainGrass terrainGrass;
    [SerializeField] private GameObject uniqueElement;
    [SerializeField] private GameObject uniqueElementInstance;
    private bool isElementSpawned;
    private void Start()
    {
        if (planet == null)
        {
            planet = GetComponent<Planet>();
        }
    }

    private void Update()
    {
        if (planet.IsInitialized() && !isElementSpawned)
        {
            var targetPosition = planet.GetFirstPointAboveOcean(200);
            var newElement = Instantiate(uniqueElement);
            newElement.transform.parent = planet.transform;
            newElement.transform.localPosition = targetPosition;
            newElement.transform.LookAt(planet.transform);
            uniqueElementInstance = newElement;
            planet.AddFlatModifier(targetPosition, 500, 100);
            planet.AddRemoveTreeModifier(targetPosition, 80);
            if(terrainGrass != null)
                terrainGrass.AddTerrainRemover(targetPosition, 100);
            
            isElementSpawned = true;
        }
    }
}
    
}
