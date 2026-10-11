using UnityEngine;

public class AssignPoem : RevealableObject
{
   [SerializeField]
    private int _poemId;

    [SerializeField]
    protected string _assignedDate;

    [SerializeField]
    protected string _assignedPoemName;

    [SerializeField]
    protected string _assignedPlace;

    [SerializeField]
    [TextArea(3, 10)]
    protected string _assignedPoemText;

   public PoemInfo poemInfo;
    protected override void OnRevealChanged(bool revealed)
    {
        if (revealed)
        {
            Debug.Log("Poem revealed!");
            AddPoemInfo();
        }
        else
        {
            Debug.Log("Poem hidden!");
        }
    }
    private void Awake()
    {
        if (poemInfo == null)
        {
            Debug.LogError("PoemInfo is not assigned in AssignPoem script on " + gameObject.name);
            poemInfo._poemsList = new System.Collections.Generic.List<PoemInfoContainer>();
            return;
        }

        poemInfo._poemsList.Add(new PoemInfoContainer
        {
            poemId = _poemId,
            poemName = _assignedPoemName,
            poemText = _assignedPoemText,
            poemDate = _assignedDate,
            poemPlace = _assignedPlace


        });
    }


    void AddPoemInfo()
    {
        if (poemInfo == null)
        {
            Debug.LogError("PoemInfo is not assigned in AssignPoem script on " + gameObject.name);
            poemInfo._poemsList = new System.Collections.Generic.List<PoemInfoContainer>();
            return;
        }

        poemInfo._poemsList.Add(new PoemInfoContainer
        {
            poemId = _poemId,
            poemName = _assignedPoemName,
            poemText = _assignedPoemText,
            poemDate = _assignedDate,
            poemPlace = _assignedPlace


        });
    }


    void RemovePoemInfo()
    {
        poemInfo._poemsList.Remove(new PoemInfoContainer
        {
            poemId = _poemId,
            poemName = _assignedPoemName,
            poemText = _assignedPoemText,
            poemDate = _assignedDate,
            poemPlace = _assignedPlace


        });
    }



}
