using BetterSongList.UI;

namespace BetterSongList.Api {
	public class BetterSongListApi {
		public static void RegisterSorter(ISortFilter sorter) {
			FilterUI.sortOptions.Add(sorter.Name, sorter);
		}
	}
}
