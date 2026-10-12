# Ability cut-in illustrations

These 20 opaque portrait-format images are full-screen illustrations to show when a role uses an ability or, for designs without a coded ability, as visual concept art. They are separate from the small role sprites and from the empty night backgrounds.

`AbilityCutIns.asset` (in `Assets/Resources/NightScene/`, loaded at run time by `NightRoomView`, which plays the cut-in through `AbilityCutInView` when the player's night action is accepted) maps each role/design code to its sprite. `hasGameplayAbility` is true only for the seven roles whose night actions are currently defined in `GameConstants.cs` and `AbilityRules.cs`. A false entry is a visual concept and does not add a game mechanic.

| Design code | Illustration idea | Gameplay ability |
| --- | --- | --- |
| PIRATE_RAIDER | Cutlass and target selection | Attack |
| PIRATE_PARROT | Monocular observation | Watch action |
| CREW_CAPTAIN | Compass and faction clues | Investigate faction |
| CREW_DOCTOR | Glowing protective bandage | Protect |
| CREW_LOOKOUT | Spyglass and visitor tracks | Watch visitors |
| CREW_BOATSWAIN | Rigging knot seal | Block |
| CREW_DRUNK | Spectral identity token | Read corpse role |
| PIRATE_SPY | Stealth reconnaissance | Concept only |
| PIRATE_KRAKEN | Kraken emergence | Concept only |
| CONCEPT_SIREN | Siren song | Concept only |
| CREW_GUNNER | Cannon fire | Concept only |
| CREW_SAILOR | Rope repair and teamwork | Concept only |
| CREW_GHOST | Guiding spectral lantern | Concept only |
| CREW_MONKEY | Map trick | Concept only |
| CREW_MONKEY_ALT | Compass coin trick | Concept only |
| CONCEPT_GHOST_CAPTAIN | Spectral command | Concept only |
| PIRATE_COOK | Steaming stew | Concept only |
| THIRD_MERMAID_MALE | Tide shield | Concept only |
| THIRD_MERMAID | Pearl song | Concept only |
| PIRATE_COOK_FEMALE | Sizzling seafood skillet | Concept only |

Place the corresponding image in a full-stretch `Image` on a portrait UI canvas. Use `TryFind(roleCode, out sprite, out hasGameplayAbility)` to retrieve the art. The gameplay action outcome still comes from the server; the illustration should not be used to reveal hidden results.
