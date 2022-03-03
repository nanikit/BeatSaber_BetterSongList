#nullable enable

namespace BetterSongList.Api {
	using System.Collections.Generic;

	public interface ISortFilterResult {
		/// <summary>
		/// Sort / filter result.
		/// </summary>
		IEnumerable<IPreviewBeatmapLevel> Levels { get; }

		IEnumerable<(string Label, int Index)>? Legend { get; }
	}

	public class SortFilterResult : ISortFilterResult {
		public IEnumerable<IPreviewBeatmapLevel> Levels => _levels;
		public IEnumerable<(string Label, int Index)>? Legend => _legend;

		public SortFilterResult(IEnumerable<IPreviewBeatmapLevel> levels, IEnumerable<(string Label, int Index)>? legend = null) {
			_levels = levels;
			_legend = legend;
		}

		private readonly IEnumerable<IPreviewBeatmapLevel> _levels;
		private readonly IEnumerable<(string Label, int Index)>? _legend;
	}
}
