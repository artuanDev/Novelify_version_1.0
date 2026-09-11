# Top-Down Key Quest

This is a deliberately small “first game after importing Novelify” example. Open
`Scenes/TopDownKeyQuest.unity` and press Play.

## Controls

- Move with **WASD**.
- Press **E** near Daisy or the north door.
- Advance dialogue with **Space**, **Enter**, or left click.
- Click a dialogue choice to answer Daisy.

## What Novelify owns

- `Graphs/DaisyConversation.novelgraph` presents the persuasion choices, changes
  `HasKey`, and gives different dialogue after the key has already been received.
- `Graphs/DoorInteraction.novelgraph` checks `HasKey`, changes the door dialogue,
  lets Hoki answer, and emits `topdown.door.open` only on the successful branch.
- `Variables/HasKey.asset` is story-scoped state shared by both graphs.

Double-click either graph to inspect or edit it in Novelify's graph editor.

## What the ordinary game scripts own

- `TopDownPlayerController` handles WASD, proximity, and E/advance input.
- `TopDownNovelInteractable` starts a selected graph.
- `TopDownNovelDoor` turns the graph event into collider and animation changes.
- `TopDownQuestHUD` mirrors Novelify's `HasKey` variable in the HUD.

That separation is intentional: no game script contains dialogue, persuasion
branches, or the key condition. A new interaction can be made by authoring another
graph and assigning it to another `TopDownNovelInteractable`.
