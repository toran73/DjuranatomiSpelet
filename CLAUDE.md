# CLAUDE.md – Djuranatomi Spelet

Kontext för Claude Code. Projektet skapades i en tidigare chatt med Claude. Den här filen sammanfattar beslut och läge så att arbetet kan fortsätta här.

**Svara på svenska.** Gränssnittet och all användartext är på svenska. Kod, identifierare och kommentarer är på engelska.

## Vad det är

Ett brädspel för undervisning i djuranatomi som visas på projektor och styrs av läraren. Det kommer att användas av lärare vid SLU (Uppsala). Spelarna går genom ett djurs kroppsdelar från START till MÅL och svarar på frågor om den kroppsdel de landar på.

- **Upphovsrätt:** © 2026 Torbjörn Nilsson och DSS Maja Blomqvist. Spelet är gjort på fritiden.
- **Licens:** EUPL-1.2 för både kod och innehåll (`LICENSE` innehåller den officiella texten från SPDX). Varje källfil har ett huvud med copyright och `SPDX-License-Identifier: EUPL-1.2`. Behåll det i nya filer.
- **AI-användning** redovisas på startsidan och i README.

## Spelregler (beslutade – ändra inte utan att fråga)

1. Start: läraren väljer djur, antal spelare (1–6) och svårighet (Lätt / Medel / Svår / Blandat).
2. Turvis: slå en tärning och flytta fram så många steg, men aldrig förbi MÅL (positionen stannar vid målet).
3. En fråga visas för kroppsdelen på rutan. Läraren klickar **Visa svar** och därefter **Rätt** eller **Fel**.
   - **Rätt:** spelaren står kvar.
   - **Fel:** spelaren går högst två steg tillbaka (bara ett steg om senaste slaget var ett steg).
4. **Inga poäng.** Den som först landar på MÅL och svarar rätt vinner.
   - Går tillbaka gör man högst `GameEngine.MaxStepsBack` (2) steg, räknat från rutan man landade på, och aldrig fler steg än man faktiskt flyttade den turen (slag 1 = 1 steg tillbaka; klipptes slaget av vid MÅL räknas bara de faktiska stegen).
   - **Tom ruta** (ljusgrå, ingen kroppsdel): ingen fråga, turen går direkt vidare. START får vara tom men MÅL måste ha en kroppsdel.
5. Frågor dras per kroppsdel och svårighet utan upprepning tills högen är slut. Saknas frågor på vald nivå används någon annan nivå. Saknas frågor helt får läraren hitta på en egen.
6. "Annan fråga" byter fråga. Rätt/Fel är bara aktiva när svaret har visats.

## Generellt djur (viktigt designbeslut)

Spelet är **inte** knutet till en hund. Läraren väljer valfri bild, skapar egna **kroppsdelar** (namn + färg) och klickar ut rutor i gångordning. Första rutan är START (ingen fråga), sista är MÅL. Rutor kan också vara tomma (`BoardSpace.Part == null`, `IsEmpty`). Frågor hör till en kroppsdel på ett visst djur. Rutornas koordinater sparas relativt bilden (0..1), så alla bildstorlekar fungerar. En exempelhund (`Resources/hund.png`, 7 kroppsdelar, 33 rutor, 69 svenska frågor) skapas i en tom databas.

## Arkitektur

