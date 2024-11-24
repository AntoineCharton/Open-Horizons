using System;
using UnityEngine;

namespace BigWorld.Doubles
{
    [Serializable]
    public struct DoubleVector3
    {
        public double X;
        public double Y;
        public double Z;

        public DoubleVector3(Vector3 position)
        {
            X = position.x;
            Y = position.y;
            Z = position.z;
        }
        
        public DoubleVector3(DoubleVector3 position)
        {
	        X = position.X;
	        Y = position.Y;
	        Z = position.Z;
        }

        public static float DistanceFloat(DoubleVector3 from, DoubleVector3 to)
        {
            return Mathf.Sqrt((float)(Square(from.X - to.X) +
                                      Square(from.Y - to.Y) +
                                      Square(from.Z - to.Z)));
        }

        public static double Square(double value)
        {
            return value * value;
        }

        public static DoubleVector3 InverseTransformPoint(DoubleVector3 transforPos, Quaternion transformRotation,
            DoubleVector3 transformScale, DoubleVector3 pos)
        {
            DoubleMatrix4X4 matrix =
                DoubleMatrix4X4.GetTRSMatrix(transforPos, transformRotation.eulerAngles, transformScale);
            DoubleMatrix4X4 inverse = DoubleMatrix4X4.Invert(matrix);
            return inverse.MultiplyPoint3X4(pos);
        }
        
        		private const double EPSILON = 1.401298E-45;

		public DoubleVector3 normalized
		{
			get { return DoubleVector3.Normalize(this); }
		}

		public double magnitude
		{
			get { return Math.Sqrt(this.X * this.X + this.Y * this.Y + this.Z * this.Z); }
		}

		public double sqrMagnitude
		{
			get { return this.X * this.X + this.Y * this.Y + this.Z * this.Z; }
		}

		public static DoubleVector3 zero
		{
			get { return new DoubleVector3(0d, 0d, 0d); }
		}

		public static DoubleVector3 one
		{
			get { return new DoubleVector3(1d, 1d, 1d); }
		}

		public DoubleVector3(double x, double y, double z)
		{
			this.X = x;
			this.Y = y;
			this.Z = z;
		}

		public DoubleVector3(float x, float y, float z)
		{
			this.X = x;
			this.Y = y;
			this.Z = z;
		}

		public DoubleVector3(double x, double y)
		{
			this.X = x;
			this.Y = y;
			this.Z = 0d;
		}

		public static DoubleVector3 operator +(DoubleVector3 a, DoubleVector3 b)
		{
			return new DoubleVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		}

		public static DoubleVector3 operator -(DoubleVector3 a, DoubleVector3 b)
		{
			return new DoubleVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
		}

		public static DoubleVector3 operator -(DoubleVector3 a)
		{
			return new DoubleVector3(-a.X, -a.Y, -a.Z);
		}

		public static DoubleVector3 operator *(DoubleVector3 a, double d)
		{
			return new DoubleVector3(a.X * d, a.Y * d, a.Z * d);
		}

		public static DoubleVector3 operator *(double d, DoubleVector3 a)
		{
			return new DoubleVector3(a.X * d, a.Y * d, a.Z * d);
		}

		public static DoubleVector3 operator /(DoubleVector3 a, double d)
		{
			return new DoubleVector3(a.X / d, a.Y / d, a.Z / d);
		}

		public static bool operator ==(DoubleVector3 lhs, DoubleVector3 rhs)
		{
			return DoubleVector3.SqrMagnitude(lhs - rhs) < 0.0 / 1.0;
		}

		public static bool operator !=(DoubleVector3 lhs, DoubleVector3 rhs)
		{
			return DoubleVector3.SqrMagnitude(lhs - rhs) >= 0.0 / 1.0;
		}

		public static DoubleVector3 Lerp(DoubleVector3 from, DoubleVector3 to, double t)
		{
			t = t < 0 ? 0 : (t > 1.0 ? 1.0 : t);
			return new DoubleVector3(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t, from.Z + (to.Z - from.Z) * t);
		}

		public static DoubleVector3 MoveTowards(DoubleVector3 current, DoubleVector3 target, double maxDistanceDelta)
		{
			DoubleVector3 vector3   = target - current;
			double   magnitude = vector3.magnitude;
			if (magnitude <= maxDistanceDelta || magnitude == 0.0)
			{
				return target;
			}
			else
			{
				return current + vector3 / magnitude * maxDistanceDelta;
			}
		}

