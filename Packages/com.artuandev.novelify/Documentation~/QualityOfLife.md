# Novelify editor quality-of-life tools

These additions install automatically when Unity compiles the package. Existing package files, graph assets, scenes, and runtime behavior are unchanged.

## Animation preview

1. Open a Novel Graph or Novel Function.
2. Double-click the title or empty body of a **Bounce Character**, **Shake Character**, or **Sway Character** node. You can also right-click the node and choose **Novelify > Preview Animation**.
3. The preview starts immediately without entering Play Mode or running a Start node.
4. Change Amplitude, Frequency, or Duration in your graph. The preview refreshes automatically and restarts with the new values.

The window provides **Play/Pause**, **Restart**, **Step 1/60 s**, **Loop**, **Speed**, and a **Time** slider. Dragging Time pauses playback for inspection. A zero-duration node plays continuously and its timeline expands as time advances. Loop repeats finite previews. Finite animations return to their authored transform at their duration, just as in the game.

**Game setup (optional)** accepts a scene NovelGraphRunner. When a runner uses the current story graph, it is selected automatically. The preview uses its portrait prefab and presentation reference resolution; otherwise it creates the same four-layer, 520 by 760 portrait layout used by generated presentation at 1920 by 1080. You can set the canvas resolution manually without a runner.

**Preview character** can supply a character when the node uses a dynamic character reference or has no assigned asset. **Preview emotion**, **Starting position (canvas)**, **Starting scale**, and **Starting rotation** let you reproduce the state you want to inspect. These are preview-only controls. **Reload Portrait** rebuilds the portrait after you change its sprites or prefab.

The motion is sampled through the existing `CharacterInfo` runtime animation method, including the same bounce, Perlin shake, sway, and duration behavior. The preview runs in an isolated temporary scene and never modifies scene portraits or graph values. Its sampling helper is compiled only in the editor.

Matching motion requires matching the character's starting transform, prefab, and canvas setup. A node alone does not contain earlier story state. Connected inputs use authoring defaults when the existing resolver can resolve them; runtime variables, computed values, and dynamically selected instances may differ. The window flags connected inputs. The preview does not run prior nodes or recreate the full game scene. Transform Characters keeps its existing double-click composer.

## Find nodes

Open **Window > Novelify > Graph Tools**, or press **Ctrl+Shift+F** on Windows / **Cmd+Shift+F** on macOS while a graph has focus. Right-clicking a node also offers **Novelify > Find Nodes and Check Graph**.

- Search node titles/types, dialogue text, string inputs/options, character asset/display names, or node IDs. Searches ignore letter case.
- Click a result to center its node in the graph and briefly highlight its border.
- Click **Preview** beside an animation result to open its preview.
- Use **Use Active** after interacting with another graph to switch the tools to it.

## Bookmarks

Click the star beside a search result, or right-click a node and choose **Novelify > Toggle Bookmark**. Enable **Bookmarks** in Graph Tools to filter to saved nodes. Click the star again to remove a bookmark.

Bookmarks persist across Unity restarts in local EditorPrefs. They are scoped to your project, graph, and node; they do not add data to graph assets or affect teammates.

## Graph checks

Click **Check Graph** in Graph Tools. It checks:

- Missing or multiple Start nodes in story graphs.
- Empty and duplicate Label names, and Jump nodes with no matching Label.
- Unconnected Enter and continuation ports, including branches and choice outputs.
- Character action nodes with no assigned Character or Character Reference.
- Zero, negative, or non-finite animation amplitude/frequency, and invalid durations.

Use **Locate** under a result to center the relevant node. Rerun the check after editing. These are advisory checks for common mistakes; disconnected ports can be intentional. Checks never repair, remove, connect, or rewrite anything.

## Copy node IDs

Right-click a node and choose **Novelify > Copy Node ID**, or click **Copy ID** beside a Graph Tools result. This copies the stable ID to your clipboard for debugging and searching.
