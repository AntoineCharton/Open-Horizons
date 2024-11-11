using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Serialization;

namespace BigWorld
{

    public struct DoubleMatrix4X4 : IEquatable<DoubleMatrix4X4>, IFormattable
    {
        // memory layout:
        //
        //                row no (=vertical)
        //               |  0   1   2   3
        //            ---+----------------
        //            0  | m00 m10 m20 m30
        // column no  1  | m01 m11 m21 m31
        // (=horiz)   2  | m02 m12 m22 m32
        //            3  | m03 m13 m23 m33

        [FormerlySerializedAs("M00")] public double M11;
        [FormerlySerializedAs("M10")] public double M12;
        [FormerlySerializedAs("M20")] public double M13;
        [FormerlySerializedAs("M30")] public double M14;

        [FormerlySerializedAs("M01")] public double M21;
        [FormerlySerializedAs("M11")] public double M22;
        [FormerlySerializedAs("M21")] public double M23;
        [FormerlySerializedAs("M31")] public double M24;

        [FormerlySerializedAs("M02")] public double M31;
        [FormerlySerializedAs("M12")] public double M32;
        [FormerlySerializedAs("M22")] public double M33;
        [FormerlySerializedAs("M32")] public double M34;

        [FormerlySerializedAs("M03")] public double M41;
        [FormerlySerializedAs("M13")] public double M42;

        [FormerlySerializedAs("m43")] [FormerlySerializedAs("M23")]
        public double M43;

        [FormerlySerializedAs("M33")] public double M44;

        public DoubleMatrix4X4(DoubleVector4 column0, DoubleVector4 column1, DoubleVector4 column2,
            DoubleVector4 column3)
        {
            M11 = column0.X;
            M21 = column1.X;
            M31 = column2.X;
            M41 = column3.X;
            M12 = column0.Y;
            M22 = column1.Y;
            M32 = column2.Y;
            M42 = column3.Y;
            M13 = column0.Z;
            M23 = column1.Z;
            M33 = column2.Z;
            M43 = column3.Z;
            M14 = column0.W;
            M24 = column1.W;
            M34 = column2.W;
            M44 = column3.W;
        }

        // Access element at [row, column].
        public double this[int row, int column]
        {
            get { return this[row + column * 4]; }

            set { this[row + column * 4] = value; }
        }

