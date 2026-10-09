using System.Globalization;
using Hortensia.Narrative;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hortensia.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DebugMenu : MonoBehaviour
    {
        private const string DreamSceneName = "DreamGreenhouse";

        private bool menuVisible;
        private bool overlayVisible = true;
        private string beatInput = "0";
        private Vector2 flagScroll;
        private Rect windowRect = new Rect(20f, 60f, 440f, 680f);
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.f1Key.wasPressedThisFrame)
                SetMenuVisible(!menuVisible);
            if (Keyboard.current.f2Key.wasPressedThisFrame)
                overlayVisible = !overlayVisible;
        }

        private void OnDisable()
        {
            SetMenuVisible(false);
        }

        private void OnGUI()
        {
            GameSession session = GameSession.Instance;
            if (session == null || session.State == null)
                return;

            if (overlayVisible)
                DrawOverlay(session);
            if (menuVisible)
                windowRect = GUI.Window(GetEntityId().GetHashCode(), windowRect, _ => DrawWindow(session), "HORTENSIA DEBUG");
        }

        private static void DrawOverlay(GameSession session)
        {
            NarrativeState state = session.State;
            ChapterDefinition chapter = session.CurrentChapter;
            string flags = string.Empty;
            foreach (FlagId flag in state.Flags)
            {
                if (flag != null)
                    flags += (flags.Length == 0 ? string.Empty : ", ") + flag.Id;
            }

            string text =
                $"F1 DEBUG  F2 OVERLAY\n" +
                $"CH {state.ChapterIndex}: {(chapter != null ? chapter.Title : "?")}  BEAT {state.BeatIndex}\n" +
                $"BLOOMS {state.Garden.BloomCount}  LIES {state.Garden.LiesTold}  FED {state.Garden.PatientsFed}\n" +
                $"TASKS: {DescribeTasks(session)}\n" +
                $"CARRYING: {DescribeCarriedItem(session)}  ENDING: {session.CurrentEndingId ?? "none"}\n" +
                $"SEQUENCE: {session.ActiveSequenceId ?? "none"}\n" +
                $"FLAGS: {(flags.Length > 0 ? flags : "none")}";

            GUI.Box(new Rect(10f, 10f, Mathf.Min(Screen.width - 20f, 760f), 136f), text);
        }

        private static string DescribeTasks(GameSession session)
        {
            if (session == null || session.State == null || session.Catalog == null)
                return "none";

            string summary = string.Empty;
            for (int i = 0; i < session.Catalog.TaskObjectives.Count; i++)
            {
                TaskObjective objective = session.Catalog.TaskObjectives[i];
                if (objective == null)
                    continue;

                int count = session.State.Tasks.CountFor(objective);
                int required = session.State.Tasks.RequiredFor(objective, 0);
                string total = required > 0 ? required.ToString(CultureInfo.InvariantCulture) : "-";
                summary += (summary.Length > 0 ? "  " : string.Empty) +
                    $"{objective.PlayerFacingLabel.ToUpperInvariant()} {count}/{total}";
            }

            return summary.Length > 0 ? summary : "none";
        }

        private static string DescribeCarriedItem(GameSession session)
        {
            return session != null &&
                session.TryGetCarriedItemHolder(out CarriedItemHolder holder) &&
                holder.HasItem
                    ? holder.PlayerFacingLabel.ToUpperInvariant()
                    : "none";
        }

        private void DrawWindow(GameSession session)
        {
            GUILayout.Label("Gates");
            session.SkipGates = GUILayout.Toggle(session.SkipGates, "Skip every gate");

            GUILayout.Space(8f);
            GUILayout.Label("Jump to chapter");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < session.Catalog.Chapters.Count; i++)
            {
                ChapterDefinition chapter = session.Catalog.Chapters[i];
                if (chapter != null && GUILayout.Button(chapter.Index.ToString(CultureInfo.InvariantCulture)))
                {
                    SetMenuVisible(false);
                    session.JumpToChapter(chapter.Index);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Beat", GUILayout.Width(42f));
            beatInput = GUILayout.TextField(beatInput, GUILayout.Width(70f));
            if (GUILayout.Button("Jump") && int.TryParse(beatInput, out int beatIndex))
            {
                SetMenuVisible(false);
                session.JumpToBeat(beatIndex);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Dreams");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("First"))
                JumpToDream(session, 1);
            if (GUILayout.Button("Second"))
                JumpToDream(session, 4);
            if (GUILayout.Button("Third"))
                JumpToDream(session, 6);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Endings");
            for (int i = 0; i < session.Catalog.Endings.Count; i++)
            {
                EndingDefinition ending = session.Catalog.Endings[i];
                if (ending != null && GUILayout.Button(ending.Title))
                {
                    SetMenuVisible(false);
                    session.StartEnding(ending);
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label("Tasks");
            for (int i = 0; i < session.Catalog.TaskObjectives.Count; i++)
            {
                TaskObjective objective = session.Catalog.TaskObjectives[i];
                if (objective == null)
                    continue;

                int count = session.State.Tasks.CountFor(objective);
                int required = session.State.Tasks.RequiredFor(objective, 0);
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    $"{objective.PlayerFacingLabel}: {count}/{(required > 0 ? required.ToString(CultureInfo.InvariantCulture) : "-")}",
                    GUILayout.Width(275f));

                bool controlsEnabled = GUI.enabled;
                GUI.enabled = controlsEnabled && count > 0;
                if (GUILayout.Button("-", GUILayout.Width(42f)))
                    session.DebugSetTaskCount(objective, count - 1);
                GUI.enabled = controlsEnabled && required > 0 && count < required;
                if (GUILayout.Button("+", GUILayout.Width(42f)))
                    session.DebugSetTaskCount(objective, count + 1);
                GUI.enabled = controlsEnabled;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Flags");
            flagScroll = GUILayout.BeginScrollView(flagScroll, GUILayout.Height(170f));
            for (int i = 0; i < session.Catalog.Flags.Count; i++)
            {
                FlagId flag = session.Catalog.Flags[i];
                if (flag == null)
                    continue;

                bool present = session.State.HasFlag(flag);
                bool next = GUILayout.Toggle(present, flag.Id);
                if (next != present)
                {
                    if (next) session.SetFlag(flag);
                    else session.ClearFlag(flag);
                }
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("Save checkpoint"))
                session.SaveCheckpoint();
            if (GUILayout.Button("Close (F1)"))
                SetMenuVisible(false);

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
        }

        private void JumpToDream(GameSession session, int chapterIndex)
        {
            ChapterDefinition chapter = session?.Catalog?.ChapterAt(chapterIndex);
            if (!TryFindDreamBeatIndex(chapter, out int beatIndex))
            {
                Debug.LogError(
                    $"Debug dream jump could not find a {nameof(TravelBeat)} to " +
                    $"'{DreamSceneName}' in Chapter {chapterIndex}.");
                return;
            }

            SetMenuVisible(false);
            session.JumpToChapter(chapterIndex, beatIndex);
        }

        private static bool TryFindDreamBeatIndex(
            ChapterDefinition chapter,
            out int beatIndex)
        {
            beatIndex = -1;
            if (chapter == null)
                return false;

            for (int i = 0; i < chapter.Beats.Count; i++)
            {
                if (chapter.Beats[i] is TravelBeat travel &&
                    string.Equals(
                        travel.TargetScene,
                        DreamSceneName,
                        System.StringComparison.Ordinal))
                {
                    beatIndex = i;
                    return true;
                }
            }

            return false;
        }

        private void SetMenuVisible(bool visible)
        {
            if (menuVisible == visible)
                return;

            menuVisible = visible;
            GameSession session = GameSession.Instance;

            if (visible)
            {
                previousCursorLock = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                session?.PushNarrativeInputLock();
                session?.PushPlayerLock();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            session?.PopPlayerLock();
            session?.PopNarrativeInputLock();
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
    }
}
