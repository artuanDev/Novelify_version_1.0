# Novelify

Novelify provides visual graph authoring, a dialogue runner, generated Unity UI,
character staging, choices, save data and localization tables for Unity 6000.6+.

## Install

Add this package with Unity Package Manager using a Git URL with the package path:

```text
https://github.com/artuanDev/Novelify_version_1.0.git?path=/Packages/com.artuandev.novelify
```

The package requires `com.unity.ugui`. The Input System adapter is compiled only
when `com.unity.inputsystem` is present. The sample project uses additional
packages, including URP; those are not required by Novelify.

## First conversation

Open **Window > Novelify > Setup Wizard**. Select **Dialogue** or **Visual Novel**
and click **Create starter scene**. Press Play. Click to advance the text and
select a choice when one appears. The wizard creates an editable `.novelgraph`,
a scene and a localization table beneath `Assets`.

The wizard creates an editable art-free backdrop, a character, rich-text story
beats, and a localization table with source-formatted translation drafts. The
runner creates dialogue UI and choices at Play time. The runtime-only hierarchy
object is named **Story UI (runtime)**; it is not story content to edit. Existing
projects can use `NovelGraphRunner` directly with their own input and UI.

`Assets/NovelifyGenerated/Resources/NovelGraphCatalog.asset` is a separate
auto-maintained lookup for graphs and save restoration. It is not the scene UI
or an authoring folder; you normally do not edit it.

## Default player controls

The wizard adds `NovelPlayerController` beside `NovelGraphRunner`. It generates
Backlog, Auto, Skip and Settings controls. Settings for text speed, auto delay,
talk volume and read-only skipping are saved locally with `PlayerPrefs`. Skip
works on unread dialogue by default and stops at choices. If **Read text only**
is enabled, it also stops when it reaches a line not marked
read in the current story state or restored save. Backlog shows the bounded
dialogue history.

Click/tap or press Space/Enter to advance. Backspace opens Backlog, P opens
Preferences, A toggles Auto, S toggles Skip, and Escape closes overlays. On
gamepads, south advances, east opens Backlog, west toggles Auto, north toggles
Skip, and Start opens Preferences. The optional
Input System adapter is added automatically when that package is installed;
otherwise the controller uses Unity's legacy input manager if enabled. For a
custom game UI, use `NovelGraphRunner` without the controller and call
`runner.Session.Advance()` yourself.
Backlog supports drag/wheel scrolling, Page Up/Page Down, gamepad D-pad, and
on-screen Up/Down buttons.

## Content and save providers

Use `runner.Session.UseContentProvider(INovelContentProvider)` for Addressables,
remote chapters or another graph source. `PlayChapterAsync(id, token)` waits
for a graph and only starts the latest requested chapter. The built-in
`NovelResourcesContentProvider` loads graphs with `Resources.LoadAsync`; call
`Register(graphID, resourcesPath)` for every chapter you intend to restore from
a save on a fresh launch. `runner.Session.LoadAsync(slotID, token)` hydrates
saved graph references before restoring.

```csharp
var chapters = new NovelResourcesContentProvider();
chapters.Register(openingGraphID, "Chapters/Opening"); // Assets/.../Resources/Chapters/Opening.novelgraph
runner.Session.UseContentProvider(chapters);
bool started = await runner.Session.PlayChapterAsync(openingGraphID);
```

Call these APIs from Unity's main thread. A provider should honor cancellation
and return the compiled `RuntimeNovelGraph`, not a raw text or editor graph.
The runtime asset catalog is still used for saved character and variable IDs;
replacing chapter loading does not replace those asset references.

Use `runner.Session.UseSaveProvider(INovelSaveProvider)` to replace the default
atomic JSON file storage. The interface covers slots and profile data; deferred
checkpoint saves and the optional save-slot UI use the selected provider too.
The provider methods are synchronous, while chapter loading is asynchronous.
For component-based providers, assign implementing `MonoBehaviour` components
to the runner's **Content Provider Behaviour** or **Save Provider Behaviour**
fields in the Inspector. Disable **Auto Play On Start** on `NovelPlayerController`
when another script will call `PlayChapterAsync`.

## Extend authoring and presentation

Create Graph Toolkit nodes in an Editor assembly and implement
`INovelFlowNodeCompiler` or `INovelValueNodeCompiler` to compile them without
editing Novelify's importer. At runtime, register a node handler or value
evaluator on `NovelGraphRunner`. The full example and API notes are in
[Extending Novelify](Documentation~/ExtendingNovelify.md).

Built-in **Screen Flash**, **Screen Shake**, and **Narration** nodes are available
in the graph menu. Flash and shake support connected duration values and can
either wait for the effect or let the story continue immediately. Narration is a
speakerless, localizable line with optional timed continuation.

## Translate text

Open **Window > Novelify > Localization Workspace**. Select a localization
table and its Story Graph, set a language code such as `es`, then click
**Sync keys**. Only that graph's dialogue, choices and disabled
reasons appear. Every line shows its speaker portrait and places the current
story text beside an editable translation, using the graph's rich-text editor.
Edit dialogue and choices inline, then use **Save all translations** once. Sync
creates a draft in that locale for every entry, copying all source rich-text
tags (bold, italic, size, color and effects). Reviewed translations are preserved
by later syncs. CSV export/import remains available for external workflows.

For example, a graph line containing `Where should we go?` stays in the graph.
Its table entry can contain Spanish text `¿Adónde vamos?` under locale `es`.
The same stable key identifies both versions, even if the graph line is edited.
The wizard assigns its table to the starter runner automatically; select that
runner before opening the workspace to pick up the table automatically.

Assign the table to a runner and set its `Locale` before playing. The original
graph text is used for missing translations. Keys are based on stable graph and
node IDs, so editing text does not invalidate saved dialogue history.

## License

Novelify is free to use in commercial games and applications. You may not sell
or bundle the installable plugin, modified plugin, or a Unity project containing
its files as a paid development deliverable. A paid game build containing
compiled Novelify runtime code is allowed. See [LICENSE.md](LICENSE.md) for the
complete terms. This is a custom source-available license, not an open-source
license. Third-party components keep their own licenses.

## Limitations

The current text renderer uses Unity UI Text and needs fonts with glyphs for each
target language. Runtime locale changes apply from the next line. Voice and font
variants, plural rules, RTL layout and grapheme-aware reveal are future work.

