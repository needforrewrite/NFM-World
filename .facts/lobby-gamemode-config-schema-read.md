# The gamemode schema is read by the phase, never by a view slot

`GamemodeCatalog.ConfigProperties` / `ConfigFields` load the gamemode's assembly to read what it declares and unload it
again. That makes any call from a view expensive in a way the view cannot see.

The create dialog built its config rows inside a `Func<object?>` slot. That slot re-ran on every hover of the gamemode
buttons: the first run of `RenderScope.Bind` executed eagerly inside the enclosing computation's tracking scope, so the
slot subscribed to each button's `hovered` signal. Each re-run called `ConfigProperties` again — the plugin was loaded
and unloaded on every mouse move over an option.

Two independent fixes, both still load-bearing:

- **`RenderScope.Bind` (and `Sx.Show`/`For`/`Switch` first mounts) run under `Signal.UntrackedScope()`.** A bind's own
  compute belongs to its own effect; running it under the caller's computation leaks every child signal into the
  parent effect.
- **The phase owns the schema.** `LobbyPhase.OnSetGamemode` calls `GamemodeCatalog.ConfigFields(id)` once, publishes
  the plain `GamemodeConfigField` records on `UiState.Lobby.GamemodeFields`, and `LobbyView` renders only those. Nothing
  in the view names the catalog.

The gamemode picker and each config property are `Controls.Dropdown` (one dropdown per declared property), not a row of
buttons — a plugin can declare many options, and buttons did not fit the dialog.

Hit-testing note: the open popup (z 1000) was still unreachable until `Component.EscapeZ` stopped counting hidden
descendants, because the stage select's closed `TextEditContextMenu` (z 1000, `display:none`) tied the stage column to
the popup's z and the later-sibling tie-break handed it the hover. `ZIndexHitTestTests` covers that.
