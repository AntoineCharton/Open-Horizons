using UnityEngine;

namespace BigWorld.Kepler
{
    public class SetOarthOrbit : MonoBehaviour
    {
        public Transform attractorTransform; // Assign the Sun's Transform in the Inspector
        public double attractorMass = 1.7565459e28; // Sun's mass
        public double GConstant = 6.67430e-11; // Gravitational constant

        void Start()
        {
            var body = GetComponent<KeplerOrbitMover>();
            body.SetOrbitSettings(attractorTransform, attractorMass, GConstant);

            body.SetOrbitData(new KeplerOrbitData(
                eccentricity: 0, // Circular orbit
                semiMajorAxis: 13599840256, // Kerbin's semi-major axis in meters
                meanAnomalyDeg: 0, // Start at periapsis
                inclinationDeg: 90, // Equatorial orbit
                argOfPerifocusDeg: 0, // Not relevant for circular orbit
                ascendingNodeDeg: 0, // Not relevant for equatorial orbit
                attractorMass: attractorMass,
                gConst: GConstant
            ));

            body.ForceUpdateViewFromInternalState();
        }
    }
}