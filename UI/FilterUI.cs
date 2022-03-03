﻿using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BetterSongList.Api;
using BetterSongList.FilterModels;
using BetterSongList.HarmonyPatches;
using BetterSongList.Sorters;
using BetterSongList.Util;
using HMUI;
using IPA.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace BetterSongList.UI {
#if DEBUG
	public
#endif
	class FilterUI {
		internal static readonly FilterUI persistentNuts = new FilterUI();
#pragma warning disable 649
		[UIComponent("root")] private RectTransform rootTransform;
#pragma warning restore
		[UIParams] readonly BSMLParserParams parserParams = null;

		FilterUI() { }

		public static Dictionary<string, ISortFilter> sortOptions = new List<ISortFilter>() {
			new AdaptedSortFilter("Song Name", SortMethods.alphabeticalSongname),
			new AdaptedSortFilter("Download Date", SortMethods.downloadTime),
			new AdaptedSortFilter("Ranked Stars", SortMethods.stars),
			new AdaptedSortFilter("Song Length", SortMethods.songLength),
			new AdaptedSortFilter("BPM", SortMethods.bpm),
			new AdaptedSortFilter("BeatSaver Date", SortMethods.beatSaverDate),
		}.ToDictionary(sorter => sorter.Name);

		static Dictionary<string, IFilter> filterOptions = new Dictionary<string, IFilter>() {
			{ "All", null },
			{ "Ranked", FilterMethods.ranked },
			{ "Qualified", FilterMethods.qualified },
			{ "Unplayed", FilterMethods.unplayed },
			{ "Played", FilterMethods.played },
			{ "Requirements", FilterMethods.requirements },
			// TODO: { "Not on Beatsaver", FilterMethods.notOnBeatsaver },
			{ "Unranked", FilterMethods.unranked },
		};

		[UIValue("_sortOptions")] static List<object> _sortOptions { get => sortOptions.Keys.ToList<object>(); }
		[UIValue("_filterOptions")] static List<object> _filterOptions = filterOptions.Keys.ToList<object>();

		void _SetSort(string selected) => SetSort(selected);
		internal static void SetSort(string selected, bool storeToConfig = true, bool refresh = true) {
			if(selected == null || !sortOptions.ContainsKey(selected))
				selected = sortOptions.Keys.Last();

			var newSort = sortOptions[selected];
			var unavReason = (newSort as IAvailabilityCheck)?.GetUnavailabilityReason();

			if(unavReason != null) {
				persistentNuts?.ShowErrorASAP($"Can't sort by {selected} - {unavReason}");
				SetSort(null, false, false);
				return;
			}

#if DEBUG
			Plugin.Log.Warn(string.Format("Setting Sort to {0}", selected));
#endif
			if(HookLevelCollectionTableSet.Sorter != newSort) {
				if(storeToConfig)
					Config.Instance.LastSort = selected;

				HookLevelCollectionTableSet.Sorter = newSort;
				RestoreTableScroll.ResetScroll();
				if(refresh)
					HookLevelCollectionTableSet.Refresh();
			}

			XD.FunnyNull(persistentNuts._sortDropdown)?.SelectCellWithIdx(_sortOptions.IndexOf(selected));
		}

		public static void ClearFilter(bool reloadTable = false) => SetFilter(null, false, reloadTable);
		void _SetFilter(string selected) => SetFilter(selected);
		internal static void SetFilter(string selected, bool storeToConfig = true, bool refresh = true) {
			if(selected == null || !filterOptions.ContainsKey(selected))
				selected = filterOptions.Keys.First();

			var newFilter = filterOptions[selected];
			var unavReason = (newFilter as IAvailabilityCheck)?.GetUnavailabilityReason();

			if(unavReason != null) {
				persistentNuts?.ShowErrorASAP($"Can't filter by {selected} - {unavReason}");
				SetFilter(null, false, false);
				return;
			}

#if DEBUG
			Plugin.Log.Warn(string.Format("Setting Filter to {0}", selected));
#endif
			if(HookLevelCollectionTableSet.filter != filterOptions[selected]) {
				if(storeToConfig)
					Config.Instance.LastFilter = selected;

				HookLevelCollectionTableSet.filter = filterOptions[selected];
				RestoreTableScroll.ResetScroll();
				if(refresh)
					HookLevelCollectionTableSet.Refresh();
			}

			XD.FunnyNull(persistentNuts._filterDropdown)?.SelectCellWithIdx(_filterOptions.IndexOf(selected));
		}

		internal static void SetSortDirection(bool ascending, bool refresh = true) {
			if(HookLevelCollectionTableSet.Sorter == null)
				return;

			if(Config.Instance.SortAsc != ascending) {
				Config.Instance.SortAsc = ascending;
				RestoreTableScroll.ResetScroll();
				if(refresh)
					HookLevelCollectionTableSet.Refresh();
			}

			XD.FunnyNull(persistentNuts._sortDirection)?.SetText(ascending ? "▲" : "▼");
		}

		static void ToggleSortDirection() {
			if(HookLevelCollectionTableSet.Sorter == null)
				return;

			SetSortDirection(!Config.Instance.SortAsc);
		}

		static void SelectRandom() {
			var x = Object.FindObjectOfType<LevelCollectionTableView>();

			if(x == null)
				return;

			var ml = HookLevelCollectionTableSet.lastOutMapList ?? HookLevelCollectionTableSet.lastInMapList;

			if(ml.Length < 2)
				return;

			x.SelectLevel(ml[Random.Range(0, ml.Length)]);
		}

		Queue<string> warnings = new Queue<string>();
		bool warningLoadInProgress;
		public void ShowErrorASAP(string text = null) {
			if(text != null)
				warnings.Enqueue(text);
			if(!warningLoadInProgress)
				SharedCoroutineStarter.instance.StartCoroutine(_ShowError());
		}

		[UIAction("PossiblyShowNextWarning")] void PossiblyShowNextWarning() => ShowErrorASAP();

		IEnumerator _ShowError() {
			warningLoadInProgress = true;
			yield return new WaitUntil(() => _failTextLabel != null);
			var x = _failTextLabel.GetComponentInParent<ViewController>();
			if(x != null) {
				yield return new WaitUntil(() => !x.isInTransition);

				if(x.isActivated && warnings.Count > 0) {
					_failTextLabel.text = warnings.Dequeue();
					parserParams.EmitEvent("IncompatabilityNotice");
				}
			}
			warningLoadInProgress = false;
		}


		[UIComponent("filterLoadingIndicator")] internal readonly ImageView _filterLoadingIndicator = null;
		[UIComponent("sortDropdown")] readonly DropdownWithTableView _sortDropdown = null;
		[UIComponent("filterDropdown")] readonly DropdownWithTableView _filterDropdown = null;
		[UIComponent("sortDirection")] readonly ClickableText _sortDirection = null;
		[UIComponent("failTextLabel")] readonly TextMeshProUGUI _failTextLabel = null;

		internal static void Init() {
			SetSort(Config.Instance.LastSort, true, false);
			SetFilter(Config.Instance.LastFilter, true, false);
			SetSortDirection(Config.Instance.SortAsc);

			if(!SongDataCoreChecker.didCheck && SongDataCoreChecker.IsInstalled() && !SongDataCoreChecker.IsUsed()) {
				BlockSongDataCoreLoad.doBlock = true;
				persistentNuts.ShowErrorASAP("You have the Plugin 'SongDataCore' installed. It's advised to delete it as it can increase load times.\nIf you use ModAssistant you need to remove SongBrowser (Disabled by BetterSongList) to be able to remove SongDataCore");
			}
		}

		internal static void AttachTo(Transform target) {
			BSMLParser.instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "BetterSongList.UI.BSML.MainUI.bsml"), target.gameObject, persistentNuts);
			persistentNuts.rootTransform.localScale *= 0.7f;

			(target as RectTransform).sizeDelta += new Vector2(0, 2);
			target.GetChild(0).position -= new Vector3(0, 0.02f);
		}

		bool settingsWereOpened = false;
		BSMLParserParams settingsViewParams = null;
		void SettingsOpened() {
			Config.Instance.SettingsSeenInVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			settingsWereOpened = true;

			BSMLStuff.InitSplitView(ref settingsViewParams, rootTransform.gameObject, SplitViews.Settings.instance).EmitEvent("ShowSettings");
		}
		[UIComponent("settingsButton")] readonly ClickableImage _settingsButton = null;
		[UIComponent("settingsButtonArrow")] readonly TextMeshProUGUI _settingsButtonArrow = null;

		[UIAction("#post-parse")]
		void Parsed() {
			HackDropdown(_sortDropdown);
			HackDropdown(_filterDropdown);

			foreach(var x in sortOptions) {
				if(x.Value == HookLevelCollectionTableSet.Sorter) {
					SetSort(x.Key, false, false);
					break;
				}
			}
			foreach(var x in filterOptions) {
				if(x.Value == HookLevelCollectionTableSet.filter) {
					SetFilter(x.Key, false, false);
					break;
				}
			}

			SetSortDirection(Config.Instance.SortAsc, false);

#if !DEBUG
			SharedCoroutineStarter.instance.StartCoroutine(PossiblyDrawUserAttentionToSettingsButton());
#endif
		}

		IEnumerator PossiblyDrawUserAttentionToSettingsButton() {
			try {
				if(System.Version.TryParse(Config.Instance.SettingsSeenInVersion, out var oldV)) {
					if(oldV >= new System.Version("0.2.6.0"))
						yield break;
				}
			} catch { }

			var blinks = 0;
			while(!settingsWereOpened) {
				if(blinks++ == 120)
					_settingsButtonArrow.gameObject.SetActive(true);

				yield return new WaitForSeconds(blinks < 100 ? .5f : 0.25f);
				if(_settingsButton != null)
					_settingsButton.color = Color.green;

				if(blinks > 150 && _settingsButtonArrow != null)
					_settingsButtonArrow.gameObject.SetActive(true);

				yield return new WaitForSeconds(blinks < 100 ? .5f : 0.25f);
				if(_settingsButton != null)
					_settingsButton.color = Color.white;

				if(blinks > 150 && _settingsButtonArrow != null)
					_settingsButtonArrow.gameObject.SetActive(false);
			}

			if(_settingsButton != null)
				_settingsButtonArrow.gameObject.SetActive(false);
		}

		static void HackDropdown(DropdownWithTableView dropdown) {
			var c = Mathf.Min(9, dropdown.tableViewDataSource.NumberOfCells());
			ReflectionUtil.SetField(dropdown, "_numberOfVisibleCells", c);
			dropdown.ReloadData();

			// TODO: Remove this funny business when required game version >= 1.19.0 - Apparently is now a basegame thing?
			var isPostGagaUI = UnityGame.GameVersion >= new AlmostVersion("1.19.0");

			if(isPostGagaUI)
				return;

			var m = ReflectionUtil.GetField<ModalView, DropdownWithTableView>(dropdown, "_modalView");
			((RectTransform)m.transform).pivot = new Vector2(0.5f, 0.14f - (c * 0.011f));
		}
	}
}
