public class VoicesRiseChange : ChangeBehaviour
{
    public override void Arm()
    {
        base.Arm();
        Hortensia.Runtime.StreetVoices.Begin();
    }

    public override void OnChange() => _changed = true;
}
