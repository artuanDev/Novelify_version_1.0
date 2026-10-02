using NUnit.Framework;
using UnityEngine;

namespace Novelify.Tests
{
    public class NovelPresentationLayoutTests
    {
        [Test]
        public void StretchedDialogueUsesSymmetricHorizontalMargins()
        {
            NovelRectLayout layout = NovelPresentationLayout.Dialogue(
                NovelDialogueAnchor.BottomCenter,
                0f,
                180f,
                32f,
                48f);

            Assert.That(layout.AnchorMin,
                Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(layout.AnchorMax,
                Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(layout.Pivot,
                Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(layout.AnchoredPosition,
                Is.EqualTo(new Vector2(0f, 32f)));
            Assert.That(layout.SizeDelta,
                Is.EqualTo(new Vector2(-96f, 180f)));
        }

        [Test]
        public void FixedTopRightDialogueOffsetsFromBothTopAndRight()
        {
            NovelRectLayout layout = NovelPresentationLayout.Dialogue(
                NovelDialogueAnchor.TopRight,
                920f,
                205f,
                32f,
                48f);

            Assert.That(layout.AnchorMin,
                Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(layout.AnchorMax,
                Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(layout.Pivot,
                Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(layout.AnchoredPosition,
                Is.EqualTo(new Vector2(-48f, -32f)));
            Assert.That(layout.SizeDelta,
                Is.EqualTo(new Vector2(920f, 205f)));
        }

        [Test]
        public void RightCenterSpeakerUsesFinalBoxWidthForOverlap()
        {
            NovelRectLayout layout = NovelPresentationLayout.Speaker(
                NovelSpeakerAnchor.RightCenter,
                282f,
                48f,
                24f,
                27f);

            Assert.That(layout.AnchorMin,
                Is.EqualTo(new Vector2(1f, 0.5f)));
            Assert.That(layout.AnchorMax,
                Is.EqualTo(new Vector2(1f, 0.5f)));
            Assert.That(layout.Pivot,
                Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(layout.AnchoredPosition,
                Is.EqualTo(new Vector2(114f, 24f)));
            Assert.That(layout.SizeDelta,
                Is.EqualTo(new Vector2(282f, 48f)));
        }

        [TestCase(NovelSpeakerAnchor.TopLeft, 0f, 1f, 0f, 0.5f)]
        [TestCase(NovelSpeakerAnchor.TopCenter, 0.5f, 1f, 0.5f, 0.5f)]
        [TestCase(NovelSpeakerAnchor.TopRight, 1f, 1f, 1f, 0.5f)]
        [TestCase(NovelSpeakerAnchor.LeftTop, 0f, 1f, 0.5f, 1f)]
        [TestCase(NovelSpeakerAnchor.LeftCenter, 0f, 0.5f, 0.5f, 0.5f)]
        [TestCase(NovelSpeakerAnchor.LeftBottom, 0f, 0f, 0.5f, 0f)]
        [TestCase(NovelSpeakerAnchor.BottomLeft, 0f, 0f, 0f, 0.5f)]
        [TestCase(NovelSpeakerAnchor.BottomCenter, 0.5f, 0f, 0.5f, 0.5f)]
        [TestCase(NovelSpeakerAnchor.BottomRight, 1f, 0f, 1f, 0.5f)]
        [TestCase(NovelSpeakerAnchor.RightBottom, 1f, 0f, 0.5f, 0f)]
        [TestCase(NovelSpeakerAnchor.RightCenter, 1f, 0.5f, 0.5f, 0.5f)]
        [TestCase(NovelSpeakerAnchor.RightTop, 1f, 1f, 0.5f, 1f)]
        public void SpeakerAnchorUsesExpectedPointAndPivot(
            NovelSpeakerAnchor anchor,
            float anchorX,
            float anchorY,
            float pivotX,
            float pivotY)
        {
            NovelRectLayout layout = NovelPresentationLayout.Speaker(
                anchor,
                260f,
                54f,
                24f,
                27f);

            Vector2 expectedAnchor = new Vector2(anchorX, anchorY);
            Vector2 expectedPivot = new Vector2(pivotX, pivotY);
            Assert.That(layout.AnchorMin, Is.EqualTo(expectedAnchor));
            Assert.That(layout.AnchorMax, Is.EqualTo(expectedAnchor));
            Assert.That(layout.Pivot, Is.EqualTo(expectedPivot));
        }

        [TestCase(NovelChoiceArrangement.Vertical)]
        [TestCase(NovelChoiceArrangement.Horizontal)]
        [TestCase(NovelChoiceArrangement.Circular)]
        public void OversizedChoiceGroupsFitInsideTheirPanel(
            NovelChoiceArrangement arrangement)
        {
            const int count = 8;
            const int perGroup = 4;
            const float padding = 12f;
            Vector2 choiceSize = new Vector2(640f, 64f);
            Vector2 panelSize = new Vector2(720f, 260f);
            int groups = Mathf.CeilToInt(count / (float)perGroup);
            float fit = NovelChoiceLayoutGroup.CalculateFitScale(
                arrangement,
                count,
                perGroup,
                choiceSize,
                14f,
                24f,
                210f,
                90f,
                360f,
                panelSize,
                padding);

            Assert.That(fit, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Vector2 halfPanel = panelSize * 0.5f - Vector2.one * padding;
            Vector2 halfChoice = choiceSize * 0.5f * fit;
            for (int index = 0; index < count; index++)
            {
                Vector2 position = NovelChoiceLayoutGroup
                    .CalculateChoicePosition(
                        arrangement,
                        index,
                        count,
                        perGroup,
                        groups,
                        choiceSize,
                        14f,
                        24f,
                        210f,
                        90f,
                        360f) * fit;
                Assert.That(position.x - halfChoice.x,
                    Is.GreaterThanOrEqualTo(-halfPanel.x - 0.01f));
                Assert.That(position.x + halfChoice.x,
                    Is.LessThanOrEqualTo(halfPanel.x + 0.01f));
                Assert.That(position.y - halfChoice.y,
                    Is.GreaterThanOrEqualTo(-halfPanel.y - 0.01f));
                Assert.That(position.y + halfChoice.y,
                    Is.LessThanOrEqualTo(halfPanel.y + 0.01f));
            }
        }

        [Test]
        public void ChoiceContentWidthTracksEntireAuthoredButtonWidth()
        {
            Vector2 narrow = NovelChoiceLayoutGroup.CalculateContentSize(
                NovelChoiceArrangement.Vertical,
                4,
                4,
                new Vector2(320f, 64f),
                14f,
                24f,
                210f,
                90f,
                360f);
            Vector2 wide = NovelChoiceLayoutGroup.CalculateContentSize(
                NovelChoiceArrangement.Vertical,
                4,
                4,
                new Vector2(760f, 64f),
                14f,
                24f,
                210f,
                90f,
                360f);

            Assert.That(narrow.x, Is.EqualTo(320f).Within(0.01f));
            Assert.That(wide.x, Is.EqualTo(760f).Within(0.01f));
            Assert.That(wide.y, Is.EqualTo(narrow.y).Within(0.01f));
        }
    }
}
