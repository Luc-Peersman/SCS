# SCS — Show Control System

Unity-applicatie die fysieke hardware aanstuurt en test: een netwerk van ESP32-"Nodes" (servo's,
stopcontacten/relais, IR, digitale pinnen, knoppen), verbonden via een seriële link met een centrale
"Nexus"-microcontroller. Met een cue-systeem speel je een hele show getimed af (licht, geluid, servo's).

## Openen

1. Download de repo als ZIP (groene knop **Code → Download ZIP**) en pak hem uit.
2. Open de map in Unity **6000.3.20f1** (Unity Hub: *Add project from disk*).
3. De scene staat in `Assets/SCS-Engine [Afblijven]/Scenes/SCS.unity`.

Er hoeft niets extra's geïmporteerd te worden.

- Alles onder `SCS-Engine [Afblijven]/` is de motor: niet aanpassen of verplaatsen. Je eigen werk komt
  daarbuiten. Uitleg staat in de commentaren van de scripts ("HOE GEBRUIK JE HET").
- Geluiden en plaatjes uit de Unity Asset Store zitten bewust niet in dit project (de licentie staat
  openbaar verspreiden niet toe). Waar de show een Node-plaatje zou tonen, verschijnt een melding in de
  **Console**. Eigen geluiden voeg je toe via de lijst *Geluiden* op het object met `ShowList`.
