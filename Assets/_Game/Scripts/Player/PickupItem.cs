using System;
using Hortensia.Runtime;
using UnityEngine;

public class PickupItem : MonoBehaviour, IInteractable
{
    public enum Item
    {
        Broom,
        Diary
    }

    [SerializeField] private Item item;
    [SerializeField] private string prompt = "PICK UP";
    [SerializeField] private AudioClip pickupClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [Tooltip("Off: it can be taken only when the Level Flow allows it (after the first dialogue).")]
    [SerializeField] private bool availableFromStart = false;

    public event Action<PickupItem> PickedUp;

    public Item Kind => item;
    public bool Available { get; set; }
    public bool Taken { get; private set; }
    public string Prompt => Available && !Taken ? prompt : string.Empty;

    private void Awake() => Available = availableFromStart;

    public void Interact()
    {
        if (!Available || Taken)
            return;
        Taken = true;

        if (item == Item.Broom)
            FindAnyObjectByType<BroomSweeper>()?.GiveBroom();
        else
            FindAnyObjectByType<DiaryInteraction>()?.GiveDiary();

        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, volume);
        gameObject.SetActive(false);
        PickedUp?.Invoke(this);
    }
}
