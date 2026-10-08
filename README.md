# BreedingInPen

A BepInEx plugin for Valheim that keeps newborn animals inside their pen and lets you set a breeding limit for each creature.

> **Server-side only.** Install it on the dedicated server only. Players don't install anything, so it works with vanilla clients and with crossplay players on console or Game Pass.

It covers every creature the game breeds itself: boars, wolves, lox, hens, asksvin, moose and creatures added by other mods, as long as they use the game's own breeding.

## How it works

- **Birth at the parent (`BirthAtParent`, on by default):** the game normally spawns a baby up to 2 m behind its mother, which can be on the other side of a fence. With this on, the baby appears exactly where the mother stands.
- **Birth between the parents (`BirthBetweenParents`, on by default):** a moment after birth, the newborn is moved between its mother and her nearest tamed partner. Both parents are inside the pen, so the baby is too. The baby may visibly jump once, by a metre or two. Hens and asksvin lay eggs, so this part doesn't apply to them; `BirthAtParent` is enough there.
- **Breeding limit per creature (`[MaxCreatures]`):** one setting per creature. Tamed animals stop breeding when this many of their kind and their young are nearby (the game's own check and range). `-1`, the default, keeps the game's limit (boar 5, wolf 4, lox 4, hen 10, asksvin 10, ...). Only the same kind counts: lox never count toward the boar limit.

## How it works without a client mod

Breeding runs on the game of the player nearest to the parents. The plugin uses the game's own per-creature setting overrides, which every vanilla client already reads, to change the birth distance and the breeding limit. A client only reads them when it loads a creature, so when a creature's settings change, the server swaps it for an identical copy (same name, stars, saddle and everything else). It may blink once. This happens once per creature, and again only if you change the settings.

A newborn is moved the same way: the server replaces it with a copy placed between the parents.

Only tamed creatures in areas loaded around players are changed, the same areas where the game runs breeding.

## Configuration

`BepInEx/config/Tie.BreedingInPen.cfg`:

| Section | Setting | Default | |
| --- | --- | --- | --- |
| `General` | `TickSeconds` | 2 | How often creatures near players are checked. |
| `General` | `DebugLog` | false | Log every creature changed or newborn moved. |
| `Pen` | `BirthAtParent` | true | Babies appear where the mother stands. |
| `Pen` | `BirthBetweenParents` | true | Newborns are moved between their parents. |
| `Pen` | `NewbornSeconds` | 20 | Only tamed babies younger than this are moved. |
| `MaxCreatures` | one per creature | -1 | Breeding limit; `-1` keeps the game's value. |

## Compatibility

- **Deer and necks** aren't bred by the game. For them, see [ServerTaming](https://github.com/Shuiei/valheim-server-taming), which already places their offspring between the parents.
- **ServersideQoL** (CreatureLevelUp, TameAssist) also uses per-creature overrides. Both mods keep each other's settings when they swap a creature.

## Installation

Put `BreedingInPen.dll` in `BepInEx/plugins/BreedingInPen/` on the **dedicated server** only. Players don't install anything: no client mod is needed or used.

## Building

The project targets .NET Framework 4.7.2. It references the game and BepInEx assemblies from a Valheim dedicated server install with BepInEx. By default it looks two folders above the project; you can point it somewhere else:

```sh
dotnet build -c Release -p:ValheimDir=/path/to/valheim_server
```
