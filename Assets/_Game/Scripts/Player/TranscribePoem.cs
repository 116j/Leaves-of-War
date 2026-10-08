using UnityEngine;
using TMPro;
using System.Linq;

public class TranscribePoem : RevealableObject
{
    public PoemInfo poemInfo;

    [SerializeField]
    protected TextMeshProUGUI _assignedDate;

    [SerializeField]
    protected TextMeshProUGUI _assignedPoemName;

    [SerializeField]
    protected TextMeshProUGUI _assignedPlace;

    [SerializeField]
    
    protected TextMeshProUGUI _assignedPoemText;



    [SerializeField]
    protected TextMeshProUGUI _assignedDate1;

    [SerializeField]
    protected TextMeshProUGUI _assignedPoemName11;

    [SerializeField]
    protected TextMeshProUGUI _assignedPlace1;

    [SerializeField]

    protected TextMeshProUGUI _assignedPoemText1;


    private void OnEnable()
    {
        Book.CurrentPageChanged += CurrentPage;
    }

    private void OnDisable()
    {
        Book.CurrentPageChanged -= CurrentPage;
    }

    void CurrentPage(int page)
    {
        InitiatePoem(page-1);
        InitiatePoem1(page);
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

    void InitiatePoem(int poemIndex)
    {
      for (int i = 0; i < poemInfo._poemsList.Count; i++)
        {
            if (i == poemIndex)
            {
                _assignedDate.text = poemInfo._poemsList[i].poemDate;
                _assignedPoemName.text = poemInfo._poemsList[i].poemName;
                _assignedPlace.text = poemInfo._poemsList[i].poemPlace;
                _assignedPoemText.text = poemInfo._poemsList[i].poemText;
                break;
            }
        }
      
    }

    void InitiatePoem1(int poemIndex)
    {
        for (int i = 0; i < poemInfo._poemsList.Count; i++)
        {
            if (i == poemIndex)
            {
                _assignedDate.text = poemInfo._poemsList[i].poemDate;
                _assignedPoemName.text = poemInfo._poemsList[i].poemName;
                _assignedPlace.text = poemInfo._poemsList[i].poemPlace;
                _assignedPoemText.text = poemInfo._poemsList[i].poemText;
                break;
            }
        }

    }

    private void OnApplicationQuit()
    {
        poemInfo._poemsList.Clear();
    }
}
