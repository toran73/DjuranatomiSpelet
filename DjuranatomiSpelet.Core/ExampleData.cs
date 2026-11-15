// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

namespace DjuranatomiSpelet.Core;

/// <summary>
/// A ready-made example animal (a dog) with body parts, a path and questions.
/// It is written into a new, empty database so the game can be played straight away.
/// </summary>
public static class ExampleData
{
    public static byte[] LoadDogImage()
    {
        using var stream = typeof(ExampleData).Assembly.GetManifestResourceStream("DjuranatomiSpelet.Core.hund.png")
            ?? throw new InvalidOperationException("Exempelbilden saknas i programmet.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>The example dog. Questions refer to body parts by name (ids are assigned when saved).</summary>
    public static (Animal Animal, IReadOnlyList<(string PartName, Question Question)> Questions) CreateDog()
    {
        var parts = new Dictionary<string, BodyPart>();
        BodyPart P(string name, string color) => parts[name] = new BodyPart { Name = name, Color = color };

        var svans = P("Svans", "#F4A261");
        var bakben = P("Bakben", "#2A9D8F");
        var mage = P("Mage", "#E9C46A");
        var brost = P("Bröstkorg", "#E76F51");
        var framben = P("Framben", "#8AB17D");
        var hals = P("Hals", "#9B5DE5");
        var huvud = P("Huvud", "#4EA8DE");

        // Coordinates in pixels of the 1600x900 example image; stored relative (0..1).
        var spaces = new List<BoardSpace>();
        void S(BodyPart part, params (double X, double Y)[] points)
        {
            foreach (var (x, y) in points)
                spaces.Add(new BoardSpace { X = x / 1600.0, Y = y / 900.0, Part = part });
        }

        S(svans, (215, 130));                                   // START
        S(svans, (265, 224), (317, 320), (382, 370));
        S(bakben, (520, 580), (520, 680), (530, 775), (630, 775), (640, 680), (640, 580));
        S(mage, (700, 500), (620, 440), (540, 380), (640, 345), (720, 400));
        S(brost, (790, 470), (830, 375), (920, 370), (900, 480));
        S(framben, (920, 590), (920, 690), (930, 780), (1030, 780), (1030, 690), (1030, 590));
        S(hals, (1080, 420), (1120, 340), (1170, 260));
        S(huvud, (1240, 200), (1290, 130), (1340, 200), (1400, 250));
        S(huvud, (1470, 225));                                  // MÅL (nosen)

        var animal = new Animal
        {
            Name = "Hund (exempel)",
            Image = LoadDogImage(),
            SpaceSize = 0.033,
            Parts = parts.Values.ToList(),
            Spaces = spaces
        };

        return (animal, BuildQuestions());
    }

    private static List<(string, Question)> BuildQuestions()
    {
        const Difficulty L = Difficulty.Easy, M = Difficulty.Medium, S = Difficulty.Hard;
        var list = new List<(string, Question)>();
        void Q(string part, Difficulty d, string text, string answer) =>
            list.Add((part, new Question { Difficulty = d, Text = text, Answer = answer }));

        Q("Svans", L, "Vad gör en hund ofta med svansen när den är glad?", "Viftar med den");
        Q("Svans", L, "Vad kan det betyda när en hund stoppar svansen mellan benen?", "Att den är rädd eller nervös");
        Q("Svans", L, "Finns det ben (skelett) inne i en hunds svans?", "Ja – små kotor");
        Q("Svans", L, "Nämn ett djur som kan hålla sig fast i grenar med svansen.", "En apa (t.ex. spindelapa), även pungråttor och kameleonter");
        Q("Svans", M, "Svansen är den sista delen av vilken del av skelettet?", "Ryggraden");
        Q("Svans", M, "Nämn en sak hunden använder svansen till förutom att visa känslor.", "Balans när den springer och svänger (och styrning när den simmar)");
        Q("Svans", M, "Vad kallas kotorna i svansen?", "Svanskotor");
        Q("Svans", S, "Ungefär hur många kotor finns i en hunds svans?", "Ungefär 6–23, beroende på ras");
        Q("Svans", S, "Vad kallas det när en ödla tappar svansen för att komma undan ett rovdjur?", "Autotomi");
        Q("Svans", S, "Forskning visar att hundar viftar mer åt ena hållet när de känner sig positiva. Vilket?", "Åt hundens högra sida");

        Q("Bakben", L, "Hur många ben har en hund?", "Fyra");
        Q("Bakben", L, "Vilka ben ger mest kraft när hunden hoppar – fram- eller bakbenen?", "Bakbenen");
        Q("Bakben", L, "Vad heter de mjuka dynorna under hundens fötter?", "Trampdynor");
        Q("Bakben", L, "Hur många tår når marken på varje baktass?", "Fyra");
        Q("Bakben", M, "Vad heter det stora lårbenet?", "Femur (lårbenet)");
        Q("Bakben", M, "Vilken led på bakbenet ser ut som ett bakvänt knä men är egentligen hälen/fotleden?", "Hasleden");
        Q("Bakben", M, "Hundar går på tårna, inte på hela foten. Vad kallas det?", "Tågångare");
        Q("Bakben", S, "Vilken sorts led fäster bakbenet i bäckenet?", "En kulled (höftleden)");
        Q("Bakben", S, "Vad kallas en extra tå som sitter högre upp på hundens ben?", "Sporre (vargklo)");
        Q("Bakben", S, "Vad heter knäskålen på latin?", "Patella");

        Q("Mage", L, "Vilket organ i magen tar emot maten när hunden har svalt den?", "Magsäcken");
        Q("Mage", L, "Vad dricker nyfödda valpar?", "Mammans mjölk");
        Q("Mage", L, "Har hundar en navel?", "Ja – ett litet platt ärr där navelsträngen satt");
        Q("Mage", L, "Vart tar maten vägen efter magsäcken?", "Till tunntarmen");
        Q("Mage", M, "Vilket stort organ renar blodet och gör galla som hjälper matsmältningen?", "Levern");
        Q("Mage", M, "Vilka organ filtrerar blodet och bildar urin?", "Njurarna");
        Q("Mage", M, "Vilken del av tarmen tar tillbaka det mesta av vattnet ur maten?", "Tjocktarmen");
        Q("Mage", S, "Vilket organ tillverkar insulin?", "Bukspottkörteln");
        Q("Mage", S, "Vad kallas det farliga tillståndet när en stor hunds magsäck fylls med gas och vrider sig?", "Magomvridning (GDV)");
        Q("Mage", S, "En kos mage har flera avdelningar. Hur många?", "Fyra");

        Q("Bröstkorg", L, "Vilket organ pumpar runt blodet i kroppen?", "Hjärtat");
        Q("Bröstkorg", L, "Vilka organ andas hunden med?", "Lungorna");
        Q("Bröstkorg", L, "Vilka ben skyddar hjärtat och lungorna som en bur?", "Revbenen (bröstkorgen)");
        Q("Bröstkorg", L, "Vilken gas tar lungorna upp från luften?", "Syre");
        Q("Bröstkorg", M, "Hur många rum (kamrar och förmak) har en hunds hjärta?", "Fyra");
        Q("Bröstkorg", M, "Vad heter muskeln under lungorna som hjälper hunden att andas?", "Mellangärdet (diafragman)");
        Q("Bröstkorg", M, "Varför flämtar hundar?", "För att kyla ner sig – de kan nästan inte svettas");
        Q("Bröstkorg", S, "Hur många par revben har en hund oftast?", "13 par");
        Q("Bröstkorg", S, "Vilka blodkärl för blodet bort från hjärtat?", "Artärerna (pulsådrorna)");
        Q("Bröstkorg", S, "Vilken gas gör lungorna sig av med när hunden andas ut?", "Koldioxid");

        Q("Framben", L, "Vad kallas en hunds fot?", "Tass");
        Q("Framben", L, "Vad heter de hårda, spetsiga sakerna längst ut på hundens tår?", "Klor");
        Q("Framben", L, "Vilka tassar använder hunden mest när den gräver?", "Framtassarna");
        Q("Framben", L, "Hur många tår når marken på varje framtass?", "Fyra (plus en sporre högre upp)");
        Q("Framben", M, "Bär hunden mest av sin vikt på frambenen eller bakbenen?", "Frambenen (ungefär 60 %)");
        Q("Framben", M, "Vad heter skulderbladet på latin?", "Scapula");
        Q("Framben", M, "Vad heter 'handleden' på hundens framben?", "Framknäet (carpus)");
        Q("Framben", S, "Vad heter överarmsbenet?", "Humerus (överarmsbenet)");
        Q("Framben", S, "Vad heter de två benen i underarmen/nedre frambenet?", "Strålbenet och armbågsbenet (radius och ulna)");
        Q("Framben", S, "Vilket ben förbinder vår axel med bröstbenet, men saknas eller är pyttelitet hos hundar?", "Nyckelbenet");

        Q("Hals", L, "Vilken kroppsdel sitter mellan huvudet och kroppen?", "Halsen");
        Q("Hals", L, "Vad sätter vi runt hundens hals för att fästa kopplet?", "Ett halsband");
        Q("Hals", L, "Nämn ett ljud som hunden gör med rösten.", "Skäller (eller morrar, gnäller, ylar)");
        Q("Hals", M, "Genom vilket rör färdas maten ner genom halsen till magsäcken?", "Matstrupen");
        Q("Hals", M, "Genom vilket rör färdas luften ner genom halsen till lungorna?", "Luftstrupen");
        Q("Hals", M, "Vad heter struphuvudet på latin?", "Larynx");
        Q("Hals", S, "Hur många halskotor har de flesta däggdjur – både hundar och giraffer?", "Sju");
        Q("Hals", S, "Vilken körtel i halsen styr hur snabbt kroppen gör av med energi?", "Sköldkörteln");
        Q("Hals", S, "Vilka stora blodkärl i halsen för blod till hjärnan?", "Halspulsådrorna");

        Q("Huvud", L, "Vilket är hundens starkaste sinne?", "Luktsinnet");
        Q("Huvud", L, "Vad heter de långa, styva håren på hundens nos?", "Morrhår");
        Q("Huvud", L, "Vad kallas valpens första tänder?", "Mjölktänder");
        Q("Huvud", L, "Vilket organ inne i huvudet styr hela kroppen?", "Hjärnan");
        Q("Huvud", M, "Hur många tänder har en vuxen hund?", "42");
        Q("Huvud", M, "Hur många tänder har en valp?", "28");
        Q("Huvud", M, "Vad heter de fyra långa, spetsiga tänderna som används för att hålla fast?", "Hörntänderna");
        Q("Huvud", S, "Vilka färger ser hundar bäst?", "Blått och gult (de är rödgröna färgblinda)");
        Q("Huvud", S, "Människor har ungefär 5–6 miljoner luktreceptorer. Ungefär hur många har en hund?", "Upp till ungefär 300 miljoner");
        Q("Huvud", S, "Vad heter organet i gommen som hjälper hunden att 'smaka' på dofter?", "Jacobsons organ (vomeronasalorganet)");

        return list;
    }
}
