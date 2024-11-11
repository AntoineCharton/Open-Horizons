using System;
using System.Collections.Generic;
using BigWorld.Doubles;
using UnityEngine;
using UnityEngine.Serialization;

namespace BigWorld.Kepler
{
    /// <summary>
    /// Attractor data, necessary for calculation orbit.
    /// </summary>
    [Serializable]
    public class AttractorData
    {
        public Transform attractorObject;
        public double attractorMass = 1000;
        public double gravityConstant = 0.1f;
    }

    public class EllipseData
    {
        public double A;
        public double B;
        public double Eccentricity;
        public DoubleVector3 FocusDistance;
        public DoubleVector3 AxisMain;
        public DoubleVector3 AxisSecondary;
        public DoubleVector3 Center;
        public DoubleVector3 Focus0;
        public DoubleVector3 Focus1;

        public DoubleVector3 Normal
        {
            get { return DoubleVector3.Cross(AxisMain, AxisSecondary).normalized; }
        }

        public EllipseData(DoubleVector3 focus0, DoubleVector3 focus1, DoubleVector3 p0)
        {
            Focus0 = focus0;
            Focus1 = focus1;
            FocusDistance = Focus0 - Focus1;
            A = ((Focus0 - p0).magnitude + (focus1 - p0).magnitude) * 0.5;
            if (A < 0)
            {
                A = -A;
            }

            Eccentricity = (FocusDistance.magnitude * 0.5) / A;
            B = A * Math.Sqrt(1 - Eccentricity * Eccentricity);
            AxisMain = FocusDistance.normalized;
            var tempNorm = DoubleVector3.Cross(AxisMain, p0 - Focus0).normalized;
            AxisSecondary = DoubleVector3.Cross(AxisMain, tempNorm).normalized;
            Center = Focus1 + FocusDistance * 0.5;
        }

        /// <summary>
        /// Get point on ellipse at specified angle from center.
        /// </summary>
        /// <param name="eccentricAnomaly">Angle from center in radians</param>
        /// <returns></returns>
        public DoubleVector3 GetSamplePoint(double eccentricAnomaly)
        {
            return Center + AxisMain * (A * Math.Cos(eccentricAnomaly)) +
                   AxisSecondary * (B * Math.Sin(eccentricAnomaly));
        }

        /// <summary>
        /// Calculate eccentric anomaly in radians for point.
        /// </summary>
        /// <param name="point">Point in plane of elliptic shape.</param>
        /// <returns>Eccentric anomaly radians.</returns>
        public double GetEccentricAnomalyForPoint(DoubleVector3 point)
        {
            var vector = point - Focus0;
            var trueAnomaly = DoubleVector3.Angle(vector, AxisMain) * KeplerOrbitUtils.Deg2Rad;
            if (DoubleVector3.Dot(vector, AxisSecondary) > 0)
            {
                trueAnomaly = KeplerOrbitUtils.PI2 - trueAnomaly;
            }

            var result = KeplerOrbitUtils.ConvertTrueToEccentricAnomaly(trueAnomaly, Eccentricity);
            return result;
        }
    }

    public class HyperbolaData
    {
        public double A;
        public double B;
        public double C;
        public double Eccentricity;
        public DoubleVector3 Center;
        public DoubleVector3 FocusDistance;
        public DoubleVector3 Focus0;
        public DoubleVector3 Focus1;
        public DoubleVector3 AxisMain;
        public DoubleVector3 AxisSecondary;

        public DoubleVector3 Normal
        {
            get { return DoubleVector3.Cross(AxisMain, AxisSecondary).normalized; }
        }

        /// <summary>
        /// Construct new hyperbola from 2 focuses and a point on one of branches.
        /// </summary>
        /// <param name="focus0">Focus of branch 0.</param>
        /// <param name="focus1">Focus of branch 1.</param>
        /// <param name="p0">Point on hyperbola branch 0.</param>
        public HyperbolaData(DoubleVector3 focus0, DoubleVector3 focus1, DoubleVector3 p0)
        {
            Initialize(focus0, focus1, p0);
        }

        private void Initialize(DoubleVector3 focus0, DoubleVector3 focus1, DoubleVector3 p0)
        {
            Focus0 = focus0;
            Focus1 = focus1;
            FocusDistance = Focus1 - Focus0;
            AxisMain = FocusDistance.normalized;
            var tempNormal = DoubleVector3.Cross(AxisMain, p0 - Focus0).normalized;
            AxisSecondary = DoubleVector3.Cross(AxisMain, tempNormal).normalized;
            C = FocusDistance.magnitude * 0.5;
            A = Math.Abs(((p0 - Focus0).magnitude - (p0 - Focus1).magnitude)) * 0.5;
            Eccentricity = C / A;
            B = Math.Sqrt(C * C - A * A);
            Center = focus0 + FocusDistance * 0.5;
        }

        /// <summary>
        /// Get point on hyperbola curve.
        /// </summary>
        /// <param name="hyperbolicCoordinate">Hyperbola's parametric function time parameter.</param>
        /// <param name="isMainBranch">Is taking first branch, or, if false, second branch.</param>
        /// <returns>Point on hyperbola at given time (-inf..inf).</returns>
        /// <remarks>
        /// First branch is considered the branch, which was specified in constructor of hyperboal with a point, laying on that branch.
        /// Therefore second branch is always opposite from that.
        /// </remarks>
        public DoubleVector3 GetSamplePointOnBranch(double hyperbolicCoordinate, bool isMainBranch)
        {
            double x = A * Math.Cosh(hyperbolicCoordinate);
            double y = B * Math.Sinh(hyperbolicCoordinate);
            DoubleVector3 result = Center + (isMainBranch ? AxisMain : -AxisMain) * x + AxisSecondary * y;

            return result;
        }
    }

    /// <summary>
    /// Orbit data container.
    /// Also contains methods for altering and updating orbit state.
    /// </summary>
    [Serializable]
    public class KeplerOrbitData
    {
        public double gravConst = 1;

