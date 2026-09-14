# MeowxMeadow

A robust multiplayer integration and architectural refactor for **Push To Meow**, built specifically for **Rain Meadow**. 

---

### Key Features
* **SteamID Identity Resolution:** Replaces volatile local player indices with unique `SteamID64` keys to ensure collision-free state tracking across multiplayer lobbies.
* **Isolated State Architecture:** Custom player state manager decoupled from legacy data structures, guaranteeing stable input processing and network synchronization.
* **Host Settings Sync:** Automatic coroutine-based synchronization of lobby flags and Remix configuration settings between host and clients.
* **Dynamic Audio Injection System:** Layered runtime audio injector that registers inactive local meows and custom sound packs directly into `SoundLoader` (`allAudio`, `soundTriggers`, and `workingTriggers`) without triggering full game reloads, VRAM duplication, or frame drops.
* **Calibration & Pitch Parsing:** Native parsing of `sounds.txt` configuration files to seamlessly apply volume, pitch, and doppler parameters on injected audio samples.
* **Global Asset Registry:** Unified indexing system that maps both active and inactive local mods, creating a solid baseline for future real-time P2P asset transfers.

---

## ⚙️ Compilation Notes
Target framework: **.NET Framework 4.8**
Dependencies required for compilation:
* `BepInEx.dll`
* `com.rlabrecque.steamworks.net.dll`
* `HOOKS-Assembly-CSharp.dll`
* `ImprovedInput.dll`
* `MonoMod.RuntimeDetour.dll`
* `Newtonsoft.Json.dll`
* `PUBLIC-Assembly-CSharp.dll`
* `PushToMeowMod.dll`
* `RainMeadow.dll`
* `UnityEngine.dll`
* `UnityEngine.AudioModule.dll`
* `UnityEngine.CoreModule.dll`