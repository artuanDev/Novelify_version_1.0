using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    [Serializable]
    public struct RichDialogueText
    {
        public string Text;
        public TMP_FontAsset DefaultFont;
        public List<TMP_FontAsset> FontAssets;

        public RichDialogueText(string text)
        {
            Text = text;
            DefaultFont = null;
            FontAssets = new List<TMP_FontAsset>();
        }
    }

    /// <summary>
    /// Draws Novelify dialogue as a full-width rich-text authoring surface in
    /// both UI Toolkit and IMGUI inspectors.
    /// </summary>
    [CustomPropertyDrawer(typeof(RichDialogueText))]
    internal sealed class NovelRichDialogueInspector : PropertyDrawer
    {
        private const float RichEditorHeight = 330f;
        private const float ToolbarHeight = 23f;
        private const float SourceHeight = 132f;
        private const float PreviewHeight = 60f;
        private const float PreviewBaseFontSize = 13f;
        private const string PreviewEffectsPrefsKey =
            "Novelify.RichDialogue.PreviewEffectsEnabled";

        private static readonly List<string> TextSizes = new()
        {
            "Small (75%)",
            "Normal (100%)",
            "Large (125%)",
            "Extra large (160%)"
        };

        private static readonly string[] TextSizeValues =
        {
            "75%",
            "100%",
            "125%",
            "160%"
        };

        private static readonly Dictionary<string, TextSelection> Selections = new();
        private static Color _selectedColor = Color.white;

        private GUIStyle _previewStyle;
        private GUIStyle _previewPlaceholderStyle;

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            return RichEditorHeight;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            SerializedProperty textProperty = property.FindPropertyRelative(
                nameof(RichDialogueText.Text));
            if (textProperty == null)
            {
                EditorGUI.LabelField(position, "Unable to load rich dialogue text.");
                return;
            }

            DrawRichDialogueEditor(position, textProperty);
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty textProperty = property.FindPropertyRelative(
                nameof(RichDialogueText.Text));
            SerializedProperty defaultFontProperty = property.FindPropertyRelative(
                nameof(RichDialogueText.DefaultFont));
            SerializedProperty fontAssetsProperty = property.FindPropertyRelative(
                nameof(RichDialogueText.FontAssets));
            var root = new VisualElement();
            root.style.width = Length.Percent(100f);
            root.style.minWidth = 0f;
            root.style.maxWidth = Length.Percent(100f);
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 8f;
            root.style.paddingLeft = 6f;
            root.style.paddingRight = 6f;
            root.style.backgroundColor = (Color)new Color32(11, 18, 32, 255);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 2f;
            root.Add(header);

            var title = new Label("Dialogue text");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 13f;
            title.style.flexGrow = 1f;
            header.Add(title);

            var characterCount = new Label("0 characters");
            characterCount.style.fontSize = 9f;
            characterCount.style.color = (Color)new Color32(148, 163, 184, 255);
            header.Add(characterCount);

            var help = new Label(
                "Write directly in the preview. Select words, then apply formatting. Shortcuts: Ctrl+B and Ctrl+I.");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.fontSize = 10f;
            help.style.color = (Color)new Color32(148, 163, 184, 255);
            help.style.marginBottom = 5f;
            root.Add(help);

            var previewEffects = new Toggle("Preview FX")
            {
                value = EditorPrefs.GetBool(PreviewEffectsPrefsKey, true),
                tooltip = "Editor preview only. Turn off Wave and Shake motion while authoring. Runtime effects are unchanged."
            };
            previewEffects.style.alignSelf = Align.FlexStart;
            previewEffects.style.marginLeft = 0f;
            previewEffects.style.marginBottom = 2f;
            previewEffects.style.fontSize = 9f;
            previewEffects.RegisterValueChangedCallback(evt =>
                EditorPrefs.SetBool(PreviewEffectsPrefsKey, evt.newValue));
            root.Add(previewEffects);

            var source = new TextField
            {
                name = "rich-dialogue-keyboard-input",
                multiline = true
            };
            source.tooltip = "Write, select, and format dialogue directly in this in-game preview.";
            source.style.width = 1f;
            source.style.height = 1f;
            source.style.minHeight = 0f;
            source.style.position = Position.Absolute;
            source.style.left = -10000f;
            source.style.right = StyleKeyword.Auto;
            source.style.top = 0f;
            source.style.bottom = StyleKeyword.Auto;
            source.style.backgroundColor = Color.clear;
            source.style.color = Color.clear;
            source.style.opacity = 0f;
            source.pickingMode = PickingMode.Ignore;
            source.verticalScrollerVisibility = ScrollerVisibility.Auto;
            source.selectAllOnFocus = false;
            source.selectAllOnMouseUp = false;
#pragma warning disable CS0618 // Unity 6.0 has no public inline API for these USS custom properties.
            source.textSelection.cursorColor = Color.clear;
            source.textSelection.selectionColor = Color.clear;
#pragma warning restore CS0618
            MakeTextInputTransparent(source);

            var editorSurface = new VisualElement
            {
                name = "rich-dialogue-editing-surface"
            };
            editorSurface.style.width = Length.Percent(100f);
            editorSurface.style.minWidth = 0f;
            editorSurface.style.maxWidth = Length.Percent(100f);
            editorSurface.style.minHeight = SourceHeight;
            editorSurface.style.height = StyleKeyword.Auto;
            editorSurface.style.position = Position.Relative;
            editorSurface.style.marginBottom = 5f;

            VisualElement preview = CreatePreviewSurface(
                textProperty,
                source,
                SourceHeight,
                "Write dialogue here...",
                count => characterCount.text = count == 1
                    ? "1 character"
                    : $"{count} characters",
                fontName => ResolveFontAsset(
                    fontAssetsProperty,
                    defaultFontProperty,
                    fontName),
                () => previewEffects.value,
                out Action<string> RefreshPreview);
            editorSurface.Add(source);
            editorSurface.Add(preview);
            root.Add(editorSurface);

            var toolbar = new VisualElement();
            toolbar.style.paddingLeft = 5f;
            toolbar.style.paddingRight = 5f;
            toolbar.style.paddingTop = 4f;
            toolbar.style.paddingBottom = 4f;
            toolbar.style.marginBottom = 3f;
            toolbar.style.backgroundColor = (Color)new Color32(21, 31, 49, 255);
            toolbar.style.borderTopLeftRadius = 4f;
            toolbar.style.borderTopRightRadius = 4f;
            toolbar.style.borderBottomLeftRadius = 4f;
            toolbar.style.borderBottomRightRadius = 4f;
            root.Add(toolbar);

            var styleRow = CreateToolbarRow();
            toolbar.Add(styleRow);

            var optionRow = CreateToolbarRow();
            optionRow.style.marginTop = 3f;
            toolbar.Add(optionRow);

            var fontRow = CreateToolbarRow();
            fontRow.style.marginTop = 3f;
            toolbar.Add(fontRow);

            var effectRow = CreateToolbarRow();
            effectRow.style.marginTop = 3f;
            toolbar.Add(effectRow);

            var feedback = new Label("Select some text to format it.");
            feedback.style.fontSize = 9f;
            feedback.style.color = (Color)new Color32(125, 211, 252, 255);
            feedback.style.marginTop = 3f;
            toolbar.Add(feedback);

            AddToolbarCaption(styleRow, "Style");

            var bold = new Button
            {
                text = "Bold",
                tooltip = "Toggle bold on the selected text (Ctrl+B)"
            };
            StyleToolbarButton(bold);
            bold.style.unityFontStyleAndWeight = FontStyle.Bold;
            styleRow.Add(bold);

            var italic = new Button
            {
                text = "Italic",
                tooltip = "Toggle italic on the selected text (Ctrl+I)"
            };
            StyleToolbarButton(italic);
            italic.style.unityFontStyleAndWeight = FontStyle.Italic;
            styleRow.Add(italic);

            AddToolbarDivider(styleRow);
            var clearFormatting = new Button
            {
                text = "Clear all formatting",
                tooltip = "Remove styles and motion while keeping the dialogue"
            };
            StyleToolbarButton(clearFormatting);
            styleRow.Add(clearFormatting);

            AddToolbarCaption(optionRow, "Text size");
            var size = new DropdownField(TextSizes, 1);
            size.tooltip = "Choose a size, then click Apply size";
            size.style.width = 122f;
            size.style.marginRight = 3f;
            optionRow.Add(size);

            var applySize = new Button
            {
                text = "Apply size",
                tooltip = "Apply this size to the selected text"
            };
            StyleToolbarButton(applySize);
            optionRow.Add(applySize);

            var color = new ColorField
            {
                value = Color.white,
                showAlpha = false,
                tooltip = "Choose a color, then click Apply color"
            };
            AddToolbarDivider(optionRow);
            AddToolbarCaption(optionRow, "Text color");
            color.style.width = 54f;
            color.style.marginRight = 3f;
            optionRow.Add(color);

            var applyColor = new Button
            {
                text = "Apply color",
                tooltip = "Apply this color to the selected text"
            };
            StyleToolbarButton(applyColor);
            optionRow.Add(applyColor);

            AddToolbarCaption(fontRow, "Default TMP font");
            var defaultFont = new ObjectField
            {
                objectType = typeof(TMP_FontAsset),
                allowSceneObjects = false,
                value = defaultFontProperty?.objectReferenceValue,
                tooltip = "Font used for dialogue that has no per-selection font override."
            };
            defaultFont.style.width = 150f;
            defaultFont.style.marginRight = 8f;
            fontRow.Add(defaultFont);

            AddToolbarCaption(fontRow, "Selected text font");
            var selectedFont = new ObjectField
            {
                objectType = typeof(TMP_FontAsset),
                allowSceneObjects = false,
                tooltip = "Choose a TMP font, then apply it to the selected text. None removes the override."
            };
            selectedFont.style.width = 150f;
            selectedFont.style.marginRight = 3f;
            fontRow.Add(selectedFont);

            var applyFont = new Button
            {
                text = "Apply font",
                tooltip = "Apply this TMP font to the selected text"
            };
            StyleToolbarButton(applyFont);
            fontRow.Add(applyFont);

            AddToolbarCaption(effectRow, "Motion effect");
            var wave = new Button
            {
                text = "Wave",
                tooltip = "Make the selected letters move in a smooth wave"
            };
            StyleToolbarButton(wave);
            effectRow.Add(wave);

            var shake = new Button
            {
                text = "Shake",
                tooltip = "Make the selected letters jitter"
            };
            StyleToolbarButton(shake);
            effectRow.Add(shake);

            var removeMotion = new Button
            {
                text = "Remove motion",
                tooltip = "Remove Wave or Shake from the selected text"
            };
            StyleToolbarButton(removeMotion);
            effectRow.Add(removeMotion);

            var soundStart = new Button
            {
                text = "Sound start",
                tooltip = "Play this dialogue's Sound clip when the selected word begins"
            };
            StyleToolbarButton(soundStart);
            effectRow.Add(soundStart);

            int savedSelectionStart = 0;
            int savedSelectionEnd = 0;

            void CaptureSelection()
            {
                int textLength = source.value?.Length ?? 0;
                int firstIndex = Mathf.Clamp(
                    Mathf.Min(source.cursorIndex, source.selectIndex),
                    0,
                    textLength);
                int lastIndex = Mathf.Clamp(
                    Mathf.Max(source.cursorIndex, source.selectIndex),
                    0,
                    textLength);

                // Do not overwrite a useful saved range after another toolbar
                // control has already taken focus from the text field.
                if (lastIndex > firstIndex)
                {
                    savedSelectionStart = firstIndex;
                    savedSelectionEnd = lastIndex;
                }
            }

            void RestoreSelection(int firstIndex, int lastIndex)
            {
                savedSelectionStart = firstIndex;
                savedSelectionEnd = lastIndex;
                source.schedule.Execute(() =>
                {
                    source.Focus();
                    source.SelectRange(lastIndex, firstIndex);
                });
            }

            void CommitDocument(
                DialogueDocument document,
                string undoLabel,
                bool updateEditorText)
            {
                string markup = SerializeDocument(document);
                Undo.RecordObject(textProperty.serializedObject.targetObject, undoLabel);
                textProperty.stringValue = markup;
                textProperty.serializedObject.ApplyModifiedProperties();
                if (updateEditorText)
                {
                    source.SetValueWithoutNotify(document.GetPlainText());
                }

                RefreshPreview(markup);
            }

            bool TryGetSelection(out int firstIndex, out int lastIndex)
            {
                int textLength = source.value?.Length ?? 0;
                firstIndex = Mathf.Clamp(
                    Mathf.Min(source.cursorIndex, source.selectIndex),
                    0,
                    textLength);
                lastIndex = Mathf.Clamp(
                    Mathf.Max(source.cursorIndex, source.selectIndex),
                    0,
                    textLength);
                if (lastIndex > firstIndex)
                {
                    savedSelectionStart = firstIndex;
                    savedSelectionEnd = lastIndex;
                    return true;
                }

                firstIndex = Mathf.Clamp(savedSelectionStart, 0, textLength);
                lastIndex = Mathf.Clamp(savedSelectionEnd, 0, textLength);
                if (lastIndex > firstIndex)
                {
                    return true;
                }

                SetFeedback(feedback, "Select one or more words first.", true);
                source.schedule.Execute(source.Focus);
                return false;
            }

            void ApplyToSelection(
                Action<DialogueDocument, int, int> formattingAction,
                string description)
            {
                if (!TryGetSelection(out int firstIndex, out int lastIndex))
                {
                    return;
                }

                DialogueDocument document = ParseDocument(textProperty.stringValue);
                lastIndex = Mathf.Min(lastIndex, document.Characters.Count);
                formattingAction(document, firstIndex, lastIndex);
                CommitDocument(document, $"Format Dialogue: {description}", false);
                SetFeedback(
                    feedback,
                    $"{description} applied. Ctrl+Z to undo.",
                    false);
                RestoreSelection(firstIndex, lastIndex);
            }

            void ToggleBold()
            {
                ApplyToSelection(
                    (document, firstIndex, lastIndex) =>
                    {
                        bool enable = !AllCharactersMatch(
                            document,
                            firstIndex,
                            lastIndex,
                            style => style.Bold);
                        for (int index = firstIndex; index < lastIndex; index++)
                        {
                            StyledCharacter character = document.Characters[index];
                            character.Style.Bold = enable;
                            document.Characters[index] = character;
                        }
                    },
                    "Bold");
            }

            void ToggleItalic()
            {
                ApplyToSelection(
                    (document, firstIndex, lastIndex) =>
                    {
                        bool enable = !AllCharactersMatch(
                            document,
                            firstIndex,
                            lastIndex,
                            style => style.Italic);
                        for (int index = firstIndex; index < lastIndex; index++)
                        {
                            StyledCharacter character = document.Characters[index];
                            character.Style.Italic = enable;
                            document.Characters[index] = character;
                        }
                    },
                    "Italic");
            }

            bold.clicked += ToggleBold;
            italic.clicked += ToggleItalic;
            defaultFont.RegisterValueChangedCallback(evt =>
            {
                if (defaultFontProperty == null)
                {
                    return;
                }

                Undo.RecordObject(
                    defaultFontProperty.serializedObject.targetObject,
                    "Change Dialogue Default Font");
                defaultFontProperty.objectReferenceValue = evt.newValue as TMP_FontAsset;
                defaultFontProperty.serializedObject.ApplyModifiedProperties();
                ApplySourceFont(source, evt.newValue as TMP_FontAsset);
                RefreshPreview(textProperty?.stringValue ?? string.Empty);
            });
            applySize.clicked += () =>
            {
                int sizeIndex = Mathf.Clamp(size.index, 0, TextSizeValues.Length - 1);
                string value = TextSizeValues[sizeIndex];
                ApplyToSelection(
                    (document, firstIndex, lastIndex) =>
                    {
                        for (int index = firstIndex; index < lastIndex; index++)
                        {
                            StyledCharacter character = document.Characters[index];
                            character.Style.Size = value == "100%" ? null : value;
                            document.Characters[index] = character;
                        }
                    },
                    TextSizes[sizeIndex]);
            };
            applyColor.clicked += () =>
            {
                string value = "#" + ColorUtility.ToHtmlStringRGB(color.value);
                ApplyToSelection(
                    (document, firstIndex, lastIndex) =>
                    {
                        for (int index = firstIndex; index < lastIndex; index++)
                        {
                            StyledCharacter character = document.Characters[index];
                            character.Style.Color = value;
                            document.Characters[index] = character;
                        }
                    },
                    "Color");
            };
            applyFont.clicked += () =>
            {
                TMP_FontAsset font = selectedFont.value as TMP_FontAsset;
                if (font != null)
                {
                    EnsureFontTracked(fontAssetsProperty, font);
                }

                ApplyToSelection(
                    (document, firstIndex, lastIndex) =>
                    {
                        for (int index = firstIndex; index < lastIndex; index++)
                        {
                            StyledCharacter character = document.Characters[index];
                            character.Style.Font = font != null ? font.name : null;
                            document.Characters[index] = character;
                        }
                    },
                    font != null ? $"Font: {font.name}" : "Font override removed");
            };
            wave.clicked += () => ApplyToSelection(
                (document, firstIndex, lastIndex) =>
                {
                    for (int index = firstIndex; index < lastIndex; index++)
                    {
                        StyledCharacter character = document.Characters[index];
                        character.Style.Effect = "novelify-wave";
                        document.Characters[index] = character;
                    }
                },
                "Wave motion");
            shake.clicked += () => ApplyToSelection(
                (document, firstIndex, lastIndex) =>
                {
                    for (int index = firstIndex; index < lastIndex; index++)
                    {
                        StyledCharacter character = document.Characters[index];
                        character.Style.Effect = "novelify-shake";
                        document.Characters[index] = character;
                    }
                },
                "Shake motion");
            removeMotion.clicked += () => ApplyToSelection(
                (document, firstIndex, lastIndex) =>
                {
                    for (int index = firstIndex; index < lastIndex; index++)
                    {
                        StyledCharacter character = document.Characters[index];
                        character.Style.Effect = null;
                        document.Characters[index] = character;
                    }
                },
                "Motion removed");
            soundStart.clicked += () => ApplyToSelection(
                (document, firstIndex, lastIndex) =>
                {
                    // A cue is a single start position. Clear earlier cues and
                    // mark the selected word so the marker remains easy to see
                    // and move when the surrounding text is edited.
                    for (int index = 0; index < document.Characters.Count; index++)
                    {
                        StyledCharacter character = document.Characters[index];
                        character.Style.SoundCue = index >= firstIndex && index < lastIndex;
                        if (character.Style.SoundCue)
                            character.Style.Effect = null;
                        document.Characters[index] = character;
                    }
                },
                "Sound start");
            clearFormatting.clicked += () =>
            {
                DialogueDocument document = ParseDocument(textProperty.stringValue);
                bool hadFormatting = false;
                for (int index = 0; index < document.Characters.Count; index++)
                {
                    StyledCharacter character = document.Characters[index];
                    hadFormatting |= !character.Style.IsDefault;
                    character.Style = default;
                    document.Characters[index] = character;
                }

                if (!hadFormatting)
                {
                    SetFeedback(feedback, "This dialogue has no formatting to clear.", false);
                    return;
                }

                CommitDocument(document, "Clear Dialogue Formatting", false);
                SetFeedback(feedback, "All formatting removed. Ctrl+Z to undo.", false);
                source.schedule.Execute(source.Focus);
            };

            DialogueDocument initialDocument = ParseDocument(
                textProperty?.stringValue ?? string.Empty);
            source.SetValueWithoutNotify(initialDocument.GetPlainText());
            ApplySourceFont(
                source,
                defaultFontProperty?.objectReferenceValue as TMP_FontAsset);
            RefreshPreview(textProperty?.stringValue ?? string.Empty);

            if (textProperty != null)
            {
                source.RegisterValueChangedCallback(evt =>
                {
                    savedSelectionStart = 0;
                    savedSelectionEnd = 0;
                    DialogueDocument currentDocument = ParseDocument(textProperty.stringValue);
                    DialogueDocument editedDocument = MergePlainTextEdit(
                        currentDocument,
                        evt.newValue ?? string.Empty);
                    CommitDocument(editedDocument, "Edit Dialogue", false);
                });

                root.TrackPropertyValue(textProperty, changedProperty =>
                {
                    DialogueDocument changedDocument = ParseDocument(
                        changedProperty.stringValue);
                    string plainText = changedDocument.GetPlainText();
                    if (source.value != plainText)
                    {
                        source.SetValueWithoutNotify(plainText);
                    }

                    RefreshPreview(changedProperty.stringValue);
                });
            }
            if (defaultFontProperty != null)
            {
                root.TrackPropertyValue(defaultFontProperty, changedProperty =>
                {
                    var font = changedProperty.objectReferenceValue as TMP_FontAsset;
                    defaultFont.SetValueWithoutNotify(font);
                    ApplySourceFont(source, font);
                    RefreshPreview(textProperty?.stringValue ?? string.Empty);
                });
            }

            source.RegisterCallback<PointerUpEvent>(_ => CaptureSelection());
            source.RegisterCallback<KeyUpEvent>(_ => CaptureSelection());
            source.RegisterCallback<FocusOutEvent>(_ => CaptureSelection());
            toolbar.RegisterCallback<PointerDownEvent>(
                _ => CaptureSelection(),
                TrickleDown.TrickleDown);

            source.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!evt.ctrlKey && !evt.commandKey)
                {
                    return;
                }

                if (evt.keyCode == KeyCode.B)
                {
                    ToggleBold();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.I)
                {
                    ToggleItalic();
                    evt.StopPropagation();
                }
            });

            return root;
        }

        private static VisualElement CreatePreviewSurface(
            SerializedProperty textProperty,
            TextField source,
            float height,
            string placeholderText,
            Action<int> characterCountChanged,
            Func<string, TMP_FontAsset> resolveFont,
            Func<bool> previewEffectsEnabled,
            out Action<string> refreshPreview)
        {
            var preview = new VisualElement
            {
                name = "rich-dialogue-preview",
                pickingMode = PickingMode.Position
            };
            preview.style.width = Length.Percent(100f);
            preview.style.minWidth = 0f;
            preview.style.maxWidth = Length.Percent(100f);
            preview.style.minHeight = height;
            preview.style.height = StyleKeyword.Auto;
            preview.style.flexGrow = 1f;
            preview.style.alignSelf = Align.Stretch;
            preview.style.flexDirection = FlexDirection.Column;
            preview.style.justifyContent = Justify.FlexStart;
            preview.style.alignItems = Align.Stretch;
            preview.style.overflow = Overflow.Hidden;
            preview.style.paddingLeft = 7f;
            preview.style.paddingRight = 7f;
            preview.style.paddingTop = 5f;
            preview.style.paddingBottom = 5f;
            preview.style.backgroundColor = (Color)new Color32(7, 12, 22, 255);
            SetMouseCursor(preview, MouseCursor.Text);

            var textFlow = new VisualElement
            {
                name = "rich-dialogue-text-flow",
                pickingMode = PickingMode.Position
            };
            textFlow.style.width = Length.Percent(100f);
            textFlow.style.minWidth = 0f;
            textFlow.style.maxWidth = Length.Percent(100f);
            textFlow.style.height = StyleKeyword.Auto;
            textFlow.style.flexGrow = 0f;
            textFlow.style.flexShrink = 1f;
            textFlow.style.flexDirection = FlexDirection.Row;
            textFlow.style.flexWrap = Wrap.Wrap;
            textFlow.style.alignContent = Align.FlexStart;
            textFlow.style.alignItems = Align.FlexEnd;

            var previewGlyphs = new List<AnimatedPreviewGlyph>();
            var selectionLayer = new VisualElement
            {
                name = "rich-dialogue-selection",
                pickingMode = PickingMode.Ignore
            };
            selectionLayer.style.position = Position.Absolute;
            selectionLayer.style.left = 0f;
            selectionLayer.style.right = 0f;
            selectionLayer.style.top = 0f;
            selectionLayer.style.bottom = 0f;

            var caret = new VisualElement
            {
                name = "rich-dialogue-caret",
                pickingMode = PickingMode.Ignore
            };
            caret.style.position = Position.Absolute;
            caret.style.width = 1.5f;
            caret.style.backgroundColor = (Color)new Color32(226, 232, 240, 255);
            caret.style.display = DisplayStyle.None;

            var selectionHighlights = new List<VisualElement>();
            var lineSelections = new List<Rect>();
            bool sourceFocused = false;
            int dragAnchor = 0;
            int desiredCursorIndex = 0;
            int desiredSelectIndex = 0;

            Rect PreviewLocalBounds(VisualElement element)
            {
                Rect worldBounds = element.worldBound;
                Vector2 localMin = preview.WorldToLocal(new Vector2(
                    worldBounds.xMin,
                    worldBounds.yMin));
                Vector2 localMax = preview.WorldToLocal(new Vector2(
                    worldBounds.xMax,
                    worldBounds.yMax));
                return Rect.MinMaxRect(
                    Mathf.Min(localMin.x, localMax.x),
                    Mathf.Min(localMin.y, localMax.y),
                    Mathf.Max(localMin.x, localMax.x),
                    Mathf.Max(localMin.y, localMax.y));
            }

            void Refresh(string markup)
            {
                DialogueDocument document = ParseDocument(markup);
                int visibleCharacters = document.Characters.Count;
                characterCountChanged?.Invoke(visibleCharacters);

                preview.Clear();
                previewGlyphs.Clear();
                textFlow.Clear();
                selectionLayer.Clear();
                selectionHighlights.Clear();
                caret.style.display = DisplayStyle.None;
                preview.Add(selectionLayer);
                preview.Add(textFlow);
                if (visibleCharacters == 0)
                {
                    var placeholder = new Label(placeholderText);
                    placeholder.style.fontSize = 10f;
                    placeholder.style.color = (Color)new Color32(100, 116, 139, 255);
                    placeholder.style.unityFontStyleAndWeight = FontStyle.Italic;
                    placeholder.style.whiteSpace = WhiteSpace.Normal;
                    placeholder.pickingMode = PickingMode.Ignore;
                    textFlow.Add(placeholder);
                    preview.Add(caret);
                    return;
                }

                for (int index = 0; index < document.Characters.Count; index++)
                {
                    StyledCharacter character = document.Characters[index];
                    if (character.Character == '\n')
                    {
                        var lineBreak = new VisualElement();
                        lineBreak.style.width = Length.Percent(100f);
                        lineBreak.style.height = 1f;
                        lineBreak.style.flexGrow = 0f;
                        lineBreak.style.flexShrink = 0f;
                        lineBreak.style.alignSelf = Align.FlexStart;
                        lineBreak.pickingMode = PickingMode.Position;
                        lineBreak.userData = index;
                        textFlow.Add(lineBreak);
                        previewGlyphs.Add(new AnimatedPreviewGlyph
                        {
                            Element = lineBreak,
                            Character = character.Character,
                            CharacterIndex = index
                        });
                        continue;
                    }

                    var glyph = new Label(BuildPreviewGlyphText(character))
                    {
                        enableRichText = false,
                        pickingMode = PickingMode.Position,
                        userData = index
                    };
                    glyph.style.flexGrow = 0f;
                    glyph.style.flexShrink = 0f;
                    glyph.style.marginLeft = 0f;
                    glyph.style.marginRight = 0f;
                    glyph.style.marginTop = 0f;
                    glyph.style.marginBottom = 0f;
                    glyph.style.paddingLeft = 0f;
                    glyph.style.paddingRight = 0f;
                    glyph.style.paddingTop = 0f;
                    glyph.style.paddingBottom = 0f;
                    glyph.style.whiteSpace = WhiteSpace.NoWrap;
                    glyph.style.alignSelf = Align.FlexEnd;
                    glyph.style.unityFontStyleAndWeight = character.Style.Bold
                        ? character.Style.Italic
                            ? FontStyle.BoldAndItalic
                            : FontStyle.Bold
                        : character.Style.Italic
                            ? FontStyle.Italic
                            : FontStyle.Normal;
                    glyph.style.fontSize = PreviewBaseFontSize *
                        GetPreviewSizeScale(character.Style.Size);
                    if (!string.IsNullOrEmpty(character.Style.Color) &&
                        ColorUtility.TryParseHtmlString(
                            character.Style.Color,
                            out Color glyphColor))
                    {
                        glyph.style.color = glyphColor;
                    }
                    TMP_FontAsset font = resolveFont?.Invoke(character.Style.Font);
                    if (font?.sourceFontFile != null)
                    {
                        glyph.style.unityFont = font.sourceFontFile;
                    }
                    textFlow.Add(glyph);
                    previewGlyphs.Add(new AnimatedPreviewGlyph
                    {
                        Element = glyph,
                        Character = character.Character,
                        Effect = character.Style.Effect,
                        CharacterIndex = index
                    });
                }

                preview.Add(caret);
            }

            void RefreshSelection()
            {
                int firstIndex = Mathf.Min(source.cursorIndex, source.selectIndex);
                int lastIndex = Mathf.Max(source.cursorIndex, source.selectIndex);
                bool hasSelection = lastIndex > firstIndex;
                bool showCaret = sourceFocused && !hasSelection &&
                    ((float)EditorApplication.timeSinceStartup % 1f) < 0.55f;
                Color selectionColor = new Color(0.08f, 0.46f, 0.82f, 0.9f);

                if (hasSelection)
                {
                    lineSelections.Clear();
                    foreach (AnimatedPreviewGlyph glyph in previewGlyphs)
                    {
                        if (glyph.CharacterIndex < firstIndex ||
                            glyph.CharacterIndex >= lastIndex ||
                            glyph.Character == '\n')
                        {
                            continue;
                        }

                        Rect bounds = PreviewLocalBounds(glyph.Element);
                        if (bounds.width <= 0f || bounds.height <= 0f)
                        {
                            continue;
                        }

                        if (lineSelections.Count > 0)
                        {
                            Rect current = lineSelections[lineSelections.Count - 1];
                            bool sameTextBand =
                                Mathf.Abs(current.yMin - bounds.yMin) < 1.5f &&
                                Mathf.Abs(current.yMax - bounds.yMax) < 1.5f &&
                                bounds.xMin <= current.xMax + 1.5f;
                            if (sameTextBand)
                            {
                                lineSelections[lineSelections.Count - 1] = Rect.MinMaxRect(
                                    Mathf.Min(current.xMin, bounds.xMin),
                                    Mathf.Min(current.yMin, bounds.yMin),
                                    Mathf.Max(current.xMax, bounds.xMax),
                                    Mathf.Max(current.yMax, bounds.yMax));
                                continue;
                            }
                        }

                        lineSelections.Add(bounds);
                    }

                    while (selectionHighlights.Count < lineSelections.Count)
                    {
                        var highlight = new VisualElement
                        {
                            pickingMode = PickingMode.Ignore
                        };
                        highlight.style.position = Position.Absolute;
                        highlight.style.backgroundColor = selectionColor;
                        highlight.style.borderTopWidth = 1f;
                        highlight.style.borderRightWidth = 1f;
                        highlight.style.borderBottomWidth = 1f;
                        highlight.style.borderLeftWidth = 1f;
                        highlight.style.borderTopColor = (Color)new Color32(96, 190, 255, 255);
                        highlight.style.borderRightColor = (Color)new Color32(96, 190, 255, 255);
                        highlight.style.borderBottomColor = (Color)new Color32(96, 190, 255, 255);
                        highlight.style.borderLeftColor = (Color)new Color32(96, 190, 255, 255);
                        highlight.style.borderTopLeftRadius = 2f;
                        highlight.style.borderTopRightRadius = 2f;
                        highlight.style.borderBottomLeftRadius = 2f;
                        highlight.style.borderBottomRightRadius = 2f;
                        selectionLayer.Add(highlight);
                        selectionHighlights.Add(highlight);
                    }

                    for (int index = 0; index < selectionHighlights.Count; index++)
                    {
                        VisualElement highlight = selectionHighlights[index];
                        if (index >= lineSelections.Count)
                        {
                            highlight.style.display = DisplayStyle.None;
                            continue;
                        }

                        Rect bounds = lineSelections[index];
                        highlight.style.display = DisplayStyle.Flex;
                        highlight.style.left = bounds.xMin;
                        highlight.style.top = bounds.yMin;
                        highlight.style.width = bounds.width;
                        highlight.style.height = bounds.height;
                    }
                }
                else
                {
                    foreach (VisualElement highlight in selectionHighlights)
                    {
                        highlight.style.display = DisplayStyle.None;
                    }
                }

                caret.style.display = showCaret
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                if (!showCaret)
                {
                    return;
                }

                int cursorIndex = Mathf.Clamp(source.cursorIndex, 0, previewGlyphs.Count);
                AnimatedPreviewGlyph? previous = cursorIndex > 0
                    ? previewGlyphs[cursorIndex - 1]
                    : null;
                AnimatedPreviewGlyph? next = cursorIndex < previewGlyphs.Count
                    ? previewGlyphs[cursorIndex]
                    : null;
                AnimatedPreviewGlyph? sizeReference = previous;
                if (sizeReference.HasValue && sizeReference.Value.Character == '\n')
                {
                    for (int index = cursorIndex - 2; index >= 0; index--)
                    {
                        if (previewGlyphs[index].Character != '\n')
                        {
                            sizeReference = previewGlyphs[index];
                            break;
                        }
                    }
                }
                sizeReference ??= next;

                if (!sizeReference.HasValue)
                {
                    caret.style.left = 7f;
                    caret.style.top = 5f;
                    caret.style.height = 14f;
                    return;
                }

                Rect referenceBounds = PreviewLocalBounds(sizeReference.Value.Element);
                float caretHeight = Mathf.Max(12f, referenceBounds.height);
                Rect? previousBounds = previous.HasValue
                    ? PreviewLocalBounds(previous.Value.Element)
                    : null;
                Rect? nextBounds = next.HasValue
                    ? PreviewLocalBounds(next.Value.Element)
                    : null;
                bool startsVisualLine = next.HasValue &&
                    (!previous.HasValue ||
                     previous.Value.Character == '\n' ||
                     nextBounds.Value.center.y > previousBounds.Value.center.y + 2f);
                Rect positionBounds = startsVisualLine
                    ? nextBounds.Value
                    : previousBounds ?? nextBounds.Value;
                float caretX = startsVisualLine
                    ? positionBounds.xMin
                    : positionBounds.xMax;
                caret.style.left = caretX;
                caret.style.top = positionBounds.yMax - caretHeight;
                caret.style.height = caretHeight;
            }

            int HitTest(Vector2 localPosition)
            {
                if (previewGlyphs.Count == 0)
                {
                    return 0;
                }

                AnimatedPreviewGlyph nearest = previewGlyphs[0];
                float nearestDistance = float.MaxValue;
                foreach (AnimatedPreviewGlyph glyph in previewGlyphs)
                {
                    Rect bounds = PreviewLocalBounds(glyph.Element);
                    float dx = localPosition.x < bounds.xMin
                        ? bounds.xMin - localPosition.x
                        : localPosition.x > bounds.xMax
                            ? localPosition.x - bounds.xMax
                            : 0f;
                    float dy = localPosition.y < bounds.yMin
                        ? bounds.yMin - localPosition.y
                        : localPosition.y > bounds.yMax
                            ? localPosition.y - bounds.yMax
                            : 0f;
                    float distance = dx * dx + dy * dy;
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = glyph;
                    }
                }

                if (nearest.Character == '\n')
                {
                    return nearest.CharacterIndex + 1;
                }

                return localPosition.x >= PreviewLocalBounds(nearest.Element).center.x
                    ? nearest.CharacterIndex + 1
                    : nearest.CharacterIndex;
            }

            int HitTestPointerDown(PointerDownEvent evt)
            {
                VisualElement target = evt.target as VisualElement;
                while (target != null && target != preview)
                {
                    if (target.userData is int characterIndex)
                    {
                        AnimatedPreviewGlyph glyph = previewGlyphs.Find(
                            candidate => candidate.CharacterIndex == characterIndex);
                        if (glyph.Element != null)
                        {
                            if (glyph.Character == '\n')
                            {
                                return characterIndex + 1;
                            }

                            Rect bounds = PreviewLocalBounds(glyph.Element);
                            return evt.localPosition.x >= bounds.center.x
                                ? characterIndex + 1
                                : characterIndex;
                        }
                    }

                    target = target.parent;
                }

                return HitTest(evt.localPosition);
            }

            void SelectAt(int cursorIndex, int anchorIndex)
            {
                int length = source.value?.Length ?? 0;
                desiredCursorIndex = Mathf.Clamp(cursorIndex, 0, length);
                desiredSelectIndex = Mathf.Clamp(anchorIndex, 0, length);
                source.SelectRange(desiredCursorIndex, desiredSelectIndex);
                RefreshSelection();
            }

            void FocusSourceForEditing()
            {
                preview.schedule.Execute(() =>
                {
                    source.Focus();
                    VisualElement textInput = source.Q(
                        className: TextField.inputUssClassName);
                    if (textInput?.focusable == true)
                    {
                        textInput.Focus();
                    }
                    source.SelectRange(desiredCursorIndex, desiredSelectIndex);
                    sourceFocused = true;
                    RefreshSelection();
                }).ExecuteLater(0);
            }

            void SelectWordAt(int characterIndex)
            {
                string text = source.value ?? string.Empty;
                if (text.Length == 0)
                {
                    SelectAt(0, 0);
                    return;
                }

                int index = Mathf.Clamp(characterIndex, 0, text.Length - 1);
                bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';
                if (index > 0 &&
                    !IsWordCharacter(text[index]) &&
                    IsWordCharacter(text[index - 1]))
                {
                    index--;
                }
                if (!IsWordCharacter(text[index]))
                {
                    SelectAt(index + 1, index);
                    return;
                }

                int first = index;
                int last = index + 1;
                while (first > 0 && IsWordCharacter(text[first - 1])) first--;
                while (last < text.Length && IsWordCharacter(text[last])) last++;
                SelectAt(last, first);
            }

            preview.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                int hit = HitTestPointerDown(evt);
                if (evt.clickCount >= 2)
                {
                    SelectWordAt(hit);
                    dragAnchor = source.selectIndex;
                }
                else
                {
                    dragAnchor = evt.shiftKey ? source.selectIndex : hit;
                    SelectAt(hit, dragAnchor);
                }

                preview.CapturePointer(evt.pointerId);
                FocusSourceForEditing();
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            preview.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!preview.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                SelectAt(HitTest(evt.localPosition), dragAnchor);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            preview.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (preview.HasPointerCapture(evt.pointerId))
                {
                    preview.ReleasePointer(evt.pointerId);
                    RefreshSelection();
                    FocusSourceForEditing();
                    evt.StopImmediatePropagation();
                }
            }, TrickleDown.TrickleDown);
            source.RegisterCallback<FocusInEvent>(_ =>
            {
                sourceFocused = true;
                RefreshSelection();
            });
            source.RegisterCallback<FocusOutEvent>(_ =>
            {
                sourceFocused = false;
                RefreshSelection();
            });
            source.RegisterCallback<KeyUpEvent>(_ =>
            {
                desiredCursorIndex = source.cursorIndex;
                desiredSelectIndex = source.selectIndex;
                RefreshSelection();
            });

            preview.schedule.Execute(() =>
            {
                float time = (float)EditorApplication.timeSinceStartup;
                float shakeFrame = Mathf.Floor(time * 24f);
                bool animateEffects = previewEffectsEnabled?.Invoke() ?? true;
                foreach (AnimatedPreviewGlyph glyph in previewGlyphs)
                {
                    float x = 0f;
                    float y = 0f;
                    if (animateEffects && glyph.Effect == "novelify-wave")
                    {
                        y = Mathf.Sin(time * 7f + glyph.CharacterIndex * 0.65f) * 2.5f;
                    }
                    else if (animateEffects && glyph.Effect == "novelify-shake")
                    {
                        x = (PreviewHash(
                            shakeFrame + glyph.CharacterIndex * 17.17f) * 2f - 1f) * 1.5f;
                        y = (PreviewHash(
                            shakeFrame * 1.37f + glyph.CharacterIndex * 41.73f) * 2f - 1f) * 1.5f;
                    }

                    glyph.Element.style.translate = new Translate(x, y);
                }

                RefreshSelection();
            }).Every(33);

            if (textProperty != null)
            {
                preview.TrackPropertyValue(
                    textProperty,
                    changedProperty => Refresh(changedProperty.stringValue));
                Refresh(textProperty.stringValue);
            }
            else
            {
                Refresh(string.Empty);
            }

            refreshPreview = Refresh;
            return preview;
        }

        private static TMP_FontAsset ResolveFontAsset(
            SerializedProperty fontAssetsProperty,
            SerializedProperty defaultFontProperty,
            string fontName)
        {
            if (!string.IsNullOrEmpty(fontName) && fontAssetsProperty?.isArray == true)
            {
                for (int index = 0; index < fontAssetsProperty.arraySize; index++)
                {
                    var font = fontAssetsProperty.GetArrayElementAtIndex(index)
                        .objectReferenceValue as TMP_FontAsset;
                    if (font != null && string.Equals(font.name, fontName, StringComparison.Ordinal))
                    {
                        return font;
                    }
                }
            }

            return defaultFontProperty?.objectReferenceValue as TMP_FontAsset;
        }

        private static void EnsureFontTracked(
            SerializedProperty fontAssetsProperty,
            TMP_FontAsset font)
        {
            if (fontAssetsProperty?.isArray != true || font == null)
            {
                return;
            }

            for (int index = 0; index < fontAssetsProperty.arraySize; index++)
            {
                if (fontAssetsProperty.GetArrayElementAtIndex(index).objectReferenceValue == font)
                {
                    return;
                }
            }

            Undo.RecordObject(
                fontAssetsProperty.serializedObject.targetObject,
                "Track Dialogue Font");
            int newIndex = fontAssetsProperty.arraySize;
            fontAssetsProperty.InsertArrayElementAtIndex(newIndex);
            fontAssetsProperty.GetArrayElementAtIndex(newIndex).objectReferenceValue = font;
            fontAssetsProperty.serializedObject.ApplyModifiedProperties();
        }

        private static void ApplySourceFont(TextField source, TMP_FontAsset font)
        {
            if (font?.sourceFontFile != null)
            {
                source.style.unityFont = font.sourceFontFile;
            }
            else
            {
                source.style.unityFont = StyleKeyword.Null;
            }
        }

        private static void MakeTextInputTransparent(TextField source)
        {
            void Apply()
            {
                VisualElement input = source.Q(className: TextField.inputUssClassName);
                if (input != null)
                {
                    input.style.backgroundColor = Color.clear;
                    input.style.borderTopWidth = 0f;
                    input.style.borderRightWidth = 0f;
                    input.style.borderBottomWidth = 0f;
                    input.style.borderLeftWidth = 0f;
                    input.style.paddingLeft = 7f;
                    input.style.paddingRight = 7f;
                    input.style.paddingTop = 5f;
                    input.style.paddingBottom = 5f;
                }

                TextElement editableText = source.Q<TextElement>();
                if (editableText != null)
                {
                    editableText.style.color = Color.clear;
                    editableText.style.backgroundColor = Color.clear;
                }
            }

            Apply();
            source.schedule.Execute(Apply);
        }

        private static void SetMouseCursor(VisualElement element, MouseCursor mouseCursor)
        {
            var cursor = new UnityEngine.UIElements.Cursor();
            var cursorId = typeof(UnityEngine.UIElements.Cursor).GetProperty(
                "defaultCursorId",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            if (cursorId != null)
            {
                object boxed = cursor;
                cursorId.SetValue(boxed, (int)mouseCursor);
                cursor = (UnityEngine.UIElements.Cursor)boxed;
            }

            element.style.cursor = new StyleCursor(cursor);
        }

        private static VisualElement CreateToolbarRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static void AddToolbarCaption(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.fontSize = 9f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = (Color)new Color32(203, 213, 225, 255);
            label.style.marginRight = 4f;
            parent.Add(label);
        }

        private static void AddToolbarDivider(VisualElement parent)
        {
            var divider = new VisualElement();
            divider.style.width = 1f;
            divider.style.height = 18f;
            divider.style.marginLeft = 5f;
            divider.style.marginRight = 7f;
            divider.style.backgroundColor = (Color)new Color32(71, 85, 105, 255);
            parent.Add(divider);
        }

        private static void StyleToolbarButton(Button button)
        {
            button.style.height = ToolbarHeight;
            button.style.marginRight = 3f;
            button.style.paddingLeft = 7f;
            button.style.paddingRight = 7f;
        }

        private static void SetFeedback(Label feedback, string message, bool isWarning)
        {
            feedback.text = message;
            feedback.style.color = isWarning
                ? (Color)new Color32(251, 191, 36, 255)
                : (Color)new Color32(125, 211, 252, 255);
        }

        private static string BuildPreviewGlyphText(StyledCharacter character)
        {
            return character.Character switch
            {
                ' ' => "\u00a0",
                '\t' => "\u00a0\u00a0\u00a0\u00a0",
                _ => character.Character.ToString()
            };
        }

        private static float GetPreviewSizeScale(string value)
        {
            return value switch
            {
                "75%" => 0.75f,
                "120%" => 1.2f,
                "125%" => 1.25f,
                "140%" => 1.4f,
                "160%" => 1.6f,
                _ => 1f
            };
        }

        private static float PreviewHash(float value)
        {
            float sine = Mathf.Sin(value * 12.9898f) * 43758.5453f;
            return sine - Mathf.Floor(sine);
        }

        private static bool AllCharactersMatch(
            DialogueDocument document,
            int firstIndex,
            int lastIndex,
            Func<DialogueStyle, bool> predicate)
        {
            if (lastIndex <= firstIndex)
            {
                return false;
            }

            for (int index = firstIndex; index < lastIndex; index++)
            {
                if (!predicate(document.Characters[index].Style))
                {
                    return false;
                }
            }

            return true;
        }

        private static DialogueDocument ParseDocument(string markup)
        {
            var document = new DialogueDocument();
            if (string.IsNullOrEmpty(markup))
            {
                return document;
            }

            int boldDepth = 0;
            int italicDepth = 0;
            var sizes = new Stack<string>();
            var colors = new Stack<string>();
            var fonts = new Stack<string>();
            var effects = new Stack<string>();
            int soundCueDepth = 0;

            for (int index = 0; index < markup.Length; index++)
            {
                if (markup[index] == '<')
                {
                    int closingBracket = markup.IndexOf('>', index + 1);
                    if (closingBracket >= 0)
                    {
                        string tag = markup.Substring(
                            index + 1,
                            closingBracket - index - 1).Trim();
                        if (TryConsumeFormattingTag(
                            tag,
                            ref boldDepth,
                            ref italicDepth,
                            sizes,
                            colors,
                            fonts,
                            effects,
                            ref soundCueDepth))
                        {
                            index = closingBracket;
                            continue;
                        }
                    }
                }

                document.Characters.Add(new StyledCharacter
                {
                    Character = markup[index],
                    Style = new DialogueStyle
                    {
                        Bold = boldDepth > 0,
                        Italic = italicDepth > 0,
                        Size = sizes.Count > 0 ? sizes.Peek() : null,
                        Color = colors.Count > 0 ? colors.Peek() : null,
                        Font = fonts.Count > 0 ? fonts.Peek() : null,
                        Effect = effects.Count > 0 ? effects.Peek() : null,
                        SoundCue = soundCueDepth > 0
                    }
                });
            }

            return document;
        }

        private static bool TryConsumeFormattingTag(
            string tag,
            ref int boldDepth,
            ref int italicDepth,
            Stack<string> sizes,
            Stack<string> colors,
            Stack<string> fonts,
            Stack<string> effects,
            ref int soundCueDepth)
        {
            if (tag.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                boldDepth++;
                return true;
            }

            if (tag.Equals("/b", StringComparison.OrdinalIgnoreCase))
            {
                boldDepth = Mathf.Max(0, boldDepth - 1);
                return true;
            }

            if (tag.Equals("i", StringComparison.OrdinalIgnoreCase))
            {
                italicDepth++;
                return true;
            }

            if (tag.Equals("/i", StringComparison.OrdinalIgnoreCase))
            {
                italicDepth = Mathf.Max(0, italicDepth - 1);
                return true;
            }

            if (tag.StartsWith("size=", StringComparison.OrdinalIgnoreCase))
            {
                sizes.Push(CleanTagValue(tag.Substring("size=".Length)));
                return true;
            }

            if (tag.Equals("/size", StringComparison.OrdinalIgnoreCase))
            {
                if (sizes.Count > 0)
                {
                    sizes.Pop();
                }

                return true;
            }

            if (tag.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
            {
                colors.Push(CleanTagValue(tag.Substring("color=".Length)));
                return true;
            }

            if (tag.Equals("/color", StringComparison.OrdinalIgnoreCase))
            {
                if (colors.Count > 0)
                {
                    colors.Pop();
                }

                return true;
            }

            if (tag.StartsWith("font=", StringComparison.OrdinalIgnoreCase))
            {
                fonts.Push(CleanTagValue(tag.Substring("font=".Length)));
                return true;
            }

            if (tag.Equals("/font", StringComparison.OrdinalIgnoreCase))
            {
                if (fonts.Count > 0)
                {
                    fonts.Pop();
                }

                return true;
            }

            if (tag.StartsWith("link=", StringComparison.OrdinalIgnoreCase))
            {
                string effect = CleanTagValue(tag.Substring("link=".Length));
                if (effect.Equals("novelify-sound", StringComparison.OrdinalIgnoreCase))
                {
                    soundCueDepth++;
                    return true;
                }
                if (effect.Equals("novelify-wave", StringComparison.OrdinalIgnoreCase) ||
                    effect.Equals("novelify-shake", StringComparison.OrdinalIgnoreCase))
                {
                    effects.Push(effect.ToLowerInvariant());
                    return true;
                }
            }

            if (tag.Equals("/link", StringComparison.OrdinalIgnoreCase) &&
                (effects.Count > 0 || soundCueDepth > 0))
            {
                if (soundCueDepth > 0) soundCueDepth--;
                else effects.Pop();
                return true;
            }

            return false;
        }

        private static string CleanTagValue(string value)
        {
            return value.Trim().Trim('"', '\'');
        }

        private static DialogueDocument MergePlainTextEdit(
            DialogueDocument existing,
            string newText)
        {
            string oldText = existing.GetPlainText();
            if (oldText == newText)
            {
                return existing;
            }

            int prefixLength = 0;
            int comparableLength = Mathf.Min(oldText.Length, newText.Length);
            while (prefixLength < comparableLength &&
                   oldText[prefixLength] == newText[prefixLength])
            {
                prefixLength++;
            }

            int suffixLength = 0;
            while (suffixLength < oldText.Length - prefixLength &&
                   suffixLength < newText.Length - prefixLength &&
                   oldText[oldText.Length - suffixLength - 1] ==
                   newText[newText.Length - suffixLength - 1])
            {
                suffixLength++;
            }

            DialogueStyle insertedStyle = default;
            if (prefixLength > 0)
            {
                insertedStyle = existing.Characters[prefixLength - 1].Style;
            }
            else if (existing.Characters.Count > suffixLength)
            {
                insertedStyle = existing.Characters[0].Style;
            }

            var result = new DialogueDocument();
            for (int index = 0; index < prefixLength; index++)
            {
                result.Characters.Add(existing.Characters[index]);
            }

            int insertedEnd = newText.Length - suffixLength;
            for (int index = prefixLength; index < insertedEnd; index++)
            {
                result.Characters.Add(new StyledCharacter
                {
                    Character = newText[index],
                    Style = insertedStyle
                });
            }

            int oldSuffixStart = oldText.Length - suffixLength;
            int newSuffixStart = newText.Length - suffixLength;
            for (int offset = 0; offset < suffixLength; offset++)
            {
                StyledCharacter character = existing.Characters[oldSuffixStart + offset];
                character.Character = newText[newSuffixStart + offset];
                result.Characters.Add(character);
            }

            return result;
        }

        private static string SerializeDocument(DialogueDocument document)
        {
            var result = new StringBuilder();
            DialogueStyle activeStyle = default;

            foreach (StyledCharacter character in document.Characters)
            {
                if (!activeStyle.Equals(character.Style))
                {
                    AppendClosingTags(result, activeStyle);
                    AppendOpeningTags(result, character.Style);
                    activeStyle = character.Style;
                }

                result.Append(character.Character);
            }

            AppendClosingTags(result, activeStyle);
            return result.ToString();
        }

        private static void AppendOpeningTags(StringBuilder result, DialogueStyle style)
        {
            if (style.Bold)
            {
                result.Append("<b>");
            }

            if (style.Italic)
            {
                result.Append("<i>");
            }

            if (!string.IsNullOrEmpty(style.Size))
            {
                result.Append("<size=").Append(style.Size).Append('>');
            }

            if (!string.IsNullOrEmpty(style.Color))
            {
                result.Append("<color=").Append(style.Color).Append('>');
            }

            if (!string.IsNullOrEmpty(style.Font))
            {
                result.Append("<font=\"")
                    .Append(style.Font.Replace("\"", string.Empty))
                    .Append("\">");
            }

            if (!string.IsNullOrEmpty(style.Effect))
            {
                result.Append("<link=\"").Append(style.Effect).Append("\">");
            }
            else if (style.SoundCue)
            {
                result.Append("<link=\"novelify-sound\">");
            }
        }

        private static void AppendClosingTags(StringBuilder result, DialogueStyle style)
        {
            if (!string.IsNullOrEmpty(style.Effect) || style.SoundCue)
            {
                result.Append("</link>");
            }

            if (!string.IsNullOrEmpty(style.Font))
            {
                result.Append("</font>");
            }

            if (!string.IsNullOrEmpty(style.Color))
            {
                result.Append("</color>");
            }

            if (!string.IsNullOrEmpty(style.Size))
            {
                result.Append("</size>");
            }

            if (style.Italic)
            {
                result.Append("</i>");
            }

            if (style.Bold)
            {
                result.Append("</b>");
            }
        }

        private void DrawRichDialogueEditor(
            Rect position,
            SerializedProperty property)
        {
            EditorGUI.BeginProperty(position, GUIContent.none, property);

            const float outerPadding = 6f;
            const float gap = 4f;
            float y = position.y + outerPadding;
            Rect contentRect = new(
                position.x + outerPadding,
                y,
                position.width - outerPadding * 2f,
                position.height - outerPadding * 2f);

            EditorGUI.DrawRect(position, new Color32(11, 18, 32, 255));

            Rect titleRect = new(
                contentRect.x,
                y,
                contentRect.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(titleRect, "Dialogue", EditorStyles.boldLabel);
            y = titleRect.yMax + gap;

            string selectionKey = GetSelectionKey(property);
            Rect toolbarRect = new(contentRect.x, y, contentRect.width, ToolbarHeight);
            DrawToolbar(toolbarRect, property, selectionKey);
            y = toolbarRect.yMax + gap;

            Rect sourceRect = new(contentRect.x, y, contentRect.width, SourceHeight);
            DrawSourceTextArea(sourceRect, property, selectionKey);
            y = sourceRect.yMax + gap;

            Rect previewTitleRect = new(
                contentRect.x,
                y,
                contentRect.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(previewTitleRect, "Preview", EditorStyles.miniBoldLabel);
            y = previewTitleRect.yMax + 2f;

            Rect previewRect = new(contentRect.x, y, contentRect.width, PreviewHeight);
            DrawPreview(previewRect, property.stringValue);

            EditorGUI.EndProperty();
        }

        private static void DrawToolbar(
            Rect rect,
            SerializedProperty property,
            string selectionKey)
        {
            const float gap = 3f;
            float x = rect.x;

            if (DrawToolbarButton(ref x, rect.y, 44f, "Bold", "Toggle bold"))
            {
                ApplyMarkup(property, selectionKey, "<b>", "</b>");
            }

            if (DrawToolbarButton(ref x, rect.y, 46f, "Italic", "Toggle italic"))
            {
                ApplyMarkup(property, selectionKey, "<i>", "</i>");
            }

            if (DrawToolbarButton(ref x, rect.y, 50f, "Sound", "Start the dialogue sound at the selected word"))
            {
                ApplyMarkup(property, selectionKey, "<link=\"novelify-sound\">", "</link>");
            }

            x += gap;
            DrawSizeButton(ref x, rect.y, property, selectionKey, "Small", "75%", 46f);
            DrawSizeButton(ref x, rect.y, property, selectionKey, "Normal", "100%", 52f);
            DrawSizeButton(ref x, rect.y, property, selectionKey, "Large", "125%", 46f);
            DrawSizeButton(ref x, rect.y, property, selectionKey, "Extra", "160%", 44f);

            x += gap;
            float availableWidth = Mathf.Max(44f, rect.xMax - x - 55f - gap);
            Rect colorRect = new(x, rect.y, Mathf.Min(64f, availableWidth), rect.height);
            _selectedColor = EditorGUI.ColorField(
                colorRect,
                GUIContent.none,
                _selectedColor,
                true,
                false,
                false);
            x = colorRect.xMax + gap;

            Rect applyColorRect = new(x, rect.y, Mathf.Max(66f, rect.xMax - x), rect.height);
            if (GUI.Button(applyColorRect, new GUIContent("Apply color", "Apply selected color")))
            {
                string hex = ColorUtility.ToHtmlStringRGB(_selectedColor);
                ApplyMarkup(property, selectionKey, $"<color=#{hex}>", "</color>");
            }
        }

        private static bool DrawToolbarButton(
            ref float x,
            float y,
            float width,
            string text,
            string tooltip)
        {
            Rect buttonRect = new(x, y, width, ToolbarHeight);
            x = buttonRect.xMax + 3f;
            return GUI.Button(buttonRect, new GUIContent(text, tooltip));
        }

        private static void DrawSizeButton(
            ref float x,
            float y,
            SerializedProperty property,
            string selectionKey,
            string label,
            string size,
            float width = 28f)
        {
            if (DrawToolbarButton(
                ref x,
                y,
                width,
                label,
                $"Set selected text to {size}"))
            {
                ApplyMarkup(property, selectionKey, $"<size={size}>", "</size>");
            }
        }

        private static void DrawSourceTextArea(
            Rect rect,
            SerializedProperty property,
            string selectionKey)
        {
            string controlName = "NovelifyDialogue_" + selectionKey;
            bool hasPendingSelection = Selections.TryGetValue(
                selectionKey,
                out TextSelection pendingSelection) &&
                pendingSelection.RestoreOnNextDraw;
            DialogueDocument document = ParseDocument(property.stringValue);
            string plainText = document.GetPlainText();
            GUI.SetNextControlName(controlName);

            EditorGUI.BeginChangeCheck();
            string newText = EditorGUI.TextArea(rect, plainText);
            if (EditorGUI.EndChangeCheck() && !hasPendingSelection)
            {
                property.stringValue = SerializeDocument(
                    MergePlainTextEdit(document, newText));
                property.serializedObject.ApplyModifiedProperties();
            }

            if (GUI.GetNameOfFocusedControl() == controlName &&
                !hasPendingSelection)
            {
                var textEditor = (TextEditor)GUIUtility.GetStateObject(
                    typeof(TextEditor),
                    GUIUtility.keyboardControl);
                Selections[selectionKey] = new TextSelection
                {
                    Start = textEditor.selectIndex,
                    End = textEditor.cursorIndex
                };
            }

            if (hasPendingSelection)
            {
                GUI.FocusControl(controlName);
                var textEditor = (TextEditor)GUIUtility.GetStateObject(
                    typeof(TextEditor),
                    GUIUtility.keyboardControl);
                textEditor.text = ParseDocument(property.stringValue).GetPlainText();
                textEditor.selectIndex = pendingSelection.Start;
                textEditor.cursorIndex = pendingSelection.End;
                pendingSelection.RestoreOnNextDraw = false;
                Selections[selectionKey] = pendingSelection;
            }
        }

        private void DrawPreview(Rect rect, string text)
        {
            EnsurePreviewStyles();
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            Rect textRect = new(
                rect.x + 7f,
                rect.y + 5f,
                rect.width - 14f,
                rect.height - 10f);
            bool isEmpty = string.IsNullOrEmpty(text);
            GUI.Label(
                textRect,
                isEmpty ? "Your formatted dialogue will appear here." : text,
                isEmpty ? _previewPlaceholderStyle : _previewStyle);
        }

        private void EnsurePreviewStyles()
        {
            _previewStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                richText = true,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.UpperLeft
            };
            _previewPlaceholderStyle ??= new GUIStyle(_previewStyle)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color32(100, 116, 139, 255) }
            };
        }

        private static void ApplyMarkup(
            SerializedProperty property,
            string selectionKey,
            string openingTag,
            string closingTag)
        {
            DialogueDocument document = ParseDocument(property.stringValue);
            Selections.TryGetValue(selectionKey, out TextSelection selection);
            int firstIndex = Mathf.Clamp(
                Mathf.Min(selection.Start, selection.End),
                0,
                document.Characters.Count);
            int lastIndex = Mathf.Clamp(
                Mathf.Max(selection.Start, selection.End),
                0,
                document.Characters.Count);

            if (lastIndex <= firstIndex)
            {
                return;
            }

            bool isBold = openingTag == "<b>";
            bool isItalic = openingTag == "<i>";
            bool isSoundCue = openingTag.Contains("novelify-sound");
            if (isSoundCue)
            {
                for (int index = 0; index < document.Characters.Count; index++)
                {
                    StyledCharacter existing = document.Characters[index];
                    existing.Style.SoundCue = false;
                    document.Characters[index] = existing;
                }
            }
            bool toggleValue = true;
            if (isBold)
            {
                toggleValue = !AllCharactersMatch(
                    document,
                    firstIndex,
                    lastIndex,
                    style => style.Bold);
            }
            else if (isItalic)
            {
                toggleValue = !AllCharactersMatch(
                    document,
                    firstIndex,
                    lastIndex,
                    style => style.Italic);
            }

            for (int index = firstIndex; index < lastIndex; index++)
            {
                StyledCharacter character = document.Characters[index];
                if (isBold)
                {
                    character.Style.Bold = toggleValue;
                }
                else if (isItalic)
                {
                    character.Style.Italic = toggleValue;
                }
                else if (openingTag.StartsWith("<size=", StringComparison.Ordinal))
                {
                    string size = openingTag.Substring(6, openingTag.Length - 7);
                    character.Style.Size = size == "100%" ? null : size;
                }
                else if (openingTag.StartsWith("<color=", StringComparison.Ordinal))
                {
                    character.Style.Color = openingTag.Substring(7, openingTag.Length - 8);
                }
                else if (isSoundCue)
                {
                    character.Style.SoundCue = true;
                    character.Style.Effect = null;
                }

                document.Characters[index] = character;
            }

            property.stringValue = SerializeDocument(document);
            property.serializedObject.ApplyModifiedProperties();
            GUI.changed = true;

            Selections[selectionKey] = new TextSelection
            {
                Start = firstIndex,
                End = lastIndex,
                RestoreOnNextDraw = true
            };
        }

        private static string GetSelectionKey(SerializedProperty property)
        {
            return property.serializedObject.targetObject.GetEntityId().ToString() +
                ":" +
                property.propertyPath;
        }

        private sealed class DialogueDocument
        {
            public readonly List<StyledCharacter> Characters = new();

            public string GetPlainText()
            {
                var result = new StringBuilder(Characters.Count);
                foreach (StyledCharacter character in Characters)
                {
                    result.Append(character.Character);
                }

                return result.ToString();
            }
        }

        private struct StyledCharacter
        {
            public char Character;
            public DialogueStyle Style;
        }

        private struct AnimatedPreviewGlyph
        {
            public VisualElement Element;
            public char Character;
            public string Effect;
            public int CharacterIndex;
        }

        private struct DialogueStyle : IEquatable<DialogueStyle>
        {
            public bool Bold;
            public bool Italic;
            public string Size;
            public string Color;
            public string Font;
            public string Effect;
            public bool SoundCue;

            public bool IsDefault =>
                !Bold &&
                !Italic &&
                string.IsNullOrEmpty(Size) &&
                string.IsNullOrEmpty(Color) &&
                string.IsNullOrEmpty(Font) &&
                string.IsNullOrEmpty(Effect) &&
                !SoundCue;

            public bool Equals(DialogueStyle other)
            {
                return Bold == other.Bold &&
                    Italic == other.Italic &&
                    string.Equals(Size, other.Size, StringComparison.Ordinal) &&
                    string.Equals(Color, other.Color, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Font, other.Font, StringComparison.Ordinal) &&
                    string.Equals(Effect, other.Effect, StringComparison.OrdinalIgnoreCase) &&
                    SoundCue == other.SoundCue;
            }

            public override bool Equals(object obj)
            {
                return obj is DialogueStyle other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(
                    Bold,
                    Italic,
                    Size,
                    Color?.ToUpperInvariant(),
                    Font,
                    Effect?.ToUpperInvariant(),
                    SoundCue);
            }
        }

        private struct TextSelection
        {
            public int Start;
            public int End;
            public bool RestoreOnNextDraw;
        }
    }
}
