using Hortensia.Narrative;
using UnityEditor;

namespace Hortensia.Editor.Validation
{
    internal static class NarrativeCatalogLocator
    {
        public static bool TryLoadSingle(out NarrativeCatalog catalog, out string error)
        {
            catalog = null;
            string[] guids = AssetDatabase.FindAssets("t:NarrativeCatalog");
            if (guids.Length == 0)
            {
                error = "No NarrativeCatalog asset was found.";
                return false;
            }

            if (guids.Length > 1)
            {
                error = $"Expected one NarrativeCatalog asset, but found {guids.Length}.";
                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            catalog = AssetDatabase.LoadAssetAtPath<NarrativeCatalog>(path);
            if (catalog != null)
            {
                error = string.Empty;
                return true;
            }

            error = $"The NarrativeCatalog at '{path}' could not be loaded.";
            return false;
        }
    }
}
