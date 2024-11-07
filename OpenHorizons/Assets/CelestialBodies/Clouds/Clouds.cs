using System;
using UnityEngine;

static class CloudsBuilder
{
}

[Serializable]
public struct Cloud
{
    [SerializeField] private bool enabled;
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

    public Vector3 ShapeOffset => shapeOffset;

    public Color ScatteringTint => scatteringTint;

    public float WindOrientation => windOrientation;

    public float WindSpeed => windSpeed;

    public float AltitudeRange => altitudeRange;

    public float BottomAltitude => bottomAltitude;

    public float MicroErosionScale => microErosionScale;

    public float MicroErosionFactor => microErosionFactor;

    public bool MicroErosion => microErosion;

    public float ErosionScale => erosionScale;

    public float ErosionFactor => erosionFactor;

    public float ShapeScale => shapeScale;

    public float ShapeFactor => shapeFactor;

    public float DensityMultipler => densityMultipler;
}
