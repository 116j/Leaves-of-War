using System.Collections.Generic;

public static class PoemTranslations
{
    public readonly struct Entry
    {
        public readonly string Title;
        public readonly string Text;
        public readonly bool IsNote;

        public Entry(string title, string text, bool isNote = false)
        {
            Title = title;
            Text = text;
            IsNote = isNote;
        }
    }

    private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>
    {
        {
            1, new Entry("Agony",
                "To die like the thirsting larks\n" +
                "over the mirage\n" +
                "Or like the quail\n" +
                "once the sea is crossed\n" +
                "in the first bushes\n" +
                "because it no longer\n" +
                "wishes to fly\n" +
                "But not to live on lament\n" +
                "like a blinded goldfinch")
        },
        {
            2, new Entry("In Memory",
                "His name was\n" +
                "Moammed Sceab\n" +
                "Descendant\n" +
                "of emirs of nomads\n" +
                "a suicide\n" +
                "because he no longer had\n" +
                "a Homeland\n" +
                "He loved France\n" +
                "and changed his name\n" +
                "He was Marcel\n" +
                "but he was not French\n" +
                "and he no longer knew\n" +
                "how to live\n" +
                "in the tent of his people\n" +
                "where you listen to the chant\n" +
                "of the Koran\n" +
                "savouring a coffee\n" +
                "And he did not know\n" +
                "how to set free\n" +
                "the song\n" +
                "of his abandonment\n" +
                "I went with him\n" +
                "together with the landlady of the hotel\n" +
                "where we lived\n" +
                "in Paris\n" +
                "from number 5 rue des Carmes\n" +
                "a withered lane going downhill.\n" +
                "He rests\n" +
                "in the cemetery at Ivry\n" +
                "a suburb that always seems\n" +
                "to be\n" +
                "on the day\n" +
                "of a\n" +
                "decaying fair\n" +
                "And perhaps I alone\n" +
                "still know\n" +
                "that he lived")
        },
        {
            3, new Entry("Vigil",
                "A whole night long\n" +
                "thrown down beside\n" +
                "a comrade\n" +
                "butchered\n" +
                "with his mouth\n" +
                "snarling\n" +
                "turned to the full moon\n" +
                "with the congestion\n" +
                "of his hands\n" +
                "thrust\n" +
                "into my silence\n" +
                "I have written\n" +
                "letters full of love")
        },
        {
            4, new Entry("Day by Day",
                "Written in Brazil, after his son Antonietto died there at nine years old.\n\n" +
                "A father remembers the child's last days: the sparrows he scattered crumbs for, " +
                "the small hands he can now hold only in dreams. Life goes on around him, he talks, he works, " +
                "and every day the loss breaks him again; yet the child's shadow, shy at his side, " +
                "is the only light he has left.", true)
        },
        {
            5, new Entry("Shout No More",
                "Written in Rome after the occupation and the war.\n\n" +
                "The poet asks the living to stop the shouting and the hatred, to stop killing the dead a second time. " +
                "The dead can still be heard, but only in silence: their whisper is no louder " +
                "than the grass growing where no one walks.", true)
        },
        {
            6, new Entry("Soldiers",
                "We are like,\n" +
                "in autumn,\n" +
                "on the trees,\n" +
                "the leaves")
        },
    };

    public static bool TryGet(int poemId, out Entry entry) => Entries.TryGetValue(poemId, out entry);
}
