﻿using UnityEngine;
using UnityEditor;
using Beans.Unity.Editor;
using Deform;

namespace DeformEditor
{
	[CustomEditor (typeof (RippleDeformer)), CanEditMultipleObjects]
	public class RippleDeformerEditor : DeformerEditor
	{
		private class Content
		{
			public static readonly GUIContent Frequency = new GUIContent (text: "Frequency", tooltip: "Higher values mean more ripples.");
			public static readonly GUIContent Amplitude = new GUIContent (text: "Amplitude", tooltip: "The strength of the ripples,");
			public static readonly GUIContent Mode = new GUIContent (text: "Mode", tooltip: "Unlimited: Entire mesh is rippled.\nLimited: Mesh only ripples between bounds.");
			public static readonly GUIContent Falloff = new GUIContent (text: "Falloff", tooltip: "When at 0, vertices outside the bounds will match the height of the bounds edge.\nWhen at 1, vertices outside the bounds will be unchanged.");
			public static readonly GUIContent InnerRadius = new GUIContent (text: "Inner Radius", tooltip: "Vertices within this radius don't ripple.");
			public static readonly GUIContent OuterRadius = new GUIContent (text: "Outer Radius", tooltip: "Vertices outside this radius don't ripple.");
			public static readonly GUIContent Speed = new GUIContent (text: "Speed", tooltip: "How fast the offset changes.");
			public static readonly GUIContent Offset = new GUIContent (text: "Offset", tooltip: "Offset of the ripple curve.");
			public static readonly GUIContent Axis = DeformEditorGUIUtility.DefaultContent.Axis;
		}

		private class Properties
		{
			public SerializedProperty Frequency;
			public SerializedProperty Amplitude;
			public SerializedProperty Mode;
			public SerializedProperty Falloff;
			public SerializedProperty InnerRadius;
			public SerializedProperty OuterRadius;
			public SerializedProperty Speed;
			public SerializedProperty Offset;
			public SerializedProperty Axis;

			public Properties (SerializedObject obj)
			{
				Frequency	= obj.FindProperty ("frequency");
				Amplitude	= obj.FindProperty ("amplitude");
				Mode		= obj.FindProperty ("mode");
				Falloff		= obj.FindProperty ("falloff");
				InnerRadius = obj.FindProperty ("innerRadius");
				OuterRadius = obj.FindProperty ("outerRadius");
				Speed		= obj.FindProperty ("speed");
				Offset		= obj.FindProperty ("offset");
				Axis		= obj.FindProperty ("axis");
			}
		}

		private const int CURVE_RES = 100;

		private Properties properties;
		private VerticalBoundsHandle boundsHandle = new VerticalBoundsHandle ();

		protected override void OnEnable ()
		{
			base.OnEnable ();

			properties = new Properties (serializedObject);

			boundsHandle.handleCapFunction = DeformHandles.HandleCapFunction;
			boundsHandle.drawGuidelineCallback = (a, b) => DeformHandles.Line (a, b, DeformHandles.LineMode.LightDotted);
		}

		public override void OnInspectorGUI ()
		{
			base.OnInspectorGUI ();

			serializedObject.UpdateIfRequiredOrScript ();

			EditorGUILayout.PropertyField (properties.Frequency, Content.Frequency);
			EditorGUILayout.PropertyField (properties.Amplitude, Content.Amplitude);
			EditorGUILayout.PropertyField (properties.Mode, Content.Mode);

			using (new EditorGUI.DisabledScope ((BoundsMode)properties.Mode.enumValueIndex == BoundsMode.Unlimited && !properties.Mode.hasMultipleDifferentValues))
			{
				using (new EditorGUI.IndentLevelScope ())
				{
					EditorGUILayout.Slider (properties.Falloff, 0f, 1f, Content.Falloff);
					EditorGUILayoutx.MaxField (properties.InnerRadius, properties.OuterRadius.floatValue, Content.InnerRadius);
					EditorGUILayoutx.MinField (properties.OuterRadius, properties.InnerRadius.floatValue, Content.OuterRadius);
				}
			}

			EditorGUILayout.PropertyField (properties.Speed, Content.Speed);
			EditorGUILayout.PropertyField (properties.Offset, Content.Offset);
			EditorGUILayout.PropertyField (properties.Axis, Content.Axis);

			serializedObject.ApplyModifiedProperties ();

			EditorApplication.QueuePlayerLoopUpdate ();
		}

