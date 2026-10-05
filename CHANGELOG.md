# Changelog

All notable changes to Philo Lens. The release workflow reads the section matching the
csproj `<Version>`, so to release:

1. Set `<Version>` in `src/Stellar.PhiloLens/Stellar.PhiloLens.csproj` (e.g. `1.0.0`).
2. Rename `## [Unreleased]` to `## [1.0.0] - YYYY-MM-DD` and add a fresh `## [Unreleased]` above it.
3. Merge to `main`.

Use only the `### Added`, `### Changed`, `### Fixed` and `### Removed` headings (the launcher's
changelog buckets), with one `- ` bullet per change.

## [Unreleased]

## [1.3.0] - 2026-10-05

### Added

- A **Party** section beside the player's details when they're in a party: how full it is (a raid
  counts out of 20), how many tanks, healers and DPS it has, and each member's name, class (with
  spec when known), Ability Score and season strength. It works at any distance.
- Season strength now also shows for players who aren't near you.

### Changed

- The Lens window is now only as wide as what it shows, with a little more space at its edges.

## [1.2.0] - 2026-09-27

### Changed

- The Lens window now opens near the middle of the screen, and you can drag it by its title bar to
  wherever you like. It remembers where you put it.

## [1.1.0] - 2026-09-27

### Added

- Food and potion buffs, with their effect and a countdown of the time left.

## [1.0.0] - 2026-09-27

### Added

- A **Lens** button on other players' profile cards that shows their class and spec.
- Spec is read from the player's talents as soon as they come into range, even in town.
- For players out of range, spec comes from skills they used in your zone; otherwise their class is shown.
- The window closes together with the profile card.
- Ability Score and the season strength stat, named as the game names it this season.
- Equipped Battle Imagines by the name players use, with their tier.
- The season-talent board a player runs, titled with the game's own name for the system.
