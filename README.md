# Timed Craft Speed

A lightweight Valheim mod that adds an independent speed multiplier for each timed crafting machine.

## Features

- Independent speed multiplier for every supported machine
- x1.0 by default
- Changes can be applied in-game through a BepInEx Configuration Manager
- Machine names use the game's current localization
- No changes to capacity, fuel consumption, recipes or inventories
- Designed to remain as small and unobtrusive as possible

## Supported machines

- Blast Furnace
- Charcoal Kiln
- Cooking Station
- Eitr Refinery
- Fermenter
- Frigid Kiln
- Frost Foundry
- Iron Cooking Station
- Smelter
- Spinning Wheel
- Stone Oven
- Windmill

## Configuration

Configuration file:

`BepInEx/config/local.timedcraftspeed.cfg`

Each machine has its own speed multiplier.

## Compatibility

Tested with:

- Valheim 1.0.12 / Deep North
- BepInEx 5.4.23.5

## Installation

### Thunderstore / r2modman

Install the package normally through your mod manager.

### Manual

Copy `TimedCraftSpeed.dll` into:

`BepInEx/plugins/TimedCraftSpeed/`

## Source

Source code is available on GitHub.

## License

MIT
