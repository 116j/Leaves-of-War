

using TMPro;
using UnityEngine;

public class TranscribePoem : RevealableObject
{
    public PoemInfo poemInfo;

    [SerializeField]
    protected TextMeshProUGUI _assignedDateCurrent;

    [SerializeField]
    protected TextMeshProUGUI _assignedNameCurrent;

    [SerializeField]
    protected TextMeshProUGUI _assignedPlaceCurrent;

    [SerializeField]
    protected TextMeshProUGUI _assignedPoemTextCurrent;

    [SerializeField]
    protected TextMeshProUGUI _assignedDateOld;

    [SerializeField]
    protected TextMeshProUGUI _assignedNameOld;

    [SerializeField]
    protected TextMeshProUGUI _assignedPlaceOld;

    [SerializeField]
    protected TextMeshProUGUI _assignedPoemTextOld;

    int currentp=1;

    private void OnEnable()
    {
       
        Book.OnMouseDragRight += OnDragRightPage;
        Book.OnMouseDragLeft += OnDragLefttPage;


        Book.RestoreOriginalPageLeft += ResetLeftPage;
        Book.RestoreOriginalPageRight += ResetRightPage;

        Book.ForwardPage += OnForwardPage;
        Book.BackwardPage += OnBackwardPage;
    }

    private void OnDisable()
    {
      
        Book.OnMouseDragRight -= OnDragRightPage;
        Book.OnMouseDragLeft -= OnDragLefttPage;

        Book.RestoreOriginalPageLeft -= ResetLeftPage;
        Book.RestoreOriginalPageRight -= ResetRightPage;

        Book.ForwardPage -= OnForwardPage;
        Book.BackwardPage -= OnBackwardPage;
    }
    private void Start()
    {

       // Testing 
       OnFlipPage();
    }
  

    protected override void OnRevealChanged(bool revealed)
    {
        if (revealed)
        {
            Debug.Log("Poem revealed!");
        }
        else
        {
            Debug.Log("Poem hidden!");
        }
    }

    void OnDragRightPage()
    {
        InitPoemOldPoem(currentp);
        InitPoemOldDesc(currentp+1);
        InitPoemCurrentPoem(currentp + 1);

        Debug.Log("Drag Right Page");
    }

    void ResetLeftPage()
    {
        Debug.Log("After Left Page  Relase Cuurent page info " + currentp);
        InitPoemCurrentDesc(currentp);
    }

    void ResetRightPage()
    {
        Debug.Log("After Right Page  Relase Cuurent page info " + currentp);
        InitPoemCurrentPoem(currentp);
    }

    void OnForwardPage(int index)
    {
        currentp = index;

        Debug.Log($"Forward flip completed. Poem index: {currentp}");

        OnFlipPage();
    }

    void OnBackwardPage(int index)
    {
        currentp = index;

        Debug.Log($"Backward flip completed. Poem index: {currentp}");

        OnFlipPage();
    }

    void OnDragLefttPage()
    {
        InitPoemOldPoem(currentp -1);
        InitPoemOldDesc(currentp);
        InitPoemCurrentDesc(currentp-1);

        Debug.Log("Drag Right Page");
    }

    void OnFlipPage()
    {
        InitPoemCurrentDesc(currentp);
        InitPoemCurrentPoem(currentp);

        Debug.Log($"Flip Page Done. Current index: {currentp}");
    }

    // Left Page (Live)
    void InitPoemCurrentDesc(int poemIndex)
    {
        if (poemInfo == null || poemInfo._poemsList == null)
            return;

        for (int i = 0; i < poemInfo._poemsList.Count; i++)
        {
            if (poemInfo._poemsList[i].poemId == poemIndex)
            {
                if (_assignedDateCurrent != null)
                    _assignedDateCurrent.text = poemInfo._poemsList[i].poemDate;

                if (_assignedNameCurrent != null)
                    _assignedNameCurrent.text = poemInfo._poemsList[i].poemName;

                if (_assignedPlaceCurrent != null)
                    _assignedPlaceCurrent.text = poemInfo._poemsList[i].poemPlace;

                break;
            }
        }
    }

    // Right Page (Live)
    void InitPoemCurrentPoem(int poemIndex)
    {
        if (poemInfo == null || poemInfo._poemsList == null)
            return;

        for (int i = 0; i < poemInfo._poemsList.Count; i++)
        {
            if (poemInfo._poemsList[i].poemId == poemIndex)
            {
                if (_assignedPoemTextCurrent != null)
                    _assignedPoemTextCurrent.text = poemInfo._poemsList[i].poemText;

                break;
            }
        }
    }

    // Left Page (Old)
    void InitPoemOldDesc(int poemIndex)
    {
        if (poemInfo == null || poemInfo._poemsList == null)
            return;

        for (int i = 0; i < poemInfo._poemsList.Count; i++)
        {
            if (poemInfo._poemsList[i].poemId == poemIndex)
            {
                if (_assignedDateOld != null)
                    _assignedDateOld.text = poemInfo._poemsList[i].poemDate;

                if (_assignedNameOld != null)
                    _assignedNameOld.text = poemInfo._poemsList[i].poemName;

                if (_assignedPlaceOld != null)
                    _assignedPlaceOld.text = poemInfo._poemsList[i].poemPlace;

                break;
            }
        }
    }
  

    // Right Page (Old)
    void InitPoemOldPoem(int poemIndex)
    {
        Debug.Log("Old Poem Loading...");

        if (poemInfo == null || poemInfo._poemsList == null)
            return;

        for (int i = 0; i < poemInfo._poemsList.Count; i++)
        {
            if (poemInfo._poemsList[i].poemId == poemIndex)
            {
                if (_assignedPoemTextOld != null)
                {
                    _assignedPoemTextOld.text =
                        poemInfo._poemsList[i].poemText;

                    Debug.Log(
                  "Old Poem Loading Success [Loop]... " +
                  _assignedPoemTextOld.text.ToString()
              );
                }

                Debug.Log(
                    "Old Poem Loading Success... " +
                    poemInfo._poemsList[i].poemText
                );

                return;
            }
        }

        Debug.LogWarning(
            "Old Poem Loading Failed. Poem ID: " + poemIndex
        );
    }

    private void OnApplicationQuit()
    {
        poemInfo._poemsList.Clear();
    }
}