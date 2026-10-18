public class AutumnChange : ChangeBehaviour
{
    public override void OnChange()
    {
        AutumnTree.TurnUnseen();
        _changed = true;
    }
}
