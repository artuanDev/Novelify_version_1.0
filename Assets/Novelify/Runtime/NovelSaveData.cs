using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    public enum NovelPersistenceStatus
    {
        Success,
        Pending,
        Migrated,
        RestoredBackup,
        NotFound,
        InvalidSlot,
        Corrupt,
        Incompatible,
        UnsupportedValue,
        IOError
    }

    public readonly struct NovelPersistenceResult
    {
        public readonly NovelPersistenceStatus Status;
        public readonly string Message;
        public bool Succeeded => Status == NovelPersistenceStatus.Success ||
                                 Status == NovelPersistenceStatus.Migrated ||
                                 Status == NovelPersistenceStatus.RestoredBackup;

        public NovelPersistenceResult(NovelPersistenceStatus status, string message = null)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public override string ToString() => string.IsNullOrEmpty(Message) ? Status.ToString() : $"{Status}: {Message}";
    }

    [Serializable]
    public sealed class NovelSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int SchemaVersion = CurrentSchemaVersion;
        public string TimestampUtc;
        public string CheckpointID;
        public string CheckpointGraphID;
        public NovelGraphState CurrentGraph = new NovelGraphState();
        public string CurrentNodeID;
        public NovelValueScopeData CurrentScope = new NovelValueScopeData();
        [Tooltip("Oldest caller first, current caller last.")]
        public List<NovelExecutionFrameData> Frames = new List<NovelExecutionFrameData>();
        public List<NovelSavedVariable> StoryVariables = new List<NovelSavedVariable>();
        public List<string> SelectedChoiceIDs = new List<string>();
        public List<NovelVisitData> Visits = new List<NovelVisitData>();
        public List<string> ReadLineIDs = new List<string>();
        public List<NovelCharacterStateData> Characters = new List<NovelCharacterStateData>();
        public List<NovelHistoryEntryData> History = new List<NovelHistoryEntryData>();
    }

    [Serializable]
    public sealed class NovelProfileSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int SchemaVersion = CurrentSchemaVersion;
        public string TimestampUtc;
        public List<NovelSavedVariable> ProfileVariables = new List<NovelSavedVariable>();
    }

    [Serializable]
    public sealed class NovelGraphState
    {
        public string GraphID;
        public string ContentVersion;
        public int GraphSchemaVersion;
    }

    [Serializable]
    public sealed class NovelExecutionFrameData
    {
        public NovelGraphState Graph = new NovelGraphState();
        public string ReturnNodeID;
        public string CallSiteID;
        public NovelValueScopeData Scope = new NovelValueScopeData();
    }

    [Serializable]
    public sealed class NovelValueScopeData
    {
        public List<NovelNamedValueData> Inputs = new List<NovelNamedValueData>();
        public List<NovelNamedValueData> Locals = new List<NovelNamedValueData>();
        public List<NovelCachedOutputData> CachedOutputs = new List<NovelCachedOutputData>();
    }

    [Serializable]
    public sealed class NovelNamedValueData
    {
        public string Name;
        public NovelSerializedValue Value = new NovelSerializedValue();
    }

    [Serializable]
    public sealed class NovelCachedOutputData
    {
        public string NodeID;
        public string Name;
        public NovelSerializedValue Value = new NovelSerializedValue();
    }

    [Serializable]
    public sealed class NovelSerializedValue
    {
        public RuntimeValueKind Kind;
        public float FloatValue;
        public int IntegerValue;
        public bool BooleanValue;
        public string StringValue;
        public Vector2 Vector2Value;
        public string AssetID;
        public string InstanceID;
    }

    [Serializable]
    public sealed class NovelSavedVariable
    {
        public string VariableID;
        public NovelSerializedValue Value = new NovelSerializedValue();
    }

    [Serializable]
    public sealed class NovelVisitData
    {
        public string GraphID;
        public string NodeID;
        public int Count;
    }

    [Serializable]
    public sealed class NovelCharacterStateData
    {
        public string CharacterID;
        public string InstanceID;
        public bool Visible;
        public CharacterEmotion Emotion;
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale = Vector2.one;
        public CharacterFacing Facing = CharacterFacing.Right;
    }

    [Serializable]
    public sealed class NovelHistoryEntryData
    {
        public string GraphID;
        public string LineID;
        public string Speaker;
        public string ResolvedText;
        public List<NovelNamedValueData> Substitutions = new List<NovelNamedValueData>();
    }

    [Serializable]
    internal sealed class NovelSaveEnvelope
    {
        public int EnvelopeVersion = 1;
        public string Checksum;
        public string Payload;
    }
}
