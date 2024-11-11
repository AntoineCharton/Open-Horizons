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

			if (!_target.orbitData.IsValidOrbit)
			{
				GUI.enabled = false;
			}

			if (GUILayout.Button("Circularize orbit"))
			{
				_target.SetAutoCircleOrbit();
			}

			if (_target.orbitData.eccentricity >= 1.0)
			{
				GUI.enabled = false;
			}

			if (_target.orbitData.eccentricity < 1.0)
			{
				float meanAnomaly = EditorGUILayout.Slider("Mean anomaly", (float)_target.orbitData.meanAnomaly, 0, (float)KeplerOrbitUtils.PI2);
				if (Math.Abs(meanAnomaly - (float)_target.orbitData.meanAnomaly) > 0.00001f)
				{
					_target.orbitData.SetMeanAnomaly(meanAnomaly);
					_target.ForceUpdateViewFromInternalState();
					EditorUtility.SetDirty(_target);
				}
			}
			else
			{
				EditorGUILayout.LabelField("Mean anomaly", _target.orbitData.meanAnomaly.ToString(CultureInfo.InvariantCulture));
			}

			if (_target.orbitData.IsValidOrbit && _target.orbitData.eccentricity >= 1.0)
			{
				GUI.enabled = true;
			}

			EditorGUILayout.LabelField("Velocity", _target.orbitData.velocity.magnitude.ToString("0.00000"));

			string inclinationRad = _target.orbitData.Inclination.ToString(CultureInfo.InvariantCulture);
			string inclinationDeg = (_target.orbitData.Inclination * KeplerOrbitUtils.Rad2Deg).ToString("0.000");
			EditorGUILayout.LabelField("Inclination", string.Format("{0,15} (deg={1})", inclinationRad, inclinationDeg));

			string ascNodeRad = _target.orbitData.AscendingNodeLongitude.ToString(CultureInfo.InvariantCulture);
			string ascNodeDeg = (_target.orbitData.AscendingNodeLongitude * KeplerOrbitUtils.Rad2Deg).ToString("0.000");
			EditorGUILayout.LabelField("AscendingNodeLongitude", string.Format("{0,15} (deg={1})", ascNodeRad, ascNodeDeg));

			string argOfPeriRad = _target.orbitData.ArgumentOfPerifocus.ToString(CultureInfo.InvariantCulture);
			string argOfPeriDeg = (_target.orbitData.ArgumentOfPerifocus * KeplerOrbitUtils.Rad2Deg).ToString("0.000");
			EditorGUILayout.LabelField("ArgumentOfPerifocus", string.Format("{0,15} (deg={1})", argOfPeriRad, argOfPeriDeg));

			EditorGUILayout.LabelField("Current Orbit Time", _target.orbitData.GetCurrentOrbitTime().ToString("0.000"));

			EditorGUILayout.LabelField("Current MeanMotion", _target.orbitData.meanMotion.ToString("0.000"));

			GUI.enabled = true;

			if (_target.attractorSettings != null && _target.attractorSettings.attractorObject == _target.gameObject)
			{
				_target.attractorSettings.attractorObject = null;
				EditorUtility.SetDirty(_target);
			}

			if (_target.attractorSettings != null && _target.attractorSettings.gravityConstant < 0)
			{
				_target.attractorSettings.gravityConstant = 0;
				EditorUtility.SetDirty(_target);
			}

			if (_target.orbitData.gravConst < 0)
			{
				_target.orbitData.gravConst = 0;
				EditorUtility.SetDirty(_target);
			}
		}
	}
}