        /// <summary>
        /// Normal of ecliptic plane.
        /// </summary>
        public static readonly DoubleVector3 EclipticNormal = new DoubleVector3(0, 0, 1);

        /// <summary>
        /// Up direction on ecliptic plane (y-axis on xy ecliptic plane).
        /// </summary>
        public static readonly DoubleVector3 EclipticUp = new DoubleVector3(0, 1, 0);

        /// <summary>
        /// Right vector on ecliptic plane (x-axis on xy ecliptic plane).
        /// </summary>
        public static readonly DoubleVector3 EclipticRight = new DoubleVector3(1, 0, 0);

        /// <summary>
        /// Body position relative to attractor or Focal Position.
        /// </summary>
        /// <remarks>
        /// Attractor (focus) is local center of orbit system.
        /// </remarks>
        [FormerlySerializedAs("Position")] public DoubleVector3 position;

        /// <summary>
        /// Magnitude of body position vector.
        /// </summary>
        [FormerlySerializedAs("AttractorDistance")] public double attractorDistance;

        /// <summary>
        /// Attractor point mass.
        /// </summary>
        [FormerlySerializedAs("AttractorMass")] public double attractorMass;

        /// <summary>
        /// Body velocity vector relative to attractor.
        /// </summary>
        [FormerlySerializedAs("Velocity")] public DoubleVector3 velocity;

        /// <summary>
        /// Gravitational parameter of system.
        /// </summary>
        [FormerlySerializedAs("MG")] public double mg;

        public double semiMinorAxis;
        public double semiMajorAxis;
        public double focalParameter;
        public double eccentricity;
        public double period;
        public double trueAnomaly;
        public double meanAnomaly;
        public double eccentricAnomaly;
        public double meanMotion;
        public DoubleVector3 periapsis;
        public double periapsisDistance;
        public DoubleVector3 apoapsis;
        public double apoapsisDistance;
        public DoubleVector3 centerPoint;
        public double orbitCompressionRatio;
        public DoubleVector3 orbitNormal;
        public DoubleVector3 semiMinorAxisBasis;
        public DoubleVector3 semiMajorAxisBasis;

        /// <summary>
        /// if > 0, then orbit motion is clockwise
        /// </summary>
        public double orbitNormalDotEclipticNormal;

        public double EnergyTotal
        {
            get { return velocity.sqrMagnitude - 2 * mg / attractorDistance; }
        }

        /// <summary>
        /// The orbit inclination in radians relative to ecliptic plane.
        /// </summary>
        public double Inclination
        {
            get
            {
                var dot = DoubleVector3.Dot(orbitNormal, EclipticNormal);
                return Math.Acos(dot);
            }
        }

        /// <summary>
        /// Ascending node longitude in radians.
        /// </summary>
        public double AscendingNodeLongitude
        {
            get
            {
                var ascNodeDir = DoubleVector3.Cross(EclipticNormal, orbitNormal).normalized;
                var dot = DoubleVector3.Dot(ascNodeDir, EclipticRight);
                var angle = Math.Acos(dot);
                if (DoubleVector3.Dot(DoubleVector3.Cross(ascNodeDir, EclipticRight), EclipticNormal) >= 0)
                {
                    angle = KeplerOrbitUtils.PI2 - angle;
                }

                return angle;
            }
        }

        /// <summary>
        /// Angle between main orbit axis and ecliptic 0 axis in radians.
        /// </summary>
        public double ArgumentOfPerifocus
        {
            get
            {
                var ascNodeDir = DoubleVector3.Cross(EclipticNormal, orbitNormal).normalized;
                var dot = DoubleVector3.Dot(ascNodeDir, semiMajorAxisBasis.normalized);
                var angle = Math.Acos(dot);
                if (DoubleVector3.Dot(DoubleVector3.Cross(ascNodeDir, semiMajorAxisBasis), orbitNormal) < 0)
                {
                    angle = KeplerOrbitUtils.PI2 - angle;
                }

                return angle;
            }
        }

        /// <summary>
        /// Is orbit state valid and error-free.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is valid orbit; otherwise, <c>false</c>.
        /// </value>
        public bool IsValidOrbit
        {
            get
            {
                return eccentricity >= 0
                       && period > 0
                       && attractorMass > 0;
            }
        }

        /// <summary>
        /// Create new orbit state without initialization. Manual orbit initialization is required.
        /// </summary>
        /// <remarks>
        /// To manually initialize orbit, fill known orbital elements and then call CalculateOrbitStateFrom... method.
        /// </remarks>
        public KeplerOrbitData()
        {
        }

        /// <summary>
        /// Create and initialize new orbit state.
        /// </summary>
        /// <param name="position">Body local position, relative to attractor.</param>
        /// <param name="velocity">Body local velocity.</param>
        /// <param name="attractorMass">Attractor mass.</param>
        /// <param name="gConst">Gravitational Constant.</param>
        public KeplerOrbitData(DoubleVector3 position, DoubleVector3 velocity, double attractorMass, double gConst)
        {
            this.position = position;
            this.velocity = velocity;
            this.attractorMass = attractorMass;
            this.gravConst = gConst;
            CalculateOrbitStateFromOrbitalVectors();
        }

