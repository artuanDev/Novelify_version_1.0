# State and conditional-flow sample

Unity creates `StateAndConditionalFlow/StateAndConditionalFlowDemo.novelgraph` after the editor reloads this project. You can also create or reveal it from **Tools > Novelify > Samples > Create State & Conditional Flow Demo**.

The graph demonstrates every node added by the variables and conditional-flow feature:

- Get Variable, Set Variable, and Modify Variable
- Compare, And, Or, and Not
- Branch with explicit True and False continuations
- Boolean, Integer, Float, and String variables

The accompanying definitions also show Profile, Story, and Call Local scopes. Profile and Story values live in each `NovelManager`'s `NovelStateStore`; Call Local values are isolated to one function invocation. Persistence is intentionally not part of this feature yet.
