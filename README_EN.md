# Infinite Dao · Sect Chronicles

[中文](README.md) · **English**

[![CI main](https://github.com/achaosxyz/infinite-dao/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/achaosxyz/infinite-dao/actions/workflows/ci.yml?query=branch%3Amain)
[![CI develop](https://github.com/achaosxyz/infinite-dao/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/achaosxyz/infinite-dao/actions/workflows/ci.yml?query=branch%3Adevelop)
[![Windows release](https://github.com/achaosxyz/infinite-dao/actions/workflows/release.yml/badge.svg)](https://github.com/achaosxyz/infinite-dao/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Choose a cultivation sect, set its direction, and watch disciples grow and territories change hands. This Windows x64 native single-player simulator focuses on strategic choices rather than repetitive orders. Losing your last mountain gate does not end the game: surviving disciples can carry the lineage into exile and rebuild in the same world.

![The Qinglan world map](src/InfiniteDao.Game/Assets/qinglan-world.png)

## Features

- **Ten playable sects and 192 regions** across twelve prefectures, with difficulty and world-seed selection.
- **Strategy-driven automation** for recruitment, construction, cultivation, journeys, and war preparations; you decide major events.
- **Individual disciples** with focused training, accompanying guardians, breakthroughs, injuries, aging, and death.
- **A unified jade-and-ivory HUD and 50 character illustrations**: sect crests identify factions; portraits stay with each character through succession and save/load. Existing six-sect saves migrate without changing their world.
- **Years-long campaigns** where higher-realm cultivators can strike multiple lower-realm foes per round; supply, formations, relics, and injuries still affect the outcome—not aggregate power alone.
- **Exile and rebuilding** with surviving disciples and inherited knowledge; eliminate all other sect lineages to achieve unification.
- **Pause and inspect** the map, sects, people, diplomacy, treasury, and chronicles; use local automatic and manual saves.
- **Optional background Agents** using an Anthropic-compatible API, independent public memories, and persistent plans.

The game UI is currently **Chinese**. The complete offline game needs no model account, browser, Node.js, or game server.

## Getting started

### Portable Windows build

Download the Windows x64 ZIP from [GitHub Releases](https://github.com/achaosxyz/infinite-dao/releases) and extract it completely. Automated builds are numbered prereleases with a SHA256 checksum file; the EXE is not code-signed.

Keep the entire `InfiniteDao-Godot-Windows` directory and run `InfiniteDao.exe` or `Play.cmd`. It includes the native engine, .NET runtime, and art. **Do not distribute or copy the EXE alone.**

The repository's export script writes the portable build to:

```text
artifacts/releases/InfiniteDao-Godot-Windows/
```

### Run from source

Prepare the project's specified .NET SDK and Godot .NET engine, then double-click [启动游戏.cmd](启动游戏.cmd) in the repository root, or run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/start-game.ps1
```

The root launcher prefers an existing native export and uses workspace configuration and saves. Without an export, it launches from source. See the [development workflow](docs/DEVELOPMENT.md) for environment and export instructions.

## How to play

1. Choose your sect, difficulty, seed, and offline or intelligent mode, then resolve the opening council.
2. Set a long-term direction; optionally focus training on disciples and assign higher-realm guardians.
3. Let time advance automatically. Major events pause the world until you choose an option.
4. Pause with Space or the pause control to inspect the world. Losing window focus pauses time; changing inspection pages does not.
5. Maintain supplies, cultivate talent, and wage sustained campaigns. If exiled, resolve the lineage event and select a place to rebuild.

The detailed [playing guide](docs/PLAYING.md) is in Chinese.

## Optional model integration

Follow the [configuration guide](config/README.md) to configure `config/llm.local.json` under the game's data root. Never commit or share API keys. Defaults use HIGH/adaptive reasoning, a 256000-token context window, and compaction at 90%.

Agents wake for relevant events or reviews, **not every month**. Up to ten run concurrently by default, with no lifetime request quota. Existing strategies remain active while responses are pending, so model latency does not stop time. An unsuccessful task receives three retries after its initial attempt; exhaustion explicitly switches the world offline and records the change. Intelligent mode can be restored manually in settings.

**Real requests, context counting, compaction, timeouts, and retries may incur provider charges.** Offline mode remains fully playable, and the configuration template contains no private workspace keys.

## Saves and local data

- The root launcher uses workspace configuration and `artifacts/game/saves/`.
- A standalone portable build uses its own directory for configuration and the same relative save path.
- Three manual slots supplement the automatic save; JSON import/export is supported. Back up your saves before moving the game.
- Close the native window to exit; there is no separate browser or background game server to stop.

## Documentation and contributing

- [Documentation index](docs/README.md): player and developer navigation.
- [Project design](docs/design/PROJECT_DESIGN.md): current modules, data models, and flows.
- [Development workflow](docs/DEVELOPMENT.md) and [verification scope](docs/VERIFICATION.md): development and native acceptance checks.
- [Release workflow](docs/RELEASE.md): development on `develop`, merging into `main`, and Windows publishing after successful CI.
- [AGENTS.md](AGENTS.md): progressive instructions for coding Agents, not a player manual.

These developer documents are in Chinese. When reporting a problem, provide your environment, reproduction steps, and relevant redacted logs—not full private configuration or raw provider responses. Read the development workflow and keep affected Chinese and English documentation aligned when contributing.

## License

This project uses the [MIT License](LICENSE), attributed to `infinite-dao contributors`. Third-party engines, runtimes, and dependencies retain their respective licenses.
