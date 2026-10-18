using System.Collections;
using Hortensia.Runtime;
using UnityEngine;

[RequireComponent(typeof(AssignPoem))]
public sealed class PoemFinale : MonoBehaviour
{
    [Tooltip("Once the paper is uncovered the camera settles on it and will not turn away until this long after the poem is written.")]
    [SerializeField, Min(0f)] private float releaseAfter = 4f;
    [SerializeField] private bool holdCamera = true;
    [Tooltip("The golden leaf drifts to the centre of the view and lands on the paper when the notebook rises.")]
    [SerializeField] private bool goldenLeafLands = true;
    [Tooltip("Where the leaf lands, in metres from the centre of the paper (right, towards the bottom).")]
    [SerializeField] private Vector2 leafSpot = new Vector2(0.24f, -0.2f);
    [Tooltip("The transcription starts only this close to the paper (metres from the eyes): farther away, the camera is not held and a message asks to come closer.")]
    [SerializeField, Min(1f)] private float reach = 5.5f;
    [SerializeField] private string closerMessage = "GET CLOSER TO THE POEM ON THE GROUND TO START TRANSCRIBING";
    [SerializeField, Min(8f)] private float messageSize = 34f;

    private TMPro.TextMeshProUGUI message;

    private AssignPoem poem;
    private bool held;
    private bool landed;
    private bool done;

    private void Awake()
    {
        poem = GetComponent<AssignPoem>();
        message = ScreenPrompt.Create(transform, "Get Closer Message", messageSize, 50f, true);
    }

    private void OnEnable() => TranscribePoem.Transcribed += OnTranscribed;

    private void OnDisable()
    {
        TranscribePoem.Transcribed -= OnTranscribed;
        Release();
    }

    private void Update()
    {
        bool waiting = !done && !held && poem.IsRevealed && LevelFlow.PoemAllowed;
        bool near = Near();
        ScreenPrompt.Show(message, waiting && !near && !DialogueRunner.IsPlaying && !DiaryInteraction.IsReading && Time.timeScale > 0f, closerMessage);
        if (done)
            return;
        if (holdCamera && waiting && near)
        {
            held = true;
            FirstPersonController.HoldLook = transform;
        }
        if (goldenLeafLands && !landed && TranscribePoem.AnyTranscribing)
        {
            landed = true;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 down = Vector3.ProjectOnPlane(-transform.up, Vector3.up).normalized;
            FollowerLeaf.LandOn(transform.position + right * leafSpot.x - down * leafSpot.y + Vector3.up * 0.012f);
        }
    }

    private bool Near()
    {
        Camera view = Gaze.View;
        if (view == null)
            return true;
        Vector3 eye = view.transform.position;
        Bounds b = poem.Footprint;
        return Vector3.Distance(eye, b.ClosestPoint(eye)) <= reach;
    }

    private void OnTranscribed(int poemId)
    {
        if (poemId != poem.PoemId || done)
            return;
        done = true;
        StartCoroutine(ReleaseLater());
    }

    private IEnumerator ReleaseLater()
    {
        yield return new WaitForSeconds(releaseAfter);
        Release();
    }

    private void Release()
    {
        if (FirstPersonController.HoldLook == transform)
            FirstPersonController.HoldLook = null;
    }
}
