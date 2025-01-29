using System;
using BigWorld;
using BigWorld.Kepler;
using UnityEngine;

public class KeplerToRescalledTransform : MonoBehaviour
{
    //[SerializeField] private RescalledPlanet target;
    [SerializeField] private KeplerOrbitMover mover;
    
    public void Update()
    {
        //target.position = mover.OrbitData.position;
    }
}
