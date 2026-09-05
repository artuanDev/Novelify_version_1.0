using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Novelify.Tests
{
    public class NovelCharacterTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private GameObject _root, _prefab;
        private NovelCharacterStage _stage;

        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            _root = Keep(new GameObject("Stage", typeof(RectTransform)));
            _prefab = Keep(new GameObject("Portrait", typeof(RectTransform), typeof(CharacterInfo)));
            _prefab.SetActive(false);
            foreach (string layer in new[] { "PortraitBackground", "PortraitEyes", "PortraitEyesDetails", "PortraitMouth" })
                new GameObject(layer, typeof(RectTransform), typeof(Image)).transform.SetParent(_prefab.transform, false);
            _stage = new NovelCharacterStage(_root.transform, _prefab);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; --i)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test]
        public void ManyCharactersWithIdenticalDisplayNamesHaveIndependentInstances()
        {
            for (int i = 0; i < 32; ++i)
            {
                var character = Keep(ScriptableObject.CreateInstance<NovelCharacter>());
                character.SpeakerName = "Same Name";
                CharacterInfo info = _stage.Show(character);
                Assert.That(_stage.Show(character), Is.SameAs(info));
            }
            Assert.That(_stage.Characters.Count, Is.EqualTo(32));
        }

        [Test]
        public void InstanceIdsAllowCopiesAndHiddenCharactersAreReused()
        {
            var character = Keep(ScriptableObject.CreateInstance<NovelCharacter>());
            CharacterInfo first = _stage.Show(character, "left");
            CharacterInfo second = _stage.Show(character, "right");
            Assert.That(first, Is.Not.SameAs(second));
            first.Position = new Vector2(100, 20);
            _stage.Hide(character, "left");
            Assert.That(second.gameObject.activeSelf, Is.True);
            Assert.That(_stage.Show(character, "left"), Is.SameAs(first));
            Assert.That(first.Position, Is.EqualTo(new Vector2(100, 20)));
            Assert.That(_stage.Characters.Count, Is.EqualTo(2));
        }

        [Test]
        public void PreplacedInactiveCharactersAreRegisteredAndDestroyedCharactersCanRespawn()
        {
            var character = Keep(ScriptableObject.CreateInstance<NovelCharacter>());
            var existing = Object.Instantiate(_prefab, _root.transform).GetComponent<CharacterInfo>();
            existing.Initialize(character);
            var stage = new NovelCharacterStage(_root.transform, _prefab);
            Assert.That(stage.Show(character), Is.SameAs(existing));
            Object.DestroyImmediate(existing.gameObject);
            Assert.That(stage.Show(character), Is.Not.Null);
            Assert.That(stage.Characters.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExpressionOverridesOnlyAssignedLayersAndMissingLayersAreInvisible()
        {
            var character = Keep(ScriptableObject.CreateInstance<NovelCharacter>());
            var texture = Keep(new Texture2D(4, 4));
            var body = Keep(Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f));
            var mouth = Keep(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f));
            character.PortraitBody = body;
            character.Expressions.Add(new CharacterExpression { Emotion = CharacterEmotion.Happy, Mouth = mouth });
            CharacterInfo info = _stage.Show(character);
            info.SetEmotion(CharacterEmotion.Happy);
            Assert.That(info.Body.sprite, Is.SameAs(body));
            Assert.That(info.Mouth.sprite, Is.SameAs(mouth));
            Assert.That(info.Eyes.enabled, Is.False);
            Assert.That(info.Body.raycastTarget, Is.False);
            info.SetEmotion(CharacterEmotion.Sad);
            Assert.That(info.Body.sprite, Is.SameAs(body));
            Assert.That(info.Mouth.enabled, Is.False);
        }

        [Test]
        public void InstantAndZeroDurationMovesCancelEarlierMotionAndPreserveDepth()
        {
            var character = Keep(ScriptableObject.CreateInstance<NovelCharacter>());
            CharacterInfo info = _stage.Show(character);
            info.transform.localPosition = new Vector3(0, 0, 7);
            info.MoveTo(new Vector2(300, 80), true, 1);
            Assert.That(info.IsMoving, Is.True);
            info.MoveTo(new Vector2(20, 30), false, 1);
            Assert.That(info.IsMoving, Is.False);
            Assert.That(info.Position, Is.EqualTo(new Vector2(20, 30)));
            Assert.That(info.transform.localPosition.z, Is.EqualTo(7));
            info.MoveTo(Vector2.zero, true, 0);
            Assert.That(info.Position, Is.EqualTo(Vector2.zero));
            Assert.That(info.IsMoving, Is.False);
        }
    }
}
