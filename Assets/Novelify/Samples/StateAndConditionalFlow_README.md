# State and conditional-flow sample

Unity creates `StateAndConditionalFlow/StateAndConditionalFlowDemo.novelgraph` after the editor reloads this project. You can also create or reveal it from **Tools > Novelify > Samples > Create State & Conditional Flow Demo**.

`StateAndConditionalFlow/ReactiveChoiceDemo.novelgraph` extends the same sample with four reactive choices: a hidden condition, a disabled 20-coin purchase, a once-only question, an always-available exit, and an explicit all-unavailable fallback. `BuyKeyTransaction.asset` spends the coins and grants the key as one atomic operation.

`StateAndConditionalFlow/PersistenceCheckpointDemo.novelgraph` uses Hoki to demonstrate two named Checkpoint nodes. The first is snapshot-only; the checkpoint after the irreversible choice writes the reserved `autosave` slot. The state graph also uses Hoki, while the reactive Choice graph uses Daisy, so every new example has a template character and a real portrait preview.

The graph demonstrates every node added by the variables and conditional-flow feature:

- Get Variable, Set Variable, and Modify Variable
- Compare, And, Or, and Not
- Branch with explicit True and False continuations
- Boolean, Integer, Float, and String variables

The accompanying definitions also show Profile, Story, and Call Local scopes. Profile and Story values live in each `NovelManager`'s `NovelStateStore`; Call Local values are isolated to one function invocation. Story slots capture story values, choices, visits, read lines, call frames, stage state and backlog history. Profile values are saved separately with `SaveProfile()`.

To try persistence, assign `PersistenceCheckpointDemo.novelgraph` to a configured `NovelManager`, reach either line after a Checkpoint, and call `SaveSlot("slot_1")`. Restart Play Mode and call `LoadSlot("slot_1")`. You can also add `NovelSaveSlotMenu` to an existing menu and assign its dropdown, input, buttons and status label for a ready-made slot/resume controller.
