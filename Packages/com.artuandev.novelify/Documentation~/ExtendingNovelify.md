# Extending Novelify

Novelify's editor authoring types live in `Novelify.Editor`; compiled graph data and
runtime interfaces live in `Novelify`. Keep authoring nodes and compilers in an
Editor-only assembly that references `Novelify.Editor` and `Novelify.Runtime`.
Put serializable runtime data and handlers in a runtime assembly that references
`Novelify.Runtime`. Do not make a runtime assembly reference Graph Toolkit or
`UnityEditor`.

## Custom flow node

The public `ActionNode` base provides Enter and Continue ports. A node can also
derive from Graph Toolkit's `Node` directly. Annotate it with `Node` and
`UseWithGraph` so it appears in a Novel Graph and Novel Function Graph.

```csharp
// Editor assembly
using System;
using Novelify;
using Novelify.Editor;
using Unity.GraphToolkit.Editor;

[Serializable, Node("Novelify/Custom", null, "Log Message")]
[UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
public sealed class LogMessageAuthoringNode : ActionNode
{
    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        base.OnDefinePorts(context);
        context.AddInputPort<string>("Message").WithDefaultValue("").Build();
    }
}

public sealed class LogMessageCompiler : INovelFlowNodeCompiler
{
    public bool CanCompile(INode node) => node is LogMessageAuthoringNode;

    public RuntimeNode Compile(INode node, NovelNodeCompileContext context) =>
        new RuntimeLogMessageNode { Message = context.PortValue<string>(node, "Message") };
}
```

```csharp
// Runtime assembly
using System;
using Novelify;
using UnityEngine;

[Serializable]
public sealed class RuntimeLogMessageNode : RuntimeNode
{
    public string Message;
}

public sealed class LogMessageInstaller : MonoBehaviour
{
    private IDisposable _registration;

    private void OnEnable()
    {
        var runner = GetComponent<NovelGraphRunner>();
        if (runner != null)
            _registration = runner.RegisterNodeHandler<RuntimeLogMessageNode>((context, node) =>
            {
                Debug.Log(node.Message);
                return NovelNodeExecutionResult.Continue();
            });
    }

    private void OnDisable() => _registration?.Dispose();
}
```

Attach the installer to the runner before starting a graph. Compiler classes with
public parameterless constructors are discovered automatically on domain reload.
No registration or importer fork is needed. If two compilers claim the same node,
import reports an error. `NodeID` and a default `out` continuation are assigned
by Novelify. For branches, set `NextNodeID` on your runtime node with
`context.Destination(node, "Your Output")`. Use `context.Expression` for
connected dynamic inputs; `PortValue` reads the authored fallback. `OptionValue`,
`Warning`, `Error`, and `DependsOn` are also available. Import diagnostics are
shown in Unity's Console.

## Custom value node

Implement `INovelValueNodeCompiler` in the Editor assembly. Its `Compile`
method receives the requested output port and returns a serializable
`RuntimeValueExpression`. Value nodes are excluded from the flow-node list
automatically. Use `context.Expression(node, "Input")` for child expressions;
this retains cycle detection. You can compose built-in expressions, or define a
custom expression subclass in the runtime assembly and register its evaluator:

```csharp
registration = runner.RegisterValueEvaluator<RuntimeMyExpression>((context, expression) =>
{
    RuntimeValue input = context.Evaluate(expression.Input);
    return RuntimeValue.From(input.FloatValue * 2f);
});
```

Register evaluators before playback and dispose their registrations when the
host is disabled. A value handler can also implement `INovelValueEvaluator` for
custom matching or set a priority when registering. Node and value handlers
run before Novelify's built-ins, so register narrowly scoped types. Runtime
node data and expression data are stored with Unity's `[SerializeReference]`;
types must remain available in builds and should retain stable names/assemblies
once shipped in saved graph assets.

## Built-in cinematic nodes

`Screen Flash` pulses a colored overlay. `Screen Shake` moves the selected camera
transform each frame, restoring its position after the effect or when playback
stops. Screen Space canvases on the same display also move, so backgrounds,
portraits, and dialogue visibly shake together. Both support live duration inputs and optional wait-for-completion. `Narration` is a speakerless localized
line with optional timed continuation. These nodes work in normal and function
graphs. Flash and shake use the built-in generated presentation; a fully custom
`INovelPresentation` should provide its own effects if it needs a different look.
