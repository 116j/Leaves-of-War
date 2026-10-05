using UnityEngine;

public class TitleMenu : MonoBehaviour
{
    public void StartGame()
    {
        BroomCursor.Instance.gameObject.SetActive(false);
        //blow awy leaves
        //show notebook
        //load level 1 scene
        //set active the player
    }

    public void OpenCredits()
    {
        BroomCursor.Instance.gameObject.SetActive(false);
        //start credits cutscene
    }

    public void OpenSettings()
    {
        BroomCursor.Instance.gameObject.SetActive(false);
        SettingsMenu.Instance.Open(() =>
        {
            BroomCursor.Instance.gameObject.SetActive(true);
        });
    }

    public void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }
}
