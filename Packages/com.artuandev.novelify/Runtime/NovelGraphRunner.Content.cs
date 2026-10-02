namespace Novelify
{
    public partial class NovelGraphRunner
    {
        private INovelContentProvider _contentProvider;

        public INovelContentProvider ContentProvider => _contentProvider;

        /// <summary>Assign a chapter loader before calling Session.PlayChapterAsync.</summary>
        internal void UseContentProvider(INovelContentProvider provider) => _contentProvider = provider;
    }
}
