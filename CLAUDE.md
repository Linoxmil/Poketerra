# Wildlore — tModLoader Mod

A creature-collecting mod for Terraria. Find wild creatures roaming the world, catch
them with Snare Orbs, build a party, level them up.

## Hard rules

1. **All content is original.** No Pokémon, no Digimon, no other franchise's names,
   sprites, sounds, stats or lore. If a name sounds close to an existing creature from
   another game, rename it. This is non-negotiable — it is the reason the project can
   exist at all.
2. **Never renumber a species ID.** IDs in `Assets/Data/BeastDB.json` and `ID/BeastID.cs`
   are written into player save files. Changing one corrupts existing saves. Only append.
3. **Species are data, not code.** A new creature = a JSON entry + a sprite + a
   localization block. Do not write a new `ModNPC` subclass per species.
4. **Multiplayer-safe by default.** Any new mutable state on a creature needs handling in
   `BeastData.NetWrite`/`NetRead` and a bit in the field bitmask. Server-authoritative:
   roll randomness on the server, sync the result.

## Stack

- C# (latest lang version), .NET, tModLoader 1.4.4 API
- Newtonsoft.Json for the species database
- No external dependencies — keep it that way unless there is a strong reason

## Layout

```
Wildlore.cs                       Mod entry point. Loads DB, builds tables, registers content.
Core/
  BeastDatabase.cs                Species schema + JSON loading + localization lookups.
  BeastData.cs                    One individual creature. Save + network serialization.
  ExperienceTable.cs              Cached level→EXP curves.
  WildlorePlayer.cs               ModPlayer: party, active companion, discovery log.
Content/
  NPCs/BeastNPC.cs                Single ModNPC class, instantiated once per species.
  NPCs/BeastLoader.cs             Registers species instances, maps species ID → NPC type.
  NPCs/BeastSpawnSystem.cs        Injects creatures into the vanilla spawn pool by biome.
  Projectiles/SnareOrbProjectile.cs  Catching mechanic. Subclass for stronger orbs.
  Projectiles/BeastPet.cs         The summoned companion that follows the player.
  Items/BasicSnareOrbItem.cs      Throwable orb item + recipes.
ID/
  BeastID.cs                      Species ID constants. Append only.
  Enums.cs                        ElementType, GrowthGroup, SpawnBiome, LoreEntryStatus.
Assets/
  Data/BeastDB.json               The species database. Single source of truth.
  Beasts/<Identifier>.png         Sprite per species. Filename must match `name` in JSON.
  Items/BasicSnareOrb.png         Item sprite.
Localization/en-US.hjson          Display names and lore text.
```

## Adding a species — the full checklist

1. Add a constant to `ID/BeastID.cs` with the next free number. Bump `Count`.
2. Add the entry to `Assets/Data/BeastDB.json` under that same key.
3. Drop `Assets/Beasts/<Identifier>.png` — the filename must equal the `name` field exactly.
4. Add a `DisplayName` and `Lore` block to `Localization/en-US.hjson`.
5. Build in-game and verify it spawns in the intended biome.

No C# changes are needed. If a species seems to need one, that is a signal the schema
should gain a field instead.

## Where the numbers come from

Rule 1 covers stats, so every number in `BeastDB.json` has to be derivable from the
project's own budget rather than copied off a reference. New species follow these:

- **Stat budget.** Base forms get 180 points across hp/attack/defense/speed; evolved
  forms get 260. Spend them to fit the creature's lore — Emberkit is a glass cannon
  (36/58/34/52), Tidepup a bulwark (52/40/58/30), Mossling an allrounder (48/44/50/38).
- **`baseExp`** is the stat budget halved: 90 for a base form, 130 for an evolved one.
- **`catchRate`** is roughly 205–215 for base forms and 65–75 for evolved ones, nudged
  by how skittish the creature reads.
- **`evolution.atLevel`** is chosen per line, not shared — 18 to 22 so far.
- **EXP curves** (`ExperienceTable.Formula`) are a cubic plus a quadratic term. The
  cubic sets late-game cost, the quadratic stops the first few levels being instant.
- **`BeastData.MaxHP`** is a flat floor, plus a per-level term, plus a base-stat term.

If a proposed number happens to match a creature from another game, it is the wrong
number regardless of how well it plays.

## Sprites

- 32×32 per frame, vertically stacked for animation frames, transparent background
- Terraria's palette is muted and slightly desaturated — avoid pure saturated colours
- Readable silhouette at 100% zoom matters more than internal detail
- Frame count is `Wildlore.SpriteFrames`, and every sheet is authored to it. `BeastNPC`
  feeds it to `Main.npcFrameCount` and drives `FindFrame` itself; `BeastPet` feeds it to
  `Main.projFrames`. Change that constant and every sheet has to change with it.
- Never set `AnimationType` on `BeastNPC` — a vanilla animator indexes frames against
  that NPC's own sheet and runs off the end of a two-frame one.
- The shipped sheets are procedurally generated placeholders. Each is a drop-in replace.

## Balancing knobs

| What | Where |
|---|---|
| Spawn frequency (global) | `BeastSpawnSystem.GlobalSpawnScale` |
| Spawn frequency (per species) | `spawnWeight` in the JSON |
| Catch difficulty (per species) | `catchRate` in the JSON, 1–255, higher = easier |
| Catch difficulty (per orb) | `CatchModifier` in the orb subclass |
| Level cap | `Wildlore.MaxLevel` |
| Rare variant odds | `Wildlore.RareChance` (1-in-N) |

## Not yet built

- UI: party sidebar, discovery log screen, creature summary panel. Until those exist the
  only way to get a companion out is the `CycleCompanion` keybind (default `N`), which
  steps through the occupied party slots and then recalls.
- Rare variant sprites (`<Identifier>_R.png`) and the draw path for them
- Evolution trigger + animation (`BeastData.GetQueuedEvolution` exists but is never called)
- EXP gain — nothing currently awards it
- Multiplayer packet sync for party changes (save/load works, live sync does not)
- Sounds

Pick one and finish it before starting the next.

## Building

tModLoader compiles this in-game: Workshop → Develop Mods → Build + Reload.
Sources must live in `~/Library/Application Support/Terraria/tModLoader/ModSources/Wildlore`.
There is no separate CLI build step, and the code has not been compiled yet — expect to
fix API signature mismatches on the first build.
