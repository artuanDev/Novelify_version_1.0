using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Novelify
{
    /// <summary>
    /// Resolves chapter IDs to compiled graphs. Loaded graphs must also be discoverable
    /// by their stable GraphID so saved sessions can restore them. Use on Unity's main thread.
    /// </summary>
    public interface INovelContentProvider
    {
        bool TryGetLoadedGraph(string chapterID, out RuntimeNovelGraph graph);
        Task<RuntimeNovelGraph> LoadGraphAsync(string chapterID, CancellationToken cancellationToken);
    }

    /// <summary>Reads graphs already referenced by a generated runtime catalog.</summary>
    public sealed class NovelCatalogContentProvider : INovelContentProvider
    {
        private readonly NovelGraphCatalog _catalog;

        public NovelCatalogContentProvider(NovelGraphCatalog catalog) =>
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        public bool TryGetLoadedGraph(string chapterID, out RuntimeNovelGraph graph) =>
            _catalog.TryGetGraph(chapterID, out graph);

        public Task<RuntimeNovelGraph> LoadGraphAsync(string chapterID, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryGetLoadedGraph(chapterID, out RuntimeNovelGraph graph);
            return Task.FromResult(graph);
        }
    }

    /// <summary>
    /// Loads compiled graphs from Resources asynchronously. A chapter ID is a Resources-relative
    /// path without its extension, e.g. "Chapters/Opening". For Addressables or remote content,
    /// implement <see cref="INovelContentProvider"/> instead.
    /// </summary>
    public sealed class NovelResourcesContentProvider : INovelContentProvider
    {
        private readonly Dictionary<string, RuntimeNovelGraph> _loaded =
            new Dictionary<string, RuntimeNovelGraph>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _paths =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Map a stable graph ID to its Resources-relative path for loading saves on a fresh launch.</summary>
        public void Register(string graphID, string resourcesPath)
        {
            if (string.IsNullOrWhiteSpace(graphID) || string.IsNullOrWhiteSpace(resourcesPath))
                throw new ArgumentException("Both graph ID and Resources path are required.");
            string path = resourcesPath.Trim().Replace('\\', '/');
            if (path.EndsWith(".novelgraph", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - ".novelgraph".Length);
            _paths[graphID.Trim()] = path;
        }

        public bool TryGetLoadedGraph(string chapterID, out RuntimeNovelGraph graph) =>
            _loaded.TryGetValue(chapterID ?? string.Empty, out graph) && graph != null;

        public async Task<RuntimeNovelGraph> LoadGraphAsync(
            string chapterID, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(chapterID))
                throw new ArgumentException("A chapter ID is required.", nameof(chapterID));
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetLoadedGraph(chapterID, out RuntimeNovelGraph cached)) return cached;

            string path = _paths.TryGetValue(chapterID, out string registered) ? registered : chapterID;
            ResourceRequest request = Resources.LoadAsync<RuntimeNovelGraph>(path);
            while (!request.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
            cancellationToken.ThrowIfCancellationRequested();
            RuntimeNovelGraph graph = request.asset as RuntimeNovelGraph;
            if (graph != null)
            {
                _loaded[chapterID] = graph;
                if (!string.IsNullOrEmpty(graph.GraphID)) _loaded[graph.GraphID] = graph;
            }
            return graph;
        }
    }

    /// <summary>Stores snapshots and profile data. Unity callbacks use its synchronous results.</summary>
    public interface INovelSaveProvider
    {
        NovelPersistenceResult SaveSlot(string slotID, NovelSaveData snapshot);
        NovelPersistenceResult LoadSlot(string slotID, out NovelSaveData snapshot);
        NovelPersistenceResult SaveProfile(NovelProfileSaveData profile);
        NovelPersistenceResult LoadProfile(out NovelProfileSaveData profile);
        IReadOnlyList<string> ListSlots();
        NovelPersistenceResult DeleteSlot(string slotID);
    }
}
