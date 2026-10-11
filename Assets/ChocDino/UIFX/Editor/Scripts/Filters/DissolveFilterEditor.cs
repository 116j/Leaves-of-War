//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(DissolveFilter), true)]
	[CanEditMultipleObjects]
	internal class DissolveFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Dissolve Filter\n© Chocolate Dinosaur Ltd", "uifx-icon")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/components/filters/dissolve-filter/"),
							}
						},
						s_sectionReviewBundles,
						s_sectionSupport,
					}
				};

		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeFromStarterBundle, s_upgradeFromIndieBundle, s_aboutInfo } );

		private static readonly GUIContent Content_Area = new GUIContent("Area");
		private static readonly GUIContent Content_Space = new GUIContent("Space");
		private static readonly GUIContent Content_Dissolve = new GUIContent("Dissolve");
		private static readonly GUIContent Content_Direction = new GUIContent("Direction");
		private static readonly GUIContent Content_Edge = new GUIContent("Edge");
		private static readonly GUIContent Content_ScaleMode = new GUIContent("Scale Mode");
		private static readonly GUIContent Content_Length = new GUIContent("Length");
		private static readonly GUIContent Content_ColorMode = new GUIContent("Color Mode");
		private static readonly GUIContent Content_Ramp = new GUIContent("Ramp");
		private static readonly GUIContent Content_Emissive = new GUIContent("Emissive");
		private static readonly GUIContent Content_Invert = new GUIContent("Invert");
		private static readonly GUIContent Content_Strength = new GUIContent("Strength");

		private static readonly string s_warnRequiresScreenRenderSpace = "FillSpace of Screen requires RenderSpace also set to Screen.";

		private SerializedProperty _propFillSpace;
		private SerializedProperty _propTexture;
		private SerializedProperty _propTextureScaleMode;
		private SerializedProperty _propScale;
		private SerializedProperty _propInvert;
		private SerializedProperty _propDirection;
		private SerializedProperty _propDirectionTexture;
		private SerializedProperty _propDirectionStrength;
		private SerializedProperty _propDirectionInvert;
		private SerializedProperty _propEdgeLength;
		private SerializedProperty _propEdgeColorMode;
		private SerializedProperty _propEdgeColor;
		private SerializedProperty _propEdgeTexture;
		private SerializedProperty _propEdgeEmissive;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;

		protected override void OnEnable()
		{
			_propFillSpace = VerifyFindProperty("_fillSpace");
			_propTexture = VerifyFindProperty("_texture");
			_propTextureScaleMode = VerifyFindProperty("_textureScaleMode");
			_propScale = VerifyFindProperty("_scale");
			_propInvert = VerifyFindProperty("_invert");
			_propDirection = VerifyFindProperty("_direction");
			_propDirectionTexture = VerifyFindProperty("_directionTexture");
			_propDirectionStrength = VerifyFindProperty("_directionStrength");
			_propDirectionInvert = VerifyFindProperty("_directionInvert");
			_propEdgeLength = VerifyFindProperty("_edgeLength");
			_propEdgeColorMode = VerifyFindProperty("_edgeColorMode");
			_propEdgeColor = VerifyFindProperty("_edgeColor");
			_propEdgeTexture = VerifyFindProperty("_edgeTexture");
			_propEdgeEmissive = VerifyFindProperty("_edgeEmissive");
			_propRenderSpace = VerifyFindProperty("_renderSpace");
			_propStrength  = VerifyFindProperty("_strength");
			base.OnEnable();
		}

		public override void OnInspectorGUI()
		{
			s_aboutToolbar.OnGUI();

			serializedObject.Update();

			var filter = this.target as FilterBase;

			if (OnInspectorGUI_Check(filter))
			{
				return;
			}

			{
				GUILayout.Label(Content_Area, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EnumAsToolbarCompact(_propFillSpace, Content_Space);
				if (_propRenderSpace.intValue != (int)FilterRenderSpace.Screen && _propFillSpace.intValue == (int)FillSpace.Screen)
				{
					EditorGUILayout.HelpBox(s_warnRequiresScreenRenderSpace, MessageType.Warning, true);
					EditorGUILayout.Space();
				}
				EditorGUI.indentLevel--;
			}
			{
				GUILayout.Label(Content_Dissolve, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propTexture);
				EditorGUI.BeginDisabledGroup(_propTexture.objectReferenceValue == null);
				EnumWithButtons(_propTextureScaleMode, Content_ScaleMode);
				EditorGUILayout.PropertyField(_propScale);
				EditorGUILayout.PropertyField(_propInvert);
				EditorGUI.EndDisabledGroup();
				EditorGUI.indentLevel--;
			}
			{
				GUILayout.Label(Content_Direction, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EnumWithButtons(_propDirection);
				if (_propDirection.intValue != (int)DissolveDirection.None)
				{
					if (_propDirection.intValue == (int)DissolveDirection.Texture)
					{
						EditorGUILayout.PropertyField(_propDirectionTexture, Content_Texture);
					}
					EditorGUILayout.PropertyField(_propDirectionStrength, Content_Strength);
					EditorGUILayout.PropertyField(_propDirectionInvert, Content_Invert);
				}
				EditorGUI.indentLevel--;
			}
			{
				GUILayout.Label(Content_Edge, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propEdgeLength, Content_Length);
				EditorGUI.BeginDisabledGroup(_propEdgeLength.floatValue <= 0f);
				EnumAsToolbarCompact(_propEdgeColorMode, Content_ColorMode);
				if (_propEdgeColorMode.enumValueIndex == (int)DissolveEdgeColorMode.Color)
				{
					EditorGUILayout.PropertyField(_propEdgeColor, Content_Color);
				}
				else if (_propEdgeColorMode.enumValueIndex == (int)DissolveEdgeColorMode.Ramp)
				{
					EditorGUILayout.PropertyField(_propEdgeTexture, Content_Ramp);
				}
				if (_propEdgeColorMode.enumValueIndex != (int)DissolveEdgeColorMode.None)
				{
					EditorGUILayout.PropertyField(_propEdgeEmissive, Content_Emissive);
				}
				EditorGUI.EndDisabledGroup();
				EditorGUI.indentLevel--;
			}
			{
				GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				DrawFilterRenderSpaceControl(_propRenderSpace);
				DrawStrengthProperty(_propStrength);
				EditorGUI.indentLevel--;
			}

			if (OnInspectorGUI_Baking(filter))
			{
				return;
			}

			FilterBaseEditor.OnInspectorGUI_Debug(filter);

			serializedObject.ApplyModifiedProperties();
		}
	}
}