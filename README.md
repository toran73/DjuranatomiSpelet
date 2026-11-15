# Djuranatomi Spelet

Ett brädspel för klassrummet som visas på projektor och styrs av läraren. Spelarna går genom ett djurs kroppsdelar, från START till MÅL, och svarar på frågor om den kroppsdel de landar på.

Bilden är valfri. Läraren väljer en egen bild, klickar ut rutorna i den ordning spelarna ska gå och grupperar dem i kroppsdelar som hon själv namnger. En exempelhund följer med, så att det går att spela direkt.

## Regler

1. Läraren väljer djur, antal spelare (1–6) och svårighetsgrad (Lätt / Medel / Svår / Blandat).
2. Spelarna turas om. Läraren slår tärningen, och spelaren flyttar fram så många steg (aldrig förbi målet).
3. En fråga visas för den kroppsdel spelaren landat på. Läraren klickar **Visa svar** och sedan **Rätt** eller **Fel**.
   - **Rätt:** spelaren står kvar på den nya rutan.
   - **Fel:** spelaren går högst två steg tillbaka (bara ett steg om senaste slaget var ett steg).
4. Den som först landar på MÅL och svarar rätt vinner. Det finns inga poäng.

Om en kroppsdel saknar frågor på vald nivå tas en fråga från någon annan nivå. En fråga kommer inte tillbaka förrän alla frågor för den kroppsdelen och nivån har använts.

## Kortkommandon i spelet

| Tangent | Funktion |
|---|---|
| Mellanslag / T | Slå tärningen |
| S | Visa svar |
| R | Rätt |
| F | Fel |
| A | Annan fråga (om frågan inte passar) |
| F11 / Esc | Helskärm av/på |

## Skapa ett eget djur

Klicka på **+ Nytt djur…** på startskärmen:

1. **Välj bild…**: PNG eller JPG, vilken storlek som helst.
2. **+ Ny kroppsdel**: namnge den (t.ex. "Huvud"). Varje kroppsdel får en egen färg, och **Byt färg** byter den.
3. Markera en kroppsdel och klicka på bilden i den ordning spelarna ska gå. Första rutan blir START och sista blir MÅL.
4. Klicka på en ruta för att markera den. Nya rutor läggs då in *efter* den rutan. Dra en ruta för att flytta den. Högerklick eller Delete tar bort en ruta, och Esc avmarkerar.
5. Justera rutornas storlek med reglaget och klicka på **Spara**.

Sedan lägger du till frågor med **Redigera frågor…**.

## Frågor

- Varje fråga hör till en kroppsdel på ett djur och har en svårighetsgrad och ett svar (text).
- **Importera/Exportera CSV**: semikolonseparerad fil som öppnas direkt i svenska Excel:

  ```
  Kroppsdel;Svårighet;Fråga;Svar
  Huvud;Lätt;Vilket är hundens starkaste sinne?;Luktsinnet
  Mage;Medel;Vilka organ bildar urin?;Njurarna
  ```

  Svårighet kan anges som Lätt/Medel/Svår, Easy/Medium/Hard eller 1/2/3. Kroppsdelens namn måste finnas på djuret.

## Bygga och köra

Gränssnittet är byggt med [Avalonia](https://avaloniaui.net) och fungerar på **Windows, macOS och Linux**. Det kräver [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Använd Visual Studio 2022, JetBrains Rider eller VS Code med C# Dev Kit.

```
dotnet run --project DjuranatomiSpelet
```

Gör så här för att få ett fristående program (ingen .NET-installation behövs på datorn där det körs):

```
# Windows – en enda .exe
dotnet publish DjuranatomiSpelet -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/win

# Mac med Apple Silicon (M1/M2/M3…)
dotnet publish DjuranatomiSpelet -c Release -r osx-arm64 --self-contained -o publish/mac

# Mac med Intel
dotnet publish DjuranatomiSpelet -c Release -r osx-x64 --self-contained -o publish/mac-intel
```

På Mac startar du `publish/mac/DjuranatomiSpelet`. Om macOS säger att programmet är från en "oidentifierad utvecklare": högerklicka → Öppna första gången. Ett riktigt `.app`-paket med ikon kräver ett extra paketeringssteg (t.ex. NuGet-paketet `Dotnet.Bundle`) och signering.

Kortkommandon på Mac: samma tangenter. F11 kan vara upptagen av macOS. Använd då fn+F11, eller ändra under Systeminställningar → Tangentbord.

## Databasen

Allt sparas i **en** SQLite-fil, `djuranatomi.db`, med bilderna inbakade. Filen ligger bredvid programmet om mappen är skrivbar, annars i `%LocalAppData%\DjuranatomiSpelet`. Sökvägen visas längst ner på startskärmen. Kopiera filen för att flytta eller dela djur och frågor med en kollega.

Om alla djur tas bort skapas exempelhunden igen vid nästa start.

## Projektstruktur

```
DjuranatomiSpelet.Core/    Spelregler, modeller, frågekortlek, CSV, exempeldata (inga beroenden)
  GameEngine.cs            Turordning: slå → flytta → fråga → visa svar → rätt/fel
  QuestionDeck.cs          Frågor per kroppsdel/nivå utan upprepning
  Models.cs                Animal, BodyPart, BoardSpace, Question, IGameRepository
  QuestionCsv.cs           Import/export
  ExampleData.cs           Exempelhunden (bild i Resources/hund.png)
DjuranatomiSpelet/         Avalonia-gränssnittet (Windows, macOS, Linux)
  SetupWindow              Startskärm (djur, spelare, svårighet)
  MainWindow               Själva spelet
  BoardEditorWindow        Bild, kroppsdelar och rutor
  QuestionEditorWindow     Frågor
  Views/BoardView.cs       Bilden + rutorna (delas av spel och redigerare)
  Views/Dialogs.cs         Meddelande- och ja/nej-dialoger
  Views/QuestionRow.cs     Rad i frågetabellen
  Data/SqliteGameRepository.cs
```

## Upphovsrätt och licens

Copyright © 2026 Torbjörn Nilsson och DSS Maja Blomqvist.

Djuranatomi Spelet är fri programvara. Det licensieras under **European Union Public Licence v. 1.2 (EUPL-1.2)**, se filen [`LICENSE`](LICENSE). Licensen gäller både programkoden och det medföljande innehållet (exempelbilden och exempelfrågorna).

Det innebär i korthet att:

- alla får använda, kopiera, ändra och sprida programmet, även i undervisning och kommersiellt
- den som sprider en ändrad version måste göra källkoden tillgänglig under samma (eller en förenlig) licens
- programmet tillhandahålls i befintligt skick, utan garantier

EUPL finns i officiella versioner på alla EU-språk, däribland svenska, och alla versioner är lika giltiga: https://joinup.ec.europa.eu/collection/eupl/eupl-text-eupl-12

Frågor och bilder som användare själva lägger in i programmet tillhör respektive upphovsperson. Kontrollera att du har rätt att använda bilder innan du delar en databasfil med andra.

Komponenter från tredje part och deras licenser finns i [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

### AI-användning

Programmet är utvecklat med hjälp av AI-assistenten Claude (Anthropic). Idé, spelregler, krav och design kommer från upphovspersonerna, som också har granskat och testat resultatet. Exempelfrågorna är AI-genererade och bör faktagranskas innan de används i undervisning.
