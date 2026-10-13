using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class UngarettiLines
{
    public sealed class Piece
    {
        public readonly string italian;
        public readonly string english;
        public readonly string file;

        public Piece(string italian, string english, string file)
        {
            this.italian = italian;
            this.english = english;
            this.file = file;
        }
    }

    public sealed class Line
    {
        public readonly string id;
        public readonly Piece[] pieces;

        public Line(string id, params Piece[] pieces)
        {
            this.id = id;
            this.pieces = pieces;
        }

        public string Italian => string.Join(" ", pieces.Select(p => p.italian));
        public string English => string.Join(" ", pieces.Select(p => p.english));
    }

    private static Line L(string id, string italian, string english, string file) => new Line(id, new Piece(italian, english, file));

    public static readonly Line[] All =
    {
        L("Ungaretti - Milano - 01", "Milano. Insegno il francese ai ragazzi, e la sera il vento entra da questa finestra.", "Milan. I teach French to the boys, and in the evening the wind comes in through this window.", "UNG_L1_track1"),
        L("Ungaretti - Milano - 02", "Mi scusi, signor Carlo. Mi piace sentire l'aria della strada.", "Forgive me, Signor Carlo. I like to feel the air from the street.", "UNG_L1_track2"),
        L("Ungaretti - Milano - 03", "Mai. La scopa in una mano, il quaderno nell'altra.", "Never. The broom in one hand, the notebook in the other.", "UNG_L1_track3"),
        L("Ungaretti - Milano - 04", "Ancora niente. Le parole arrivano quando vogliono loro.", "Nothing yet. Words come when they please.", "UNG_L1_track4"),
        L("Ungaretti - Milano - 05", "La sedia... era al tavolo.", "The chair... it was at the table.", "UNG_L1_track5"),
        L("Ungaretti - Milano - 06", "C'è qualcosa, sotto. Parole... come scritte sul pavimento.", "There's something underneath. Words... as if written on the floor.", "UNG_L1_track6"),
        L("Ungaretti - Milano - 07", "Non ricordo di averle scritte. Eppure è la mia scrittura.", "I don't remember writing them. And yet it's my handwriting.", "UNG_L1_track8"),
        L("Ungaretti - Milano - 08", "Grazie, signor Carlo. È il complimento più bello che potesse farmi.", "Thank you, Signor Carlo. That's the finest compliment you could pay me.", "UNG_L1_track7"),
        L("Ungaretti - Milano - 09", "Sento la stanza cambiare alle mie spalle. Come una pagina che si volta da sola.", "I can feel the room changing behind me. Like a page turning by itself.", "UNG_L1_track9"),

        L("Ungaretti - Parigi - 01", "Rue des Carmes. Questa stanza la conosco. Ci abitavamo in due.", "Rue des Carmes. I know this room. Two of us lived here.", "UNG_L2_track1"),
        L("Ungaretti - Parigi - 02", "Solo per un momento, Guillaume. Volevo rivedere la stanza.", "Only for a moment, Guillaume. I wanted to see the room again.", "UNG_L2_track2"),
        L("Ungaretti - Parigi - 03", "Moammed. Era venuto da Alessandria con me. Qui si faceva chiamare Marcel.", "Moammed. He came from Alexandria with me. Here he called himself Marcel.", "UNG_L2_track3"),
        L("Ungaretti - Parigi - 04", "Le parole sono a pezzi. Come i ricordi di quei giorni.", "The words are in pieces. Like my memories of those days.", "UNG_L2_track4"),
        L("Ungaretti - Parigi - 05", "Il suo cappotto...", "His coat...", "UNG_L2_track5"),
        L("Ungaretti - Parigi - 06", "Leggeva fino a tardi, con la candela accesa. Ora lo scaffale è vuoto.", "He read late into the night, by candlelight. Now the shelf is empty.", "UNG_L2_track6"),
        L("Ungaretti - Parigi - 07", "Al cimitero c'eravamo solo io e la padrona dell'albergo.", "At the cemetery there was only me and the landlady.", "UNG_L2_track7"),
        L("Ungaretti - Parigi - 08", "Sono rimasto solo io a ricordarlo. Se non lo scrivo, nessuno lo saprà più.", "I'm the only one left who remembers him. If I don't write it down, no one ever will.", "UNG_L2_track8"),
        L("Ungaretti - Parigi - 09", "Se n'è andato quando ho smesso di guardare.", "He left when I stopped looking.", "UNG_L2_track9"),
        L("Ungaretti - Parigi - 10", "L'ho scritta tre anni dopo, in trincea. Anche là cadevano le foglie.", "I wrote it three years later, in the trenches. The leaves were falling there too.", "UNG_L2_track10"),

        L("Ungaretti - Carso - 01", "Cima Quattro. La notte prima di Natale.", "Cima Quattro. The night before Christmas.", "UNG_L3_track1"),
        L("Ungaretti - Carso - 02", "Questa è casa, adesso. Pietre, sacchi, una coperta.", "This is home now. Stones, sandbags, a blanket.", "UNG_L3_track2"),
        L("Ungaretti - Carso - 03", "No, amico. Sono solo sveglio.", "No, friend. I'm just awake.", "UNG_L3_track4"),
        L("Ungaretti - Carso - 04", "Parole. A volte le vedo tra le pietre, prima ancora di scriverle.", "Words. Sometimes I see them among the stones, before I've even written them.", "UNG_L3_track5"),
        new Line("Ungaretti - Carso - 05",
            new Piece("Accanto a me c'è un compagno che non si muove più.", "Beside me there's a comrade who no longer moves.", "UNG_L3_track6"),
            new Piece("L'ho sentito tutta la notte, anche senza guardarlo.", "I've felt him there all night, even without looking.", "UNG_L3_track7")),
        L("Ungaretti - Carso - 06", "Lo so. Resto io con lui fino a mattina.", "I know. I'll stay with him until morning.", "UNG_L3_track8"),
        L("Ungaretti - Carso - 07", "Per restare sveglio. Per restare vivo.", "To stay awake. To stay alive.", "UNG_L3_track9"),
        L("Ungaretti - Carso - 08", "La guerra è finita, un giorno. Le foglie hanno continuato a cadere. E io ho attraversato il mare.", "One day, the war ended. The leaves kept falling. And I crossed the sea.", "UNG_L3_track10"),

        L("Ungaretti - San Paolo - 01", "Il Brasile. Una natura enorme, che cresce anche quando non la guardi.", "Brazil. A vast nature, that keeps growing even when you aren't looking.", "UNG_L4_track1"),
        L("Ungaretti - San Paolo - 02", "Le foglie entrano dal giardino, Rui. Non smettono mai.", "The leaves come in from the garden, Rui. They never stop.", "UNG_L4_track2"),
        L("Ungaretti - San Paolo - 03", "Non tocco niente. Glielo prometto.", "I won't touch a thing. I promise.", "UNG_L4_track3"),
        L("Ungaretti - San Paolo - 04", "Nessuno, Rui. Le cose se ne vanno da sole, quando smettiamo di guardarle.", "No one, Rui. Things leave on their own, when we stop looking at them.", "UNG_L4_track4"),
        L("Ungaretti - San Paolo - 05", "Antonietto aveva nove anni.", "Antonietto was nine years old.", "UNG_L4_track5"),
        L("Ungaretti - San Paolo - 06", "Per te no, Rui. Per me c'è quello che non riesco a dire a nessuno. L'ho scritto per lui.", "Not for you, Rui. For me, there's what I can't say to anyone. I wrote it for him.", "UNG_L4_track6"),
        L("Ungaretti - San Paolo - 07", "Non so fare altro. Spazzo, trovo, scrivo.", "It's all I know how to do. I sweep, I find, I write.", "UNG_L4_track7"),
        L("Ungaretti - San Paolo - 08", "Torno. Porto il quaderno.", "I'm going back. I'll take the notebook.", "UNG_L4_track8"),
        L("Ungaretti - San Paolo - 09", "Non c'è più niente da spazzare qui. Solo da ricordare.", "There's nothing left to sweep here. Only to remember.", "UNG_L4_track9"),
        L("Ungaretti - San Paolo - ALT", "Se chiudo gli occhi, sono ancora là. In un bosco, in Francia.", "If I close my eyes, I'm still there. In a wood, in France.", "UNG_L4_track10"),

        L("Ungaretti - Roma - 01", "Roma. La città è occupata. Si esce poco, si parla piano.", "Rome. The city is occupied. We rarely go out, we speak softly.", "UNG_L5_track1"),
        L("Ungaretti - Roma - 02", "Non guardo, Leone. Spazzo.", "I'm not looking, Leone. I'm sweeping.", "UNG_L5_track2"),
        L("Ungaretti - Roma - 03", "Quando guardo, tacciono. Quando mi volto, ricominciano.", "When I look, they fall silent. When I turn away, they start again.", "UNG_L5_track3"),
        L("Ungaretti - Roma - 04", "Parole. Le vedo solo io. Sono per quelli che non possono più parlare.", "Words. Only I can see them. They're for those who can no longer speak.", "UNG_L5_track4"),
        L("Ungaretti - Roma - 05", "Ci penso sempre. Se chiudo gli occhi sono ancora là. In un bosco, in Francia.", "I think about it all the time. If I close my eyes I'm still there. In a wood, in France.", "UNG_L5_track5"),

        L("Ungaretti - Courton - 01", "Courton. Francia. L'ultima estate della guerra.", "Courton. France. The last summer of the war.", "UNG_L6_track1"),
        L("Ungaretti - Courton - 02", "Grazie. Lei non dovrebbe essere qui, di notte.", "Thank you. You shouldn't be out here at night.", "UNG_L6_track2"),
        L("Ungaretti - Courton - 03", "Cadono quando non le guardo. Come noi.", "They fall when I'm not looking. Like us.", "UNG_L6_track3"),
        L("Ungaretti - Courton - 04", "Ha ragione. Ci siamo tutti, qui. Basta voltarsi.", "He's right. We're all here. You only have to turn around.", "UNG_L6_track4"),
        L("Ungaretti - Courton - 05", "Sì. Era già qui. Io l'ho solo trovata, sotto le foglie.", "Yes. It was already here. I only found it, under the leaves.", "UNG_L6_track5"),
        L("Ungaretti - Courton - 06", "Le poesie erano già qui. Bisognava solo guardarle. Senza voltarsi.", "The poems were already here. You only had to look at them. Without turning away.", "UNG_L6_track6"),
    };

    public static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static string FileKey(string fileName)
    {
        int dash = fileName.IndexOf(" - ");
        return Clean(dash > 0 ? fileName.Substring(0, dash) : fileName);
    }

    public static Line Get(string id) => All.FirstOrDefault(l => l.id == id);

    public static Piece FindPiece(string text)
    {
        string key = Clean(text);
        if (key.Length == 0)
            return null;
        return All.SelectMany(l => l.pieces).FirstOrDefault(p => Clean(p.italian) == key || Clean(p.english) == key);
    }

    public static Line FindLine(string text)
    {
        string key = Clean(text);
        if (key.Length == 0)
            return null;
        return All.FirstOrDefault(l => Clean(l.Italian) == key || Clean(l.English) == key);
    }

    private static Dictionary<string, string> clipPaths;

    public static void Refresh() => clipPaths = null;

    public static AudioClip Clip(string file)
    {
        if (clipPaths == null)
        {
            clipPaths = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/_Trash"))
                    continue;
                string key = FileKey(Path.GetFileNameWithoutExtension(path));
                if (!clipPaths.ContainsKey(key))
                    clipPaths[key] = path;
            }
        }
        return clipPaths.TryGetValue(Clean(file), out string found) ? AssetDatabase.LoadAssetAtPath<AudioClip>(found) : null;
    }
}
