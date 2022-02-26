using BetterSongList.Api;
using BetterSongList.SortModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BetterSongList.Sorters {
	internal class AdaptedSortFilter : ISortFilter {
		public AdaptedSortFilter(string name, ISorter sorter) {
			Name = name;
			Sorter = sorter;
			IsVisible.value = true;
		}

		public ISorter Sorter { get; private set; }

		public string Name { get; private set; }

		public ObservableVariable<bool> IsVisible { get; private set; } = new ObservableVariable<bool>();

		public ObservableVariable<IEnumerable<IPreviewBeatmapLevel>> ResultLevels { get; private set; } = new ObservableVariable<IEnumerable<IPreviewBeatmapLevel>>();

		public async Task NotifyChange(IEnumerable<IPreviewBeatmapLevel> newLevels, bool isSelected = false, CancellationToken? token = null) {
			if(!Sorter.isReady) {
				await Sorter.Prepare(CancellationToken.None).ConfigureAwait(false);
			}
			if(!isSelected || newLevels == null) {
				return;
			}
			if(Sorter is BasicSongDetailsSorterWithLegend sorter) {
				sorter.DoSort(ref newLevels);
				ResultLevels.value = newLevels;
			} else {
				ResultLevels.value = newLevels.OrderBy(x => x, Sorter).ToList();
			}
		}
	}
}
