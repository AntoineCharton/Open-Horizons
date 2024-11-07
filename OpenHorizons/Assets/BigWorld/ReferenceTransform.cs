using System;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
public class ReferenceTransform : MonoBehaviour
{
    public doubleVector3 referencePosition;
    public doubleVector3 localPosition;

    private void Update()
    {
        localPosition = new doubleVector3(transform.position);
        if (Vector3.Distance(transform.position, Vector3.zero) > 1000)
        {
            referencePosition.X += localPosition.X;
            referencePosition.Y += localPosition.Y;
            referencePosition.Z += localPosition.Z;
            transform.position = Vector3.zero;
            localPosition = new doubleVector3(Vector3.zero);
        }
    }
}
