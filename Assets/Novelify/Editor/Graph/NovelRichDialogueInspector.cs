using System;
using System.Collections.Generic;
using System.Text;
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

        public RichDialogueText(string text)
        {
            Text = text;
        }
    }

    /// <summary>
    /// Draws Novelify dialogue as a full-width rich-text authoring surface in
    /// both UI Toolkit and IMGUI inspectors.
    /// </summary>
    [CustomPropertyDrawer(typeof(RichDialogueText))]
    internal sealed class NovelRichDialogueInspector : PropertyDrawer
    {
        private const float RichEditorHeight = 342f;
        private const float ToolbarHeight = 23f;
        private const float SourceHeight = 132f;
        private const float PreviewHeight = 60f;

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
            var root = new VisualElement();
            root.style.width = Length.Percent(100f);
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
                "Select words in the dialogue, then apply a style. Shortcuts: Ctrl+B and Ctrl+I.");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.fontSize = 10f;
            help.style.color = (Color)new Color32(148, 163, 184, 255);
            help.style.marginBottom = 5f;
            root.Add(help);

            var source = new TextField { multiline = true };
            source.tooltip = "Write and select dialogue normally. Formatting markup stays hidden.";
            source.style.width = Length.Percent(100f);
            source.style.minHeight = SourceHeight;
            source.style.marginBottom = 5f;
            source.verticalScrollerVisibility = ScrollerVisibility.Auto;
            root.Add(source);

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
                tooltip = "Remove bold, italic, size, and color while keeping the dialogue"
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

            var previewTitle = new Label("In-game preview");
            previewTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewTitle.style.fontSize = 9f;
            previewTitle.style.marginTop = 3f;
            previewTitle.style.marginBottom = 2f;
            root.Add(previewTitle);

            var preview = new Label { enableRichText = true };
            preview.style.height = PreviewHeight;
            preview.style.whiteSpace = WhiteSpace.Normal;
            preview.style.paddingLeft = 7f;
            preview.style.paddingRight = 7f;
            preview.style.paddingTop = 5f;
            preview.style.paddingBottom = 5f;
            preview.style.backgroundColor = (Color)new Color32(7, 12, 22, 255);
            root.Add(preview);

            void RefreshPreview(string markup)
            {
                int visibleCharacters = ParseDocument(markup).Characters.Count;
                characterCount.text = visibleCharacters == 1
                    ? "1 character"
                    : $"{visibleCharacters} characters";
                preview.text = string.IsNullOrEmpty(markup)
                    ? "Write dialogue above to see how it will look in the game."
                    : markup;
            }

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
                            colors))
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
                        Color = colors.Count > 0 ? colors.Peek() : null
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
            Stack<string> colors)
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
        }

        private static void AppendClosingTags(StringBuilder result, DialogueStyle style)
        {
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

        private struct DialogueStyle : IEquatable<DialogueStyle>
        {
            public bool Bold;
            public bool Italic;
            public string Size;
            public string Color;

            public bool IsDefault =>
                !Bold &&
                !Italic &&
                string.IsNullOrEmpty(Size) &&
                string.IsNullOrEmpty(Color);

            public bool Equals(DialogueStyle other)
            {
                return Bold == other.Bold &&
                    Italic == other.Italic &&
                    string.Equals(Size, other.Size, StringComparison.Ordinal) &&
                    string.Equals(Color, other.Color, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is DialogueStyle other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Bold, Italic, Size, Color?.ToUpperInvariant());
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
