namespace Hortensia.Narrative
{
    public static class NameVariants
    {
        public const string Token = "{name}";

        // The enum names are the authored display names. Keeping this mapping
        // here prevents dialogue, documents, and tooling from drifting apart.
        public static string FirstName(NameVariant variant) => variant.ToString();

        public static string Substitute(string text, NameVariant variant)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text.Replace(Token, FirstName(variant));
        }

        public static bool ContainsToken(string text) =>
            !string.IsNullOrEmpty(text) && text.Contains(Token);

        /// <summary>
        /// The name to show for a speaker in dialogue/subtitles. For the
        /// protagonist, whose SpeakerDefinition.DisplayName holds only the
        /// surname ("Hortensia"), this prepends the chosen first name
        /// ("Laura"/"Everie"). Every other speaker's display name is unchanged.
        /// </summary>
        public static string SpeakerDisplayName(SpeakerDefinition speaker, NameVariant variant)
        {
            if (speaker == null)
                return string.Empty;

            return speaker.IsProtagonist
                ? $"{FirstName(variant)} {speaker.DisplayName}"
                : speaker.DisplayName;
        }
    }
}