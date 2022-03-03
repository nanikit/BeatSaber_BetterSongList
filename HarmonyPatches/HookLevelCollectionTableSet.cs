using BetterSongList.Api;
using BetterSongList.FilterModels;
using BetterSongList.UI;
using BetterSongList.Util;
using HarmonyLib;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;

namespace BetterSongList.HarmonyPatches {

	// The main class that handles the modification of the data in the song list
	[HarmonyPatch(typeof(LevelCollectionTableView), nameof(LevelCollectionTableView.SetData))]
#if DEBUG
	public
#endif
	static class HookLevelCollectionTableSet {
		static readonly Thread _mainThread = Thread.CurrentThread;

		public static ISortFilter Sorter {
			get => _sorter;
			set {
				if(_sorter != null) {
					_sorter.OnResultChanged -= RefreshWithContent;
				}
				_sorter = value;
				if(_sorter != null) {
					_sorter.OnResultChanged += RefreshWithContent;
				}
			}
		}
		private static ISortFilter _sorter;
		public static IFilter filter;

		public static IPreviewBeatmapLevel[] lastInMapList { get; private set; }
		public static IPreviewBeatmapLevel[] lastOutMapList { get; private set; }
		static Action<IPreviewBeatmapLevel[]> recallLast = null;

		/// <summary>
		/// Refresh the SongList with the last used BeatMaps array
		/// </summary>
		/// <param name="processAsync"></param>
		public static void Refresh() {
			if(lastInMapList == null)
				return;
#if TRACE
			Plugin.Log.Warn(string.Format("Refresh()"));
#endif
			/*
			 * This probably has problems in regards to race conditions / thread safety... We will see...
			 * Pre-processes the desired songlist state in a second thread - This will then get stored in
			 * a vaiable and used as the result on the next SetData() in the Prefix hook below
			 */
			_sorter.NotifyChange(lastInMapList, true);
		}

		static void RefreshWithContent(ISortFilterResult result) {
			if(result == null) {
				// TODO: disable / enable sorter
				return;
			}

			var levels = result.Levels.ToArray();
			customLegend = result.Legend?.Select(x => new KeyValuePair<string, int>(x.Label, x.Index)).ToList();
			Plugin.Log.Debug($"RefreshWithContent: levels.Length = {levels.Length}, legend.Count = {customLegend?.Count}");

			void RunOnMainThread() {
				_entrancyLevel = EntrancyPhase.PrefixDepth1;
				recallLast?.Invoke(levels);
			}

			if(Thread.CurrentThread == _mainThread) {
				RunOnMainThread();
			} else {
				HMMainThreadDispatcher.instance.Enqueue(RunOnMainThread);
			}
		}

		static CancellationTokenSource sortCancelSrc;

		enum EntrancyPhase {
			None,
			PrefixDepth1,
			PrefixDepth2,
			PostfixDepth2,
		}

		static EntrancyPhase _entrancyLevel = EntrancyPhase.None;

