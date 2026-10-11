//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(CameraSource))]
	[CanEditMultipleObjects]
	internal class CameraSourceEditor : BaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Camera Source\n© Chocolate Dinosaur Ltd", "uifx-icon")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/components/sources/camera-source/"),
							}
						},
						s_sectionReviewBundles,
						s_sectionSupport,
					}
				};

		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeFromStarterBundle, s_upgradeFromIndieBundle, s_aboutInfo } );

		private static readonly GUIContent Content_Preview = new GUIContent("Preview");

		private GUIStyle _styleButtonFoldout;
		private SerializedProperty _propCamera;
		private SerializedProperty _propMaterial;
		private SerializedProperty _propColor;
		private SerializedProperty _propRaycastTarget;
		private SerializedProperty _propMaskable;

		public override bool RequiresConstantRepaint()
		{
			var cameraSource = target as CameraSource;
			if (!cameraSource.isActiveAndEnabled) return false;

			return Preferences.CameraSourcePreviewExpand;
		}

		void OnEnable()
		{
			_propCamera = VerifyFindProperty("_camera");
			_propMaterial = VerifyFindProperty("m_Material");
			_propColor = VerifyFindProperty("m_Color");
			_propRaycastTarget = VerifyFindProperty("m_RaycastTarget");
			_propMaskable = VerifyFindProperty("m_Maskable");
		}

		public override void OnInspectorGUI()
		{	
			s_aboutToolbar.OnGUI();

			serializedObject.Update();

			if (_styleButtonFoldout == null)
			{
				_styleButtonFoldout = new GUIStyle(GUI.skin.box);
				_styleButtonFoldout.margin = new RectOffset();
				_styleButtonFoldout.fontStyle = FontStyle.Bold;
				_styleButtonFoldout.alignment = TextAnchor.MiddleCenter;
				_styleButtonFoldout.stretchWidth = true;
				_styleButtonFoldout.stretchHeight = false;
				_styleButtonFoldout.fixedHeight = 0;
				_styleButtonFoldout.wordWrap = false;
				_styleButtonFoldout.padding = new RectOffset(12, 10, 3, 3);
				_styleButtonFoldout.normal.textColor = Color.white;
				_styleButtonFoldout.hover.textColor = Color.white;
				_styleButtonFoldout.active.textColor = Color.white;
				_styleButtonFoldout.focused.textColor = Color.white;
				_styleButtonFoldout.clipping = TextClipping.Overflow;
			}
			{
				var cameraSource = target as CameraSource;
				// Draw preview of texture
				float width = _styleButtonFoldout.CalcSize(Content_Preview).x;
				float height = _styleButtonFoldout.CalcHeight(Content_Preview, Screen.width);
				GUILayout.Box(Content_Preview, _styleButtonFoldout, GUILayout.MinWidth(width / 2f), GUILayout.Height(height));
				Rect foldoutRect = GUILayoutUtility.GetLastRect();
				Preferences.CameraSourcePreviewExpand = GUI.Toggle(foldoutRect, Preferences.CameraSourcePreviewExpand, GUIContent.none, EditorStyles.label);
				if (Preferences.CameraSourcePreviewExpand)
				{
					var texture = cameraSource.mainTexture as RenderTexture;
					if (texture != null)
					{
						float aspect = (float)texture.width / (float)texture.height;
						float maxAspect = 1.8f;
						aspect = Mathf.Clamp(aspect, 1f / maxAspect, maxAspect);
						Rect r = GUILayoutUtility.GetAspectRect(aspect, GUILayout.ExpandWidth(true));
						//Rect r = GUILayoutUtility.GetRect(256f, 256f);
						if (Event.current.type == EventType.Repaint)
						{
							EditorGUI.DrawTextureTransparent(r, Texture2D.blackTexture, ScaleMode.StretchToFill);
							if (texture != null)
							{
								GUI.DrawTexture(r, texture, ScaleMode.ScaleToFit, true);
							}
						}
						EditorGUILayout.LabelField(texture.width + "x" + texture.height + " " + texture.format + " updates: " + texture.updateCount);
					}
					else
					{
						Rect r = GUILayoutUtility.GetRect(128f, 128f);
						if (Event.current.type == EventType.Repaint)
						{
							EditorGUI.DrawTextureTransparent(r, Texture2D.blackTexture, ScaleMode.StretchToFill);
						}
					}
					EditorGUILayout.Space();
				}
			}

			EditorGUILayout.PropertyField(_propCamera);

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(_propMaterial);
			EditorGUILayout.PropertyField(_propColor);
			EditorGUILayout.PropertyField(_propRaycastTarget);
			EditorGUILayout.PropertyField(_propMaskable);

			serializedObject.ApplyModifiedProperties();
		}
	}
}