using System;
using UnityEngine;

public class RescalledTransform : MonoBehaviour
{
    public doubleVector3 position;
    public doubleQuaternion rotation;
    public doubleVector3 scale;
}

[Serializable]
public struct doubleVector3
{
    public double X;
    public double Y;
    public double Z;
}

[Serializable]
public struct doubleQuaternion
{
    public double X;
    public double Y;
    public double Z;
    public double W;
}