		public override void OnSceneGUI ()
		{
			base.OnSceneGUI ();

			var ripple = target as RippleDeformer;

			DrawMagnitudeHandle (ripple);

			if (ripple.Mode == BoundsMode.Limited)
			{
				boundsHandle.handleColor = DeformEditorSettings.SolidHandleColor;
				boundsHandle.screenspaceHandleSize = DeformEditorSettings.ScreenspaceSliderHandleCapSize;
				if (boundsHandle.DrawHandle (ripple.OuterRadius, ripple.InnerRadius, ripple.Axis, Vector3.up))
				{
					Undo.RecordObject (ripple, "Changed Bounds");
					ripple.OuterRadius = boundsHandle.top;
					ripple.InnerRadius = boundsHandle.bottom;
				}
			}

			DrawCurve (ripple);

			using (new Handles.DrawingScope (Matrix4x4.TRS (ripple.Axis.position, ripple.Axis.rotation, ripple.Axis.lossyScale)))
			{
				DeformHandles.Circle (Vector3.zero, Vector3.forward, Vector3.right, ripple.InnerRadius);
				DeformHandles.Circle (Vector3.zero, Vector3.forward, Vector3.right, ripple.OuterRadius);
			}

			EditorApplication.QueuePlayerLoopUpdate ();
		}

		private void DrawMagnitudeHandle (RippleDeformer ripple)
		{
			var direction = ripple.Axis.forward;
			var worldPosition = ripple.Axis.position + direction * ripple.Amplitude;

			using (var check = new EditorGUI.ChangeCheckScope ())
			{
				var newWorldPosition = DeformHandles.Slider (worldPosition, direction);
				if (check.changed)
				{
					Undo.RecordObject (ripple, "Changed Magnitude");
					var newMagnitude = DeformHandlesUtility.DistanceAlongAxis (ripple.Axis, ripple.Axis.position, newWorldPosition, Axis.Z);
					ripple.Amplitude = newMagnitude;
				}
				var offset = newWorldPosition - ripple.Axis.position;
				DeformHandles.Line (ripple.Axis.position - offset, newWorldPosition, DeformHandles.LineMode.LightDotted);
			}
		}

		private void DrawCurve (RippleDeformer ripple)
		{
			using (new Handles.DrawingScope (Matrix4x4.TRS (ripple.Axis.position, ripple.Axis.rotation, ripple.Axis.lossyScale)))
			{
				var lastPoint = Vector3.zero;
				for (int i = 0; i < CURVE_RES; i++)
				{
					var point = Vector3.Lerp (Vector3.zero, Vector3.up * ripple.OuterRadius, (float)i / CURVE_RES);

					point = RipplePoint (ripple, point, ripple.Mode == BoundsMode.Limited);

					DeformHandles.Line (lastPoint, point, DeformHandles.LineMode.Light);

					lastPoint = point;
				}

				DeformHandles.Line (lastPoint, Vector3.up * ripple.OuterRadius, DeformHandles.LineMode.Light);
			}
		}

		private Vector3 RipplePoint (RippleDeformer ripple, Vector3 point, bool limited)
		{
			if (ripple.Frequency == 0f)
				return point;

			var d = new Vector2 (point.x, point.y).magnitude;

			if (limited)
			{
				var range = ripple.OuterRadius - ripple.InnerRadius;

				var clampedD = Mathf.Clamp (d, ripple.InnerRadius, ripple.OuterRadius);

				var positionOffset = Mathf.Sin ((-ripple.Offset + clampedD * ripple.Frequency) * (float)Mathf.PI * 2f) * ripple.Amplitude;
				if (range != 0f)
				{
					var pointBetweenBounds = Mathf.Clamp ((clampedD - ripple.InnerRadius) / range, 0f, 1f);
					point.z += Mathf.Lerp (positionOffset, 0f, pointBetweenBounds * ripple.Falloff);
				}
				else
				{
					if (d > ripple.OuterRadius)
						point.z += Mathf.Lerp (positionOffset, 0f, ripple.Falloff);
					else if (d < ripple.InnerRadius)
						point.z += positionOffset;
				}
			}
			else
				point.z += Mathf.Sin ((ripple.Offset + d * ripple.Frequency) * (float)Mathf.PI * 2f) * ripple.Amplitude;

			return point;
		}
	}
}