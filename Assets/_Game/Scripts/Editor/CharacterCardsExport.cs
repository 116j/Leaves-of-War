using System.IO;
using Hortensia.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterCardsExport
{
    private const string Folder = "Assets/_Game/Art/2D/Character Cards";

    [MenuItem("Tools/Leaves of War/UI/Export character cards (PNG 1024)")]
    private static void Export()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            Directory.CreateDirectory(Folder);
        var cameraObject = new GameObject("Card Camera") { hideFlags = HideFlags.HideAndDontSave };
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.orthographic = true;
        camera.cullingMask = 1 << 5;
        var texture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = texture;

        var canvasObject = new GameObject("Card Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)) { hideFlags = HideFlags.HideAndDontSave };
        canvasObject.layer = 5;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 5f;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024f, 1024f);

        var names = new System.Collections.Generic.List<string>();
        try
        {
            for (int i = 0; i <= CharacterCards.Cast.Length; i++)
            {
                RectTransform card = CharacterCards.Build(canvasObject.transform, i);
                foreach (Transform t in card.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = 5;
                Canvas.ForceUpdateCanvases();
                foreach (TMPro.TMP_Text text in card.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    text.ForceMeshUpdate(true);
                foreach (Graphic g in card.GetComponentsInChildren<Graphic>(true))
                    g.Rebuild(CanvasUpdate.PreRender);
                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                string name = (i < CharacterCards.Cast.Length ? (i + 1) + " - " + CharacterCards.Cast[i].name : "7 - Giuseppe Ungaretti") + ".png";
                File.WriteAllBytes(Path.Combine(Folder, name), image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(card.gameObject);
                names.Add(name);
            }
        }
        finally
        {
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(cameraObject);
        }
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(Folder);
        EditorUtility.DisplayDialog("Character Cards", names.Count + " cards saved in " + Folder + ":\n" + string.Join("\n", names) +
            "\n\nIn game they are drawn live by the Level Flow action \"Character Cards Then Next Level\" (Courton, the end of the game).", "OK");
    }
}
