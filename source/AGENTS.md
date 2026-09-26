## Save-game compatibility

Every serialized instance field on a `StateObject` is permanent save-game schema.

Never delete, rename, move, repurpose, change the type of, or add `[NonSerialized]` to such a field without explicit confirmation from the user in the current conversation. This applies even when the field appears unused, redundant, obsolete, or unreleased.

When a serialized field becomes unused, preserve the declaration and append exactly:

`// [unused]`

Example:

`public int NPCFoodProduction; // [unused]`

Before performing cleanup in a `StateObject`, check every affected field for serialization. If removing or altering serialized state appears necessary, stop and ask for confirmation.

## Graceful failure

Avoid throwing exceptions for runtime, resource, or data edge cases. An unhandled exception crashes the game and is not a useful outcome for the player.

Prefer a safe, graceful fallback such as returning without changing state, preserving the affected object, using a valid default, or skipping the unavailable operation. Do not replace a possible hang, missing value, malformed resource, or other recoverable condition with an exception merely to make the failure explicit.
