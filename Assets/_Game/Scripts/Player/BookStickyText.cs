using UnityEngine;
using System.Collections.Generic;

public class BookStickyText : MonoBehaviour
{
    [SerializeField]
    protected GameObject _currentRightPageObj;

    [SerializeField]
    protected GameObject _currentLeftPageObj;

    [SerializeField]
    protected GameObject _holdRightPageObj;

    [SerializeField]
    protected GameObject _holdLeftPageObj;


    [SerializeField]
    protected List<GameObject> _leftPageGameobjectText;
    [SerializeField]
    protected GameObject _rightPageGameobjectText;


    protected Dictionary<string,RectTransform> _pageTextRects;


   public void HoldTempRightStick()
    {
        _currentRightPageObj.transform.SetParent(null,true);

        _holdRightPageObj.transform.SetParent(_rightPageGameobjectText.transform, true);
    }

   public void UndoTempRightStick()
    {
        _holdRightPageObj.transform.SetParent(null, true);

        _currentRightPageObj.transform.SetParent(_rightPageGameobjectText.transform, true);
    }

}
