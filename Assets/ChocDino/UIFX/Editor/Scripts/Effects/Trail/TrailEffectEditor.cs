//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(TrailEffectBase), true)]
	[CanEditMultipleObjects]
	internal class TrailEffectEditor : BaseEditor
	{
		static readonly GUIContent Content_Trail = new GUIContent("Trail");
		static readonly GUIContent Content_Layers = new GUIContent("Layers");
		static readonly GUIContent Content_Gradient = new GUIContent("Gradient");
		static readonly GUIContent Content_Offset = new GUIContent("Offset");
		static readonly GUIContent Content_Scale = new GUIContent("Scale");
		static readonly GUIContent Content_Animation = new GUIContent("Animation");
		static readonly GUIContent Content_OffsetSpeed = new GUIContent("Offset Speed");
		static readonly GUIContent Content_Apply = new GUIContent("Apply");
		static readonly GUIContent Content_Mode = new GUIContent("Vertex Modifier");

		private SerializedProperty _propLayerCount;
		private SerializedProperty _propDampingFront;
		private SerializedProperty _propDampingBack;
		private SerializedProperty _propAlphaCurve;
		private SerializedProperty _propVertexModifierSource;
		private SerializedProperty _propGradient;
		private SerializedProperty _propGradientOffset;
		private SerializedProperty _propGradientScale;
		private SerializedProperty _propGradientOffsetSpeed;
		private SerializedProperty _propShowTrailOnly;
		private SerializedProperty _propBlendMode;
		private SerializedProperty _propStrengthMode;
		private SerializedProperty _propStrength;

		private static readonly AboutInfo s_aboutInfo =
				new AboutInfo(s_aboutHelp, "UIFX - Trail Effect\n© Chocolate Dinosaur Ltd", "uifx-icon")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/components/effects/trail-effect/"),
							}
						},
						s_sectionReviewBundles,
						s_sectionSupport,
					}
				};

		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeFromStarterBundle, s_upgradeFromIndieBundle, s_aboutInfo } );

		void OnEnable()
		{
			_propLayerCount = VerifyFindProperty("_layerCount");
			_propDampingFront = VerifyFindProperty("_dampingFront");
			_propDampingBack = VerifyFindProperty("_dampingBack");
			_propAlphaCurve = VerifyFindProperty("_alphaCurve");
			_propVertexModifierSource = VerifyFindProperty("_vertexModifierSource");
			_propGradient = VerifyFindProperty("_gradient");
			_propGradientOffset = VerifyFindProperty("_gradientOffset");
			_propGradientScale = VerifyFindProperty("_gradientScale");
			_propGradientOffsetSpeed = VerifyFindProperty("_gradientOffsetSpeed");
			_propShowTrailOnly = VerifyFindProperty("_showTrailOnly");
			_propBlendMode = VerifyFindProperty("_blendMode");
			_propStrengthMode = VerifyFindProperty("_strengthMode");
			_propStrength = VerifyFindProperty("_strength");
		}

		public override void OnInspectorGUI()
		{
			s_aboutToolbar.OnGUI();

			serializedObject.Update();

			GUILayout.Label(Content_Trail, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propLayerCount, Content_Layers);
			EditorGUILayout.PropertyField(_propDampingFront);
			EditorGUILayout.PropertyField(_propDampingBack);
			EditorGUILayout.PropertyField(_propAlphaCurve);
			// Show a warning if curve key values of out of sensible range.
			// NOTE: We have to detect whether the curve window has focus currently, otherwise if the HelpBox() appears
			// while dragging curve keys, it can cause the keys to not update.
			if (!EditorHelper.IsEditingCurve())
			{
				if (_propAlphaCurve.animationCurveValue != null && _propAlphaCurve.animationCurveValue.HasOutOfRangeValues(0f, 1f, 0f, 1f))
				{
					EditorGUILayout.HelpBox("Some curve points are outside of the range [0..1]. This might be fine, or could lead to unexpected results.", MessageType.Warning, true);
				}
			}
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(_propVertexModifierSource, Content_Mode);
			if (EditorGUI.EndChangeCheck())
			{
				// Manually adjust the property so that OnChangedVertexModifier() gets called. 
				foreach (TrailEffectBase trail in this.targets)
				{
					trail.VertexModifierSource = (VertexModifierSource)_propVertexModifierSource.enumValueIndex;
				}
			}
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Gradient, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propGradient);
			EditorGUILayout.PropertyField(_propGradientOffset, Content_Offset);
			EditorGUILayout.PropertyField(_propGradientScale, Content_Scale);
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Animation, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propGradientOffsetSpeed, Content_OffsetSpeed);
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propShowTrailOnly);
			EditorGUILayout.PropertyField(_propBlendMode);
			EditorGUILayout.PropertyField(_propStrengthMode);
			EditorGUILayout.PropertyField(_propStrength);
			EditorGUI.indentLevel--;

			serializedObject.ApplyModifiedProperties();
		}
	}
}