        // Access element at sequential index (0..15 inclusive).
        public double this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return M11;
                    case 1: return M12;
                    case 2: return M13;
                    case 3: return M14;
                    case 4: return M21;
                    case 5: return M22;
                    case 6: return M23;
                    case 7: return M24;
                    case 8: return M31;
                    case 9: return M32;
                    case 10: return M33;
                    case 11: return M34;
                    case 12: return M41;
                    case 13: return M42;
                    case 14: return M43;
                    case 15: return M44;
                    default:
                        throw new IndexOutOfRangeException("Invalid matrix index!");
                }
            }

            set
            {
                switch (index)
                {
                    case 0:
                        M11 = value;
                        break;
                    case 1:
                        M12 = value;
                        break;
                    case 2:
                        M13 = value;
                        break;
                    case 3:
                        M14 = value;
                        break;
                    case 4:
                        M21 = value;
                        break;
                    case 5:
                        M22 = value;
                        break;
                    case 6:
                        M23 = value;
                        break;
                    case 7:
                        M24 = value;
                        break;
                    case 8:
                        M31 = value;
                        break;
                    case 9:
                        M32 = value;
                        break;
                    case 10:
                        M33 = value;
                        break;
                    case 11:
                        M34 = value;
                        break;
                    case 12:
                        M41 = value;
                        break;
                    case 13:
                        M42 = value;
                        break;
                    case 14:
                        M43 = value;
                        break;
                    case 15:
                        M44 = value;
                        break;

                    default:
                        throw new IndexOutOfRangeException("Invalid matrix index!");
                }
            }
        }

        public static DoubleMatrix4X4 Invert(DoubleMatrix4X4 matrix)
        {
            DoubleMatrix4X4 result = Zero;
            double a = matrix.M11, b = matrix.M12, c = matrix.M13, d = matrix.M14;
            double e = matrix.M21, f = matrix.M22, g = matrix.M23, h = matrix.M24;
            double i = matrix.M31, j = matrix.M32, k = matrix.M33, l = matrix.M34;
            double m = matrix.M41, n = matrix.M42, o = matrix.M43, p = matrix.M44;

            double kpLo = k * p - l * o;
            double jpLN = j * p - l * n;
            double joKn = j * o - k * n;
            double ipLm = i * p - l * m;
            double ioKm = i * o - k * m;
            double inJm = i * n - j * m;

            double a11 = +(f * kpLo - g * jpLN + h * joKn);
            double a12 = -(e * kpLo - g * ipLm + h * ioKm);
            double a13 = +(e * jpLN - f * ipLm + h * inJm);
            double a14 = -(e * joKn - f * ioKm + g * inJm);

            double det = a * a11 + b * a12 + c * a13 + d * a14;

            if (Math.Abs(det) < double.Epsilon)
            {
                result = Zero;
                return result;
            }

            double invDet = 1.0f / det;

            result.M11 = a11 * invDet;
            result.M21 = a12 * invDet;
            result.M31 = a13 * invDet;
            result.M41 = a14 * invDet;

            result.M12 = -(b * kpLo - c * jpLN + d * joKn) * invDet;
            result.M22 = +(a * kpLo - c * ipLm + d * ioKm) * invDet;
            result.M32 = -(a * jpLN - b * ipLm + d * inJm) * invDet;
            result.M42 = +(a * joKn - b * ioKm + c * inJm) * invDet;

            double gpHo = g * p - h * o;
            double fpHn = f * p - h * n;
            double foGn = f * o - g * n;
            double epHm = e * p - h * m;
            double eoGm = e * o - g * m;
            double enFm = e * n - f * m;

            result.M13 = +(b * gpHo - c * fpHn + d * foGn) * invDet;
            result.M23 = -(a * gpHo - c * epHm + d * eoGm) * invDet;
            result.M33 = +(a * fpHn - b * epHm + d * enFm) * invDet;
            result.M43 = -(a * foGn - b * eoGm + c * enFm) * invDet;

            double glHk = g * l - h * k;
            double flHj = f * l - h * j;
            double fkGj = f * k - g * j;
            double elHi = e * l - h * i;
            double ekGI = e * k - g * i;
            double ejFi = e * j - f * i;

            result.M14 = -(b * glHk - c * flHj + d * fkGj) * invDet;
            result.M24 = +(a * glHk - c * elHi + d * ekGI) * invDet;
            result.M34 = -(a * flHj - b * elHi + d * ejFi) * invDet;
            result.M44 = +(a * fkGj - b * ekGI + c * ejFi) * invDet;

            return result;
        }

        // used to allow Matrix4x4s to be used as keys in hash tables
        public override int GetHashCode()
        {
            return GetColumn(0).GetHashCode() ^ (GetColumn(1).GetHashCode() << 2) ^ (GetColumn(2).GetHashCode() >> 2) ^
                   (GetColumn(3).GetHashCode() >> 1);
        }

        // also required for being able to use Matrix4x4s as keys in hash tables
        public override bool Equals(object other)
        {
            if (other is DoubleMatrix4X4 m)
                return Equals(m);
            return false;
        }

        public bool Equals(DoubleMatrix4X4 other)
        {
            return GetColumn(0).Equals(other.GetColumn(0))
                   && GetColumn(1).Equals(other.GetColumn(1))
                   && GetColumn(2).Equals(other.GetColumn(2))
                   && GetColumn(3).Equals(other.GetColumn(3));
        }


        // Multiplies two matrices.
        public static DoubleMatrix4X4 operator *(DoubleMatrix4X4 lhs, DoubleMatrix4X4 rhs)
        {
            DoubleMatrix4X4 res;
            res.M11 = lhs.M11 * rhs.M11 + lhs.M21 * rhs.M12 + lhs.M31 * rhs.M13 + lhs.M41 * rhs.M14;
            res.M21 = lhs.M11 * rhs.M21 + lhs.M21 * rhs.M22 + lhs.M31 * rhs.M23 + lhs.M41 * rhs.M24;
            res.M31 = lhs.M11 * rhs.M31 + lhs.M21 * rhs.M32 + lhs.M31 * rhs.M33 + lhs.M41 * rhs.M34;
            res.M41 = lhs.M11 * rhs.M41 + lhs.M21 * rhs.M42 + lhs.M31 * rhs.M43 + lhs.M41 * rhs.M44;

            res.M12 = lhs.M12 * rhs.M11 + lhs.M22 * rhs.M12 + lhs.M32 * rhs.M13 + lhs.M42 * rhs.M14;
            res.M22 = lhs.M12 * rhs.M21 + lhs.M22 * rhs.M22 + lhs.M32 * rhs.M23 + lhs.M42 * rhs.M24;
            res.M32 = lhs.M12 * rhs.M31 + lhs.M22 * rhs.M32 + lhs.M32 * rhs.M33 + lhs.M42 * rhs.M34;
            res.M42 = lhs.M12 * rhs.M41 + lhs.M22 * rhs.M42 + lhs.M32 * rhs.M43 + lhs.M42 * rhs.M44;

            res.M13 = lhs.M13 * rhs.M11 + lhs.M23 * rhs.M12 + lhs.M33 * rhs.M13 + lhs.M43 * rhs.M14;
            res.M23 = lhs.M13 * rhs.M21 + lhs.M23 * rhs.M22 + lhs.M33 * rhs.M23 + lhs.M43 * rhs.M24;
            res.M33 = lhs.M13 * rhs.M31 + lhs.M23 * rhs.M32 + lhs.M33 * rhs.M33 + lhs.M43 * rhs.M34;
            res.M43 = lhs.M13 * rhs.M41 + lhs.M23 * rhs.M42 + lhs.M33 * rhs.M43 + lhs.M43 * rhs.M44;

            res.M14 = lhs.M14 * rhs.M11 + lhs.M24 * rhs.M12 + lhs.M34 * rhs.M13 + lhs.M44 * rhs.M14;
            res.M24 = lhs.M14 * rhs.M21 + lhs.M24 * rhs.M22 + lhs.M34 * rhs.M23 + lhs.M44 * rhs.M24;
            res.M34 = lhs.M14 * rhs.M31 + lhs.M24 * rhs.M32 + lhs.M34 * rhs.M33 + lhs.M44 * rhs.M34;
            res.M44 = lhs.M14 * rhs.M41 + lhs.M24 * rhs.M42 + lhs.M34 * rhs.M43 + lhs.M44 * rhs.M44;

            return res;
        }

        public static float ConvertDegToRad(float degrees)
        {
            return ((float)Math.PI / 180) * degrees;
        }


        public static DoubleMatrix4X4 GetTranslationMatrix(DoubleVector3 position)
        {
            return new DoubleMatrix4X4(new DoubleVector4(1, 0, 0, 0),
                new DoubleVector4(0, 1, 0, 0),
                new DoubleVector4(0, 0, 1, 0),
                new DoubleVector4(position.X, position.Y, position.Z, 1));
        }

        public static DoubleMatrix4X4 GetRotationMatrix(Vector3 anglesDeg)
        {
            anglesDeg = new Vector3(ConvertDegToRad(anglesDeg.x), ConvertDegToRad(anglesDeg.y),
                ConvertDegToRad(anglesDeg.z));

            DoubleMatrix4X4 rotationX = new DoubleMatrix4X4(new DoubleVector4(1, 0, 0, 0),
                new DoubleVector4(0, Mathf.Cos(anglesDeg.x), Mathf.Sin(anglesDeg.x), 0),
                new DoubleVector4(0, -Mathf.Sin(anglesDeg.x), Mathf.Cos(anglesDeg.x), 0),
                new DoubleVector4(0, 0, 0, 1));

            DoubleMatrix4X4 rotationY = new DoubleMatrix4X4(
                new DoubleVector4(Mathf.Cos(anglesDeg.y), 0, -Mathf.Sin(anglesDeg.y), 0),
                new DoubleVector4(0, 1, 0, 0),
                new DoubleVector4(Mathf.Sin(anglesDeg.y), 0, Mathf.Cos(anglesDeg.y), 0),
                new DoubleVector4(0, 0, 0, 1));

            DoubleMatrix4X4 rotationZ = new DoubleMatrix4X4(
                new DoubleVector4(Mathf.Cos(anglesDeg.z), Mathf.Sin(anglesDeg.z), 0, 0),
                new DoubleVector4(-Mathf.Sin(anglesDeg.z), Mathf.Cos(anglesDeg.z), 0, 0),
                new DoubleVector4(0, 0, 1, 0),
                new DoubleVector4(0, 0, 0, 1));

            return rotationX * rotationY * rotationZ;
        }

        public static DoubleMatrix4X4 GetScaleMatrix(DoubleVector3 scale)
        {
            return new DoubleMatrix4X4(new DoubleVector4(scale.X, 0, 0, 0),
                new DoubleVector4(0, scale.Y, 0, 0),
                new DoubleVector4(0, 0, scale.Z, 0),
                new DoubleVector4(0, 0, 0, 1));
        }

        public static DoubleMatrix4X4 GetTRSMatrix(DoubleVector3 position, Vector3 rotationAngles, DoubleVector3 scale)
        {
            return GetTranslationMatrix(position) * GetRotationMatrix(rotationAngles) * GetScaleMatrix(scale);
        }

        // Transforms a [[Vector4]] by a matrix.
        public static DoubleVector4 operator *(DoubleMatrix4X4 lhs, DoubleVector4 vector)
        {
            DoubleVector4 res;
            res.X = lhs.M11 * vector.X + lhs.M21 * vector.Y + lhs.M31 * vector.Z + lhs.M41 * vector.W;
            res.Y = lhs.M12 * vector.X + lhs.M22 * vector.Y + lhs.M32 * vector.Z + lhs.M42 * vector.W;
            res.Z = lhs.M13 * vector.X + lhs.M23 * vector.Y + lhs.M33 * vector.Z + lhs.M43 * vector.W;
            res.W = lhs.M14 * vector.X + lhs.M24 * vector.Y + lhs.M34 * vector.Z + lhs.M44 * vector.W;
            return res;
        }

        //*undoc*
        public static bool operator ==(DoubleMatrix4X4 lhs, DoubleMatrix4X4 rhs)
        {
            // Returns false in the presence of NaN values.
            return lhs.GetColumn(0) == rhs.GetColumn(0)
                   && lhs.GetColumn(1) == rhs.GetColumn(1)
                   && lhs.GetColumn(2) == rhs.GetColumn(2)
                   && lhs.GetColumn(3) == rhs.GetColumn(3);
        }

        //*undoc*
        public static bool operator !=(DoubleMatrix4X4 lhs, DoubleMatrix4X4 rhs)
        {
            // Returns true in the presence of NaN values.
            return !(lhs == rhs);
        }

        // Get a column of the matrix.
        public DoubleVector4 GetColumn(int index)
        {
            switch (index)
            {
                case 0: return new DoubleVector4(M11, M12, M13, M14);
                case 1: return new DoubleVector4(M21, M22, M23, M24);
                case 2: return new DoubleVector4(M31, M32, M33, M34);
                case 3: return new DoubleVector4(M41, M42, M43, M44);
                default:
                    throw new IndexOutOfRangeException("Invalid column index!");
            }
        }

        // Returns a row of the matrix.
        public DoubleVector4 GetRow(int index)
        {
            switch (index)
            {
                case 0: return new DoubleVector4(M11, M21, M31, M41);
                case 1: return new DoubleVector4(M12, M22, M32, M42);
                case 2: return new DoubleVector4(M13, M23, M33, M43);
                case 3: return new DoubleVector4(M14, M24, M34, M44);
                default:
                    throw new IndexOutOfRangeException("Invalid row index!");
            }
        }

        public DoubleVector3 GetPosition()
        {
            return new DoubleVector3(M41, M42, M43);
        }

        // Sets a column of the matrix.
        public void SetColumn(int index, Vector4 column)
        {
            this[0, index] = column.x;
            this[1, index] = column.y;
            this[2, index] = column.z;
            this[3, index] = column.w;
        }

        // Sets a row of the matrix.
        public void SetRow(int index, Vector4 row)
        {
            this[index, 0] = row.x;
            this[index, 1] = row.y;
            this[index, 2] = row.z;
            this[index, 3] = row.w;
        }

        // Transforms a position by this matrix, with a perspective divide. (generic)
        public DoubleVector3 MultiplyPoint(DoubleVector3 point)
        {
            DoubleVector3 res;
            double w;
            res.X = M11 * point.X + M21 * point.Y + M31 * point.Z + M41;
            res.Y = M12 * point.X + M22 * point.Y + M32 * point.Z + M42;
            res.Z = M13 * point.X + M23 * point.Y + M33 * point.Z + M43;
            w = M14 * point.X + M24 * point.Y + M34 * point.Z + M44;

            w = 1F / w;
            res.X *= w;
            res.Y *= w;
            res.Z *= w;
            return res;
        }

        // Transforms a position by this matrix, without a perspective divide. (fast)
        public DoubleVector3 MultiplyPoint3X4(DoubleVector3 point)
        {
            DoubleVector3 res;
            res.X = M11 * point.X + M21 * point.Y + M31 * point.Z + M41;
            res.Y = M12 * point.X + M22 * point.Y + M32 * point.Z + M42;
            res.Z = M13 * point.X + M23 * point.Y + M33 * point.Z + M43;
            return res;
        }

        // Transforms a direction by this matrix.
        public DoubleVector3 MultiplyVector(DoubleVector3 vector)
        {
            DoubleVector3 res;
            res.X = M11 * vector.X + M21 * vector.Y + M31 * vector.Z;
            res.Y = M12 * vector.X + M22 * vector.Y + M32 * vector.Z;
            res.Z = M13 * vector.X + M23 * vector.Y + M33 * vector.Z;
            return res;
        }

        // Creates a scaling matrix.
        public static DoubleMatrix4X4 Scale(Vector3 vector)
        {
            DoubleMatrix4X4 m;
            m.M11 = vector.x;
            m.M21 = 0F;
            m.M31 = 0F;
            m.M41 = 0F;
            m.M12 = 0F;
            m.M22 = vector.y;
            m.M32 = 0F;
            m.M42 = 0F;
            m.M13 = 0F;
            m.M23 = 0F;
            m.M33 = vector.z;
            m.M43 = 0F;
            m.M14 = 0F;
            m.M24 = 0F;
            m.M34 = 0F;
            m.M44 = 1F;
            return m;
        }

        // Creates a translation matrix.
        public static DoubleMatrix4X4 Translate(Vector3 vector)
        {
            DoubleMatrix4X4 m;
            m.M11 = 1F;
            m.M21 = 0F;
            m.M31 = 0F;
            m.M41 = vector.x;
            m.M12 = 0F;
            m.M22 = 1F;
            m.M32 = 0F;
            m.M42 = vector.y;
            m.M13 = 0F;
            m.M23 = 0F;
            m.M33 = 1F;
            m.M43 = vector.z;
            m.M14 = 0F;
            m.M24 = 0F;
            m.M34 = 0F;
            m.M44 = 1F;
            return m;
        }

        // Creates a rotation matrix. Note: Assumes unit quaternion
        public static DoubleMatrix4X4 Rotate(Quaternion q)
        {
            // Precalculate coordinate products
            float x = q.x * 2.0F;
            float y = q.y * 2.0F;
            float z = q.z * 2.0F;
            float xx = q.x * x;
            float yy = q.y * y;
            float zz = q.z * z;
            float xy = q.x * y;
            float xz = q.x * z;
            float yz = q.y * z;
            float wx = q.w * x;
            float wy = q.w * y;
            float wz = q.w * z;

            // Calculate 3x3 matrix from orthonormal basis
            DoubleMatrix4X4 m;
            m.M11 = 1.0f - (yy + zz);
            m.M12 = xy + wz;
            m.M13 = xz - wy;
            m.M14 = 0.0F;
            m.M21 = xy - wz;
            m.M22 = 1.0f - (xx + zz);
            m.M23 = yz + wx;
            m.M24 = 0.0F;
            m.M31 = xz + wy;
            m.M32 = yz - wx;
            m.M33 = 1.0f - (xx + yy);
            m.M34 = 0.0F;
            m.M41 = 0.0F;
            m.M42 = 0.0F;
            m.M43 = 0.0F;
            m.M44 = 1.0F;
            return m;
        }

        // Matrix4x4.zero is of questionable usefulness considering C# sets everything to 0 by default, however:
        //  1. it's consistent with other Math structs in Unity such as Vector2, Vector3 and Vector4,
        //  2. "Matrix4x4.zero" is arguably more readable than "new Matrix4x4()",
        //  3. it's already in the API ..
        static readonly DoubleMatrix4X4 ZeroMatrix = new DoubleMatrix4X4(new DoubleVector4(0, 0, 0, 0),
            new DoubleVector4(0, 0, 0, 0),
            new DoubleVector4(0, 0, 0, 0),
            new DoubleVector4(0, 0, 0, 0));

        // Returns a matrix with all elements set to zero (RO).
        public static DoubleMatrix4X4 Zero
        {
            get { return ZeroMatrix; }
        }

        static readonly DoubleMatrix4X4 IdentityMatrix = new DoubleMatrix4X4(new DoubleVector4(1, 0, 0, 0),
            new DoubleVector4(0, 1, 0, 0),
            new DoubleVector4(0, 0, 1, 0),
            new DoubleVector4(0, 0, 0, 1));

        // Returns the identity matrix (RO).
        public static DoubleMatrix4X4 Identity
        {
            get { return IdentityMatrix; }
        }

        public override string ToString()
        {
            return ToString(null, null);
        }

        public string ToString(string format)
        {
            return ToString(format, null);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format))
                format = "F5";
            if (formatProvider == null)
                formatProvider = CultureInfo.InvariantCulture.NumberFormat;
            return String.Format(
                "{0}\t{1}\t{2}\t{3}\n{4}\t{5}\t{6}\t{7}\n{8}\t{9}\t{10}\t{11}\n{12}\t{13}\t{14}\t{15}\n",
                M11.ToString(format, formatProvider), M21.ToString(format, formatProvider),
                M31.ToString(format, formatProvider), M41.ToString(format, formatProvider),
                M12.ToString(format, formatProvider), M22.ToString(format, formatProvider),
                M32.ToString(format, formatProvider), M42.ToString(format, formatProvider),
                M13.ToString(format, formatProvider), M23.ToString(format, formatProvider),
                M33.ToString(format, formatProvider), M43.ToString(format, formatProvider),
                M14.ToString(format, formatProvider), M24.ToString(format, formatProvider),
                M34.ToString(format, formatProvider), M44.ToString(format, formatProvider));
        }
    }
}