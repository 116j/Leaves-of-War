using System.Collections.Generic;
using Hortensia.Runtime;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Leaves of War/Notebook Font", fileName = "NotebookFont")]
public sealed class NotebookFont : ScriptableObject
{
    public const string ResourcePath = "Settings/NotebookFont";
    public const string FontFolder = "Fonts/Notebook";

    [Tooltip("Handwriting of the notebook, the poems on the floor, the flying letters and the last page, in every level. Empty: the font in Resources/Fonts/Notebook. Off below: the font set in each scene.")]
    public Font font;
    public bool use = true;
    [Tooltip("Size compared to the old notebook font.")]
    [Range(0.4f, 1.6f)] public float size = 0.75f;
    [Tooltip("Extra space between the lines of the poems.")]
    [Range(-40f, 80f)] public float lineSpacing = 25f;

    private static NotebookFont cached;
    private static bool searched;
    private static TMP_FontAsset built;
    private static Font builtFrom;

    public static NotebookFont Current
    {
        get
        {
            if (!searched)
            {
                searched = true;
                cached = Resources.Load<NotebookFont>(ResourcePath);
            }
            return cached;
        }
    }

    public static bool Active => Current == null || Current.use;
    public static float Size => Current != null ? Current.size : 0.75f;
    public static float LineSpacing => Current != null ? Current.lineSpacing : 25f;

    public static TMP_FontAsset Asset(TMP_FontAsset look)
    {
        if (!Active)
            return null;
        Font file = Current != null && Current.font != null ? Current.font : null;
        if (file == null)
        {
            Font[] found = Resources.LoadAll<Font>(FontFolder);
            file = found.Length > 0 ? found[0] : null;
        }
        if (file == null)
            return null;
        if (built == null || builtFrom != file)
        {
            built = MainMenuController.ResolveFont(file, look, "Notebook Font");
            builtFrom = file;
        }
        return built;
    }

    public static void Apply(IEnumerable<TMP_FontAsset> replace, TMP_FontAsset look)
    {
        TMP_FontAsset font = Asset(look);
        if (font == null)
            return;
        var old = new HashSet<TMP_FontAsset>(replace);
        old.Remove(null);
        old.Remove(font);
        if (old.Count == 0)
            return;
        foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (text == null || !old.Contains(text.font))
                continue;
            text.font = font;
            text.fontSize *= Size;
        }
    }
}
