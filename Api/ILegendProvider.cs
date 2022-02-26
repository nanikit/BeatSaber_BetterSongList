#nullable enable
using System.Collections.Generic;

namespace BetterSongList.Api {
	public interface ILegendProvider {
		ObservableVariable<IEnumerable<(string Label, int Index)>> Legend { get; }
	}
}
