using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public enum Language
{
    English,
    Italian
}

public static class Loc
{
    public const string PrefKey = "LeavesOfWar.Language";
    public const string ItalianPath = "Localization/Italian";

    private static bool loaded;
    private static Language current;
    private static readonly Dictionary<string, string> Table = new Dictionary<string, string>();
    private static readonly List<(Regex pattern, string italian)> Templates = new List<(Regex, string)>();
    private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
    private static readonly Dictionary<string, float> Sizes = new Dictionary<string, float>();

    public static event Action Changed;

    public static Language Current
    {
        get
        {
            Load();
            return current;
        }
    }

    public static bool Italian => Current == Language.Italian;

    public static void Set(Language language)
    {
        Load();
        if (language == current)
            return;
        current = language;
        PlayerPrefs.SetInt(PrefKey, (int)language);
        PlayerPrefs.Save();
        Cache.Clear();
        Changed?.Invoke();
    }

    public static void Reload()
    {
        loaded = false;
        Load();
        Changed?.Invoke();
    }

    public static string T(string english)
    {
        if (string.IsNullOrEmpty(english))
            return english;
        Load();
        if (current == Language.English)
            return english;
        if (Cache.TryGetValue(english, out string cached))
            return cached;
        string result = Translate(english, 0);
        if (Cache.Count > 4000)
            Cache.Clear();
        Cache[english] = result;
        return result;
    }

    public static float Size(string english)
    {
        Load();
        if (current == Language.English || string.IsNullOrEmpty(english))
            return 1f;
        return Sizes.TryGetValue(Key(english), out float size) ? size : 1f;
    }

    public static bool Has(string english)
    {
        Load();
        return !string.IsNullOrEmpty(english) && Table.ContainsKey(Key(english));
    }

    public static IEnumerable<string> Keys
    {
        get
        {
            Load();
            return Table.Keys;
        }
    }

    private static string Translate(string english, int depth)
    {
        string key = Key(english);
        if (key.Length == 0)
            return english;
        if (Table.TryGetValue(key, out string italian))
            return Case(english, italian);
        if (depth < 2)
            foreach (var (pattern, template) in Templates)
            {
                Match m = pattern.Match(Normal(english));
                if (!m.Success)
                    continue;
                string result = template;
                for (int g = 1; g < m.Groups.Count; g++)
                    result = result.Replace("{" + (g - 1) + "}", Translate(m.Groups[g].Value, depth + 1));
                return Case(english, result);
            }
        return english;
    }

    private static string Case(string source, string result)
    {
        if (source.Contains("<") || result.Contains("<"))
            return result;
        bool letters = false, lower = false;
        foreach (char c in source)
        {
            if (!char.IsLetter(c))
                continue;
            letters = true;
            if (char.IsLower(c))
                lower = true;
        }
        if (!letters)
            return result;
        if (!lower)
            return source.Length > 1 ? result.ToUpper(CultureInfo.InvariantCulture) : result;
        bool resultLower = false;
        foreach (char c in result)
            if (char.IsLower(c))
            {
                resultLower = true;
                break;
            }
        if (resultLower || result.Length < 2)
            return result;
        string down = result.ToLower(CultureInfo.InvariantCulture);
        return char.ToUpper(down[0], CultureInfo.InvariantCulture) + down.Substring(1);
    }

    private static string Normal(string s) => Regex.Replace(s.Replace("\r", string.Empty).Trim(), @"\s+", " ");

    private static string Key(string s) => Normal(s).ToLowerInvariant();

    private static void Load()
    {
        if (loaded)
            return;
        loaded = true;
        int saved = PlayerPrefs.GetInt(PrefKey, -1);
        current = saved >= 0 ? (Language)Mathf.Clamp(saved, 0, 1) : Application.systemLanguage == SystemLanguage.Italian ? Language.Italian : Language.English;
        Table.Clear();
        Sizes.Clear();
        Templates.Clear();
        Cache.Clear();
        TextAsset asset = Resources.Load<TextAsset>(ItalianPath);
        if (asset != null)
            Parse(asset.text);
    }

    private static void Parse(string text)
    {
        string english = null;
        var italian = new StringBuilder();
        var englishBuilder = new StringBuilder();
        bool inEnglish = false, inItalian = false;

        void Flush()
        {
            if (english != null && inItalian)
                Add(english, italian.ToString());
            english = null;
            italian.Clear();
            englishBuilder.Clear();
            inEnglish = inItalian = false;
        }

        foreach (string raw in text.Replace("\r", string.Empty).Split('\n'))
        {
            if (raw.StartsWith(">>>"))
            {
                Flush();
                inEnglish = true;
                englishBuilder.Append(raw.Length > 3 ? raw.Substring(3).TrimStart(' ') : string.Empty);
                continue;
            }
            if (raw.StartsWith("<<<"))
            {
                english = englishBuilder.ToString();
                inEnglish = false;
                inItalian = true;
                italian.Append(raw.Length > 3 ? raw.Substring(3).TrimStart(' ') : string.Empty);
                continue;
            }
            if (raw.StartsWith("#"))
            {
                Flush();
                continue;
            }
            if (inEnglish)
                englishBuilder.Append('\n').Append(raw);
            else if (inItalian)
                italian.Append('\n').Append(raw);
        }
        Flush();
    }

    private static void Add(string english, string italian)
    {
        italian = italian.TrimEnd('\n', ' ');
        if (string.IsNullOrWhiteSpace(english) || string.IsNullOrWhiteSpace(italian))
            return;
        string key = Key(english);
        Match size = Regex.Match(italian, @"^@(\d{2,3})\s+");
        if (size.Success)
        {
            Sizes[key] = Mathf.Clamp(int.Parse(size.Groups[1].Value) / 100f, 0.3f, 2f);
            italian = italian.Substring(size.Length);
        }
        Table[key] = italian;
        if (Regex.IsMatch(english, @"\{\d\}"))
        {
            string pattern = "^" + Regex.Replace(Regex.Escape(Normal(english)), @"\\\{(\d)\}", "(.+?)") + "$";
            Templates.Add((new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant), italian));
        }
    }
}
