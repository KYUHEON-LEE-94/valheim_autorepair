# Unified Auto Repair

Repairs every eligible item as soon as you use a crafting station
(workbench, forge, black forge, …). No button, no menu.

Split out of [Valheim Unified](https://thunderstore.io/c/valheim/p/maizz/ValheimUnified/)
so it can be installed on its own.

## Behaviour

- Triggers when **you** open a crafting station, not when someone else does.
- Repairs only what that station is able to repair, exactly as Valheim's own
  repair button would, one item at a time until nothing eligible is left.
- Shows a single message with the number of items repaired.

## Configuration

`BepInEx/config/com.xman0922.unifiedautorepair.cfg`

| Section | Key | Default |
|---|---|---|
| Automatic repair | `Enabled` | `true` |

## Upgrading from Valheim Unified 0.41.0 or older

Valheim Unified 0.42.0 no longer repairs by itself. Install this mod to keep
automatic repair. Running it next to an older Valheim Unified is harmless: the
second pass simply finds nothing left to repair.

Source: <https://github.com/KYUHEON-LEE-94/valheim_autorepair>. MIT licensed.
