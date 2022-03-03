using BetterSongList.Api;
using BetterSongList.SortModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BetterSongList.Sorters {
	internal class AdaptedSortFilter : ISortFilter {
		public AdaptedSortFilter(string name, ISorter sorter) {
			Name = name;
			Sorter = sorter;
		}

		public ISorter Sorter { get; private set; }

		public string Name { get; private set; }

		public event Action<ISortFilterResult> OnResultChanged;

		public async void NotifyChange(IEnumerable<IPreviewBeatmapLevel> newLevels, bool isSelected, CancellationToken? token) {
			if(!Sorter.isReady) {
				await Sorter.Prepare(CancellationToken.None).ConfigureAwait(false);
			}
			if(!isSelected || newLevels == null) {
				return;
			}
			if(Sorter is BasicSongDetailsSorterWithLegend sorter) {
				sorter.DoSort(ref newLevels);
				var legend = sorter.BuildLegend(newLevels.ToArray()).Select(x => (x.Key, x.Value));
				OnResultChanged(new SortFilterResult(newLevels, legend));
			} else {
				OnResultChanged(new SortFilterResult(newLevels.OrderBy(x => x, Sorter)));
			}
		}
	}
}
