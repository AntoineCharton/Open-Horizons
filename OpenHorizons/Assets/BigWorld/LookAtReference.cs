using UnityEngine;

namespace BigWorld
{
    [ExecuteInEditMode]
    public class LookAtReference : MonoBehaviour
    {
        [SerializeField] private ReferenceTransform referenceTransform;

        // Update is called once per frame
        void Update()
        {
            if (referenceTransform is null)
            {
                referenceTransform = FindAnyObjectByType<ReferenceTransform>();
            }

            transform.LookAt(referenceTransform.transform);
        }
    }
}