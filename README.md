# MeowxMeadow

A high-performance **BepInEx code mod** for *Rain World* that bridges **Push To Meow** custom sound packs and slugcat vocalizations with **Rain Meadow** multiplayer.

Instead of binding custom audio profiles to volatile local player indices (which causes audio cross-talk), **MeowxMeadow** introduces a fully decoupled **SteamID-based Audio Identity System**, a zero-reload **Dynamic Audio Injection Engine**, and an upcoming **Volatile P2P Audio Streaming Protocol**.

---

### Key Features & Technical Highlights

* **SteamID Identity Resolution (`PlayerMeowProfileManager`):** Replaces volatile local player indices with unique `SteamID64` keys to ensure collision-free state tracking across multiplayer lobbies.
* **Isolated State Architecture (`Plugin` & `MeowSerializer`):** Custom player state manager decoupled from legacy data structures, guaranteeing stable input processing, priority evaluation, and network synchronization.
* **Zero-Reload Dynamic Audio Injector (`MeowRegistration`):** Extends `SoundLoader` at runtime by manipulating `allAudio[]`, `soundTriggers[]`, and `workingTriggers[]`. Injects new audio samples and triggers on the fly without triggering native game reloads, VRAM duplication, or frame drops. Includes a native parser for `sounds.txt` configuration files that extracts volume, pitch, doppler, and random variation parameters, mapping them directly into custom `SoundTrigger` instances.
* **Global Asset Registry & Inactive Mod Mapping:** Unified indexing system (`MeowRegistration`) that scans both active and inactive local mods, establishing a clean baseline for local audio overrides and network fallback management.
* **Host Settings & Remix Sync (`MeowxMeadowOptions`):** Automatic coroutine-based synchronization of lobby flags, permission rules, and Remix configuration settings between host and connected clients.

---

## 🛠️ Technical Architecture

### 1. Identity & State Resolution Engine (`PlayerMeowProfileManager`)
* **SteamID Key Binding:** Maps player vocalizations using composite keys (`SteamID64:SlugcatID`).
* **Priority Resolution Hierarchy:** Evaluates vocalization triggers in strict order:
  1. **Active Local Override:** Direct custom audio mappings configured by the local client.
  2. **Received P2P Audio Payload:** Remote player audio clips dynamically injected into runtime memory.
  3. **Inactive Mod Fallback:** Local disk scanning of unenabled mods to satisfy missing audio IDs without requiring modpack activation.
  4. **Native Game Audio:** Clean fallback to default Push To Meow slugcat sounds.

### 2. Dynamic Audio Injection Pipeline (`MeowRegistration`)
The injection engine modifies Rain World's sound system through a modular three-tier architecture:
* **High-Level Orchestration (`InjectInactiveLocalMeows`):** Scans disk roots for unmanaged mods, filters out active game entries via $O(1)$ set differences, and routes valid audio folders to the parser.
* **Configuration Parser (`LoadAndInjectFromSoundsTxt`):** Parses `[ADD]` headers and comma-separated sample definitions from `sounds.txt`, building `SoundPlayInstruction` groups and custom parameter dictionaries.
* **Low-Level Execution Motor (`InjectAudioPackage`):** Safely resizes `SoundLoader` internal arrays, instantiates Unity `AudioClip` references via `AssetManager.SafeWWWAudioClip`, and registers new `SoundTrigger` indices with zero runtime overhead.

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