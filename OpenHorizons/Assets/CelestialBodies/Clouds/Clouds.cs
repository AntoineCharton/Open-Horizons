using System;
using UnityEngine;

static class CloudsBuilder
{
}

[Serializable]
struct Cloud
{
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
    [SerializeField] private float windSpeed;
    [SerializeField, Range(0, 360)] private float windOrientation;
    [SerializeField] private Color scatteringTint;
}
