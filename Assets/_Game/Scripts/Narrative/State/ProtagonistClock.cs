namespace Hortensia.Narrative
{
    // The body never ages; the garden spends the difference.
    public sealed class ProtagonistClock
    {
        public const int BodyAgeYears = 19;

        public ProtagonistClock(int chronologicalAge)
        {
            ChronologicalAge = chronologicalAge;
        }

        public int ChronologicalAge { get; }
        public int BodyAge => BodyAgeYears;
        public int YearsOwed => ChronologicalAge - BodyAge;
    }
}
