# PyramidRunner

**PyramidRunner** is a multiplayer Unity game about surviving, moving fast, and working together in a shared online world.

Built as the client for **[pyramid-server](https://github.com/UntitledOutput/pyramid-server)**, PyramidRunner connects players into rooms, synchronizes movement and actions in real time, and drives the full game flow from the home screen to the match itself.

---

## What you do in PyramidRunner

- Join online rooms with other players
- Customize your character before a match
- Move, sprint, crouch, and survive in a networked world
- Complete objectives together
- Track health, fear, and match progress
- Watch the match unfold through opening and closing game sequences

---

## Game flow

PyramidRunner is built around a simple loop:

1. **Start in the home scene**
2. **Customize your outfit**
3. **Join or host a room**
4. **Launch into a match**
5. **Complete tasks and survive**
6. **Return to the home screen after the match ends**

The game uses networking to keep players, rooms, and world state synchronized across the session.

---

## Core systems

### Player movement and survival
The player controller handles:

- first-person movement
- sprinting and crouching
- camera control
- flashlight behavior
- health and fear
- damage and death states
- spectating after death

### Multiplayer networking
The network layer handles:

- connecting to the server
- joining and creating rooms
- spawning networked objects
- synchronizing player state
- sending damage, updates, and room events
- loading scene changes during match flow

### Match and room management
The game manages:

- room UI and player lists
- match start and end states
- win/loss transitions
- rewards and level progression
- player cosmetics in room previews

### Persistent player data
PyramidRunner saves player progress locally, including:

- username
- outfit selections
- money
- level
- level progress

---

## Script structure

The `Assets/Scripts` folder is organized into a few clear gameplay layers:

| Folder | Purpose |
|---|---|
| `Controllers/` | Player and map gameplay logic |
| `Managers/` | Game flow, menus, persistence, and UI control |
| `Network/` | Multiplayer sync, room state, and object replication |
| `ScriptedObjs/` | Scriptable objects for maps, cosmetics, and registries |

---

## Key scripts

### `Network/NetworkMgr.cs`
The main multiplayer manager.

It handles:
- connecting to the backend
- room creation and joining
- scene changes
- object spawning and destruction
- player updates
- match start/end events

### `Controllers/PlayerController.cs`
The main player gameplay script.

It handles:
- movement and camera behavior
- sprint/crouch logic
- flashlight flicker
- health and fear values
- damage reactions
- death and spectating
- syncing movement and state over the network

### `Managers/GameMgr.cs`
Controls the in-match presentation and transitions.

It handles:
- opening and closing match animations
- map setup
- UI sliders for health, stamina, and fear
- match start/end sequences

### `Managers/HomeMgr.cs`
Controls the home scene and interaction menus.

It handles:
- outfit customization
- online room menu transitions
- moving the player and camera into UI spaces
- saving customization changes

### `Managers/RoomScreenMgr.cs`
Controls the online room UI.

It handles:
- player list display
- cosmetic previews
- room menu visibility
- match result screen
- reward and progression animation

### `Managers/DataManager.cs`
Handles persistent player save data.

It stores:
- username
- suit and hat selection
- money
- level and level progress

---

## Visual identity and content

PyramidRunner uses scriptable objects and registry-based content loading for:

- player cosmetics
- maps
- networked prefabs
- preview art and map data

This makes the game easier to extend with new outfits, maps, and networked objects.

---

## Multiplayer behavior

PyramidRunner uses a server-authoritative multiplayer flow:

- players connect to the server
- rooms are created or joined
- the host drives match progression
- players sync movement and actions
- task completion and game end states are broadcast to everyone

The client supports both regular networking and WebGL-friendly WebSocket behavior.

---

## Build and runtime notes

This project includes Unity build automation and a browser-friendly deployment path.

- WebGL build support is included
- Local save data is stored through Unity PlayerPrefs
- The game can run with a networked backend and room-based sessions

---

## Related repository

**[pyramid-server](https://github.com/UntitledOutput/pyramid-server)**  
The multiplayer backend that powers PyramidRunner.

---

## License

Licensed under the Apache License 2.0.