```
DjuranatomiSpelet.Core/   net8.0, inga beroenden – all spellogik, testbar
  Models.cs               (Part på BoardSpace är nullable = tom ruta) Animal, BodyPart, BoardSpace, Question, Difficulty, IGameRepository, AnimalInfo, GameConfig
  GameEngine.cs           Tillståndsmaskin: WaitingForRoll → Moving → ShowingQuestion → ShowingAnswer → (GameOver). Landning på tom ruta: Arrive() går direkt till nästa spelare och sätter LandedOnEmpty.
  QuestionDeck.cs         Dragning per (kroppsdel, nivå) utan upprepning
  QuestionCsv.cs          Semikolon-CSV (svensk Excel): Kroppsdel;Svårighet;Fråga;Svar
  ExampleData.cs          Exempelhunden (inbäddad bild som resurs)
DjuranatomiSpelet/        Avalonia 11.2.3 – Windows, macOS, Linux
  App.axaml               FluentTheme (Light), DataGrid-stil, ControlTheme "BigButton"
  SetupWindow             Startsida: djur, spelare, svårighet, copyright/licens/AI-rader
  MainWindow              Spelet. Hela fönstret ligger i en Viewbox över en fast yta på 1920x1080 (fungerar på små skärmar)
  BoardEditorWindow       Bild, kroppsdelar, rutor (klick = lägg till efter markerad, dra = flytta, högerklick/Delete = ta bort). Högerklick på bilden lägger ut en tom ruta (högerklick på en ruta tar bort den), och kryssrutan "Lägg ut tomma rutor" och knappen "Gör rutan tom" skapar ljusgrå rutor.
  Assets/icon.ico         Programikon = spelare 1:s pjäs (röd #D62828, vit ring, vit "1"). Används som ApplicationIcon och Icon i alla fyra fönster. Ritad med Segoe UI Black (appen använder Inter).
  QuestionEditorWindow    DataGrid med mallkolumner + CSV-import/export
  Views/BoardView.cs      Bild + bana + rutor + pjäslager (delas av spel och redigerare)
  Views/Dialogs.cs        Egen MessageBox (Info / Confirm / AskSave / Guard)
  Views/QuestionRow.cs    Radvymodell för DataGrid (Avalonia saknar DataGridComboBoxColumn)
  Views/WindowFit.cs      Krymper dialoger till skärmens arbetsyta
  Data/SqliteGameRepository.cs  Microsoft.Data.Sqlite, en fil: djuranatomi.db (bilder som BLOB)
```

Databasschema: `Animals(Id, Name, Image BLOB, SpaceSize)`, `BodyParts(Id, AnimalId, Name, Color, SortOrder)`, `Spaces(AnimalId, Seq, X, Y, BodyPartId)` (BodyPartId NULL = tom ruta; äldre databaser med NOT NULL byggs om automatiskt vid start i `MigrateSpacesAllowNullPart`), `Questions(Id, BodyPartId, Difficulty, Text, Answer)`, med `ON DELETE CASCADE` och `PRAGMA foreign_keys = ON`. Databasfilen ligger bredvid programmet om mappen är skrivbar, annars i LocalApplicationData.

Kortkommandon i spelet: Mellanslag/T = slå, S = visa svar, R = rätt, F = fel, A = annan fråga, F11 = helskärm (Esc lämnar helskärm).

## Historik och läge

- Projektet började som WPF och konverterades till **Avalonia** för att också fungera på Mac.
- Avalonia-delen kompilerar nu (`dotnet build`: 0 fel, 3 varningar AVLN3001 om att fönstren saknar publik parameterlös konstruktor – påverkar bara förhandsgranskaren). Den är **ännu inte körd eller testad**: starta med `dotnet run` och gå igenom fönstren.
- Ändringar som är gjorda men inte provkörda: max 2 steg tillbaka vid fel, programikonen, tomma rutor (redigerare, spel och databasmigrering – ta backup av `djuranatomi.db` före första körningen).
- Core-projektet har kompilerats och testats (300 simulerade spel, CSV-rundresa med åäö, semikolon och citattecken, samt validering av exempeldata). Det finns ännu inget testprojekt i solutionen. Ett xUnit-projekt för Core vore ett bra tillägg.

## Testa särskilt

- Redigering i DataGrid: kombobox-cellerna (Kroppsdel, Svårighet) och "+ Ny fråga", som ska hoppa in i frågecellen för redigering.
- Avalonias Closing-mönster i redigerarna: avbryt → fråga "Spara / Spara inte / Avbryt" → stäng igen.
- Banredigeraren: klicka, dra och högerklicka genom Viewbox-skalningen, samt bildval via StorageProvider.
- Spelfönstret på en liten skärm och i helskärm. Pjäsernas skugga och skalning för aktuell spelare.
- Tomma rutor: lägg ut, gör tom, spela över dem, och att en gammal databas migreras utan att rutor försvinner.
- Fel svar nära START/MÅL: antal steg tillbaka (1 eller 2) och animationen.
- Mac: att `dotnet run` fungerar och att fildialogerna öppnas.

## Möjliga nästa steg (diskuterade, inte beslutade)

- Kopiera frågor mellan djur (gemensamma frågor som "Vad pumpar blodet?").
- `.app`-paket och signering för Mac (kräver `.icns`; `icon.ico` finns redan för Windows).
- Exempelfrågorna är AI-genererade och bör faktagranskas av lärarna innan de används.
