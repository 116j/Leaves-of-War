using UnityEngine;

public class AssignPoem : MonoBehaviour
{
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
            poemName = _assignedPoemName,
            poemText = _assignedPoemText,
            poemDate = _assignedDate,
            poemPlace = _assignedPlace
           

        });
    }

   




}
