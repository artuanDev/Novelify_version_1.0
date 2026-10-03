# Changelog

## [0.1.0] - Unreleased

- Removed development-only screen-shake diagnostic probes, test sources and
  test assemblies so package installation does not require development assets.
- Initial embedded UPM package preserving existing asset GUIDs.
- Added an optional Input System adapter and an input-independent runtime assembly.
- Added a setup wizard that creates an art-free playable starter scene.
- Added stable dialogue localization keys, runtime translation fallback and a
  graph-scoped localization workspace with CSV import/export.
- Added the Novelify Free Use License 1.0, allowing commercial finished games
  while prohibiting paid redistribution of the plugin or projects containing it.
- Added a default VN player controller with backlog, auto, read-aware skip,
  persisted preferences and legacy/Input System controls.
- Added replaceable content and save providers, asynchronous chapter playback,
  and save restoration that hydrates unloaded chapters.
- Added public flow/value node compiler interfaces with automatic editor
  discovery, import diagnostics and runtime value evaluators.
- Added Screen Flash, Screen Shake and timed Narration nodes.
- Fixed Screen Shake to move the camera transform each frame and restore its pose,
  preserving dialogue during shake and flash effects. Camera selection supports
  an explicit runner camera, the canvas camera, and MainCamera fallback.
- Fixed visible Screen Shake in the starter scene by moving Screen Space canvas
  content alongside the camera, including separate backdrop and portrait
  canvases. Pixel displacement accounts for canvas scale and avoids nested
  offsets; positions restore on completion, replacement, or graph stop.
- Added Bounce Character, Shake Character, Sway Character, and Stop Character
  Animation, with per-instance targets, value inputs, optional durations and
  timed waits. Effects compose with transforms and clear on hide or graph stop.
- Fixed custom Transform easing curve assignment and import, retaining legacy
  graph options, and honor dialogue panel assignments made after initialization.
- Bound automatic execution through custom handlers and respect session stops
  from node-entry callbacks before executing further actions.
- Added compilation checks for all built-in node types and visual-effect
  regressions for timing, restoration, cancellation and nonblocking playback.
- Reworked generated Backlog/Preferences controls with readable modal layouts
  and visible Auto/Skip states. Skip now defaults to all text so fresh stories
  can use it immediately; read-only skipping remains optional.
- Source-formatted localization drafts now cover every detected entry in a
  chosen locale. Every dialogue and choice has inline source/translation rich
  editors and a speaker preview; edited translations save together in one pass.
  The IMGUI editor can also apply Wave and Shake effects.
- Fixed rich translation drafts being inadvertently marked NotEditable, and
  automatically locate a graph for older tables without a Story Graph reference.
- The Setup Wizard now generates an editable showcase backdrop, character and
  richer branching story instead of a plain empty scene.

The public package is not yet released. Sample media provenance and final
license review should be completed before publication.
