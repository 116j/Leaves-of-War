public class VanishChange : ChangeBehaviour
{
    public override void OnChange()
    {
        gameObject.SetActive(false);
    }
}
