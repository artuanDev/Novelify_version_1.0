# Novelify playable showcase

This package sample is a guide, not a static `.unity` scene. The Setup Wizard
creates an editable scene and story directly in your project:

1. Open **Window > Novelify > Setup Wizard**.
2. Select **Visual Novel** for the full two-path showcase or **Dialogue** for a
   one-line starter. Leave the source locale at `en` and draft locale at `es`
   to see the localization workflow.
3. Click **Create starter scene** and press Play. Click or press Space to advance.
   The top-right controls open Backlog and Preferences or toggle Auto and Skip.
   Skip works on unread lines by default; choose **Read text only** in Preferences
   if you want it to stop at new dialogue.
4. Double-click `Starter.novelgraph` in the created folder to change the story.
5. Open **Window > Novelify > Localization Workspace**. Every detected text
   entry has an `es` draft copied from the source, including rich-text markup.
   Click **Edit rich text** to translate and review it.

The new scene contains an editable vector-style backdrop, a named character,
rich dialogue, a choice, and a brief flash/shake beat. Replace the backdrop or
UI freely; the runner and graph are not tied to this sample artwork.
