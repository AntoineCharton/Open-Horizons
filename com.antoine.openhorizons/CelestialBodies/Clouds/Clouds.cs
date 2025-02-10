using System;
using UnityEngine;

[Serializable]
public struct Cloud
{
    [SerializeField] private bool enabled;
    [SerializeField] private bool visible;
    [SerializeField, Range(0, 1)] private float densityMultipler;
    [SerializeField, Range(0, 1)] private float shapeFactor;
    [SerializeField] private float shapeScale;
    [SerializeField, Range(0, 1)] private float erosionFactor;
    [SerializeField] private float erosionScale;
    [SerializeField] private bool microErosion;
    [SerializeField, Range(0, 1)] private float microErosionFactor;
    [SerializeField] private float microErosionScale;
    [SerializeField] private float bottomAltitude;
    [SerializeField] private float altitudeRange;
    [SerializeField] private Vector3 shapeOffset;
    [SerializeField] private float windSpeed;
    [SerializeField, Range(0, 360)] private float windOrientation;
    [SerializeField] private Color scatteringTint;

    public bool Enabled => enabled;
    
    public bool Visible
    {
        get => visible;
        set => visible = value;
    }
    
    public Vector3 ShapeOffset => shapeOffset;

    public Color ScatteringTint
    {
        get { return scatteringTint; }
        internal set => scatteringTint = value;
    }

    public float WindOrientation => windOrientation;

    public float WindSpeed => windSpeed;

    public float AltitudeRange
    {
        get => altitudeRange;
        internal set => altitudeRange = value;
    }

    public float BottomAltitude
    {
        get => bottomAltitude;
        internal set => bottomAltitude = value;
    }

    public float MicroErosionScale => microErosionScale;

    public float MicroErosionFactor => microErosionFactor;

    public bool MicroErosion => microErosion;

    public float ErosionScale => erosionScale;

    public float ErosionFactor => erosionFactor;

    public float ShapeScale
    {
        get => shapeScale;
        internal set => shapeScale = value;
    }

    public float ShapeFactor
    {
        get => shapeFactor;
        internal set => shapeFactor = value;
    }

    public float DensityMultipler
    {
        get => densityMultipler;
        internal set => densityMultipler = value;
    }
}