		public void Set(double new_x, double new_y, double new_z)
		{
			this.X = new_x;
			this.Y = new_y;
			this.Z = new_z;
		}

		public static DoubleVector3 Scale(DoubleVector3 a, DoubleVector3 b)
		{
			return new DoubleVector3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
		}

		public void Scale(DoubleVector3 scale)
		{
			this.X *= scale.X;
			this.Y *= scale.Y;
			this.Z *= scale.Z;
		}

		public static DoubleVector3 Cross(DoubleVector3 a, DoubleVector3 b)
		{
			return new DoubleVector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
		}

		public override int GetHashCode()
		{
			return this.X.GetHashCode() ^ this.Y.GetHashCode() << 2 ^ this.Z.GetHashCode() >> 2;
		}

		public override bool Equals(object other)
		{
			if (!(other is DoubleVector3))
			{
				return false;
			}

			DoubleVector3 vector3d = (DoubleVector3)other;
			if (this.X.Equals(vector3d.X) && this.Y.Equals(vector3d.Y))
			{
				return this.Z.Equals(vector3d.Z);
			}
			else
			{
				return false;
			}
		}

		public static DoubleVector3 Reflect(DoubleVector3 inDirection, DoubleVector3 inNormal)
		{
			return -2d * DoubleVector3.Dot(inNormal, inDirection) * inNormal + inDirection;
		}

		public static DoubleVector3 Normalize(DoubleVector3 value)
		{
			double num = DoubleVector3.Magnitude(value);
			if (num > EPSILON)
			{
				return value / num;
			}
			else
			{
				return DoubleVector3.zero;
			}
		}

		public void Normalize()
		{
			double num = DoubleVector3.Magnitude(this);
			if (num > EPSILON)
			{
				this = this / num;
			}
			else
			{
				this = DoubleVector3.zero;
			}
		}

		public override string ToString()
		{
			return "(" + this.X + "; " + this.Y + "; " + this.Z + ")";
		}

		public string ToString(string format)
		{
			return "(" + this.X.ToString(format) + "; " + this.Y.ToString(format) + "; " + this.Z.ToString(format) + ")";
		}

		public static double Dot(DoubleVector3 a, DoubleVector3 b)
		{
			return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
		}

		public static DoubleVector3 Project(DoubleVector3 vector, DoubleVector3 onNormal)
		{
			double num = DoubleVector3.Dot(onNormal, onNormal);
			if (num < 1.40129846432482E-45d)
			{
				return DoubleVector3.zero;
			}
			else
			{
				return onNormal * DoubleVector3.Dot(vector, onNormal) / num;
			}
		}

		public static DoubleVector3 Exclude(DoubleVector3 excludeThis, DoubleVector3 fromThat)
		{
			return fromThat - DoubleVector3.Project(fromThat, excludeThis);
		}

		public static double Distance(DoubleVector3 a, DoubleVector3 b)
		{
			DoubleVector3 vector3d = new DoubleVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
			return Math.Sqrt(vector3d.X * vector3d.X + vector3d.Y * vector3d.Y + vector3d.Z * vector3d.Z);
		}

		public static DoubleVector3 ClampMagnitude(DoubleVector3 vector, double maxLength)
		{
			if (vector.sqrMagnitude > maxLength * maxLength)
			{
				return vector.normalized * maxLength;
			}
			else
			{
				return vector;
			}
		}

		public static double Angle(DoubleVector3 from, DoubleVector3 to)
		{
			double dot = Dot(from.normalized, to.normalized);
			return Math.Acos(dot < -1.0 ? -1.0 : (dot > 1.0 ? 1.0 : dot)) * 57.29578d;
		}

		public static double Magnitude(DoubleVector3 a)
		{
			return Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
		}

		public static double SqrMagnitude(DoubleVector3 a)
		{
			return a.X * a.X + a.Y * a.Y + a.Z * a.Z;
		}

		public static DoubleVector3 Min(DoubleVector3 a, DoubleVector3 b)
		{
			return new DoubleVector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
		}

		public static DoubleVector3 Max(DoubleVector3 a, DoubleVector3 b)
		{
			return new DoubleVector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
		}
    }
}