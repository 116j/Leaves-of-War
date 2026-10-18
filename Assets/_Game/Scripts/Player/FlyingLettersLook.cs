using UnityEngine;

public sealed class FlyingLettersLook : ScriptableObject
{
    public const string ResourcePath = "Settings/FlyingLetters";
    public const string AssetPath = "Assets/Resources/" + ResourcePath + ".asset";

    public bool lettersFly = true;
    public float flightSeconds = 2.4f;
    public float flightSize = 2.6f;
    public float spiralRadius = 0.24f;
    public float spiralTurns = 1.5f;

    private static FlyingLettersLook cached;
    private static bool searched;

    public static FlyingLettersLook Current
    {
        get
        {
            if (!searched || cached == null)
            {
                searched = true;
                cached = Resources.Load<FlyingLettersLook>(ResourcePath);
            }
            return cached;
        }
    }

#if UNITY_EDITOR
    public static void Save(bool fly, float seconds, float size, float radius, float turns)
    {
        FlyingLettersLook look = UnityEditor.AssetDatabase.LoadAssetAtPath<FlyingLettersLook>(AssetPath);
        if (look == null)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Settings"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "Settings");
            look = CreateInstance<FlyingLettersLook>();
            UnityEditor.AssetDatabase.CreateAsset(look, AssetPath);
        }
        if (look.lettersFly == fly && Mathf.Approximately(look.flightSeconds, seconds) && Mathf.Approximately(look.flightSize, size) &&
            Mathf.Approximately(look.spiralRadius, radius) && Mathf.Approximately(look.spiralTurns, turns))
            return;
        look.lettersFly = fly;
        look.flightSeconds = seconds;
        look.flightSize = size;
        look.spiralRadius = radius;
        look.spiralTurns = turns;
        UnityEditor.EditorUtility.SetDirty(look);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(look);
        cached = look;
        searched = true;
    }
#endif
}
