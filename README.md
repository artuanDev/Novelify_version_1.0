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
- Layered 2D portraits using body, eyes, facial details and mouth sprites.
- Optional blinking and mouth animation while text is revealed.
- Typewriter-style dialogue reveal with configurable characters-per-second speed.
- Per-character talking sounds with pitch variation.
- Optional audio clips played when individual nodes are displayed.
- Rich dialogue editing with bold, italic, colour and multiple text sizes.
- Wave and shake text effects with an animated editor preview.
- Automatic conversion from editor graphs to runtime dialogue data.
- Included sample graph, character asset, UI setup and playable scene.

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

- `Assets/Novelify/NovelGraphs/SelfReflection.novelgraph`.
- `Assets/Novelify/Samples/Characters/Hoki.asset`.
- A configured `NovelManager`.
- A TextMesh Pro dialogue interface.
- A choice button prefab and choice container.

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
- **Dialogue** accepts a `NovelCharacter` speaker and exposes portrait preview, emotion metadata, text timing and portrait animation options.
- **Choice** combines dialogue presentation with multiple player-selected branches.

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
- The portrait `Image` components for body, eyes, details and mouth.
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
├── NovelGraphs/               # Example .novelgraph assets
├── Prefabs/                   # Reusable UI prefabs
└── Samples/                   # Example character and scene
```

## Screenshots

# Novel Graph Dialogue example:

![Novel Graph Dialogue in an example:](Images/Dialogue_Runtime.PNG)

# Graph inspector rich text editor:
![Graph inspector for writing the dialogue:](Images/Rich_Text_Inspector.PNG)

# Novel Graph Choice example:
![Novel Graph Choice example:](Images/Choice_Runtime.PNG)

# Character asset example:
![Character Asset:](Images/Character_Asset.PNG)

## Roadmap

- [ ] Package the framework for easier reuse in other Unity projects.
- [ ] Add more runtime flow controls and dialogue events.
- [ ] Add custom character creator to preview emotions and examples and make tweaks to them.
- [ ] Add localization support.
- [ ] Add save, load and conversation history support.
- [ ] Add utility nodes that eases scenes with more than one character.
- [ ] Expand the sample content and documentation.

## Contributing

Suggestions, bug reports and improvements are welcome. Please open an issue with reproduction steps and the Unity version you are using. For code changes, create a feature branch and submit a pull request.

## License

No license has been specified for this repository yet. Add a license file before distributing Novelify or accepting external contributions.

## Contact

Antonio Mata Marín

- GitHub: [@artuanDev](https://github.com/artuanDev)
- LinkedIn: [Antonio Mata Marín](https://www.linkedin.com/in/antonio-mata-mar%C3%ADn-7a936a1aa/)

Project repository: [Novelify_version_1.0](https://github.com/artuanDev/Novelify_version_1.0)

<p align="right">(<a href="#readme-top">back to top</a>)</p>
