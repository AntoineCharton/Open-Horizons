using System;
using UnityEngine;

[ExecuteAlways]
public class RescalledTransform : MonoBehaviour
{
    public doubleVector3 position;
    public Quaternion rotation;
    public doubleVector3 scale;
    [SerializeField] private ReferenceTransform referenceTransform;

    private void LateUpdate()
    {
        if (referenceTransform is null)
        {
            referenceTransform = FindAnyObjectByType<ReferenceTransform>();
        }

        if (referenceTransform is not null)
        {
            transform.position = new Vector3((float)(position.X - referenceTransform.referencePosition.X),
                (float)(position.Y- referenceTransform.referencePosition.Y), (float)(position.Z - referenceTransform.referencePosition.Z));
        }
    }
}

public static class Rescale
{
}

[Serializable]
public struct doubleVector3
{
    public double X;
    public double Y;
    public double Z;

    public doubleVector3(Vector3 position)
    {
        X = position.x;
        Y = position.y;
        Z = position.z;
    }
}
