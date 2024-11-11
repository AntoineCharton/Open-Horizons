using System;

namespace BigWorld
{
    public struct DoubleVector4
    {
        public bool Equals(DoubleVector4 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
        }

        public override bool Equals(object obj)
        {
            return obj is DoubleVector4 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z, W);
        }

        public double X;
        public double Y;
        public double Z;
        public double W;

        public DoubleVector4(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static bool operator ==(DoubleVector4 lhs, DoubleVector4 rhs)
        {
            double diffx = lhs.X - rhs.X;
            double diffy = lhs.Y - rhs.Y;
            double diffz = lhs.Z - rhs.Z;
            double diffw = lhs.W - rhs.W;
            double sqrmag = diffx * diffx + diffy * diffy + diffz * diffz + diffw * diffw;
            return sqrmag < double.Epsilon * double.Epsilon;
        }

        public static bool operator !=(DoubleVector4 lhs, DoubleVector4 rhs)
        {
            return !(lhs == rhs);
        }
    }
}