        /// <summary>
        /// Create and initialize new orbit state from orbital elements.
        /// </summary>
        /// <param name="eccentricity">Eccentricity.</param>
        /// <param name="semiMajorAxis">Main axis semi width.</param>
        /// <param name="meanAnomalyDeg">Mean anomaly in degrees.</param>
        /// <param name="inclinationDeg">Orbit inclination in degrees.</param>
        /// <param name="argOfPerifocusDeg">Orbit argument of perifocus in degrees.</param>
        /// <param name="ascendingNodeDeg">Longitude of ascending node in degrees.</param>
        /// <param name="attractorMass">Attractor mass.</param>
        /// <param name="gConst">Gravitational constant.</param>
        public KeplerOrbitData(double eccentricity, double semiMajorAxis, double meanAnomalyDeg, double inclinationDeg,
            double argOfPerifocusDeg, double ascendingNodeDeg, double attractorMass,
            double gConst)
        {
            this.eccentricity = Math.Max(eccentricity, 0.00005f);
            this.semiMajorAxis = semiMajorAxis;
            if (eccentricity < 1.0)
            {
                this.semiMinorAxis = this.semiMajorAxis * Math.Sqrt(1 - this.eccentricity * this.eccentricity);
            }
            else if (eccentricity > 1.0)
            {
                this.semiMinorAxis = this.semiMajorAxis * Math.Sqrt(this.eccentricity * this.eccentricity - 1);
            }
            else
            {
                this.semiMajorAxis = 0;
            }

            var normal = EclipticNormal.normalized;
            var ascendingNode = EclipticRight.normalized;

            ascendingNodeDeg %= 360;
            if (ascendingNodeDeg > 180) ascendingNodeDeg -= 360;
            inclinationDeg %= 360;
            if (inclinationDeg > 180) inclinationDeg -= 360;
            argOfPerifocusDeg %= 360;
            if (argOfPerifocusDeg > 180) argOfPerifocusDeg -= 360;

            ascendingNode = KeplerOrbitUtils
                .RotateVectorByAngle(ascendingNode, ascendingNodeDeg * KeplerOrbitUtils.Deg2Rad, normal).normalized;
            normal = KeplerOrbitUtils
                .RotateVectorByAngle(normal, inclinationDeg * KeplerOrbitUtils.Deg2Rad, ascendingNode).normalized;
            periapsis = ascendingNode;
            periapsis = KeplerOrbitUtils
                .RotateVectorByAngle(periapsis, argOfPerifocusDeg * KeplerOrbitUtils.Deg2Rad, normal).normalized;

            this.semiMajorAxisBasis = periapsis;
            this.semiMinorAxisBasis = DoubleVector3.Cross(periapsis, normal);

            this.meanAnomaly = meanAnomalyDeg * KeplerOrbitUtils.Deg2Rad;
            this.eccentricAnomaly = KeplerOrbitUtils.ConvertMeanToEccentricAnomaly(this.meanAnomaly, this.eccentricity);
            this.trueAnomaly = KeplerOrbitUtils.ConvertEccentricToTrueAnomaly(this.eccentricAnomaly, this.eccentricity);
            this.attractorMass = attractorMass;
            this.gravConst = gConst;
            CalculateOrbitStateFromOrbitalElements();
        }

        /// <summary>
        /// Create and initialize new orbit state from orbital elements and main axis vectors.
        /// </summary>
        /// <param name="eccentricity">Eccentricity.</param>
        /// <param name="semiMajorAxis">Semi major axis vector.</param>
        /// <param name="semiMinorAxis">Semi minor axis vector.</param>
        /// <param name="meanAnomalyDeg">Mean anomaly in degrees.</param>
        /// <param name="attractorMass">Attractor mass.</param>
        /// <param name="gConst">Gravitational constant.</param>
        public KeplerOrbitData(double eccentricity, DoubleVector3 semiMajorAxis, DoubleVector3 semiMinorAxis,
            double meanAnomalyDeg, double attractorMass, double gConst)
        {
            this.eccentricity = eccentricity;
            this.semiMajorAxisBasis = semiMajorAxis.normalized;
            this.semiMinorAxisBasis = semiMinorAxis.normalized;
            this.semiMajorAxis = semiMajorAxis.magnitude;
            this.semiMinorAxis = semiMinorAxis.magnitude;

            this.meanAnomaly = meanAnomalyDeg * KeplerOrbitUtils.Deg2Rad;
            this.eccentricAnomaly = KeplerOrbitUtils.ConvertMeanToEccentricAnomaly(this.meanAnomaly, this.eccentricity);
            this.trueAnomaly = KeplerOrbitUtils.ConvertEccentricToTrueAnomaly(this.eccentricAnomaly, this.eccentricity);
            this.attractorMass = attractorMass;
            this.gravConst = gConst;
            CalculateOrbitStateFromOrbitalElements();
        }

