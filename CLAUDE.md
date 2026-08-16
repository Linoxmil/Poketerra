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
  ElementChart.cs                 Which element beats which. One side written, other derived.
  Combat.cs                       Damage maths, EXP payouts, element lookup for vanilla NPCs.
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
  Enums.cs                        ElementType, GrowthGroup, SpawnBiome, MovementStyle,
                                  LoreEntryStatus.
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
  forms get 260; species with no evolution get 215. Spend them to fit the creature's
  lore — Emberkit is a glass cannon (36/58/34/52), Tidepup a bulwark (52/40/58/30),
  Mossling an allrounder (48/44/50/38).
- **`baseExp`** is the stat budget halved: 90, 130, or 108.
- **`catchRate`** is roughly 205–215 for base forms, 65–75 for evolved ones, and
  120–140 for standalone species, nudged by how skittish the creature reads.
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

## How a fight works

There is no battle screen. Wildlore fights happen in the world, on Terraria's own terms.

- A companion auto-attacks hostile NPCs on sight. It attacks **wild creatures only while
  its owner is holding a Snare Orb** — otherwise crossing a meadow would start six fights
  nobody asked for.
- It stops hitting a wild creature below `BeastPet.SpareThreshold` health, so it can never
  finish off the rare you came for.
- Each strike is a lunge: damage is set on the projectile for the lunge window only, so a
  companion drifting into something never hurts it.
- Wild creatures wander on their own — `BeastNPC` runs its own AI at `aiStyle = 0` rather
  than borrowing a vanilla one, which is why gravity is applied by hand. The behaviour a
  species uses comes from `movement` in the JSON.
- They are passive until struck, then hit back for `BeastNPC.AggroMemory` ticks and flee
  once below half health.
- Name and level float over any creature within `BeastNPC.LabelRange`, and over the
  companion. Until the party UI exists this is the only place levels are visible.
- Damage is `attack * 2 - defence` with a floor, times the element multiplier. Defence
  lengthens a fight rather than ending it, which matters when the point is to weaken
  something enough to catch it.
- Catch chance scales with how worn down the target is — that is the whole reason to send
  a companion in first.
- A fainted companion is recalled and heals out of combat. Death heals the whole party.

Every consequence of a fight — damage, EXP, level-ups, evolution — resolves on the owning
player's own machine, because that is the only place party data exists. See the multiplayer
note under "Not yet built".

## How spawning is paced

Creature density is one number — `GlobalSpawnScale` — meaning "what share of nearby spawns
should be creatures", measured against vanilla's own pool entry of 1. `spawnWeight` only
splits that share between the species of a biome, so a biome holding four species is not
twice as busy as one holding two.

Two things then scale it:

- **World size.** `WorldPopulation` squares the ratio against a medium world and clamps to
  0.5–1.5, so a small world runs at 15% creatures and a large one at 45%, with the nearby
  cap moving 4 → 12 alongside it.
- **Spawn rate.** Weighting the pool only decides what comes out of it. On a quiet daytime
  surface Terraria hardly reaches into the pool at all, so weight alone leaves a meadow
  empty; `SpawnRateBoost` raises how often it reaches. It lifts vanilla spawns a little
  too — set it to 0 to leave Terraria's pacing untouched.

## Balancing knobs

| What | Where |
|---|---|
| Share of spawns that are creatures | `BeastSpawnSystem.GlobalSpawnScale` |
| How often the pool is sampled | `BeastSpawnSystem.SpawnRateBoost` (0 = leave vanilla alone) |
| Creatures alive near one player | `BeastSpawnSystem.NearbyCap` |
| World-size scaling | `BeastSpawnSystem.WorldPopulation` |
| Split within a biome | `spawnWeight` in the JSON |
| Catch difficulty (per species) | `catchRate` in the JSON, 1–255, higher = easier |
| Catch difficulty (per orb) | `CatchModifier` in the orb subclass |
| Level cap | `Wildlore.MaxLevel` |
| Rare variant odds | `Wildlore.RareChance` (1-in-N) |
| Party size | `WildlorePlayer.PartySize` |
| How a species moves | `movement` in the JSON (0 walk, 1 hop, 2 fly, 3 drift, 4 swim) |
| Wander pace | `BeastNPC.WanderSpeed`, derived from the Speed stat |
| Companion aggro range | `BeastPet.AggroRange` |
| Attack rate | `BeastPet.BeginLunge`, derived from the Speed stat |
| When a companion spares a target | `BeastPet.SpareThreshold` |
| Out-of-combat healing | `BeastPet.RegenInterval` / `OutOfCombatTicks` |
| Damage formula | `Combat.Damage` |
| EXP payouts | `Combat.ExperienceFrom` |
| Element matchups | `ElementChart.StrongAgainst` |

## Not yet built

- UI: party sidebar, discovery log screen, creature summary panel. Until those exist the
  only way to get a companion out is the `CycleCompanion` keybind (default `N`), which
  steps through the occupied party slots and then recalls.
- Rare variant sprites (`<Identifier>_R.png`) — rare creatures are currently only tinted,
  scaled up slightly, and given a glow
- Multiplayer packet sync for party changes. Save/load works, live sync does not, which is
  why every fight resolves on the owner's own machine and why a companion's despawn is
  decided there too. Other players see the companion move, but not its health.
- Sounds of its own — combat currently borrows vanilla `SoundID` entries
- Status effects, held items, or anything else that would make two creatures of the same
  species play differently

Pick one and finish it before starting the next.

## Building

tModLoader compiles this in-game: Workshop → Develop Mods → Build + Reload.
Sources must live in `~/Library/Application Support/Terraria/tModLoader/ModSources/Wildlore`.
There is no separate CLI build step, and the code has not been compiled yet — expect to
fix API signature mismatches on the first build.
