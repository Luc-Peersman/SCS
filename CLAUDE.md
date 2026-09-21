# SCS — Show Control System

## Wat is dit project

Geen game, maar een **Unity-applicatie die fysieke hardware aanstuurt en test**: een netwerk
van ESP32-gebaseerde "Nodes" (servo's, stopcontacten/relais, IR-zenders, digitale pinnen,
knoppen), verbonden via een seriële link met een centrale "Nexus"-microcontroller.

Gezien de aanwezige assets (`Assets/Horror Elements/`, `Assets/Art By Kandles/Halloween Icons/`,
een "Save Room") is dit vermoedelijk bedoeld voor een horror/escape-room-achtige installatie
(licht, geluid, servo's en stopcontacten op cue aangestuurd).

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
  samplerate.
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
- Let op: `Assets/Scripts/Esp32Serial2.cs` (klasse `EspSerial2`) is **dode/ongebruikte code**.
  Geverifieerd: alleen `SerialController` staat als component in `SCS.unity` (op COM6,
  115200 baud) en wordt aangeroepen door `ShowManager`, `ShowNode`, `ShowControl`,
  `ShowControlTester` en `ShowLogger`. `EspSerial2` komt nergens voor in scenes, prefabs, of
  andere scripts — een geïsoleerd prototype dat nooit is ingebouwd in de showpijplijn. Kan
  vermoedelijk verwijderd/gearchiveerd worden.
- `Assets/Scenes/_Recovery/0.unity` is een Unity auto-recovery bestand, geen bewuste scene.

## Belangrijkste bestanden

| Bestand | Rol |
|---|---|
| `Assets/Scripts/SerialController.cs` | Kern: seriële verbinding + protocol-parsing, events |
| `Assets/Scripts/ShowControl.cs` | Cue/tijdlijn-systeem + ASIO-audio-integratie |
| `Assets/Scripts/ShowNode.cs` | Per-node component met Inspector-events |
| `Assets/Scripts/ShowManager.cs` | Overzicht/ping van alle nodes |
| `Assets/Scripts/ShowList.cs` | Datalijst van geluiden + voorbeeld-tijdlijn |
| `Assets/Scripts/ShowControlTester.cs` | OnGUI testdashboard + zelftest |
| `Assets/Scripts/ShowLogger.cs` | Console-logging van alle node-events |
| `Assets/Scripts/AsioUitgangRouter.cs` | NAudio/ASIO multichannel audio-routing |
| `Assets/Scripts/AudioResampler.cs` | Resampling van audioclips naar ASIO-samplerate |
| `Assets/Scripts/Esp32Serial2.cs` | Alternatieve/oudere seriële implementatie |
| `Assets/Scenes/SCS.unity` | De enige "echte" scene |
| `Packages/manifest.json` | Package-dependencies |