		[HarmonyPriority(int.MaxValue)]
		static bool Prefix(
			LevelCollectionTableView __instance, TableView ____tableView,
			ref IPreviewBeatmapLevel[] previewBeatmapLevels, HashSet<string> favoriteLevelIds, ref bool beatmapLevelsAreSorted
		) {
			if(_entrancyLevel != EntrancyPhase.None) {
				Plugin.Log.Trace($"LevelCollectionTableView.SetData():Prefix re-enter: previewBeatmapLeves.Length = {previewBeatmapLevels.Length}");
				_entrancyLevel = EntrancyPhase.PrefixDepth2;
				return true;
			} else {
				Plugin.Log.Trace($"LevelCollectionTableView.SetData():Prefix enter: previewBeatmapLeves.Length = {previewBeatmapLevels.Length}");
				_entrancyLevel = EntrancyPhase.PrefixDepth1;
			}

			// If SetData is called with the literal same maplist as before we might as well ignore it
			if(previewBeatmapLevels == lastInMapList) {
				Plugin.Log.Trace("LevelCollectionTableView.SetData():Prefix => previewBeatmapLevels == lastInMapList");
				previewBeatmapLevels = lastOutMapList;
				return true;
			}

			// Playlistlib has its own custom wrapping class for Playlists so it can properly track duplicates, so we need to use its collection
			if(HookSelectedCollection.lastSelectedCollection != null && PlaylistsUtil.hasPlaylistLib) {
				if(PlaylistsUtil.requiresListCast) {
					previewBeatmapLevels = PlaylistsUtil.GetLevelsForLevelCollection(HookSelectedCollection.lastSelectedCollection)?.Cast<IPreviewBeatmapLevel>().ToArray() ?? previewBeatmapLevels;
				} else {
					previewBeatmapLevels = PlaylistsUtil.GetLevelsForLevelCollection(HookSelectedCollection.lastSelectedCollection) ?? previewBeatmapLevels;
				}
			}

			lastInMapList = previewBeatmapLevels;
			var _isSorted = beatmapLevelsAreSorted;
			recallLast = (overrideData) => __instance.SetData(overrideData ?? lastInMapList, favoriteLevelIds, _isSorted);

			//Console.WriteLine("=> {0}", new System.Diagnostics.StackTrace().ToString());

			// It may call synchoronously OnResultChanged -> RefreshWithContent ->
			// recallLast -> this.Prefix, so re-enter.
			try {
				_sorter.NotifyChange(previewBeatmapLevels, true);
			} catch(Exception e) {
				Plugin.Log.Error(e);
			}

			XD.FunnyNull(FilterUI.persistentNuts._filterLoadingIndicator)?.gameObject.SetActive(false);

			Plugin.Log.Trace($"Prefix exit _entrancyLevel: {_entrancyLevel}");

			bool alreadyBaseGameProcessed = _entrancyLevel == EntrancyPhase.PostfixDepth2;
			if(alreadyBaseGameProcessed) {
				return false;
			}

			// If this is true the default Alphabet scrollbar is processed / shown - We dont want that when we use a custom filter
			beatmapLevelsAreSorted = false;
			return true;
		}

		static List<KeyValuePair<string, int>> customLegend = null;

		static void Postfix(TableView ____tableView, AlphabetScrollbar ____alphabetScrollbar, IPreviewBeatmapLevel[] previewBeatmapLevels, bool beatmapLevelsAreSorted) {
			lastOutMapList = previewBeatmapLevels;

			Plugin.Log.Debug($"Postfix: _entrancyLevel: {_entrancyLevel}");
			switch(_entrancyLevel) {
				case EntrancyPhase.PrefixDepth1:
					_entrancyLevel = EntrancyPhase.None;
					break;
				case EntrancyPhase.PrefixDepth2:
					_entrancyLevel = EntrancyPhase.PostfixDepth2;
					break;
				case EntrancyPhase.PostfixDepth2:
					_entrancyLevel = EntrancyPhase.None;
					return;
				case EntrancyPhase.None:
					Plugin.Log.Warn($"Postfix: UNREACHABLE _entrancyLevel: {_entrancyLevel}");
					return;
			}

			// Basegame already handles cleaning up the legend etc
			if(customLegend == null) {
				return;
			}

			/*
			 * We essentially gotta double-init the alphabet scrollbar because basegame
			 * made the great decision to unnecessarily lock down the scrollbar to only
			 * use characters, not strings
			 */
			var scrollData = customLegend.Select(x => new AlphabetScrollInfo.Data('?', x.Value)).ToArray();
			____alphabetScrollbar.SetData(scrollData);

			// Now that all labels are there we can insert the text we want there...
			var x = ReflectionUtil.GetField<List<TextMeshProUGUI>, AlphabetScrollbar>(____alphabetScrollbar, "_texts");
			for(var i = customLegend.Count; i-- != 0;) {
				x[i].text = customLegend[i].Key;
			}

			customLegend = null;

			// Move the table a bit to the right to accomodate for alphabet scollbar (Basegame behaviour)
			((RectTransform)____tableView.transform).offsetMin = new Vector2(((RectTransform)____alphabetScrollbar.transform).rect.size.x + 1f, 0f);
			____alphabetScrollbar.gameObject.SetActive(true);
		}
	}
}