        /// <summary>
        /// Calculates full orbit state from cartesian vectors: current body position, velocity, attractor mass, and gravConstant.
        /// </summary>
        public void CalculateOrbitStateFromOrbitalVectors()
        {
            mg = attractorMass * gravConst;
            attractorDistance = position.magnitude;
            DoubleVector3 angularMomentumVector = DoubleVector3.Cross(position, velocity);
            orbitNormal = angularMomentumVector.normalized;
            DoubleVector3 eccVector;
            if (orbitNormal.sqrMagnitude < 0.99)
            {
                orbitNormal = DoubleVector3.Cross(position, EclipticUp).normalized;
                eccVector = new DoubleVector3();
            }
            else
            {
                eccVector = DoubleVector3.Cross(velocity, angularMomentumVector) / mg - position / attractorDistance;
            }

            orbitNormalDotEclipticNormal = DoubleVector3.Dot(orbitNormal, EclipticNormal);
            focalParameter = angularMomentumVector.sqrMagnitude / mg;
            eccentricity = eccVector.magnitude;
            semiMinorAxisBasis = DoubleVector3.Cross(angularMomentumVector, -eccVector).normalized;
            if (semiMinorAxisBasis.sqrMagnitude < 0.99)
            {
                semiMinorAxisBasis = DoubleVector3.Cross(orbitNormal, position).normalized;
            }

            semiMajorAxisBasis = DoubleVector3.Cross(orbitNormal, semiMinorAxisBasis).normalized;
            if (eccentricity < 1.0)
            {
                orbitCompressionRatio = 1 - eccentricity * eccentricity;
                semiMajorAxis = focalParameter / orbitCompressionRatio;
                semiMinorAxis = semiMajorAxis * Math.Sqrt(orbitCompressionRatio);
                centerPoint = -semiMajorAxis * eccVector;
                var p = Math.Sqrt(Math.Pow(semiMajorAxis, 3) / mg);
                period = KeplerOrbitUtils.PI2 * p;
                meanMotion = 1d / p;
                apoapsis = centerPoint - semiMajorAxisBasis * semiMajorAxis;
                periapsis = centerPoint + semiMajorAxisBasis * semiMajorAxis;
                periapsisDistance = periapsis.magnitude;
                apoapsisDistance = apoapsis.magnitude;
                trueAnomaly = DoubleVector3.Angle(position, semiMajorAxisBasis) * KeplerOrbitUtils.Deg2Rad;
                if (DoubleVector3.Dot(DoubleVector3.Cross(position, -semiMajorAxisBasis), orbitNormal) < 0)
                {
                    trueAnomaly = KeplerOrbitUtils.PI2 - trueAnomaly;
                }

                eccentricAnomaly = KeplerOrbitUtils.ConvertTrueToEccentricAnomaly(trueAnomaly, eccentricity);
                meanAnomaly = eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly);
            }
            else if (eccentricity > 1.0)
            {
                orbitCompressionRatio = eccentricity * eccentricity - 1;
                semiMajorAxis = focalParameter / orbitCompressionRatio;
                semiMinorAxis = semiMajorAxis * Math.Sqrt(orbitCompressionRatio);
                centerPoint = semiMajorAxis * eccVector;
                period = double.PositiveInfinity;
                meanMotion = Math.Sqrt(mg / Math.Pow(semiMajorAxis, 3));
                apoapsis = new DoubleVector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
                periapsis = centerPoint - semiMajorAxisBasis * (semiMajorAxis);
                periapsisDistance = periapsis.magnitude;
                apoapsisDistance = double.PositiveInfinity;
                trueAnomaly = DoubleVector3.Angle(position, eccVector) * KeplerOrbitUtils.Deg2Rad;
                if (DoubleVector3.Dot(DoubleVector3.Cross(position, -semiMajorAxisBasis), orbitNormal) < 0)
                {
                    trueAnomaly = -trueAnomaly;
                }

                eccentricAnomaly = KeplerOrbitUtils.ConvertTrueToEccentricAnomaly(trueAnomaly, eccentricity);
                meanAnomaly = Math.Sinh(eccentricAnomaly) * eccentricity - eccentricAnomaly;
            }
            else
            {
                orbitCompressionRatio = 0;
                semiMajorAxis = 0;
                semiMinorAxis = 0;
                periapsisDistance = angularMomentumVector.sqrMagnitude / mg;
                centerPoint = new DoubleVector3();
                periapsis = -periapsisDistance * semiMinorAxisBasis;
                period = double.PositiveInfinity;
                meanMotion = Math.Sqrt(mg / Math.Pow(periapsisDistance, 3));
                apoapsis = new DoubleVector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
                apoapsisDistance = double.PositiveInfinity;
                trueAnomaly = DoubleVector3.Angle(position, eccVector) * KeplerOrbitUtils.Deg2Rad;
                if (DoubleVector3.Dot(DoubleVector3.Cross(position, -semiMajorAxisBasis), orbitNormal) < 0)
                {
                    trueAnomaly = -trueAnomaly;
                }

                eccentricAnomaly = KeplerOrbitUtils.ConvertTrueToEccentricAnomaly(trueAnomaly, eccentricity);
                meanAnomaly = Math.Sinh(eccentricAnomaly) * eccentricity - eccentricAnomaly;
            }
        }

        /// <summary>
        /// Calculates the full state of orbit from current orbital elements: eccentricity, mean anomaly, semi major and semi minor axis.
        /// </summary>
        /// <remarks>
        /// Update orbital state using known main orbital elements and basis axis vectors.
        /// Can be used for first initialization of orbit state, in this case initial data must be filled before this method call.
        /// Required initial data: eccentricity, mean anomaly, inclination, attractor mass, grav constant, all anomalies, semi minor and semi major axis vectors and magnitudes.
        /// Note that semi minor and semi major axis must be fully precalculated from inclination and argument of periapsis or another source data;
        /// </remarks>
        public void CalculateOrbitStateFromOrbitalElements()
        {
            mg = attractorMass * gravConst;
            orbitNormal = -DoubleVector3.Cross(semiMajorAxisBasis, semiMinorAxisBasis).normalized;
            orbitNormalDotEclipticNormal = DoubleVector3.Dot(orbitNormal, EclipticNormal);
            if (eccentricity < 1.0)
            {
                orbitCompressionRatio = 1 - eccentricity * eccentricity;
                centerPoint = -semiMajorAxisBasis * semiMajorAxis * eccentricity;
                period = KeplerOrbitUtils.PI2 * Math.Sqrt(Math.Pow(semiMajorAxis, 3) / mg);
                meanMotion = KeplerOrbitUtils.PI2 / period;
                apoapsis = centerPoint - semiMajorAxisBasis * semiMajorAxis;
                periapsis = centerPoint + semiMajorAxisBasis * semiMajorAxis;
                periapsisDistance = periapsis.magnitude;
                apoapsisDistance = apoapsis.magnitude;
                // All anomalies state already preset.
            }
            else if (eccentricity > 1.0)
            {
                centerPoint = semiMajorAxisBasis * semiMajorAxis * eccentricity;
                period = double.PositiveInfinity;
                meanMotion = Math.Sqrt(mg / Math.Pow(semiMajorAxis, 3));
                apoapsis = new DoubleVector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
                periapsis = centerPoint - semiMajorAxisBasis * (semiMajorAxis);
                periapsisDistance = periapsis.magnitude;
                apoapsisDistance = double.PositiveInfinity;
            }
            else
            {
                centerPoint = new DoubleVector3();
                period = double.PositiveInfinity;
                meanMotion = Math.Sqrt(mg * 0.5 / Math.Pow(periapsisDistance, 3));
                apoapsis = new DoubleVector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
                periapsisDistance = semiMajorAxis;
                semiMajorAxis = 0;
                periapsis = -periapsisDistance * semiMajorAxisBasis;
                apoapsisDistance = double.PositiveInfinity;
            }

            position = GetFocalPositionAtEccentricAnomaly(eccentricAnomaly);
            double compresion =
                eccentricity < 1 ? (1 - eccentricity * eccentricity) : (eccentricity * eccentricity - 1);
            focalParameter = semiMajorAxis * compresion;
            velocity = GetVelocityAtTrueAnomaly(this.trueAnomaly);
            attractorDistance = position.magnitude;
        }

