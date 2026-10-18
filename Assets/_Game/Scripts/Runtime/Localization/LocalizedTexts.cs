using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LocalizedTexts : MonoBehaviour
{
    private sealed class State
    {
        public string original;
        public string applied;
        public Language language;
    }

    private static LocalizedTexts instance;
    private readonly Dictionary<TMP_Text, State> states = new Dictionary<TMP_Text, State>();
    private readonly HashSet<TMP_Text> pending = new HashSet<TMP_Text>();
    private readonly List<TMP_Text> dead = new List<TMP_Text>();
    private readonly HashSet<TMP_Text> fitted = new HashSet<TMP_Text>();
    private bool busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (instance != null)
            return;
        var go = new GameObject("Localized Texts");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LocalizedTexts>();
    }

    private void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        Canvas.willRenderCanvases += Refresh;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Loc.Changed += OnLanguage;
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        Canvas.willRenderCanvases -= Refresh;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Loc.Changed -= OnLanguage;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            pending.Add(text);
    }

    private void OnTextChanged(Object changed)
    {
        if (!busy && changed is TMP_Text text)
            pending.Add(text);
    }

    private void OnLanguage()
    {
        foreach (TMP_Text text in states.Keys)
            if (text != null)
                pending.Add(text);
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            pending.Add(text);
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (busy)
            return;
        busy = true;
        foreach (KeyValuePair<TMP_Text, State> pair in states)
        {
            if (pair.Key == null)
                dead.Add(pair.Key);
            else if (!ReferenceEquals(pair.Key.text, pair.Value.applied) || pair.Value.language != Loc.Current)
                pending.Add(pair.Key);
        }
        foreach (TMP_Text text in dead)
        {
            states.Remove(text);
            fitted.Remove(text);
        }
        dead.Clear();
        if (pending.Count > 0)
        {
            foreach (TMP_Text text in pending)
                if (text != null)
                    Apply(text);
            pending.Clear();
        }
        busy = false;
    }

    private void Apply(TMP_Text text)
    {
        string now = text.text;
        Language language = Loc.Current;
        if (!states.TryGetValue(text, out State state))
            states[text] = state = new State { original = now, applied = now, language = language };
        else if (now != state.applied)
            state.original = now;
        else if (state.language == language)
        {
            state.applied = now;
            return;
        }
        string translated = Loc.T(state.original);
        state.language = language;
        state.applied = translated;
        if (translated != now)
        {
            text.text = translated;
            state.applied = text.text;
        }
        else
            state.applied = now;
        if (translated != state.original)
            fitted.Add(text);
        if (fitted.Contains(text))
            LocFit.Apply(text, state.original, translated);
    }
}
