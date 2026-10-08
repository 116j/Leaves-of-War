using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PoemInfo", menuName = "Scriptable Objects/PoemInfo")]
public class PoemInfo : ScriptableObject
{
    public List<PoemInfoContainer> _poemsList;

}

[System.Serializable]
public class PoemInfoContainer
{
    public string poemName;
    public string poemDate;

    public string poemPlace;
    public string poemText;

   
}
