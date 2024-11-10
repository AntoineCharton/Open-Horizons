using System;
using UnityEngine;

[Serializable]
public struct DoubleVector3
{
    public double X;
    public double Y;
    public double Z;

    public DoubleVector3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public DoubleVector3(Vector3 position)
    {
        X = position.x;
        Y = position.y;
        Z = position.z;
    }
    
    public static float Distance(DoubleVector3 from, DoubleVector3 to)
    {
        return Mathf.Sqrt((float)(Square(from.X - to.X) +
                                  Square(from.Y - to.Y) +
                                  Square(from.Z - to.Z)));
    }
    
    public static double Square(double value)
    {
        return value * value;
    }
    
    public static DoubleVector3 InverseTransformPoint(DoubleVector3 transforPos, Quaternion transformRotation, DoubleVector3 transformScale, DoubleVector3 pos) {
        DoubleMatrix4x4 matrix = DoubleMatrix4x4.GetTRSMatrix(transforPos, transformRotation.eulerAngles, transformScale);
        DoubleMatrix4x4 inverse = DoubleMatrix4x4.Invert(matrix);
        return inverse.MultiplyPoint3x4(pos);
    }
}