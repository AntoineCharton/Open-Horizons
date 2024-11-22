using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace BigWorld.Kepler.Editor
{
	[CustomEditor(typeof(KeplerOrbitMover))]
	[CanEditMultipleObjects]
	public class KeplerOrbitMoverEditor : UnityEditor.Editor
	{
		private KeplerOrbitMover _target;

		private void OnEnable()
		{
			_target = target as KeplerOrbitMover;
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			if (!_target.OrbitData.IsValidOrbit)
			{
				GUI.enabled = false;
			}

			if (GUILayout.Button("Circularize orbit"))
			{
				_target.SetAutoCircleOrbit();
			}

			if (_target.OrbitData.eccentricity >= 1.0)
			{
				GUI.enabled = false;
			}

			if (_target.OrbitData.eccentricity < 1.0)
			{
				float meanAnomaly = EditorGUILayout.Slider("Mean anomaly", (float)_target.OrbitData.meanAnomaly, 0, (float)KeplerOrbitUtils.PI2);
				if (Math.Abs(meanAnomaly - (float)_target.OrbitData.meanAnomaly) > 0.00001f)
				{
					_target.OrbitData.SetMeanAnomaly(meanAnomaly);
					_target.ForceUpdateViewFromInternalState();
					EditorUtility.SetDirty(_target);
				}
			}
			else
			{
				EditorGUILayout.LabelField("Mean anomaly", _target.OrbitData.meanAnomaly.ToString(CultureInfo.InvariantCulture));
			}

			if (_target.OrbitData.IsValidOrbit && _target.OrbitData.eccentricity >= 1.0)
			{
				GUI.enabled = true;
			}

			EditorGUILayout.LabelField("Velocity", _target.OrbitData.velocity.magnitude.ToString("0.00000"));

			string inclinationRad = _target.OrbitData.Inclination.ToString(CultureInfo.InvariantCulture);
			string inclinationDeg = (_target.OrbitData.Inclination * KeplerOrbitUtils.Rad2Deg).ToString("0.000");
			EditorGUILayout.LabelField("Inclination", string.Format("{0,15} (deg={1})", inclinationRad, inclinationDeg));

			string ascNodeRad = _target.OrbitData.AscendingNodeLongitude.ToString(CultureInfo.InvariantCulture);
			string ascNodeDeg = (_target.OrbitData.AscendingNodeLongitude * KeplerOrbitUtils.Rad2Deg).ToString("0.000");
			EditorGUILayout.LabelField("AscendingNodeLongitude", string.Format("{0,15} (deg={1})", ascNodeRad, ascNodeDeg));

			string argOfPeriRad = _target.OrbitData.ArgumentOfPerifocus.ToString(CultureInfo.InvariantCulture);
			string argOfPeriDeg = (_target.OrbitData.ArgumentOfPerifocus * KeplerOrbitUtils.Rad2Deg).ToString("0.000");
			EditorGUILayout.LabelField("ArgumentOfPerifocus", string.Format("{0,15} (deg={1})", argOfPeriRad, argOfPeriDeg));

			EditorGUILayout.LabelField("Current Orbit Time", _target.OrbitData.GetCurrentOrbitTime().ToString("0.000"));

			EditorGUILayout.LabelField("Current MeanMotion", _target.OrbitData.meanMotion.ToString("0.000"));

			GUI.enabled = true;

			if (_target.AttractorSettings != null && _target.AttractorSettings.attractorObject == _target.gameObject)
			{
				_target.AttractorSettings.attractorObject = null;
				EditorUtility.SetDirty(_target);
			}

			if (_target.AttractorSettings != null && _target.AttractorSettings.gravityConstant < 0)
			{
				_target.AttractorSettings.gravityConstant = 0;
				EditorUtility.SetDirty(_target);
			}

			if (_target.OrbitData.gravConst < 0)
			{
				_target.OrbitData.gravConst = 0;
				EditorUtility.SetDirty(_target);
			}
		}
	}
}