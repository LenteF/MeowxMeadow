# MeowxMeadow

A robust multiplayer integration and architectural refactor for **Push To Meow**, built specifically for **Rain Meadow**. 

### Key Features
* **SteamID Identity Resolution:** Replaces volatile local player indices with unique `SteamID64` keys to ensure collision-free state tracking across multiplayer lobbies.
* **Isolated State Architecture:** Custom player state manager decoupled from legacy data structures, guaranteeing stable input processing and network synchronization.
* **Host Settings Sync:** Automatic coroutine-based synchronization of lobby flags and Remix configuration settings between host and clients.
