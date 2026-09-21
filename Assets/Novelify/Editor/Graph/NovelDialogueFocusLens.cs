using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    /// <summary>
    /// Presents the real rich-dialogue editor in a window-space overlay while
    /// it has focus. Graph zoom therefore no longer makes dialogue authoring
    /// unreadable, while every existing editing and styling control keeps its
    /// original binding and behaviour.
    /// </summary>
    [InitializeOnLoad]
    internal static class NovelDialogueFocusLens
    {
        private const double ScanIntervalSeconds = 0.5d;
        private static readonly List<LensController> Controllers = new();
        private static double _nextScanTime;

        static NovelDialogueFocusLens()
        {
            EditorApplication.update += ScanOpenWindows;
        }

        private static void ScanOpenWindows()
        {
            if (EditorApplication.timeSinceStartup < _nextScanTime)
            {
                return;
            }

            _nextScanTime = EditorApplication.timeSinceStartup + ScanIntervalSeconds;

            for (int index = Controllers.Count - 1; index >= 0; index--)
            {
                if (!Controllers[index].IsAlive)
                {
                    Controllers[index].Dispose();
                    Controllers.RemoveAt(index);
                }
            }

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window is not IGraphWindow graphWindow ||
                    graphWindow.Graph is not NovelGraph &&
                    graphWindow.Graph is not NovelFunctionGraph)
                {
                    continue;
                }

                VisualElement root = window.rootVisualElement;
                if (root?.panel == null)
                {
                    continue;
                }

                LensController controller = Controllers.Find(candidate =>
                    ReferenceEquals(candidate.Window, window));
                if (controller == null)
                {
                    controller = new LensController(window, root);
                    Controllers.Add(controller);
                }

                controller.ScanForEditors();
            }
        }

        private sealed class LensController : IDisposable
        {
            private const string HookClass = "novelify-dialogue-lens-hook-v1";
            private const float PreferredWidth = 560f;
            private const float PreferredHeight = 600f;
            private const float WindowMargin = 12f;
            private const float NodeGap = 18f;
            // At 72% or below the graph is in overview territory: several
            // dialogue nodes can be read at once, but their editors are too
            // small for comfortable authoring. A little hysteresis prevents
            // the lens flickering when a wheel step lands on the boundary.
            private const float OpenAtOrBelowScale = 0.72f;
            private const float CloseAboveScale = 0.78f;

            private readonly VisualElement _root;
            private readonly VisualElement _lens;
            private readonly VisualElement _pointer;
            private readonly ScrollView _scroll;
            private readonly Label _context;
            private readonly EventCallback<PointerDownEvent> _outsidePointerDown;
            private readonly EventCallback<KeyDownEvent> _rootKeyDown;
            private readonly IVisualElementScheduledItem _positionSchedule;

            private VisualElement _activeNodeView;
            private ReparentSlot _portraitSlot;
            private ReparentSlot _dialogueSlot;
            private int _openRequestVersion;

            public EditorWindow Window { get; }
            public bool IsAlive => Window != null && _root?.panel != null;

            public LensController(EditorWindow window, VisualElement root)
            {
                Window = window;
                _root = root;

                _lens = new VisualElement
                {
                    name = "novelify-dialogue-focus-lens",
                    pickingMode = PickingMode.Position
                };
                _lens.AddToClassList("novelify-dialogue-focus-lens");
                _lens.style.display = DisplayStyle.None;

                var header = new VisualElement();
                header.AddToClassList("novelify-dialogue-focus-lens__header");

                var title = new Label("FOCUSED DIALOGUE");
                title.AddToClassList("novelify-dialogue-focus-lens__title");
                header.Add(title);

                _context = new Label("Full-size authoring view");
                _context.AddToClassList("novelify-dialogue-focus-lens__context");
                header.Add(_context);

                var close = new Button(Close) { text = "×" };
                close.tooltip = "Close the focused dialogue view (Esc)";
                close.AddToClassList("novelify-dialogue-focus-lens__close");
                header.Add(close);
                _lens.Add(header);

                _scroll = new ScrollView(ScrollViewMode.Vertical);
                _scroll.AddToClassList("novelify-dialogue-focus-lens__scroll");
                _lens.Add(_scroll);

                var hint = new Label(
                    "Edit here normally. Click outside or press Esc to return to the node.");
                hint.AddToClassList("novelify-dialogue-focus-lens__hint");
                _lens.Add(hint);

                _pointer = new VisualElement { pickingMode = PickingMode.Ignore };
                _pointer.AddToClassList("novelify-dialogue-focus-lens__pointer");
                _lens.Add(_pointer);

                _root.Add(_lens);

                _outsidePointerDown = OnRootPointerDown;
                _rootKeyDown = OnRootKeyDown;
                _root.RegisterCallback(_outsidePointerDown, TrickleDown.TrickleDown);
                _root.RegisterCallback(_rootKeyDown, TrickleDown.TrickleDown);
                _positionSchedule = _lens.schedule.Execute(UpdatePosition).Every(50);
                _positionSchedule.Pause();
            }

            public void Dispose()
            {
                Close();
                _positionSchedule?.Pause();
                _root?.UnregisterCallback(_outsidePointerDown, TrickleDown.TrickleDown);
                _root?.UnregisterCallback(_rootKeyDown, TrickleDown.TrickleDown);
                _lens?.RemoveFromHierarchy();
            }

            public void ScanForEditors()
            {
                HookEditorsRecursively(_root);
                if (IsOpen)
                {
                    _lens.BringToFront();
                }
            }

            private bool IsOpen => _activeNodeView != null;

            private void HookEditorsRecursively(VisualElement element)
            {
                if (element.name == "rich-dialogue-preview" &&
                    !element.ClassListContains(HookClass) &&
                    FindAncestorView(element, "NodeView") != null)
                {
                    element.AddToClassList(HookClass);
                    VisualElement preview = element;
                    preview.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button != 0)
                        {
                            return;
                        }

                        // Let the editor place its caret before moving the same
                        // controls into the window-space lens.
                        ScheduleOpen(preview);
                    }, TrickleDown.TrickleDown);

                    TextField input = FindAncestorNamed(
                            preview,
                            "rich-dialogue-editor")?
                        .Q<TextField>("rich-dialogue-keyboard-input");
                    input?.RegisterCallback<FocusInEvent>(_ =>
                        ScheduleOpen(preview));
                }

                // The active editor is reparented under the lens. It is already
                // hooked, so this traversal remains cheap and cannot recurse.
                for (int index = 0; index < element.hierarchy.childCount; index++)
                {
                    HookEditorsRecursively(element.hierarchy[index]);
                }
            }

            private void ScheduleOpen(VisualElement preview)
            {
                int requestVersion = ++_openRequestVersion;
                // Wait until the pointer/focus dispatch has completed before
                // changing hierarchy. This preserves Graph Toolkit's property
                // binding while retaining the original automatic focus lens.
                _root.schedule.Execute(() =>
                {
                    if (requestVersion == _openRequestVersion)
                        OpenFor(preview);
                }).ExecuteLater(1);
            }

            private void OpenFor(VisualElement preview)
            {
                if (preview?.panel == null)
                {
                    return;
                }

                if (IsOpen && IsDescendantOf(preview, _lens))
                {
                    return;
                }

                VisualElement dialogue = FindAncestorNamed(preview, "rich-dialogue-editor");
                VisualElement nodeView = FindAncestorView(dialogue, "NodeView");
                if (dialogue == null || nodeView == null)
                {
                    return;
                }

                // At a normal/readable canvas zoom, preserve Graph Toolkit's
                // original inline editing behaviour.
                if (GetEffectiveScale(nodeView) > OpenAtOrBelowScale)
                {
                    return;
                }

                Close();
                _activeNodeView = nodeView;
                TextField input = dialogue.Q<TextField>(
                    "rich-dialogue-keyboard-input");
                string textBeforeMove = input?.value ?? string.Empty;
                int cursorIndex = input?.cursorIndex ?? 0;
                int selectIndex = input?.selectIndex ?? 0;

                VisualElement portrait = nodeView.Q<VisualElement>("speaker-portrait-option");
                if (portrait != null)
                {
                    _portraitSlot = ReparentSlot.Take(portrait);
                    _scroll.Add(portrait);
                }

                _dialogueSlot = ReparentSlot.Take(dialogue);
                _scroll.Add(dialogue);

                Label nodeTitle = nodeView.Q<Label>("title");
                _context.text = nodeTitle != null && !string.IsNullOrWhiteSpace(nodeTitle.text)
                    ? nodeTitle.text
                    : "Full-size authoring view";

                _lens.style.display = DisplayStyle.Flex;
                _positionSchedule.Resume();
                UpdatePosition();
                _lens.BringToFront();

                // Removing an element from one parent can briefly release its
                // focus even though it is reattached to the same panel. Restore
                // the caret after the hierarchy move so keyboard-only focus and
                // the first click both continue seamlessly in the lens.
                input?.schedule.Execute(() =>
                {
                    // A hierarchy move must never turn a populated serialized
                    // field into an empty edit. Restore the field through its
                    // normal value callback if UI Toolkit cleared it while the
                    // property-bound control was being moved.
                    if (!string.IsNullOrEmpty(textBeforeMove) &&
                        string.IsNullOrEmpty(input.value))
                    {
                        input.value = textBeforeMove;
                    }

                    input.Focus();
                    VisualElement textInput = input.Q(
                        className: TextField.inputUssClassName);
                    if (textInput?.focusable == true)
                    {
                        textInput.Focus();
                    }

                    input.SelectRange(cursorIndex, selectIndex);
                }).ExecuteLater(0);
            }

            private void Close()
            {
                if (!IsOpen)
                {
                    return;
                }

                // Restore in visual order. Each placeholder records the exact
                // location from which its live control was taken.
                _portraitSlot.Restore();
                _dialogueSlot.Restore();
                _portraitSlot = default;
                _dialogueSlot = default;
                _activeNodeView = null;
                _positionSchedule.Pause();
                _lens.style.display = DisplayStyle.None;
            }

            private void OnRootPointerDown(PointerDownEvent evt)
            {
                if (!IsOpen || evt.target is not VisualElement target ||
                    IsDescendantOf(target, _lens))
                {
                    return;
                }

                Close();
            }

            private void OnRootKeyDown(KeyDownEvent evt)
            {
                if (!IsOpen || evt.keyCode != KeyCode.Escape)
                {
                    return;
                }

                Close();
                evt.StopPropagation();
            }

            private void UpdatePosition()
            {
                if (!IsOpen || _activeNodeView.panel == null || _root.panel == null)
                {
                    Close();
                    return;
                }

                if (GetEffectiveScale(_activeNodeView) > CloseAboveScale)
                {
                    Close();
                    return;
                }

                float rootWidth = _root.resolvedStyle.width;
                float rootHeight = _root.resolvedStyle.height;
                if (rootWidth <= 0f || rootHeight <= 0f)
                {
                    return;
                }

                float width = Mathf.Min(PreferredWidth, Mathf.Max(300f,
                    rootWidth - WindowMargin * 2f));
                float height = Mathf.Min(PreferredHeight, Mathf.Max(260f,
                    rootHeight - WindowMargin * 2f));
                _lens.style.width = width;
                _lens.style.height = height;

                Rect nodeWorld = _activeNodeView.worldBound;
                Vector2 nodeMin = _root.WorldToLocal(nodeWorld.min);
                Vector2 nodeMax = _root.WorldToLocal(nodeWorld.max);
                bool placeRight = nodeMax.x + NodeGap + width <= rootWidth - WindowMargin ||
                                  nodeMin.x - NodeGap - width < WindowMargin;
                float left = placeRight
                    ? nodeMax.x + NodeGap
                    : nodeMin.x - NodeGap - width;
                float top = Mathf.Clamp(
                    nodeMin.y + 36f,
                    WindowMargin,
                    Mathf.Max(WindowMargin, rootHeight - height - WindowMargin));
                left = Mathf.Clamp(
                    left,
                    WindowMargin,
                    Mathf.Max(WindowMargin, rootWidth - width - WindowMargin));

                _lens.style.left = left;
                _lens.style.top = top;
                _pointer.EnableInClassList(
                    "novelify-dialogue-focus-lens__pointer--right",
                    !placeRight);
            }

            private static float GetEffectiveScale(VisualElement nodeView)
            {
                if (nodeView == null)
                {
                    return 1f;
                }

                float unscaledWidth = nodeView.layout.width;
                float screenWidth = nodeView.worldBound.width;
                if (!float.IsFinite(unscaledWidth) ||
                    !float.IsFinite(screenWidth) ||
                    unscaledWidth <= 0.01f ||
                    screenWidth <= 0.01f)
                {
                    return 1f;
                }

                return screenWidth / unscaledWidth;
            }

            private static VisualElement FindAncestorNamed(
                VisualElement element,
                string name)
            {
                while (element != null)
                {
                    if (element.name == name)
                    {
                        return element;
                    }

                    element = element.parent;
                }

                return null;
            }

            private static VisualElement FindAncestorView(
                VisualElement element,
                string typeName)
            {
                while (element != null)
                {
                    Type type = element.GetType();
                    while (type != null)
                    {
                        if (type.Name == typeName &&
                            type.Namespace == "Unity.GraphToolkit.Editor")
                        {
                            return element;
                        }

                        type = type.BaseType;
                    }

                    element = element.parent;
                }

                return null;
            }

            private static bool IsDescendantOf(
                VisualElement element,
                VisualElement ancestor)
            {
                while (element != null)
                {
                    if (ReferenceEquals(element, ancestor))
                    {
                        return true;
                    }

                    element = element.parent;
                }

                return false;
            }
        }

        private struct ReparentSlot
        {
            private VisualElement _element;
            private VisualElement _placeholder;

            public static ReparentSlot Take(VisualElement element)
            {
                if (element?.parent == null)
                {
                    return default;
                }

                VisualElement parent = element.parent;
                int index = ChildIndex(parent, element);
                var placeholder = new VisualElement
                {
                    name = "novelify-dialogue-lens-placeholder",
                    pickingMode = PickingMode.Ignore
                };
                placeholder.style.width = Length.Percent(100f);
                placeholder.style.height = Mathf.Max(1f, element.resolvedStyle.height);
                placeholder.style.flexGrow = 0f;
                placeholder.style.flexShrink = 0f;
                parent.Insert(Mathf.Max(0, index), placeholder);
                element.RemoveFromHierarchy();

                return new ReparentSlot
                {
                    _element = element,
                    _placeholder = placeholder
                };
            }

            public void Restore()
            {
                if (_element == null)
                {
                    return;
                }

                _element.RemoveFromHierarchy();
                VisualElement parent = _placeholder?.parent;
                if (parent == null)
                {
                    return;
                }

                int index = ChildIndex(parent, _placeholder);
                parent.Insert(Mathf.Max(0, index), _element);
                _placeholder.RemoveFromHierarchy();
            }

            private static int ChildIndex(VisualElement parent, VisualElement child)
            {
                for (int index = 0; index < parent.hierarchy.childCount; index++)
                {
                    if (ReferenceEquals(parent.hierarchy[index], child))
                    {
                        return index;
                    }
                }

                return parent.hierarchy.childCount;
            }
        }
    }
}
