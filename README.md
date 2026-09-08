<a id="readme-top"></a>

<div align="center">

# Novelify

### A node-based dialogue and narrative system for Unity

Create branching conversations visually with custom Graph Toolkit nodes, reusable character assets, rich text, animated portraits, audio feedback and runtime dialogue presentation.

[![Unity](https://img.shields.io/badge/Unity-6000.6.0f1-black?logo=unity)](https://unity.com/)
[![URP](https://img.shields.io/badge/Render%20Pipeline-URP-5562ea)](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/InstallURPIntoAProject.html)
[![C%23](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Status](https://img.shields.io/badge/Status-Prototype-orange)](https://github.com/artuanDev/Novelify_version_1.0)

</div>

## About

Novelify is a visual dialogue framework built for Unity. Stories are authored inside custom `.novelgraph` assets using Unity Graph Toolkit and then imported into runtime data that can be played by the `NovelManager`.

The editor graph and runtime presentation are intentionally separated: graph nodes are used for authoring, while the importer generates a `RuntimeNovelGraph` that can be consumed by normal Unity components during play mode.

This repository is currently a Unity project containing the Novelify framework, its editor tooling and a working sample scene.

Please, note this is just the bare basics, I am working right now on extending the tool and adding everything it is 
missing for the moment, you can get an idea on what to expect from this tool in the "Roadmap".

![Novelify graph editor](Images/Graph.PNG)

## Features

- Visual narrative authoring with custom Graph Toolkit nodes.
- Start, End, Simple Dialogue, Dialogue and Choice nodes.
- Branching conversations with dynamically generated choice buttons.
- Reusable `NovelCharacter` ScriptableObjects.
- Multiple characters on a dedicated stage, with optional instance IDs for additional copies.
- Character-specific Transform nodes with normalized positioning, rotation, scale, easing and parallel animation.
- Reusable `.novelfunction` graphs with typed input/output ports and isolated runtime call scopes.
- Float and Vector2 Add, Subtract, Multiply and Divide value nodes.
- Split Novel Character data with live normalized/canvas position, rotation, scale and portrait layers.
- Label and Jump flow nodes for explicit non-local story routing.
- Show/Hide Character, Hide All Characters, Set Character Emotion, Wait, Dialogue Event and Stop Sound nodes.
- A custom character creator with layered emotion, blinking and talking previews.
- Emotion-aware Dialogue and Choice node previews in both `.novelgraph` and `.novelfunction` editors.
- Layered 2D portraits using body, eyes, facial details and mouth sprites.
- Optional blinking and mouth animation while text is revealed.
- Typewriter-style dialogue reveal with configurable characters-per-second speed.
- Per-character talking sounds with pitch variation.
- Optional audio clips played when individual nodes are displayed.
- Rich dialogue editing with bold, italic, colour and multiple text sizes.
- Wave and shake text effects with an animated editor preview.
- Automatic conversion from editor graphs to runtime dialogue data.
- Included sample graphs, character assets, UI setup and playable scene.
- A ready-to-use layered template character with sprites for all ten supported emotions.

## How It Works

| Stage | Responsibility |
| --- | --- |
| Graph authoring | Create and connect nodes in a `.novelgraph` asset. |
| Character data | Store a speaker's name, portrait layers, voice clip and animation timing in a `NovelCharacter` asset. |
| Import | `NovelGraphImporter` converts the editor graph into a `RuntimeNovelGraph`. |
| Runtime | `NovelManager` displays dialogue, reveals text, plays audio, animates portraits and routes choices. |

## Requirements

- Unity `6000.6.0f1`.
- Unity Graph Toolkit, compatible with the selected Unity 6 installation.
- Git LFS, required for image and other binary assets.
- Universal Render Pipeline.
- Input System.
- TextMesh Pro.

If the Graph Toolkit package is not available in the project after opening it, install the compatible package through Unity's Package Manager before opening or creating a Novelify graph.

## Getting Started

### 1. Clone the Repository

Install Git LFS once on your machine, then clone the project:

```bash
git lfs install
git clone https://github.com/artuanDev/Novelify_version_1.0.git
cd Novelify_version_1.0
git lfs pull
```

Open the project in Unity Hub using **Unity 6000.6.0f1**.

### 2. Open the Sample

Open:

```text
Assets/Novelify/Samples/Scenes/TestScene.unity
```

Press **Play**. The sample scene uses:

- `Assets/Novelify/Samples/NovelGraphs/TemplateEmotions.novelgraph`.
- The `Template`, `Hoki` and `Daisy` character assets from `Assets/Novelify/Samples/Characters/`.
- A configured `NovelManager`.
- A TextMesh Pro dialogue interface.
- A choice button prefab and choice container.

`TemplateEmotions` demonstrates layered portrait changes for Neutral, Happy, Sad, Angry, Surprised, Afraid, Disgusted, Confused, Embarrassed and Excited expressions. It also shows blinking, talking animation and rich-text effects in a short conversation. `ExampleStory.novelgraph` remains available as an additional graph-authoring example.

A left mouse click advances the current dialogue. During text reveal, the first click completes the line; the next click advances. Choice nodes are advanced through their generated UI buttons.

## Creating a Dialogue Graph

1. In the Project window, right-click and choose **Create > Novelify > Novel Graph**.
2. Open the new `.novelgraph` asset.
3. Add a **Start** node and an **End** node.
4. Add **Dialogue** or **SimpleDialogue** nodes.
5. Connect the flow ports from Start through the conversation and finally to End.
6. Add a **Choice** node when the player should select a branch.
7. Set the choice count, enter each choice's text and connect each output to its destination node.
8. Save the graph so Unity can import its runtime representation.

### Dialogue Nodes

- **SimpleDialogue** displays a line without requiring a character asset.
- **Dialogue** accepts a `NovelCharacter` speaker and exposes an emotion-aware portrait preview, emotion metadata, text timing and portrait animation options.
- **Choice** combines dialogue presentation with multiple player-selected branches and the same emotion-aware speaker preview.

Dialogue and Choice previews update when their **Speaker** or **Emotion** changes. The preview displays the selected expression's layered sprites and emotion name, using the same fallback to the character's default layers as runtime playback. This works in main Novel Graphs and reusable Novel Function subgraphs.

## Creating and Using Novel Functions

1. In the Project window, choose **Create > Novelify > Novel Function**.
2. Open the `.novelfunction` asset and add a **Start**, the reusable actions/value nodes, and an **End**.
3. In the Blackboard, create variables with kind **Input** for values supplied by the caller and **Output** for values returned to it. Drag those variables into the function graph and wire them normally.
4. Save the function. Open any `.novelgraph`, open the node library, and select the function asset (or drag the asset into the graph) to create its callable node.
5. Connect its reserved **Enter** and **Continue** ports to story flow, then connect its typed data ports.

`Enter` and `Continue` are maintained automatically and are reserved for function flow. Every call receives its own input/output scope, so the same function can safely be used for different characters and nested calls. Outputs are evaluated when the function reaches End, which means a live position output observes the character after the function's actions have completed.

For a reusable movement function, add a `NovelCharacter` Input and a `Vector2` Input, feed them into **Transform Speaker Portrait**, then optionally expose the result through an Output. The system does not special-case movement: function ports can use any supported Graph Toolkit variable type, while runtime value composition currently supports float, integer, bool, string, Vector2 and Unity object references.

## Creating a Character

Create a character asset through:

```text
Create > Novelify > Character
```

Assign the portrait layers and optional audio:

| Field | Purpose |
| --- | --- |
| Speaker Name | Name displayed above the dialogue. |
| Portrait Body | Main portrait layer. |
| Portrait Eyes | Default eye layer. |
| Portrait Eyes Closed | Sprite used during blinking. |
| Portrait Face Details | Additional facial details. |
| Portrait Mouth | Default mouth layer. |
| Portrait Mouth Open | Mouth layer used during text reveal. |
| Talk Sound | Sound played while letters are displayed. |

The character asset also contains timing controls for blinking, mouth animation, voice pitch variation and graph preview framing.

Open **Window > Novelify > Character Creator** to create, duplicate or edit a character with a live layered preview. Select a preview emotion and enable talking/blinking to audition the sprites and timing. Under **Emotions**, add one entry per emotion and assign its alternate layers; empty layers inherit the default character sprites. Dialogue, Choice and Set Character Emotion nodes use these expressions at runtime. Asset edits support Unity's normal Undo; use **Save** to save the selected character.

For a complete reference, inspect or duplicate `Assets/Novelify/Samples/Characters/Template.asset`. Its matching sprites are under `Assets/Novelify/Samples/Portraits/Template/`, and it includes configured layers for every supported emotion.

### Multiple Characters and Movement

Assign **Portrait Prefab** on `NovelManager`. Its `CharacterInfo` component exposes Body, Eyes, Details and Mouth image references. The supplied prefab's named layers are detected automatically. Empty sprite layers are hidden and portrait images do not intercept clicks.

Characters have no fixed slot limit. Each character asset gets its own default instance, even when two assets share a speaker name. To show additional copies of one asset, use different **Instance ID** values. Use the same character asset and ID in Dialogue, Choice and character utility nodes to address the same copy; a blank ID always means the default copy. Legacy Character wires pass only the asset. Use **Make Novel Character Reference** and the **Character Reference** ports to carry the asset and instance ID together; **Split Novel Character Reference** separates them again when needed.

Use **Show Character** to place a character before their first line, or connect a character to **Transform Speaker Portrait > Character**. Transform creates that character if necessary and reuses it thereafter. You can assign the asset directly, connect a Character variable, or connect a Dialogue node's **Current Speaker** output.

- **Coordinate Space:** choose normalized (`(-1,-1)` bottom-left to `(1,1)` top-right) or canvas anchored units. Show Character defaults to legacy canvas units; Transform Speaker Portrait defaults to normalized.
- **Position input:** a Vector2 interpreted in the selected coordinate space.
- **Margin input:** expands each bound in canvas units. Use at least half the portrait's relevant dimension to move it completely beyond that edge.
- **Rotation input:** absolute target Z rotation in degrees.
- **Scale input:** absolute target local X/Y scale.
- **Relative:** interpret normalized X/Y as a displacement from the current position; rotation and scale remain absolute.
- **Animate Transform:** animate position, rotation and scale together. Disabled applies them instantly.
- **Duration:** transform time in real-time seconds. Zero applies instantly.
- **Ease In Out:** smooth acceleration/deceleration; disabled uses constant speed.
- **Wait For Completion:** pause story flow until the transform finishes. Disable to continue to dialogue or animate multiple characters in parallel.

For example: `Start → Transform Speaker Portrait (Hoki, Position=(-0.65, 0)) → Dialogue → End`. A new transform on the same instance replaces its previous animation from the current position, rotation and scale. Legacy Translate nodes remain readable and retain their original canvas-unit positioning. New Transform nodes default to normalized coordinates but expose the choice explicitly.

Use **Set Facing** for deterministic left/right orientation; it applies the requested sign to the absolute X scale, so executing it repeatedly does not toggle the portrait. **Flip Character** remains an explicit axis toggle for graphs that need that behavior.

Use **Split Novel Character** when a graph needs the selected character's data. It returns the character asset, speaker name, current portrait sprites, live normalized position, live canvas position, rotation and scale. Supply the same **Instance ID** used by the character nodes when reading a non-default copy. The normalized position connects directly to Transform Speaker Portrait and Vector2 math nodes.

Math nodes are non-flow expressions and do not execute on their own. Float and Vector2 versions of **Add**, **Subtract**, **Multiply** and **Divide** can be chained into action inputs or function outputs. Vector2 multiply/divide operate component-by-component; division by zero produces zero for that component.

**Character Container** is an optional parent outside the dialogue panel. When omitted, the manager creates a separate stage under **Canvas Dialogue**'s canvas so Wait/audio/movement nodes can hide dialogue without hiding the cast. To reuse scene-authored characters, place them under an assigned Character Container with their `CharacterInfo` asset and instance ID set. **Hide Characters On End** controls whether the cast is hidden when the story ends.

### Utility Nodes

| Node | Behavior |
| --- | --- |
| Show Character | Creates/reveals one instance and sets its position and emotion. |
| Hide Character | Hides one instance without deleting it; showing it again reuses it. |
| Hide All Characters | Hides the entire stage. |
| Set Character Emotion | Applies the selected expression, creating the character if needed. |
| Wait | Pauses flow for dialogue-clock seconds; dialogue clicks cannot skip it. |
| Set Facing | Sets left/right orientation idempotently. |
| Dialogue Event | Sends Event Name to `NovelManager.OnDialogueEvent`, then continues. Connect listeners in the manager inspector. |
| Stop Sound | Stops the audio channel used by Play Sound nodes. |
| Label | Declares a unique named flow destination and continues through its output. |
| Jump | Continues immediately at the Label node with the matching name. |

Connect the **Enter/Continue** flow ports to execute these nodes. Character data wires select the target and do not execute nodes on their own. Place a Dialogue, Choice or Wait after automatic nodes to hold the scene before End. Label matching is case-insensitive; missing, empty and duplicate labels produce import warnings. Connect matching Label fields to the same string variable when you want one rename point.

Each Continue output has one story destination: connect `Dialogue → Translate → Dialogue` in sequence. Turn off Translate's **Wait For Completion** to keep moving during the following line. Use Choice outputs for alternative story paths.

The dialogue panel is hidden with a CanvasGroup, keeping its GameObject active. This allows the manager and audio sources to live inside the panel without being disabled between nodes. Play Sound continues across dialogue, waits and movement until Stop Sound or the story ends.

`NovelManager.TimeMode` defines the dialogue clock. **Unscaled** is the default and keeps text reveal, waits, portrait transitions, blinking and mouth animation running while gameplay is paused. Choose **Scaled** when pausing gameplay should also pause the conversation.

Imported runtime graphs contain a stable graph ID, stable authored node IDs, a content hash and a schema version. Player builds automatically bake `Resources/NovelGraphCatalog.asset`, which resolves graph IDs without editor-only asset lookup. Double-click the catalogue asset (or use **Window > Novelify > Graph Catalogue**) for searchable graph previews, flow diagnostics and source navigation. Refresh it with **Tools > Novelify > Rebuild Runtime Graph Catalog**; builds also refresh it automatically.

## Rich Text and Text Effects

Dialogue text can be formatted from the custom inspector. Select text and use the toolbar to apply:

- Bold and italic formatting.
- Small, normal, large and extra-large text sizes.
- Colour.
- Wave motion.
- Shake motion.

At runtime, `NovelManager` enables TextMesh Pro rich text automatically. The `NovelTextEffects` component animates ranges marked with the wave or shake effect.

## Using NovelManager in Your Own Scene

Add a `NovelManager` component to a GameObject and assign:

- The imported `RuntimeGraph` from your `.novelgraph` asset.
- A TextMesh Pro object for `DialogueText`.
- A TextMesh Pro object for `SpeakerNameText`.
- A **Portrait Prefab** with `CharacterInfo` and layered portrait Images, plus **Canvas Dialogue** or an explicit **Character Container**.
- A dialogue panel and a choices panel.
- A `Button` prefab and a container transform for generated choices.
- Optional audio sources for talking sounds and node sounds.

The manager builds its node lookup at startup and begins at the graph's Start node. Normal dialogue advances with a left mouse click; Choice nodes create their buttons at runtime.

## Project Structure

```text
Assets/Novelify/
├── Editor/
│   ├── Graph/                 # Graph, nodes, inspectors and visual styles
│   └── NovelGraphImporter.cs  # Converts editor graphs to runtime data
├── Runtime/
│   ├── NovelManager.cs        # Dialogue presentation and flow
│   ├── NovelCharacter.cs      # Character ScriptableObject
│   ├── RuntimeNovelGraph.cs   # Runtime graph data
│   └── NovelTextEffects.cs    # Wave and shake text animation
├── Prefabs/                   # Reusable UI prefabs
└── Samples/
    ├── Characters/            # Daisy, Hoki and the layered Template character
    ├── NovelGraphs/           # ExampleStory and the playable emotion showcase
    ├── Portraits/             # Layered sample portrait sprites
    ├── Prefabs/               # Sample UI and character prefabs
    └── Scenes/                # TestScene playable demo
```

## Screenshots

### Novel Graph Dialogue example:

![Novel Graph Dialogue in an example:](Images/Dialogue_Runtime.PNG)

### Graph inspector rich text editor:
![Graph inspector for writing the dialogue:](Images/Rich_Text_Inspector.PNG)

### Novel Graph Choice example:
![Novel Graph Choice example:](Images/Choice_Runtime.PNG)

### Character asset example:
![Character Asset:](Images/Character_Asset.PNG)

## Roadmap

- [ ] Package the framework for easier reuse in other Unity projects.
- [x] Add initial runtime flow controls and dialogue events (Wait and Dialogue Event nodes).
- [x] Add custom character creator to preview emotions and examples and make tweaks to them.
- [ ] Add localization support.
- [ ] Add save, load and conversation history support.
- [x] Add utility nodes that ease scenes with more than one character.
- [x] Add a layered template character and a playable emotion showcase.
- [ ] Continue expanding the sample content and documentation.

## Contributing

### Tests

Open Unity's **Window > General > Test Runner**. Run `Novelify.Editor.Tests` in Edit Mode and `Novelify.Runtime.Tests` in Play Mode. These cover independent character instances, expression fallback, graph import and character connections, smooth and simultaneous movement, wait cancellation, event callbacks and stage visibility.

Suggestions, bug reports and improvements are welcome. Please open an issue with reproduction steps and the Unity version you are using. For code changes, create a feature branch and submit a pull request.

## Contact

Antonio Mata Marín

- GitHub: [@artuanDev](https://github.com/artuanDev)
- LinkedIn: [Antonio Mata Marín](https://www.linkedin.com/in/antonio-mata-mar%C3%ADn-7a936a1aa/)
- Portfolio: [Antonio Mata Marín — Portfolio](https://portfoliowebsite-ecru-six.vercel.app/#/portfolio)

Project repository: [Novelify_version_1.0](https://github.com/artuanDev/Novelify_version_1.0)

<p align="right">(<a href="#readme-top">back to top</a>)</p>
