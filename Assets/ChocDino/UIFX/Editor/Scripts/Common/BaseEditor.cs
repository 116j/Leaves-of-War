//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	public class BaseEditor : UnityEditor.Editor
	{
		protected const string s_aboutHelp = "About & Help";

		protected static readonly string DiscordUrl = "https://discord.gg/wKRzKAHVUE";
		protected static readonly string ForumBundleUrl = "https://discussions.unity.com/t/released-uifx-bundle-advanced-effects-for-unity-ui/940575";
		protected static readonly string GithubUrl = "https://github.com/Chocolate-Dinosaur/UIFX/issues";
		protected static readonly string SupportEmailUrl = "mailto:support@chocdino.com";
		protected static readonly string UIFXAboutWebsiteUrl = "https://www.chocdino.com/products/uifx/about/";
		protected static readonly string UIFXAssetsWebsiteUrl = "https://www.chocdino.com/products/uifx/assets/";
		protected static readonly string AssetStoreProBundleUrl = "https://assetstore.unity.com/packages/slug/351962?aid=1100lSvNe";
		protected static readonly string AssetStoreProBundleReviewUrl = "https://assetstore.unity.com/packages/slug/351962?aid=1100lSvNe#reviews";
		protected static readonly string AssetStoreIndieBundleUrl = "https://assetstore.unity.com/packages/slug/351964?aid=1100lSvNe";
		protected static readonly string AssetStoreIndieBundleReviewUrl = "https://assetstore.unity.com/packages/slug/351964?aid=1100lSvNe#reviews";
		protected static readonly string AssetStoreStarterBundleUrl = "https://assetstore.unity.com/packages/slug/384538?aid=1100lSvNe";
		protected static readonly string AssetStoreStarterBundleReviewUrl = "https://assetstore.unity.com/packages/slug/384538?aid=1100lSvNe#reviews";

		internal static readonly AboutInfo s_upgradeFromStarterBundle =
				new AboutInfo("Upgrade ★", "This asset can be upgraded to the <b>Indie / Pro</b> bundle to get more features.\r\n\r\nAs an existing customer you are entitled to a discounted upgrade! (If purchased via the Unity Asset Store and not during sales event)", "uifx-icon", BaseEditor.ShowUpgradeFromStarterBundleButton)
				{
					sections = new AboutSection[]
					{
						new AboutSection("Upgrade")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("<color=#ffd700>★ </color>Upgrade to UIFX - Indie Bundle<color=#ffd700> ★</color>", AssetStoreIndieBundleUrl),
								new AboutButton("<color=#ffd700>★ </color>Upgrade to UIFX - Pro Bundle<color=#ffd700> ★</color>", AssetStoreProBundleUrl),
							}
						},
						new AboutSection("Read More")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("About UIFX Assets", UIFXAssetsWebsiteUrl),
							}
						},
					}
				};

		internal static readonly AboutInfo s_upgradeFromIndieBundle =
				new AboutInfo("Upgrade ★", "This asset can be upgraded to the <b>Pro</b> bundle to get more features.\r\n\r\nAs an existing customer you are entitled to a discounted upgrade! (If purchased via the Unity Asset Store and not during sales event)", "uifx-icon", BaseEditor.ShowUpgradeFromIndieBundleButton)
				{
					sections = new AboutSection[]
					{
						new AboutSection("Upgrade")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("<color=#ffd700>★ </color>Upgrade to UIFX - Pro Bundle<color=#ffd700> ★</color>", AssetStoreProBundleUrl),
							}
						},
						new AboutSection("Read More")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("About UIFX Assets", UIFXAssetsWebsiteUrl),
							}
						},
					}
				};

		internal static readonly AboutSection s_sectionReviewBundles = 
			new AboutSection("Unity Asset Store Review\r\n<color=#ffd700>★★★★☆</color>")
			{
				buttons = new AboutButton[]
				{
					new AboutButton("Review <b>UIFX - Pro Bundle</b>", AssetStoreProBundleReviewUrl),
					new AboutButton("Review <b>UIFX - Indie Bundle</b>", AssetStoreIndieBundleReviewUrl),
					new AboutButton("Review <b>UIFX - Starter Bundle</b>", AssetStoreStarterBundleReviewUrl),
				}
			};
		internal static readonly AboutSection s_sectionSupport =
			new AboutSection("UIFX Support")
			{
				buttons = new AboutButton[]
				{
					new AboutButton("Discord Community", DiscordUrl),
					new AboutButton("Post to Unity Discussions", ForumBundleUrl),
					new AboutButton("Post Issues to GitHub", GithubUrl),
					new AboutButton("Email Us", SupportEmailUrl),
				}
			};

		private static GUIContent s_backIcon;
		private static GUIContent s_fwdIcon;
		private static GUIStyle s_enumButtonStyle;

		internal static bool ShowUpgradeFromStarterBundleButton(bool dummy)
		{
			return !DetectUIFXIndieBundle();
		}

		internal static bool ShowUpgradeFromIndieBundleButton(bool dummy)
		{
			if (DetectUIFXIndieBundle())
			{
				return !DetectUIFXProBundle();
			}
			return false;
		}

		internal static bool DetectUIFXProBundle()
		{
			// This GUID is from ShaderStripProcessor.cs.meta which is currently only included in the UIFX Pro Bundle.
			const string fileGUID = "9700f8caf50e2274f885c5a0d2f89abd";
			return !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(fileGUID));
		}

		internal static bool DetectUIFXIndieBundle()
		{
			// This GUID is from CompositeLayerEditor.cs.meta which is currently only included in the UIFX Indie and Pro Bundles.
			const string fileGUID = "5defb0d86da020c42953860ef4239377";
			return !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(fileGUID));
		}

		// <summary>
		// Creates a button that toggles between two texts but maintains the same size by using the size of the largest.
		// This is useful so that the button doesn't change size resulting in the mouse cursor no longer being over the button.
		// </summary>
		internal static bool ToggleButton(bool value, GUIContent labelTrue, GUIContent labelFalse)
		{
			float maxWidth = Mathf.Max(GUI.skin.button.CalcSize(labelTrue).x, GUI.skin.button.CalcSize(labelFalse).x);
			GUIContent content = value ? labelTrue : labelFalse;
			return GUILayout.Button(content, GUILayout.Width(maxWidth));
		}

		protected void EnumAsToolbarCompact(SerializedProperty prop, GUIContent displayName = null)
		{
			if (displayName == null)
			{
				displayName = new GUIContent(prop.displayName);
			}

			if (prop.hasMultipleDifferentValues)
			{
				EditorGUILayout.PropertyField(prop, displayName);
				return;
			}

			Rect rect = EditorGUILayout.GetControlRect();
			EditorGUI.BeginProperty(rect, displayName, prop);

			EditorGUI.PrefixLabel(rect, displayName);
			rect.xMin += EditorGUIUtility.labelWidth;
			
			EditorGUI.BeginChangeCheck();
			int newIndex = prop.enumValueIndex;
			newIndex = GUI.Toolbar(rect, newIndex, prop.enumDisplayNames);

			// Only assign the value back if it was actually changed by the user.
			// Otherwise a single value will be assigned to all objects when multi-object editing,
			// even when the user didn't touch the control.
			if (EditorGUI.EndChangeCheck())
			{
				prop.enumValueIndex = newIndex;
			}

			EditorGUI.EndProperty();
		}

		protected void EnumWithButtons(SerializedProperty prop, GUIContent displayName = null)
		{
			if (displayName == null)
			{
				displayName = new GUIContent(prop.displayName);
			}

			if (prop.hasMultipleDifferentValues)
			{
				EditorGUILayout.PropertyField(prop, displayName);
				return;
			}

			EnumWithButtonsInternal(prop, displayName);
		}

		//int _control = -1;

		private void EnumWithButtonsInternal(SerializedProperty prop, GUIContent displayName)
		{
			if (s_backIcon == null) { s_backIcon = EditorGUIUtility.IconContent("d_back"); }
			if (s_fwdIcon == null) { s_fwdIcon = EditorGUIUtility.IconContent("d_forward"); }

			EditorGUILayout.BeginHorizontal();

			// Drop down
			EditorGUILayout.PropertyField(prop, displayName);

			bool prevItem = false;
			bool nextItem = false;

			if (s_enumButtonStyle == null)
			{
				s_enumButtonStyle = new GUIStyle(GUI.skin.button);
				s_enumButtonStyle.margin = new RectOffset(0, 0, s_enumButtonStyle.margin.top, s_enumButtonStyle.margin.bottom);
			}

			// Left button
			int controlId = EditorGUIUtility.GetControlID(FocusType.Keyboard);
			EditorGUI.BeginDisabledGroup(prop.enumValueIndex <= 0);
			GUI.SetNextControlName("Prev");
			if (GUILayout.Button(s_backIcon, s_enumButtonStyle, GUILayout.ExpandWidth(false), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
			{
				//if (Event.current.button == 0) prevItem = true; else if (Event.current.button == 1) nextItem = true;
				prevItem = true;
				GUIUtility.hotControl = GUIUtility.keyboardControl = controlId;
				GUI.FocusControl("Prev");
			}
			EditorGUI.EndDisabledGroup();

			// Right button
			int enumCount = prop.enumNames.Length;
			EditorGUI.BeginDisabledGroup((prop.enumValueIndex + 1) >= enumCount);
			GUI.SetNextControlName("Next");
			if (GUILayout.Button(s_fwdIcon, s_enumButtonStyle, GUILayout.ExpandWidth(false), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
			{
				//if (Event.current.button == 0) nextItem = true; else if (Event.current.button == 1) prevItem = true;
				nextItem = true;
				GUIUtility.hotControl = GUIUtility.keyboardControl = controlId;
				GUI.FocusControl("Next");
			}
			EditorGUI.EndDisabledGroup();

			EditorGUILayout.EndHorizontal();
			var rect = GUILayoutUtility.GetLastRect();

			Event e = Event.current;
			// Handle mouse clicks
			/*if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && (GUIUtility.keyboardControl == controlId || (GUIUtility.keyboardControl + 1) == controlId))
			{
				if (_control == GUIUtility.keyboardControl)
				{
					if (e.button == 0)
					{
						prevItem = true;
						e.Use();
					}
					else if (e.button == 1)
					{
						nextItem = true;
						e.Use();
					}
				}
				else
				{
					_control = GUIUtility.keyboardControl;
				}
			}
			// Handle arrow keys
			else*/ if (e.type == EventType.KeyDown && (GUIUtility.keyboardControl == controlId || (GUIUtility.keyboardControl + 1) == controlId))
			{
				if (!e.control && !e.shift && !e.alt)
				{
					if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.LeftArrow)
					{
						prevItem = true;
						e.Use();
					}
					else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.RightArrow)
					{
						nextItem = true;
						e.Use();
					}
				}
			}
			// Handle mouse-wheel scrolling
			else if (e.type == EventType.ScrollWheel && e.control && rect.Contains(e.mousePosition))
			{
				if (e.delta.y < 0f)
				{
					prevItem = true;
				}
				else if (e.delta.y > 0f)
				{
					nextItem = true;
				}
				e.Use();
			}

			if (prevItem)
			{
				prop.enumValueIndex = Mathf.Max(0, prop.enumValueIndex - 1);
			}
			else if (nextItem)
			{
				int lastIndex = Mathf.Max(0, enumCount - 1);
				prop.enumValueIndex = Mathf.Min(lastIndex, prop.enumValueIndex + 1);
			}
		}

		protected void EnumAsToolbar(SerializedProperty prop, GUIContent displayName = null)
		{
			if (displayName == null)
			{
				displayName = new GUIContent(prop.displayName);
			}

			if (prop.hasMultipleDifferentValues)
			{
				EditorGUILayout.PropertyField(prop, displayName);
				return;
			}

			if (!EditorGUIUtility.wideMode)
			{
				EnumWithButtonsInternal(prop, displayName);
			}
			else
			{
				Rect rect = EditorGUILayout.GetControlRect(true);
				Rect lastRect = GUILayoutUtility.GetLastRect();
				EditorGUI.BeginProperty(rect, displayName, prop);

				EditorGUI.PrefixLabel(rect, displayName);
				rect.xMin += EditorGUIUtility.labelWidth;
				
				EditorGUI.BeginChangeCheck();

				int controlId = EditorGUIUtility.GetControlID(FocusType.Keyboard);
				int newIndex = GUI.Toolbar(rect, prop.enumValueIndex, prop.enumDisplayNames);

				// Only assign the value back if it was actually changed by the user.
				// Otherwise a single value will be assigned to all objects when multi-object editing,
				// even when the user didn't touch the control.
				if (EditorGUI.EndChangeCheck())
				{
					prop.enumValueIndex = newIndex;
					GUIUtility.hotControl = GUIUtility.keyboardControl = controlId;
				}

				{
					bool prevItem = false;
					bool nextItem = false;

					var e = Event.current;
					// Handle mouse-wheel scrolling
					if (e.type == EventType.ScrollWheel && e.control && lastRect.Contains(e.mousePosition))
					{
						if (e.delta.y < 0f)
						{
							prevItem = true;
						}
						else if (e.delta.y > 0f)
						{
							nextItem = true;
						}
						e.Use();
					}

					if (prevItem)
					{
						prop.enumValueIndex = Mathf.Max(0, prop.enumValueIndex - 1);
					}
					else if (nextItem)
					{
						int enumCount = prop.enumNames.Length;
						int lastIndex = Mathf.Max(0, enumCount - 1);
						prop.enumValueIndex = Mathf.Min(lastIndex, prop.enumValueIndex + 1);
					}
				}
				EditorGUI.EndProperty();
			}
		}

		protected void TextureScaleOffset(SerializedProperty propTexture, SerializedProperty propScale, SerializedProperty propOffset, GUIContent displayName)
		{
			EditorGUILayout.PropertyField(propTexture);
			//EditorGUILayout.PropertyField(_propTextureOffset, Content_Offset);
			//EditorGUILayout.PropertyField(_propTextureScale, Content_Scale);

			Rect rect = EditorGUILayout.GetControlRect(true, 2 * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing), EditorStyles.layerMaskField);
			EditorGUI.BeginChangeCheck();
			Vector4 scaleOffset = new Vector4(propScale.vector2Value.x, propScale.vector2Value.y, propOffset.vector2Value.x, propOffset.vector2Value.y);
			EditorGUI.indentLevel++;
			scaleOffset = MaterialEditor.TextureScaleOffsetProperty(rect, scaleOffset, false);
			EditorGUI.indentLevel--;
			if (EditorGUI.EndChangeCheck())
			{
				propScale.vector2Value = new Vector2(scaleOffset.x, scaleOffset.y);
				propOffset.vector2Value = new Vector2(scaleOffset.z, scaleOffset.w);
			}
		}
		
		protected SerializedProperty VerifyFindProperty(string propertyPath)
		{
			SerializedProperty result = serializedObject.FindProperty(propertyPath);
			Debug.Assert(result != null);
			if (result == null)
			{
				Debug.LogError("Failed to find property '" + propertyPath + "' in object '" + serializedObject.ToString()+ "'");
			}
			return result;
		}

		internal static SerializedProperty VerifyFindPropertyRelative(SerializedProperty property, string relativePropertyPath)
		{
			SerializedProperty result = null;
			Debug.Assert(property != null);
			if (property == null)
			{
				Debug.LogError("Property is null while finding relative property '"+ relativePropertyPath + "'");
			}
			else
			{
				result = property.FindPropertyRelative(relativePropertyPath);
				Debug.Assert(result != null);
				if (result == null)
				{
					Debug.LogError("Failed to find relative property '" + relativePropertyPath + "' in property '" + property.name + "'");
				}
			}
			return result;
		}

		#if false
		void ShowAlignmentSelector()
		{
			GUILayoutOption layout = GUILayout.ExpandWidth(false);
			GUIStyle style = UnityEditor.EditorStyles.toolbarButton;
			//style.margin = new RectOffset(0, 0, 0, 0);
			//style.border = new RectOffset(0, 0, 0, 0);
		
			bool toggle;
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			toggle = _propGradientCenterX.floatValue == -1f;
			toggle = GUILayout.Toggle(toggle, "┏", style, layout);
			GUILayout.Toggle(false, "┳", style, layout);
			GUILayout.Toggle(false, "┓", style, layout);
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.Toggle(false, "┣", style, layout);
			GUILayout.Toggle(false, "╋", style, layout);
			GUILayout.Toggle(false, "┫", style, layout);
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.Toggle(false, "┗", style, layout);
			GUILayout.Toggle(false, "┻", style, layout);
			GUILayout.Toggle(false, "┛", style, layout);
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}
		#endif

		internal static Gradient GetGradient(SerializedProperty gradientProperty)
		{
			#if UNITY_2022_1_OR_NEWER
			return gradientProperty.gradientValue;
			#else
			System.Reflection.PropertyInfo propertyInfo = typeof(SerializedProperty).GetProperty("gradientValue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (propertyInfo == null) { return null; }
			else { return propertyInfo.GetValue(gradientProperty, null) as Gradient; }
			#endif
		}
		internal static void SetGradient(SerializedProperty gradientProperty, Gradient value)
		{
			#if UNITY_2022_1_OR_NEWER
			gradientProperty.gradientValue = value;
			#else
			System.Reflection.PropertyInfo propertyInfo = typeof(SerializedProperty).GetProperty("gradientValue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (propertyInfo != null) { propertyInfo.SetValue(gradientProperty, value); }
			#endif
		}

		protected static void ShowBetaMessage()
		{
			EditorGUILayout.HelpBox("BETA Features not enabled in settings...", MessageType.Error, true);
			if (GUILayout.Button("Open UIFX Settings"))
			{
				Preferences.Open();
			}
			EditorGUILayout.Space();
		}

		public static void SystemShowFolderContents(string path)
		{
			#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
			var windowsPath = path.Replace('/', '\\');
			System.Diagnostics.Process.Start("explorer.exe", "/select," + windowsPath);
			#else
			if (System.IO.File.Exists(path))
			{
				// Convert to folder.
				path = System.IO.Path.GetDirectoryName(path);
			}
			if (System.IO.Directory.Exists(path))
			{
				UnityEngine.Application.OpenURL(path);
			}
			#endif
		}
	}
}