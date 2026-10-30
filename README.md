# Comic Horror Fishing Buddies

![GitHub all releases](https://img.shields.io/github/downloads/xen-42/cosmic-horror-fishing-buddies/total?style=for-the-badge)
![GitHub release (latest by date)](https://img.shields.io/github/downloads/xen-42/cosmic-horror-fishing-buddies/latest/total?style=for-the-badge)
[![Support me on Patreon](https://img.shields.io/endpoint.svg?url=https%3A%2F%2Fshieldsio-patreon.vercel.app%2Fapi%3Fusername%3Dxen42%26type%3Dpatrons&style=for-the-badge)](https://patreon.com/xen42)

A WIP multiplayer mod for DREDGE. Currently early alpha and might not work as intended. Installable using the [Dredge Mod Manager](https://github.com/DREDGE-Mods/DredgeModManager).

Unfortunately connecting with a connection code (EOS) does **NOT** work with the Epic Games store version. This means you will have to manually set up port forwarding or use a VPN connection service. When connecting via IP adress the port used is **7777**.

![cosmic horror fishing buddies](https://github.com/xen-42/cosmic-horror-fishing-buddies/assets/22628069/47687a63-0313-41dd-9d79-adcf58372c06)

Huge thanks to JohnCorby for helping me set up Mirror with the mod. And shout-out to \_nebula and everyone who's contributed to [Quantum Space Buddies](https://github.com/misternebula/quantum-space-buddies), for the inspiration ~~and code that I stole~~.

## How to Install
Download the DREDGE Mod Manager from [dredgemods.com](https://dredgemods.com/)!

## Features (spoilers)
#### What is synced:
- Player movement
- Engine sounds & ship wake
- Time
  - Fast forwarding - all players must be fast-forwarding too or at least in port for time to speed up
- Fishing and dredge spots
  - Stock and "special" status, including for players who join later
  - One-off spots such as quest relics stay available to each player
- Abilities
  - Light
  - Foghorn
  - Manifest
  - Banish (has effects but only works locally)
  - Atrophy
  - Haste (missing vfx)
  - Trawl net
  - Bait
  - Crab pots, including their contents and pots placed before a player joined
- Weather, per zone, and waves
- The banish machine
- Ship appearance
  - Rigidbody boat assets (net, tires) don't animate yet
- World Events
  - Only the occurrence of events is currently synced, will also have to sync the movement of whatever is spawned by the event

![fishing buddies 2](https://github.com/xen-42/cosmic-horror-fishing-buddies/assets/22628069/fe5177b1-babc-45a6-8070-2327776e938b)

#### What is not synced (roadmap):
- Location/quest specific details
- Enemies
- Collisions?

## Dependencies (included)
- [Winch](https://github.com/DREDGE-Mods/Winch)
- [Mirror](https://mirror-networking.com/)
  - [kcp2k](https://github.com/vis2k/kcp2k)
  - [EpicOnlineTransport](https://github.com/FakeByte/EpicOnlineTransport)
- [HarmonyX](https://github.com/BepInEx/HarmonyX)

## Testing
Builds run on every push and pull request (`.github/workflows/build.yml`).

The multiplayer features are tested in the real game. With DREDGE and [Winch](https://github.com/DREDGE-Mods/Winch/releases) installed, run:

```powershell
./tools/run-game-tests.ps1
```

The script builds the mod, deploys it to your DREDGE `Mods` folder, starts a host and a client instance on this machine, and runs every scenario in `CosmicHorrorFishingBuddies/Testing/Scenarios.cs` in both. It prints PASS/FAIL per scenario per role and exits non-zero on any failure. Your saves and settings are backed up first and restored afterwards; the test plays on its own slot (2), seeded from your slot 0 or from `-Save <file>`. Logs and results for each run land in `.test-runs/<timestamp>/`.

`-Clients 2` adds a second client for three-player sessions. Every instance runs the checks that apply to everyone; two-player checks pair the host with the first client and show as SKIP for the others.

To cover a new feature, add a scenario to `Scenarios.All`: a host body, a client body, one body for both, or `Everyone` for checks every instance runs. `TestContext.Share`/`Receive` pass values between the host and the first client.
