using UnityEngine;

namespace CelestialBodies.PhysicsBodies
{
    public class GravityTarget : MonoBehaviour
    {
        [SerializeField] internal float gravity;
        
        public void Attract(Rigidbody body) {
            Vector3 gravityUp = (body.position - transform.position).normalized;
            Vector3 localUp = body.transform.up;
            
            body.AddForce(gravityUp * gravity);
            var originalRotation = body.rotation;
            body.rotation = Quaternion.FromToRotation(localUp,gravityUp) * body.rotation;
            body.rotation = Quaternion.Lerp(originalRotation, body.rotation, Time.deltaTime * 5);
        }
    }
}