        /// <summary>
        /// Gets the velocity vector value at eccentric anomaly.
        /// </summary>
        /// <param name="eccentricAnomaly">The eccentric anomaly.</param>
        /// <returns>Velocity vector.</returns>
        public DoubleVector3 GetVelocityAtEccentricAnomaly(double eccentricAnomaly)
        {
            return GetVelocityAtTrueAnomaly(
                KeplerOrbitUtils.ConvertEccentricToTrueAnomaly(eccentricAnomaly, eccentricity));
        }

        /// <summary>
        /// Gets the velocity value at true anomaly.
        /// </summary>
        /// <param name="trueAnomaly">The true anomaly.</param>
        /// <returns>Velocity vector.</returns>
        public DoubleVector3 GetVelocityAtTrueAnomaly(double trueAnomaly)
        {
            if (focalParameter <= 0)
            {
                return new DoubleVector3();
            }

            double sqrtMGdivP = Math.Sqrt(attractorMass * gravConst / focalParameter);
            double vX = sqrtMGdivP * (eccentricity + Math.Cos(trueAnomaly));
            double vY = sqrtMGdivP * Math.Sin(trueAnomaly);
            return -semiMinorAxisBasis * vX - semiMajorAxisBasis * vY;
        }

        /// <summary>
        /// Gets the central position at true anomaly.
        /// </summary>
        /// <param name="trueAnomaly">The true anomaly.</param>
        /// <returns>Position relative to orbit center.</returns>
        /// <remarks>
        /// Note: central position is not same as focal position.
        /// </remarks>
        public DoubleVector3 GetCentralPositionAtTrueAnomaly(double trueAnomaly)
        {
            double ecc = KeplerOrbitUtils.ConvertTrueToEccentricAnomaly(trueAnomaly, eccentricity);
            return GetCentralPositionAtEccentricAnomaly(ecc);
        }

        /// <summary>
        /// Gets the central position at eccentric anomaly.
        /// </summary>
        /// <param name="eccentricAnomaly">The eccentric anomaly.</param>
        /// <returns>Position relative to orbit center.</returns>
        /// <remarks>
        /// Note: central position is not same as focal position.
        /// </remarks>
        public DoubleVector3 GetCentralPositionAtEccentricAnomaly(double eccentricAnomaly)
        {
            if (eccentricity < 1.0)
            {
                DoubleVector3 result = new DoubleVector3(Math.Sin(eccentricAnomaly) * semiMinorAxis,
                    -Math.Cos(eccentricAnomaly) * semiMajorAxis);
                return -semiMinorAxisBasis * result.X - semiMajorAxisBasis * result.Y;
            }
            else if (eccentricity > 1.0)
            {
                DoubleVector3 result = new DoubleVector3(Math.Sinh(eccentricAnomaly) * semiMinorAxis,
                    Math.Cosh(eccentricAnomaly) * semiMajorAxis);
                return -semiMinorAxisBasis * result.X - semiMajorAxisBasis * result.Y;
            }
            else
            {
                var pos = new DoubleVector3(
                    periapsisDistance * Math.Sin(eccentricAnomaly) / (1.0 + Math.Cos(eccentricAnomaly)),
                    periapsisDistance * Math.Cos(eccentricAnomaly) / (1.0 + Math.Cos(eccentricAnomaly)));
                return -semiMinorAxisBasis * pos.X + semiMajorAxisBasis * pos.Y;
            }
        }

        /// <summary>
        /// Gets the focal position at eccentric anomaly.
        /// </summary>
        /// <param name="eccentricAnomaly">The eccentric anomaly.</param>
        /// <returns>Position relative to attractor (focus).</returns>
        public DoubleVector3 GetFocalPositionAtEccentricAnomaly(double eccentricAnomaly)
        {
            return GetCentralPositionAtEccentricAnomaly(eccentricAnomaly) + centerPoint;
        }

        /// <summary>
        /// Gets the focal position at true anomaly.
        /// </summary>
        /// <param name="trueAnomaly">The true anomaly.</param>
        /// <returns>Position relative to attractor (focus).</returns>
        public DoubleVector3 GetFocalPositionAtTrueAnomaly(double trueAnomaly)
        {
            return GetCentralPositionAtTrueAnomaly(trueAnomaly) + centerPoint;
        }

        /// <summary>
        /// Gets the central position.
        /// </summary>
        /// <returns>Position relative to orbit center.</returns>
        /// <remarks>
        /// Note: central position is not same as focal position.
        /// Orbit center point is geometric center of orbit ellipse, which is the further from attractor (Focus point) the larger eccentricity is.
        /// </remarks>
        public DoubleVector3 GetCentralPosition()
        {
            return position - centerPoint;
        }

        /// <summary>
        /// Gets orbit sample points with defined precision.
        /// </summary>
        /// <param name="pointsCount">The points count.</param>
        /// <param name="maxDistance">The maximum distance of points.</param>
        /// <returns>Array of orbit curve points.</returns>
        public DoubleVector3[] GetOrbitPoints(int pointsCount = 50, double maxDistance = 1000d)
        {
            return GetOrbitPoints(pointsCount, new DoubleVector3(), maxDistance);
        }

