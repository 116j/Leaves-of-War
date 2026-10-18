public class RevealChange : ChangeBehaviour
{
    public override void Arm()
    {
        base.Arm();
        foreach (PeripheralFigure figure in GetComponentsInChildren<PeripheralFigure>(true))
            figure.Reveal();
    }

    public override void OnChange() => _changed = true;
}
