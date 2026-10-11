//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChocDino.UIFX.Editor
{
    [InitializeOnLoad]
    static class ModifyInspector
    {
        static readonly GUIContent Content_AddUGUI = new GUIContent("Add uGUI");
        static readonly GUIContent Content_AddUIFX = new GUIContent("Add UIFX");
        static Rect _cachedAddUGUIButtonRect;
        static GenericMenu _uifxMenu;
        static GenericMenu _uguiMenu;

        static ModifyInspector()
        {
            // Hook into the global editor update loop to monitor open inspectors
            EditorApplication.update -= CheckAndInjectButtons;
            EditorApplication.update += CheckAndInjectButtons;
        }

        private static void CheckAndInjectButtons()
        {
            // NOTE: This method is called every editor update, so we try to early-out as soon as possible.

            // If these buttons are disabled, exit early.
            if (!Preferences.ShowAddInspectorButtonAddUGUI && !Preferences.ShowAddInspectorButtonAddUIFX) return;

            // Find all active editor windows
            var inspectors = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var inspectorCount = inspectors.Length;

            // Find the editor windows we're interested in
            int validWindows = 0;
            for (int i = 0; i < inspectorCount; i++)
            {
                var editorWindow = inspectors[i];
                if (IsInspectorWindowHasRectTransform(editorWindow))
                {
                    validWindows++;
                }
                else
                { 
                    inspectors[i] = null;
                }
            }

            if (validWindows == 0) return;

            foreach (var window in inspectors)
            {
                if (window == null) continue;

                VisualElement root = window.rootVisualElement;
                if (root == null) continue;

                // Locate the native "Add Component" button container
                VisualElement addComponentContainer = root.Q<VisualElement>(className: "unity-inspector-add-component-button");
                if (addComponentContainer != null && addComponentContainer.childCount == 1)
                {
                    IMGUIContainer imguiContainer = new IMGUIContainer(() =>
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(22f);
                        GUILayout.FlexibleSpace();
                        if (Preferences.ShowAddInspectorButtonAddUGUI)
                        {
                            if (GUILayout.Button(Content_AddUGUI, GUI.skin.button, GUILayout.ExpandWidth(true), GUILayout.MaxWidth(230), GUILayout.Height(24)))
                            {
                                ShowUGUIMenu();
                            }
                        }
                        if (Preferences.ShowAddInspectorButtonAddUIFX)
                        {
                            if (GUILayout.Button(Content_AddUIFX, GUI.skin.button, GUILayout.MaxWidth(230), GUILayout.Height(24)))
                            {
                                ShowMenu();
                            }
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.Space(22f);
                        GUILayout.EndHorizontal();
                    });

                    addComponentContainer.Add(imguiContainer);
                }
            }
        }

        private static bool IsInspectorWindowHasRectTransform(EditorWindow inspectorWindow)
        {
            if (inspectorWindow == null) return false;

            var windowType = inspectorWindow.GetType();
            if (windowType.Name != "InspectorWindow") return false;

            // Use reflection to access the internal "tracker" property of the InspectorWindow
            PropertyInfo trackerProperty = windowType.GetProperty("tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (trackerProperty == null) return false;

            ActiveEditorTracker tracker = (ActiveEditorTracker)trackerProperty.GetValue(inspectorWindow);
            if (tracker == null || tracker.activeEditors.Length == 0) return false;

            // Fetch the target object of the primary editor (usually the Transform editor or Asset editor)
            Object targetObject = tracker.activeEditors[0].target;

            // Find the GameObject
            GameObject go = null;
            if (targetObject is Component component)
            {
                go = component.gameObject;
            }
            else if (targetObject is GameObject gameObject)
            {
                go = gameObject;
            }

            // Check if the GameObject contains a RectTransform
            if (go)
            {
                return IsGUIGameObject(go);
            }

            return false;
        }

        private static bool IsGUIGameObject(GameObject go)
        {
            return (go.GetComponent<RectTransform>() != null);
        }

        private static void BuildUIFXMenu()
        {
            _uifxMenu = new GenericMenu();
            AddMainMenuItems(_uifxMenu, "Utilities", FilterBase.AddComponentsUtilityPath, "Utilities/");
            AddMainMenuItems(_uifxMenu, "Effects", FilterBase.AddComponentsEffectsPath, "Effects/");
            AddMainMenuItems(_uifxMenu, "Sources", FilterBase.AddComponentsSourcesPath, "Sources/");
            AddMainMenuItems(_uifxMenu, "Filters", FilterBase.AddComponentsFiltersPath, string.Empty);
        }

        private static void ShowMenu()
        {
            if (_uifxMenu == null)
            {
                BuildUIFXMenu();
            }
            _uifxMenu.ShowAsContext();
        }

        #if UNITY_2023_1_OR_NEWER
        private const string MenuComponentUI = "Component/UI (Canvas)/";
        #else
        private const string MenuComponentUI = "Component/UI/";
        #endif
        private const string MenuComponentLayout = "Component/Layout/";

        private static void ShowUGUIMenu()
        {
            if (_uguiMenu == null)
            {
                BuildUGUIMenu();
            }
            _uguiMenu.ShowAsContext();
        }

        public static void BuildUGUIMenu()
        {
            string[] layoutItems = UnityEditor.Unsupported.GetSubmenus(MenuComponentLayout);
            string[] uiItems = UnityEditor.Unsupported.GetSubmenus(MenuComponentUI);
            
            _uguiMenu = new GenericMenu();

            _uguiMenu.AddSeparator(string.Empty);
            _uguiMenu.AddDisabledItem(new GUIContent("Layout"), false);
            _uguiMenu.AddSeparator(string.Empty);

            foreach (var menuPath in layoutItems)
            {
                string strippedPath = menuPath.Substring(MenuComponentLayout.Length);

                _uguiMenu.AddItem(new GUIContent(strippedPath), false, () =>
                {
                    // Triggers Unity's internal menu execution sequence safely
                    EditorApplication.ExecuteMenuItem(menuPath);
                });
            }

            _uguiMenu.AddSeparator(string.Empty);
            _uguiMenu.AddDisabledItem(new GUIContent("UGUI"), false);
            _uguiMenu.AddSeparator(string.Empty);

            foreach (var menuPath in uiItems)
            {
                string strippedPath = menuPath.Substring(MenuComponentUI.Length);

                _uguiMenu.AddItem(new GUIContent(strippedPath), false, () =>
                {
                    // Triggers Unity's internal menu execution sequence safely
                    EditorApplication.ExecuteMenuItem(menuPath);
                });
            }
        }

        private static List<Assembly> GetAssembliesWithPrefix(string prefix, string skipSuffix = null)
        {
            #if UNITY_6000_4_OR_NEWER
            var allAssemblies = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
            #else
            var allAssemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            #endif

            List<Assembly> foundAssemblies = new List<Assembly>();
            foreach (Assembly assembly in allAssemblies)
            {
                // 3. Extract the clean short name of the assembly (e.g., "UnityEditor" instead of the full assembly qualified string)
                string assemblyName = assembly.GetName().Name;

                if (assemblyName != null)
                {
                    // 4. Perform a case-insensitive prefix check to guarantee accurate matches
                    if (assemblyName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip if it ends with a suffix
                        if (!string.IsNullOrEmpty(skipSuffix) && assemblyName.EndsWith(skipSuffix, System.StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
						}
                        foundAssemblies.Add(assembly);
                    }
                }
            }
            return foundAssemblies;
		}

        public static void AddMainMenuItems(GenericMenu menu, string title, string path, string prefix)
        {
            var uifxRuntimeAssemblies = GetAssembliesWithPrefix("ChocDino.UIFX", ".Editor");

            List<string> menuPaths = new List<string>(16);
            foreach (var assembly in uifxRuntimeAssemblies)
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    var menuItemAttribute = type.GetCustomAttribute<AddComponentMenu>();
                    if (menuItemAttribute != null)
                    {
                        string menuPath = menuItemAttribute.componentMenu;

                        if (menuPath.StartsWith(path, System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (!menuPaths.Contains(menuPath))
                            {
                                menuPaths.Add(menuPath);
                            }
                        }
                    }
                }
            }

            if (menuPaths.Count > 0)
            {
                if (string.IsNullOrEmpty(prefix))
                {
                    menu.AddSeparator(string.Empty);
                    menu.AddDisabledItem(new GUIContent(title), false);
                    menu.AddSeparator(string.Empty);
                }

                menuPaths.Sort();

                foreach (var menuPath in menuPaths)
                {
                    // Clean up the string path to remove the root category for the dropdown look
                    // e.g., converts "Component/UI/Effects/Shadow" to "Effects/Shadow"
                    string strippedPath = menuPath.Substring(path.Length);

                    if (strippedPath.EndsWith(" Source"))
                    {
                        strippedPath = strippedPath.Replace(" Source", string.Empty);
                    }
                    strippedPath = strippedPath.Replace("UIFX - ", string.Empty);
                    strippedPath = strippedPath.Replace(" Filter", string.Empty);

                    // Capture the loop variable safely for the anonymous callback lambda closure
                    string executePath = "Component/" + menuPath;

                    // 3. Re-map the found item smoothly into your clean GenericMenu instance
                    menu.AddItem(new GUIContent(prefix + strippedPath), false, () =>
                    {
                        // Triggers Unity's internal menu execution sequence safely
                        EditorApplication.ExecuteMenuItem(executePath);
                    });
                }
            }
        }
    }
}
