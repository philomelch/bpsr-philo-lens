# Philo Lens

A [StellarResonance](https://github.com/StellarProtocol/StellarResonanceModSystem) plugin for
*Blue Protocol: Star Resonance*.

A quick readiness check for raid and party leaders. Open a player's profile card and press
**Lens**: a small window next to the card shows

- their **class and spec** (or "Spec not seen yet" while only the class is known),
- **Ability Score** and the **season strength** stat,
- their equipped **Battle Imagines** and tier (e.g. "Phantom Arachnocrab · Tier 5"),
- the **season-talent** board they run (e.g. "Fantasia Impact"),
- active **food ("Cuisine"), serum (potion) and "Foodie's Grace"** buffs, with their effect and a
  live countdown of the time left,
- their **party**, to the right: how full it is (e.g. "Party (3/5)"), how many tanks, healers and
  DPS it has, and each member's name, class (with spec when known), Ability Score and season
  strength (headed by the initials of the season's own name for the stat, e.g. "IBS").

The window opens near the middle of the screen; drag it by its title bar to put it anywhere, and it
stays there next time. It closes together with the card.

## What is shown, and where it comes from

- **Spec** comes from the player's talents, which the server sends as soon as they come into
  your range, so it is known in town before they fight. Otherwise it falls back to skills they
  used in the current zone, then to their **class** alone (e.g. players who haven't picked a spec).
- **Class** comes from the player's live data, the party roster, or their profile data, in that
  order. A Battle Imagine transform (e.g. Lucy, Natsu) is never shown as a class; the last real
  class stays on screen.
- **Ability Score, season strength, Imagines, season talent and food/serum** are only known for
  players near you, because the game only sends them for players in range. For players further
  away, the class, Ability Score and season strength come from the profile data the game loaded for
  the card.
- **Party** comes from the profile data the game loads for the card, so it works at any distance.
  A member's spec shows only when that member is near you (or was seen casting); otherwise their
  class alone. The party's copy of a member's stats can lag a little behind the member's own profile;
  the player you inspected always shows their own, current values. A raid shows as "/20" however
  few members it has, with its members in one list in party order. It isn't split into its raid
  groups, because the card's copy of who is in which group can be out of date.
- **Season-proof labels:** season-specific names (the strength stat, the season-talent system and
  its boards, e.g. "Illusion-Breaking Strength" and "Deep Slumber" in season 3) come from the
  game's own data, so they change with the season and follow the game language. Where the game
  data has no name, neutral labels ("Season Strength", "Season Talent") are shown.

UI labels come in English, Indonesian, Japanese, Thai and Filipino. The non-English labels are
machine-generated and haven't been checked by native speakers, so expect rough wording;
corrections are welcome. Game names (classes, Imagines, buffs, boards) come from the game itself
and follow the game's language.

Requires Stellar framework **2.11.0** or newer.

## Privacy

Philo Lens only reads what the game already sends to your client: data about players near you, and
the profile data the game loads when you open someone's card. It never asks the server for anything,
stores nothing, sends nothing anywhere, and performs no game actions.

## Installing

In the Stellar launcher's settings, add this under **Plugin Sources** (including `https://`):

```
https://stellar-plugins.philomel.dev
```

Then install **Philo Lens** from the launcher's plugin list, and update it from there too.

To install by hand instead, download `Stellar.PhiloLens.dll` from the latest
[release](https://github.com/philomelch/bpsr-philo-lens/releases) and place it in
`<game_mini>/stellar/plugins/philo-lens/`.

## Game data access

Most data comes from the framework's typed services. A few names don't: the SDK has no lookup for
a Battle Imagine's item, the season-talent boards, the season-talent system's name or which buffs
are food and potions. For those, the plugin reads some of the game's own **static configuration
tables** through the SDK's reflection helper (`StellarInterop`, one of the framework's escape hatches):

| Table | Used for |
|---|---|
| `SkillAoyiTable` | Imagine skill → its Imagine item (then named by the typed item data, without the "Battle Imagine -" label all of them share) |
| `SeasonTalentTemplateTable`, `SeasonTalentTreeTable`, `SeasonTalentEffectOrdinaryTable` | Season talent: which board (template) each season-talent buff comes from, and the board's name. Only boards of the running season (from the player's own season data) are used, rebuilt when a new season starts; nodes shared by several boards are ignored. |
| `FunctionTable` | The game's name for the season-talent system (the boards' feature), used as the section title. |
| `BuffTable` | Each buff's category (`BuffAbilityType`): 101 food, 102 potion, 104 food bonus. Looked up once per buff id. Names, descriptions and timing come from the typed SDK. |

The tables are read only from the update tick while the world is loaded (never during a zone load)
and cached.

This is **read-only**: it calls only the tables' own getters, writes nothing, patches nothing and
performs no game actions. It lives in `src/Stellar.PhiloLens/Adapters/GameTables/`. Answers are
cached; the season-talent index is rebuilt when a new season starts, and a table that can't be
read is asked for again later instead of being given up on. If a game patch renames a table or
column, the plugin falls back to the Imagine's skill name and the neutral section title, and hides
the lines it can't name, instead of failing.

### Party roster (Lua, read-only)

The framework's typed profile data carries only the party's size, not its members. The members are in
the profile data the game itself loaded for the open card, which lives in the game's Lua state, so the
plugin reads it through the SDK's `Lua` escape hatch. It lives in `src/Stellar.PhiloLens/Adapters/Party/`.

- **Read-only.** The Lua chunk asks the UI for the open profile card (`Z.UIMgr:GetView('idcard')`) and
  then only walks plain tables (`next`/`rawget`). It calls no game function that could fetch, send
  or change anything; a unit test pins this down.
- **Cheap.** It runs once when you press Lens, and again at most once a second (up to ten times)
  while the card is still loading. The search under the card is bounded.
- **Fails quietly.** If a game update moves or renames the data, the party section stays hidden
  and the log says what the plugin found instead.

## Build and test

Requires the .NET 8 SDK (the plugin targets `net6.0`, the tests `net8.0`).

```bash
bash tools/check-standards.sh   # coding-standards gate (also runs in CI)
dotnet build -c Release
dotnet test -c Release
```

## Try a local build in-game

Copy `src/Stellar.PhiloLens/bin/Release/net6.0/Stellar.PhiloLens.dll` into
`<game_mini>/stellar/plugins/philo-lens/` and start the game. Logs go to
`<game_mini>/BepInEx/LogOutput.log`, tagged `[Stellar.PhiloLens]`. Toggle the plugin off and on
in **Settings → Plugins** to check that it unloads cleanly.

## Releasing

Every push to `main` runs `.github/workflows/release.yml`: CI first, then a GitHub Release **only
if** `<Version>` in the csproj is new. To release:

1. Bump `<Version>` in `src/Stellar.PhiloLens/Stellar.PhiloLens.csproj` (`0.0.0` = never released).
2. In `CHANGELOG.md`, turn `## [Unreleased]` into `## [x.y.z] - YYYY-MM-DD`.
3. Merge to `main`.

The release carries the DLL, its `.sha256`, and `manifest.json`. The minimum framework version is
taken from the pinned `Stellar.Abstractions` version. The
[registry](https://github.com/philomelch/bpsr-stellar-philo-registry) picks new releases up within
the hour. The plugin-level fields (description, tags,
homepage, …) live in `stellar-plugin.json`.

Recommended repo settings: enable **immutable releases**, and protect `main` so changes arrive
through pull requests that pass CI.

## Layout

Clean Architecture inside one assembly (the registry ships one DLL per plugin). Dependencies
point inward only:

| Folder | Holds | May reference |
|---|---|---|
| `Domain/` | Values and rules | .NET only |
| `Application/` | Use cases, plus the ports (interfaces) they need | Domain |
| `Adapters/` | Port implementations over `IPluginServices` | Application, Domain, Stellar SDK |
| `UI/` | Window/HUD element trees | Application, Domain, Stellar SDK |
| `Plugin.cs` | Composition root; wires everything, owns `Dispose()` | everything |

`tools/check-standards.sh` fails the build if `Domain/` or `Application/` reference the framework.

## License

[AGPL-3.0-or-later](LICENSE).
