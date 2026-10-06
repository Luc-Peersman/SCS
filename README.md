# SCS — Show Control System

Unity-applicatie die fysieke hardware aanstuurt en test: een netwerk van ESP32-"Nodes" (servo's,
stopcontacten/relais, IR, digitale pinnen, knoppen), verbonden via een seriële link met een centrale
"Nexus"-microcontroller. Met een cue-systeem speel je een hele show getimed af (licht, geluid, servo's).

## Openen

- Unity **6000.3.20f1** (via Unity Hub: *Add project from disk*).
- De scene staat in `Assets/SCS-Engine [Afblijven]/Scenes/SCS.unity`.
- Alles onder `SCS-Engine [Afblijven]/` is de motor: niet aanpassen of verplaatsen. Je eigen werk komt
  daarbuiten. Uitleg staat in de commentaren van de scripts ("HOE GEBRUIK JE HET").

## Zelf importeren (gratis, Unity Asset Store)

Deze packs staan bewust niet in de repo (de Asset Store-licentie staat niet toe dat ze openbaar
worden verspreid). Haal ze zelf gratis op via de Asset Store (Window → Package Manager → My Assets)
als je ze wilt gebruiken:

- [Horror Elements](https://assetstore.unity.com/packages/audio/sound-fx/horror-elements-112021) (geluiden)
- [Halloween Icons](https://assetstore.unity.com/packages/2d/gui/icons/halloween-icons-156667) (iconen)
- [Hero Fantasy Pack Vol 1](https://assetstore.unity.com/packages/audio/music/orchestral/hero-fantasy-pack-vol-1-109110) (muziek)

Het project opent en werkt ook zonder deze packs. Zonder **Halloween Icons** zie je op 5 plekken een wit
vlakje in plaats van een icoon (kaars, ketel, graf, pompoen, heksenhoed); na het importeren vindt Unity ze
vanzelf terug.
