# Changelog

All notable changes to Philo Lens. The release workflow reads the section matching the
csproj `<Version>`, so to release:

1. Set `<Version>` in `src/Stellar.PhiloLens/Stellar.PhiloLens.csproj` (e.g. `1.0.0`).
2. Rename `## [Unreleased]` to `## [1.0.0] - YYYY-MM-DD` and add a fresh `## [Unreleased]` above it.
3. Merge to `main`.

Use only the `### Added`, `### Changed`, `### Fixed` and `### Removed` headings (the launcher's
changelog buckets), with one `- ` bullet per change.

## [Unreleased]

## [1.0.0] - 2026-09-27

### Added

- A **Lens** button on other players' profile cards that shows their class and spec.
- Spec is read from the player's talents as soon as they come into range, even in town.
- For players out of range, spec comes from skills they used in your zone; otherwise their class is shown.
- The window closes together with the profile card.
- Ability Score and the season strength stat, named as the game names it this season.
- Equipped Battle Imagines by the name players use, with their tier.
- The season-talent board a player runs, titled with the game's own name for the system.