        /// <summary>
        /// Gets orbit sample points with defined precision.
        /// </summary>
        /// <param name="pointsCount">The points count.</param>
        /// <param name="origin">The origin.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <returns>Array of orbit curve points.</returns>
        public DoubleVector3[] GetOrbitPoints(int pointsCount, DoubleVector3 origin, double maxDistance = 1000d)
        {
            if (pointsCount < 2)
            {
                return new DoubleVector3[0];
            }

            DoubleVector3[] result = new DoubleVector3[pointsCount];
            if (eccentricity < 1.0)
            {
                if (apoapsisDistance < maxDistance)
                {
                    for (int i = 0; i < pointsCount; i++)
                    {
                        result[i] = GetFocalPositionAtEccentricAnomaly(i * KeplerOrbitUtils.PI2 / (pointsCount - 1d)) +
                                    origin;
                    }
                }
                else if (eccentricity > 1.0)
                {
                    double maxAngle = KeplerOrbitUtils.CalcTrueAnomalyForDistance(maxDistance, eccentricity,
                        semiMajorAxis, periapsisDistance);
                    for (int i = 0; i < pointsCount; i++)
                    {
                        result[i] = GetFocalPositionAtTrueAnomaly(-maxAngle + i * 2d * maxAngle / (pointsCount - 1)) +
                                    origin;
                    }
                }
                else
                {
                    double maxAngle = KeplerOrbitUtils.CalcTrueAnomalyForDistance(maxDistance, eccentricity,
                        periapsisDistance, periapsisDistance);
                    for (int i = 0; i < pointsCount; i++)
                    {
                        result[i] = GetFocalPositionAtTrueAnomaly(-maxAngle + i * 2d * maxAngle / (pointsCount - 1)) +
                                    origin;
                    }
                }
            }
            else
            {
                if (maxDistance < periapsisDistance)
                {
                    return new DoubleVector3[0];
                }

                double maxAngle =
                    KeplerOrbitUtils.CalcTrueAnomalyForDistance(maxDistance, eccentricity, semiMajorAxis,
                        periapsisDistance);

                for (int i = 0; i < pointsCount; i++)
                {
                    result[i] = GetFocalPositionAtTrueAnomaly(-maxAngle + i * 2d * maxAngle / (pointsCount - 1)) +
                                origin;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the orbit sample points without unnecessary memory alloc for resulting array.
        /// However, memory allocation may occur if resulting array has not correct length.
        /// </summary>
        /// <param name="orbitPoints">The orbit points.</param>
        /// <param name="pointsCount">The points count.</param>
        /// <param name="origin">The origin.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        public void GetOrbitPointsNoAlloc(ref DoubleVector3[] orbitPoints, int pointsCount, DoubleVector3 origin,
            float maxDistance = 1000f)
        {
            if (pointsCount < 2)
            {
                orbitPoints = new DoubleVector3[0];
                return;
            }

            if (eccentricity < 1)
            {
                if (orbitPoints == null || orbitPoints.Length != pointsCount)
                {
                    orbitPoints = new DoubleVector3[pointsCount];
                }

                if (apoapsisDistance < maxDistance)
                {
                    for (int i = 0; i < pointsCount; i++)
                    {
                        orbitPoints[i] =
                            GetFocalPositionAtEccentricAnomaly(i * KeplerOrbitUtils.PI2 / (pointsCount - 1d)) + origin;
                    }
                }
                else
                {
                    double maxAngle = KeplerOrbitUtils.CalcTrueAnomalyForDistance(maxDistance, eccentricity,
                        semiMajorAxis, periapsisDistance);
                    for (int i = 0; i < pointsCount; i++)
                    {
                        orbitPoints[i] =
                            GetFocalPositionAtTrueAnomaly(-maxAngle + i * 2d * maxAngle / (pointsCount - 1)) + origin;
                    }
                }
            }
            else
            {
                if (maxDistance < periapsisDistance)
                {
                    orbitPoints = new DoubleVector3[0];
                    return;
                }

                if (orbitPoints == null || orbitPoints.Length != pointsCount)
                {
                    orbitPoints = new DoubleVector3[pointsCount];
                }

                double maxAngle =
                    KeplerOrbitUtils.CalcTrueAnomalyForDistance(maxDistance, eccentricity, semiMajorAxis,
                        periapsisDistance);

                for (int i = 0; i < pointsCount; i++)
                {
                    orbitPoints[i] = GetFocalPositionAtTrueAnomaly(-maxAngle + i * 2d * maxAngle / (pointsCount - 1)) +
                                     origin;
                }
            }
        }

        /// <summary>
        /// Gets the ascending node of orbit.
        /// </summary>
        /// <param name="asc">The asc.</param>
        /// <returns><c>true</c> if ascending node exists, otherwise <c>false</c></returns>
        public bool GetAscendingNode(out DoubleVector3 asc)
        {
            DoubleVector3 ascNodeDir = DoubleVector3.Cross(orbitNormal, EclipticNormal);
            bool s = DoubleVector3.Dot(DoubleVector3.Cross(ascNodeDir, semiMajorAxisBasis), orbitNormal) >= 0;
            double ecc;
            double trueAnom = DoubleVector3.Angle(ascNodeDir, centerPoint) * KeplerOrbitUtils.Deg2Rad;
            if (eccentricity < 1.0)
            {
                double cosT = Math.Cos(trueAnom);
                ecc = Math.Acos((eccentricity + cosT) / (1d + eccentricity * cosT));
                if (!s)
                {
                    ecc = KeplerOrbitUtils.PI2 - ecc;
                }
            }
            else if (eccentricity > 1.0)
            {
                trueAnom = DoubleVector3.Angle(-ascNodeDir, centerPoint) * KeplerOrbitUtils.Deg2Rad;
                if (trueAnom >= Math.Acos(-1d / eccentricity))
                {
                    asc = new DoubleVector3();
                    return false;
                }

                double cosT = Math.Cos(trueAnom);
                ecc = KeplerOrbitUtils.Acosh((eccentricity + cosT) / (1 + eccentricity * cosT)) * (!s ? -1 : 1);
            }
            else
            {
                asc = new DoubleVector3();
                return false;
            }

            asc = GetFocalPositionAtEccentricAnomaly(ecc);
            return true;
        }

        /// <summary>
        /// Gets the descending node orbit.
        /// </summary>
        /// <param name="desc">The desc.</param>
        /// <returns><c>true</c> if descending node exists, otherwise <c>false</c></returns>
        public bool GetDescendingNode(out DoubleVector3 desc)
        {
            DoubleVector3 norm = DoubleVector3.Cross(orbitNormal, EclipticNormal);
            bool s = DoubleVector3.Dot(DoubleVector3.Cross(norm, semiMajorAxisBasis), orbitNormal) < 0;
            double ecc;
            double trueAnom = DoubleVector3.Angle(norm, -centerPoint) * KeplerOrbitUtils.Deg2Rad;
            if (eccentricity < 1.0)
            {
                double cosT = Math.Cos(trueAnom);
                ecc = Math.Acos((eccentricity + cosT) / (1d + eccentricity * cosT));
                if (s)
                {
                    ecc = KeplerOrbitUtils.PI2 - ecc;
                }
            }
            else if (eccentricity > 1.0)
            {
                trueAnom = DoubleVector3.Angle(norm, centerPoint) * KeplerOrbitUtils.Deg2Rad;
                if (trueAnom >= Math.Acos(-1d / eccentricity))
                {
                    desc = new DoubleVector3();
                    return false;
                }

                double cosT = Math.Cos(trueAnom);
                ecc = KeplerOrbitUtils.Acosh((eccentricity + cosT) / (1 + eccentricity * cosT)) * (s ? -1 : 1);
            }
            else
            {
                desc = new DoubleVector3();
                return false;
            }

            desc = GetFocalPositionAtEccentricAnomaly(ecc);
            return true;
        }

        /// <summary>
        /// Updates the kepler orbit dynamic state (anomalies, position, velocity) by defined deltatime.
        /// </summary>
        /// <param name="deltaTime">The delta time.</param>
        public void UpdateOrbitDataByTime(double deltaTime)
        {
            UpdateOrbitAnomaliesByTime(deltaTime);
            SetPositionByCurrentAnomaly();
            SetVelocityByCurrentAnomaly();
        }

        /// <summary>
        /// Updates the value of orbital anomalies by defined deltatime.
        /// </summary>
        /// <param name="deltaTime">The delta time.</param>
        /// <remarks>
        /// Only anomalies values will be changed. 
        /// Position and velocity states needs to be updated too after this method call.
        /// </remarks>
        public void UpdateOrbitAnomaliesByTime(double deltaTime)
        {
            if (eccentricity < 1.0)
            {
                meanAnomaly += meanMotion * deltaTime;
                meanAnomaly %= KeplerOrbitUtils.PI2;

                if (meanAnomaly < 0)
                {
                    meanAnomaly = KeplerOrbitUtils.PI2 - meanAnomaly;
                }

                eccentricAnomaly = KeplerOrbitUtils.KeplerSolver(meanAnomaly, eccentricity);
                double cosE = Math.Cos(eccentricAnomaly);
                trueAnomaly = Math.Acos((cosE - eccentricity) / (1 - eccentricity * cosE));
                if (meanAnomaly > Math.PI)
                {
                    trueAnomaly = KeplerOrbitUtils.PI2 - trueAnomaly;
                }
            }
            else if (eccentricity > 1.0)
            {
                meanAnomaly = meanAnomaly + meanMotion * deltaTime;
                eccentricAnomaly = KeplerOrbitUtils.KeplerSolverHyperbolicCase(meanAnomaly, eccentricity);
                trueAnomaly = Math.Atan2(Math.Sqrt(eccentricity * eccentricity - 1.0) * Math.Sinh(eccentricAnomaly),
                    eccentricity - Math.Cosh(eccentricAnomaly));
            }
            else
            {
                meanAnomaly = meanAnomaly + meanMotion * deltaTime;
                eccentricAnomaly = KeplerOrbitUtils.ConvertMeanToEccentricAnomaly(meanAnomaly, eccentricity);
                trueAnomaly = eccentricAnomaly;
            }
        }

        /// <summary>
        /// Get orbit time for elliption orbits at current anomaly. Result is in range [0..Period].
        /// </summary>
        /// <returns>Time, corresponding to current anomaly</returns>
        public double GetCurrentOrbitTime()
        {
            if (eccentricity < 1.0)
            {
                if (period > 0 && period < double.PositiveInfinity)
                {
                    var anomaly = meanAnomaly % KeplerOrbitUtils.PI2;
                    if (anomaly < 0)
                    {
                        anomaly = KeplerOrbitUtils.PI2 - anomaly;
                    }

                    return anomaly / KeplerOrbitUtils.PI2 * period;
                }

                return 0.0;
            }
            else if (eccentricity > 1.0)
            {
                var meanMotion = this.meanMotion;
                if (meanMotion > 0)
                {
                    return meanAnomaly / meanMotion;
                }

                return 0.0;
            }
            else
            {
                var meanMotion = this.meanMotion;
                if (meanMotion > 0)
                {
                    return meanAnomaly / meanMotion;
                }

                return 0.0;
            }
        }

        /// <summary>
        /// Updates position from anomaly state.
        /// </summary>
        public void SetPositionByCurrentAnomaly()
        {
            position = GetFocalPositionAtEccentricAnomaly(eccentricAnomaly);
        }

        /// <summary>
        /// Sets orbit velocity, calculated by current anomaly.
        /// </summary>
        public void SetVelocityByCurrentAnomaly()
        {
            velocity = GetVelocityAtEccentricAnomaly(eccentricAnomaly);
        }

        /// <summary>
        /// Sets the eccentricity and updates all corresponding orbit state values.
        /// </summary>
        /// <param name="e">The new eccentricity value.</param>
        public void SetEccentricity(double e)
        {
            if (!IsValidOrbit)
            {
                return;
            }

            e = Math.Abs(e);
            double periapsis = periapsisDistance; // Periapsis remains constant
            eccentricity = e;
            double compresion =
                eccentricity < 1 ? (1 - eccentricity * eccentricity) : (eccentricity * eccentricity - 1);
            semiMajorAxis = Math.Abs(periapsis / (1 - eccentricity));
            focalParameter = semiMajorAxis * compresion;
            semiMinorAxis = semiMajorAxis * Math.Sqrt(compresion);
            centerPoint = semiMajorAxis * Math.Abs(eccentricity) * semiMajorAxisBasis;
            if (eccentricity < 1.0)
            {
                eccentricAnomaly = KeplerOrbitUtils.KeplerSolver(meanAnomaly, eccentricity);
                double cosE = Math.Cos(eccentricAnomaly);
                trueAnomaly = Math.Acos((cosE - eccentricity) / (1 - eccentricity * cosE));
                if (meanAnomaly > Math.PI)
                {
                    trueAnomaly = KeplerOrbitUtils.PI2 - trueAnomaly;
                }
            }
            else if (eccentricity > 1.0)
            {
                eccentricAnomaly = KeplerOrbitUtils.KeplerSolverHyperbolicCase(meanAnomaly, eccentricity);
                trueAnomaly = Math.Atan2(Math.Sqrt(eccentricity * eccentricity - 1) * Math.Sinh(eccentricAnomaly),
                    eccentricity - Math.Cosh(eccentricAnomaly));
            }
            else
            {
                eccentricAnomaly = KeplerOrbitUtils.ConvertMeanToEccentricAnomaly(meanAnomaly, eccentricity);
                trueAnomaly = eccentricAnomaly;
            }

            SetVelocityByCurrentAnomaly();
            SetPositionByCurrentAnomaly();

            CalculateOrbitStateFromOrbitalVectors();
        }

        /// <summary>
        /// Sets the mean anomaly and updates all other anomalies.
        /// </summary>
        /// <param name="m">The m.</param>
        public void SetMeanAnomaly(double m)
        {
            if (!IsValidOrbit)
            {
                return;
            }

            meanAnomaly = m % KeplerOrbitUtils.PI2;
            if (eccentricity < 1.0)
            {
                if (meanAnomaly < 0)
                {
                    meanAnomaly += KeplerOrbitUtils.PI2;
                }

                eccentricAnomaly = KeplerOrbitUtils.KeplerSolver(meanAnomaly, eccentricity);
                trueAnomaly = KeplerOrbitUtils.ConvertEccentricToTrueAnomaly(eccentricAnomaly, eccentricity);
            }
            else if (eccentricity > 1.0)
            {
                eccentricAnomaly = KeplerOrbitUtils.KeplerSolverHyperbolicCase(meanAnomaly, eccentricity);
                trueAnomaly = KeplerOrbitUtils.ConvertEccentricToTrueAnomaly(eccentricAnomaly, eccentricity);
            }
            else
            {
                eccentricAnomaly = KeplerOrbitUtils.ConvertMeanToEccentricAnomaly(meanAnomaly, eccentricity);
                trueAnomaly = eccentricAnomaly;
            }

            SetPositionByCurrentAnomaly();
            SetVelocityByCurrentAnomaly();
        }

        /// <summary>
        /// Sets the true anomaly and updates all other anomalies.
        /// </summary>
        /// <param name="t">The t.</param>
        public void SetTrueAnomaly(double t)
        {
            if (!IsValidOrbit)
            {
                return;
            }

            t %= KeplerOrbitUtils.PI2;

            if (eccentricity < 1.0)
            {
                if (t < 0)
                {
                    t += KeplerOrbitUtils.PI2;
                }

                eccentricAnomaly = KeplerOrbitUtils.ConvertTrueToEccentricAnomaly(t, eccentricity);
                meanAnomaly = eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly);
            }
            else if (eccentricity > 1.0)
            {
                eccentricAnomaly = KeplerOrbitUtils.ConvertTrueToEccentricAnomaly(t, eccentricity);
                meanAnomaly = Math.Sinh(eccentricAnomaly) * eccentricity - eccentricAnomaly;
            }
            else
            {
                eccentricAnomaly = KeplerOrbitUtils.ConvertTrueToEccentricAnomaly(t, eccentricity);
                meanAnomaly = KeplerOrbitUtils.ConvertEccentricToMeanAnomaly(eccentricAnomaly, eccentricity);
            }

            SetPositionByCurrentAnomaly();
            SetVelocityByCurrentAnomaly();
        }

        /// <summary>
        /// Sets the eccentric anomaly and updates all other anomalies.
        /// </summary>
        /// <param name="e">The e.</param>
        public void SetEccentricAnomaly(double e)
        {
            if (!IsValidOrbit)
            {
                return;
            }

            e %= KeplerOrbitUtils.PI2;

            eccentricAnomaly = e;

            if (eccentricity < 1.0)
            {
                if (e < 0)
                {
                    e = KeplerOrbitUtils.PI2 + e;
                }

                eccentricAnomaly = e;
                trueAnomaly = KeplerOrbitUtils.ConvertEccentricToTrueAnomaly(e, eccentricity);
                meanAnomaly = KeplerOrbitUtils.ConvertEccentricToMeanAnomaly(e, eccentricity);
            }
            else if (eccentricity > 1.0)
            {
                trueAnomaly = KeplerOrbitUtils.ConvertEccentricToTrueAnomaly(e, eccentricity);
                meanAnomaly = KeplerOrbitUtils.ConvertEccentricToMeanAnomaly(e, eccentricity);
            }
            else
            {
                trueAnomaly = KeplerOrbitUtils.ConvertEccentricToTrueAnomaly(e, eccentricity);
                meanAnomaly = KeplerOrbitUtils.ConvertEccentricToMeanAnomaly(e, eccentricity);
            }

            SetPositionByCurrentAnomaly();
            SetVelocityByCurrentAnomaly();
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        public KeplerOrbitData CloneOrbit()
        {
            return (KeplerOrbitData)MemberwiseClone();
        }
    }

    [Serializable]
    public class TransitionOrbitData
    {
        public Transform attractor;
        public KeplerOrbitData orbit;
        public List<DoubleVector3> impulseDifferences;
        public float totalDeltaV;
        public float duration;
        public float eccAnomalyStart;
        public float eccAnomalyEnd;
    }
}