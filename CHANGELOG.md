# Changelog

## 1.0.1

- Also trigger when a station becomes the player's active one (`Player.SetCraftingStation`), not only from `CraftingStation.Interact`, so every station type is covered. Both hooks repair once per interaction.
- When a station opens with damaged items but nothing gets repaired, the log now lists each item and the reason Valheim's repair rules gave (wrong station, station level too low, no roof or fire, not repairable). Reported: repairs happen at the workbench but not at the forge.

## 1.0.0

- First standalone release. Moved out of Valheim Unified 0.41.0 with unchanged behaviour: every eligible item is repaired when the local player uses a crafting station.
- Fails safely: if a future Valheim update moves the repair methods, the mod logs an error and stays inactive instead of patching the wrong code.
