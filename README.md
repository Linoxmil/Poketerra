# Wildlore

A creature-collecting mod for Terraria, built on [tModLoader](https://github.com/tModLoader/tModLoader).

Find wild creatures roaming the world, catch them with Snare Orbs, build a party of six,
and level them up. All creatures, artwork and lore are original.

## Status

Early scaffold. The data model, spawning, catching and companion systems are in place.
UI, evolution and EXP gain are not yet implemented — see `CLAUDE.md` for the open list.

**The code has not been compiled yet.** Expect to fix API signature mismatches on the
first in-game build.

## Setup

```bash
git clone <your-repo-url> \
  ~/Library/Application\ Support/Terraria/tModLoader/ModSources/Wildlore
```

Then in tModLoader: Workshop → Develop Mods → Build + Reload.

On macOS: tModLoader runs under Rosetta 2 on Apple Silicon. Run in windowed mode —
fullscreen currently crashes on Apple Silicon.

## Adding a creature

Edit `Assets/Data/BeastDB.json`, add a sprite, add a localization entry. No C# required.
Full checklist in `CLAUDE.md`.

## License

MIT — see `LICENSE.txt`.

Not affiliated with or endorsed by Re-Logic.
