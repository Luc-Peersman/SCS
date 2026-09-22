# SCS — Show Control System

## Wat is dit project

Geen game, maar een **Unity-applicatie die fysieke hardware aanstuurt en test**: een netwerk
van ESP32-gebaseerde "Nodes" (servo's, stopcontacten/relais, IR-zenders, digitale pinnen,
knoppen), verbonden via een seriële link met een centrale "Nexus"-microcontroller.

Gezien de aanwezige assets (`Horror Elements/`, `Art By Kandles/Halloween Icons/`, een "Save Room")
is dit vermoedelijk bedoeld voor een horror/escape-room-achtige installatie (licht, geluid,
servo's en stopcontacten op cue aangestuurd).

Het project is tegelijk **lesmateriaal**: de scripts bevatten uitgebreide Nederlandstalige
onderwijscommentaren ("HOE GEBRUIK JE HET (3 stappen)", "Studenten zien dit niet"), bedoeld als
sjabloon waarop studenten voortbouwen. Er is geen apart README — de code-comments zijn de facto
de documentatie.

## Functionaliteiten

- **Serieel protocol** (115200 baud) met de Nexus/ESP32-nodes: commando's `SRV`, `SOCKET`,
  `PIN`, `PINMODE`, `IRSEND`, `IDENT`, `PING`, `STAT`; events `ONLINE/OFFLINE/HELLO/ACK/NOACK/
  BTN/PIN/IR/STAT`. Nodes zijn adresseerbaar 1–63, of broadcast via `0xFF`.
- **Cue/tijdlijn-systeem** (`ShowControl.Cue.ZetServo(tijd, node, hoek)`, `SocketAan`,
  `SpeelGeluid`, ...) om een hele show getimed af te spelen.
- **Multichannel audio-routing buiten Unity's eigen audio-engine om**: afspelen per ASIO-kanaal
  (bv. Focusrite Scarlett uit 3/4), met kanaalselectie (`Kant`: LINKS/RECHTS/BEIDE) en
  automatische resampling wanneer de samplerate van de clip niet overeenkomt met de ASIO-
  samplerate. Is er geen ASIO-interface aangesloten (bv. een student op eigen laptop), dan doet
  `SpeelGeluid`/`SpeelGeluidOpUitgangen` gewoon niets — geen foutmelding, alleen een `Debug.Log`.
- **Lokaal testen zonder Scarlett**: `Cue.SpeelGeluidLokaal(tijd, naam, kant)` speelt af via een
  gewone Unity `AudioSource` op het ShowControl-GameObject (2D, `spatialBlend = 0`, dus niet
  afhankelijk van scene-positie). Let op: `Kant` betekent hier iets **anders** dan bij
  `SpeelGeluid`/`SpeelGeluidOpUitgangen` (die blijven ongewijzigd, kanaalextractie voor ASIO):
  - `BEIDE` → gewone stereoweergave, ongewijzigd (links → linkerbox, rechts → rechterbox).
  - `LINKS`/`RECHTS` → het VOLLEDIGE geluid (links- en rechterkanaal samengevoegd) alleen uit die
    ene box; de andere box blijft stil. Wordt per aanroep als verse stereo-`AudioClip` opgebouwd
    (niet via `AudioSource.panStereo`), zodat overlappende geluiden met verschillende `Kant`
    elkaars panning niet beïnvloeden.
- **Testdashboard** (`ShowControlTester`, OnGUI): live online/offline-status per node, losse
  knoppen (servo/socket/pin/IR/ping/stat/ident) en een geautomatiseerde zelftest met
  PASS/FAIL-rapportage.
- Logging (`ShowLogger`) en scene-brede node-administratie (`ShowManager`).
- Kleine visuele feedback in de scene (`KubusDraaier`) die node-acties zichtbaar maakt.

## Technische aanpak

- **Unity 6000.3.20f1**, Universal Render Pipeline (URP), 3D-project.
- Packages: Input System, AI Navigation, Timeline, Visual Scripting, TextMeshPro/UGUI.
- 3rd-party: **NAudio** (`Assets/Plugins/NAudio`) voor ASIO-audio en resampling. Er staat ook
  een losse `FMODProject`-map naast het project — lijkt (nog) niet actief gebruikt naast de
  eigen ASIO-router.
- **Architectuur**: één centrale `SerialController` (enige MonoBehaviour die de COM-poort
  beheert; achtergrond-leesthread + concurrent queue; events op de main thread) met daarbovenop
  ontkoppelde laag-scripts die via C# events (pub/sub) communiceren:
  - `ShowNode` — per-node component, acties/events instelbaar via UnityEvents in de Inspector
  - `ShowManager` — overzicht/ping van alle nodes
  - `ShowControl` — statische `Cue`-API voor tijdlijn/cue-based scripting
  - `ShowList` — datalijst van geluiden + voorbeeld-tijdlijn
  - `ShowLogger` / `ShowControlTester` — diagnostiek en QA
- Let op: `Esp32Serial2.cs` (klasse `EspSerial2`) is **dode/ongebruikte code**.
  Geverifieerd: alleen `SerialController` staat als component in `SCS.unity` (op COM6,
  115200 baud) en wordt aangeroepen door `ShowManager`, `ShowNode`, `ShowControl`,
  `ShowControlTester` en `ShowLogger`. `EspSerial2` komt nergens voor in scenes, prefabs, of
  andere scripts — een geïsoleerd prototype dat nooit is ingebouwd in de showpijplijn. Kan
  vermoedelijk verwijderd/gearchiveerd worden.
- `_Recovery/0.unity` is een Unity auto-recovery bestand, geen bewuste scene.

## Let op: mapstructuur is niet wat de naam doet vermoeden

Bijna alle scripts, de scene, en de art-assets zitten niet direct onder `Assets/Scripts` of
`Assets/Scenes`, maar onder **`Assets/SCS-Engine [Afblijven]/`**. Ondanks de naam ("afblijven" =
handen af) is dit **geen oude back-up** — het is de actieve, huidige projectmap (bevat de meest
recente versie van `ShowControl.cs` met o.a. `SpeelGeluidOpUitgangen`, en de enige echte scene).
Alleen `ShowList.cs` staat los in `Assets/Scripts/` (nieuwste editie daarvan, met een
`SpeelGeluidOpUitgangen`-test op uitgangen 3/4).

Dit is op 2026-09-21/22 per ongeluk verward met een wegwerp-backup en tijdelijk buiten `Assets/`
verplaatst — wat het project brak (missende scriptreferenties). Niet nog eens doen: verplaats of
verwijder niets onder `SCS-Engine [Afblijven]/` zonder eerst te checken of het de enige kopie is
van een script/scene (zie git-log rond commit `5ab078b` voor de details van dat incident).

## Belangrijkste bestanden

| Bestand | Rol |
|---|---|
| `Assets/SCS-Engine [Afblijven]/Scripts/SerialController.cs` | Kern: seriële verbinding + protocol-parsing, events |
| `Assets/SCS-Engine [Afblijven]/Scripts/ShowControl.cs` | Cue/tijdlijn-systeem + ASIO-audio-integratie |
| `Assets/SCS-Engine [Afblijven]/Scripts/ShowNode.cs` | Per-node component met Inspector-events |
| `Assets/SCS-Engine [Afblijven]/Scripts/ShowManager.cs` | Overzicht/ping van alle nodes |
| `Assets/Scripts/ShowList.cs` | Datalijst van geluiden + voorbeeld-tijdlijn (enige script buiten de Afblijven-map) |
| `Assets/SCS-Engine [Afblijven]/Scripts/ShowControlTester.cs` | OnGUI testdashboard + zelftest |
| `Assets/SCS-Engine [Afblijven]/Scripts/ShowLogger.cs` | Console-logging van alle node-events |
| `Assets/SCS-Engine [Afblijven]/Scripts/AsioUitgangRouter.cs` | NAudio/ASIO multichannel audio-routing |
| `Assets/SCS-Engine [Afblijven]/Scripts/AudioResampler.cs` | Resampling van audioclips naar ASIO-samplerate |
| `Assets/SCS-Engine [Afblijven]/Scripts/Esp32Serial2.cs` | Alternatieve/oudere seriële implementatie |
| `Assets/SCS-Engine [Afblijven]/Scenes/SCS.unity` | De enige "echte" scene |
| `Packages/manifest.json` | Package-dependencies |

## Git / GitHub

- Publiek gepubliceerd op **https://github.com/Luc-Peersman/SCS** (main branch).
- `Horror Elements/` en `Art By Kandles/` (onder `SCS-Engine [Afblijven]/`) zijn licentie-gevoelige
  asset-packs en staan sinds 2026-09-22 in `.gitignore` — ze worden niet meer meegecommit, maar
  zitten nog wel in oudere git-historie (bewust niet herschreven).
