using System;
using BigWorld.Doubles;
using UnityEngine;

namespace BigWorld
{
    [ExecuteAlways]
    public class RescalledTransform : MonoBehaviour
    {
        public DoubleVector3 position;
        [SerializeField] private ReferenceTransform referenceTransform;
        private DoubleVector3 _originalOffset;
        [SerializeField] private MeshRenderer planet;
        [SerializeField] private float rescaleMultiplicator = 11;
        private double _size;
        private double _width;
        
        void CalculateWidth()
        {
            var currentScale = transform.localScale;
            transform.localScale = Vector3.one;
            _width = planet.bounds.size.z;
            transform.localScale = currentScale;
        }

        private void LateUpdate()
        {
            if (referenceTransform is null)
            {
                referenceTransform = FindAnyObjectByType<ReferenceTransform>();
            }
            CalculateWidth();
            if (gameObject != null)
            {
                _size = CalculateObjectPixelWidth(DoubleVector3.Distance(referenceTransform.UniversePosition, position),
                    60, 1920, _width);
            }

            if (referenceTransform is not null)
            {
                var distance = DoubleVector3.Distance(referenceTransform.UniversePosition, position);
                if (distance < 150000)
                {
                    transform.localScale = Vector3.one;
                    transform.position = new Vector3((float)(position.X - referenceTransform.referencePosition.X),
                        (float)(position.Y - referenceTransform.referencePosition.Y),
                        (float)(position.Z - referenceTransform.referencePosition.Z));
                }
                else
                {
                    var localPosition = DoubleVector3.InverseTransformPoint(referenceTransform.UniversePosition,
                        Quaternion.identity, new DoubleVector3(1, 1, 1), position);
                    transform.position =
                        (new Vector3((float)localPosition.X, (float)localPosition.Y, (float)localPosition.Z)
                            .normalized * 148500) + referenceTransform.transform.position;
                    float targetSize = (float)_size * rescaleMultiplicator;
                    float currentSize = planet.bounds.size.z;
                    Vector3 scale = transform.localScale;
                    scale.z = targetSize * scale.z / currentSize;
                    scale.x = targetSize * scale.x / currentSize;
                    scale.y = targetSize * scale.y / currentSize;
                    transform.localScale = scale;
                }
            }
        }

        public static double CalculateObjectPixelWidth(double distance, double fov, int imageWidthPx,
            double objectWidth)
        {
            double fovRad = fov * Math.PI / 180;
            double viewWidth = 2 * distance * Math.Tan(fovRad / 2);
            double pixelsPerMeter = imageWidthPx / viewWidth;
            double objectWidthPx = objectWidth * pixelsPerMeter;
            return objectWidthPx;
        }
    }
}