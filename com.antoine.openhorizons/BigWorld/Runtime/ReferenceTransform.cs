using BigWorld.Doubles;
using UnityEngine;

namespace BigWorld
{
    [ExecuteInEditMode]
    public class ReferenceTransform : MonoBehaviour
    {
        public DoubleVector3 referencePosition;
        public DoubleVector3 localPosition;

        public DoubleVector3 UniversePosition
        {
            get => new DoubleVector3(referencePosition.X + localPosition.X, referencePosition.Y + localPosition.Y,
                referencePosition.Z + localPosition.Z);
        }

        public void SetWorldPosition(DoubleVector3 worldPosition)
        {
            referencePosition = worldPosition;
            GetRootParent(transform).position = Vector3.zero;
            localPosition = new DoubleVector3(Vector3.zero);
        }

        private void FixedUpdate()
        {
            localPosition = new DoubleVector3(transform.position);
            if (Vector3.Distance(transform.position, Vector3.zero) > 5000)
            {
                referencePosition.X += localPosition.X;
                referencePosition.Y += localPosition.Y;
                referencePosition.Z += localPosition.Z;
                GetRootParent(transform).position = Vector3.zero;
                localPosition = new DoubleVector3(Vector3.zero);
            }
        }
        
        Transform GetRootParent(Transform childTransform)
        {
            Transform current = childTransform;
            while (current.parent != null)
            {
                current = current.parent;
            }
            return current;
        }
    }
}