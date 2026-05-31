using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility;

using FFXIVClientStructs.STD;
using GatherBuddy.Alarms;
using GatherBuddy.AutoGather;
using GatherBuddy.Classes;
using GatherBuddy.Config;
using GatherBuddy.Enums;
using GatherBuddy.FishTimer;
using GatherBuddy.Utilities;
using Dalamud.Utility;
using ElliLib;
using ElliLib.Widgets;
using FishRecord = GatherBuddy.FishTimer.FishRecord;
using GatheringType = GatherBuddy.Enums.GatheringType;
using ImRaii = ElliLib.Raii.ImRaii;

namespace GatherBuddy.Gui;

public partial class Interface
{
    private static class ConfigFunctions
    {
        public static Interface _base = null!;
        
        private static string _fishFilterText = "";
        private static Fish? _selectedFish = null;
        private static string _presetName = "";

        public static void DrawSetInput(string jobName, string oldName, Action<string> setName)
        {
            var tmp = oldName;
            ImGui.SetNextItemWidth(SetInputWidth);
            if (ImGui.InputText($"{jobName}セット", ref tmp, 15) && tmp != oldName)
            {
                setName(tmp);
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip($"{jobName}のギアセット名、またはギアセット番号を指定します。");
        }

        private static void DrawCheckbox(string label, string description, bool oldValue, Action<bool> setter)
        {
            if (ImGuiUtil.Checkbox(label, description, oldValue, setter))
                GatherBuddy.Config.Save();
        }

        private static void DrawChatTypeSelector(string label, string description, XivChatType currentValue, Action<XivChatType> setter)
        {
            ImGui.SetNextItemWidth(SetInputWidth);
            if (Widget.DrawChatTypeSelector(label, description, currentValue, setter))
                GatherBuddy.Config.Save();
        }

        // Auto-Gather Config
        public static void DrawAutoGatherBox()
            => DrawCheckbox("採集ウィンドウ操作を有効化",
                "採集場で自動採集を行うかを切り替えます。",
                GatherBuddy.Config.AutoGatherConfig.DoGathering, b => GatherBuddy.Config.AutoGatherConfig.DoGathering = b);

        public static void DrawTeleportToNextNodeBox()
            => DrawCheckbox("次の時間限定採集場へテレポ",
                "Teleport to an upcoming timed node or fishing spot and wait at the Aetheryte when there is nothing else to gather\n" +
                "This option has priority over going home when idle.",
                GatherBuddy.Config.AutoGatherConfig.TeleportToNextNode, b => GatherBuddy.Config.AutoGatherConfig.TeleportToNextNode = b);

        public static void DrawGoHomeBox()
        {
            DrawCheckbox("完了時に帰宅",                       "採集完了時に /li auto で帰宅します。",
                GatherBuddy.Config.AutoGatherConfig.GoHomeWhenDone, b => GatherBuddy.Config.AutoGatherConfig.GoHomeWhenDone = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("Lifestream")]);
            DrawCheckbox("待機時に帰宅",                       "時間限定採集場待ちの時に /li auto で帰宅します。",
                GatherBuddy.Config.AutoGatherConfig.GoHomeWhenIdle, b => GatherBuddy.Config.AutoGatherConfig.GoHomeWhenIdle = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("Lifestream")]);
        }

        public static void DrawUseSkillsForFallabckBox()
            => DrawCheckbox("予備アイテムにもスキルを使う", "予備リストのアイテム採集時にもスキルを使います。",
                GatherBuddy.Config.AutoGatherConfig.UseSkillsForFallbackItems,
                b => GatherBuddy.Config.AutoGatherConfig.UseSkillsForFallbackItems = b);

        public static void DrawAbandonNodesBox()
            => DrawCheckbox("不要な採集場を中断",
                "Stop gathering and abandon the node when you have gathered enough items,\n"
              + "or if the node didn't have any needed items on the first place.",
                GatherBuddy.Config.AutoGatherConfig.AbandonNodes, b => GatherBuddy.Config.AutoGatherConfig.AbandonNodes = b);

        public static void DrawCheckRetainersBox()
        {
            DrawCheckbox("リテイナー所持品を確認", "Use Allagan Tools to check retainer inventories when doing inventory calculations",
                GatherBuddy.Config.AutoGatherConfig.CheckRetainers, b => GatherBuddy.Config.AutoGatherConfig.CheckRetainers = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("InventoryTools", "Allagan Tools")]);
        }

        public static void DrawHonkVolumeSlider()
        {
            ImGui.SetNextItemWidth(150);
            var volume = GatherBuddy.Config.AutoGatherConfig.SoundPlaybackVolume;
            if (ImGui.DragInt("再生音量", ref volume, 1, 0, 100))
            {
                if (volume < 0)
                    volume = 0;
                else if (volume > 100)
                    volume = 100;
                GatherBuddy.Config.AutoGatherConfig.SoundPlaybackVolume = volume;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(
                "The volume of the sound played when auto-gathering shuts down because your list is complete.\nHold CTRL and click to enter custom value");
        }

        public static void DrawHonkModeBox()
            => DrawCheckbox("採集完了時に音を鳴らす", "Play a sound when auto-gathering shuts down because your list is complete",
                GatherBuddy.Config.AutoGatherConfig.HonkMode,   b => GatherBuddy.Config.AutoGatherConfig.HonkMode = b);

        public static void DrawRepairBox()
            => DrawCheckbox("必要時に装備修理",        "Repair gear when it is almost broken",
                GatherBuddy.Config.AutoGatherConfig.DoRepair, b => GatherBuddy.Config.AutoGatherConfig.DoRepair = b);

        public static void DrawRepairThreshold()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.RepairThreshold;
            if (ImGui.DragInt("修理しきい値", ref tmp, 1, 1, 100))
            {
                GatherBuddy.Config.AutoGatherConfig.RepairThreshold = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("The percentage of durability at which you will repair your gear.");
        }

        public static void DrawFishingSpotMinutes()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.MaxFishingSpotMinutes;
            if (ImGui.DragInt("Max Fishing Spot Minutes", ref tmp, 1, 1, 40))
            {
                GatherBuddy.Config.AutoGatherConfig.MaxFishingSpotMinutes = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("The maximum number of minutes you will fish at a fishing spot.");
        }

        public static void DrawAutoretainerBox()
        {
            DrawCheckbox("AutoRetainerマルチモードを待機", "Pause GBR automatically when AutoRetainer has retainers to process during Multi-mode",
                GatherBuddy.Config.AutoGatherConfig.AutoRetainerMultiMode, b => GatherBuddy.Config.AutoGatherConfig.AutoRetainerMultiMode = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new ImGuiEx.RequiredPluginInfo("AutoRetainer")]);
        }

        public static void DrawAutoretainerThreshold()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.AutoRetainerMultiModeThreshold;
            if (ImGui.DragInt("AutoRetainer待機しきい値（秒）", ref tmp, 1, 0, 3600))
            {
                GatherBuddy.Config.AutoGatherConfig.AutoRetainerMultiModeThreshold = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("How many seconds before a retainer venture completes GBR should pause and wait for MultiMode.");
        }

        public static void DrawAutoretainerTimedNodeDelayBox()
            => DrawCheckbox("時間限定採集場ではAutoRetainerを遅延",
                "Wait to process retainers until after active/upcoming timed nodes are gathered.",
                GatherBuddy.Config.AutoGatherConfig.AutoRetainerDelayForTimedNodes,
                b => GatherBuddy.Config.AutoGatherConfig.AutoRetainerDelayForTimedNodes = b);

        public static void DrawLifestreamCommandTextInput()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.LifestreamCommand;
            if (ImGui.InputText("Lifestreamコマンド", ref tmp, 100))
            {
                if (string.IsNullOrEmpty(tmp))
                    tmp = "auto";
                GatherBuddy.Config.AutoGatherConfig.LifestreamCommand = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(
                "The command used when idling or done gathering. DO NOT include '/li'\nBe careful when changing this, GBR does not validate this command!");
        }

        public static void DrawFishCollectionBox()
            => DrawCheckbox("Opt-in to fishing data collection",
                "With this enabled, whenever you catch a fish the data for that fish will be uploaded to a remote server\n"
              + "The purpose of this data collection is to allow for a usable auto-fishing feature to be built\n"
              + "No personal information about you or your character will be collected, only data relevant to the caught fish\n"
              + "You can opt-out again at any time by simply disabling this checkbox.", GatherBuddy.Config.AutoGatherConfig.FishDataCollection,
                b => GatherBuddy.Config.AutoGatherConfig.FishDataCollection = b);

        public static void DrawMaterialExtraction()
            => DrawCheckbox("マテリア精製を有効化",
                "Automatically extract materia from items with a complete spiritbond",
                GatherBuddy.Config.AutoGatherConfig.DoMaterialize,
                b => GatherBuddy.Config.AutoGatherConfig.DoMaterialize = b);

        public static void DrawAetherialReduction()
            => DrawCheckbox("精選を有効化",
                "Automatically perform Aetherial Reduction when idling or if the number of free inventory slots drops below 20",
                GatherBuddy.Config.AutoGatherConfig.DoReduce,
                b => GatherBuddy.Config.AutoGatherConfig.DoReduce = b);

        public static void DrawAlwaysReduceAllItemsBox()
            => DrawCheckbox("常に全アイテムを精選",
                "When unchecked: If the number of free inventory slots drops below 20 while gathering,\n" +
                "emergency aetherial reduction is performed for only one item type.\n"
              + "When checked: Emergency aetherial reduction is performed for all items at once.",
                GatherBuddy.Config.AutoGatherConfig.AlwaysReduceAllItems,
                b => GatherBuddy.Config.AutoGatherConfig.AlwaysReduceAllItems = b);

        public static void DrawUseFlagBox()
            => DrawCheckbox("マップマーカー移動を無効化",            "Whether or not to navigate using map markers (timed nodes only)",
                GatherBuddy.Config.AutoGatherConfig.DisableFlagPathing, b => GatherBuddy.Config.AutoGatherConfig.DisableFlagPathing = b);

        public static void DrawFarNodeFilterDistance()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.FarNodeFilterDistance;
            if (ImGui.DragFloat("遠距離採集場フィルター距離", ref tmp, 0.1f, 0.1f, 100f))
            {
                GatherBuddy.Config.AutoGatherConfig.FarNodeFilterDistance = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(
                "When looking for non-empty nodes GBR will filter out any nodes that are closer to you than this. Prevents checking nodes you can already see are empty.");
        }

        public static void DrawTimedNodePrecog()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.TimedNodePrecog;
            if (ImGui.DragInt("時間限定採集場の先読み（秒）", ref tmp, 1, 0, 600))
            {
                GatherBuddy.Config.AutoGatherConfig.TimedNodePrecog = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("How far in advance of the node actually being up GBR should consider the node to be up");
        }

        public static void DrawExecutionDelay()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = (int)GatherBuddy.Config.AutoGatherConfig.ExecutionDelay;
            if (ImGui.DragInt("実行遅延（ミリ秒）", ref tmp, 1, 0, 1500))
            {
                GatherBuddy.Config.AutoGatherConfig.ExecutionDelay = (uint)Math.Min(Math.Max(0, tmp), 10000);
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("Delay executing each action by the specified amount.");
        }

        public static void DrawUseGivingLandOnCooldown()
            => DrawCheckbox("大地の恵み使用可能時はクリスタルを採集",
                "Gather random crystals on any regular node when The Giving Land is avaiable regardles of current target item.",
                GatherBuddy.Config.AutoGatherConfig.UseGivingLandOnCooldown,
                b => GatherBuddy.Config.AutoGatherConfig.UseGivingLandOnCooldown = b);

        public static void DrawMountUpDistance()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.MountUpDistance;
            if (ImGui.DragFloat("マウント開始距離", ref tmp, 0.1f, 0.1f, 100f))
            {
                GatherBuddy.Config.AutoGatherConfig.MountUpDistance = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("The distance at which you will mount up to move to a node.");
        }

        public static void DrawLandingDistance()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.LandingDistance;
            if (ImGui.DragFloat("着地距離", ref tmp, 0.1f, 0.0f, 50f))
            {
                GatherBuddy.Config.AutoGatherConfig.LandingDistance = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(
                "The fixed distance from the node at which you will try to land.\n\n" +
                "Used when random landing positions are disabled, or when no collected data is available.\n\n" +
                "Low values increase the chance of being unable to dismount properly.\n" +
                "High values may produce weird-looking paths.\n" +
                "Reasonable values are between 4 and 8 yalms."
            );
        }

        public static void DrawMoveWhileMounting()
            => DrawCheckbox("マウント詠唱中に移動開始",
                "Begin pathfinding to the next node while summoning a mount",
                GatherBuddy.Config.AutoGatherConfig.MoveWhileMounting,
                b => GatherBuddy.Config.AutoGatherConfig.MoveWhileMounting = b);

        public static void DrawAntiStuckCooldown()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.NavResetCooldown;
            if (ImGui.DragFloat("スタック対策クールダウン", ref tmp, 0.1f, 0.1f, 10f))
            {
                GatherBuddy.Config.AutoGatherConfig.NavResetCooldown = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("The time in seconds before the navigation system will reset if you are stuck.");
        }

        public static void DrawForceWalkingBox()
            => DrawCheckbox("徒歩移動を強制",                      "Force walking to nodes instead of using mounts.",
                GatherBuddy.Config.AutoGatherConfig.ForceWalking, b => GatherBuddy.Config.AutoGatherConfig.ForceWalking = b);

        public static void DrawDisableRandomLandingPositionsBox()
            => DrawCheckbox("ランダム着地点を無効化",
                "GBR automatically collects player gathering positions as landing positions (offsets).\n" +
                "When unchecked: Lands at random position where players were observed gathering.\n" +
                "When checked: Uses the fixed landing distance instead.\n",
                GatherBuddy.Config.AutoGatherConfig.DisableRandomLandingPositions, b => GatherBuddy.Config.AutoGatherConfig.DisableRandomLandingPositions = b);

        public static void DrawUseNavigationBox()
            => DrawCheckbox("vnavmesh移動を使用",             "vnavmesh移動を使用 to move your character automatically",
                GatherBuddy.Config.AutoGatherConfig.UseNavigation, b => GatherBuddy.Config.AutoGatherConfig.UseNavigation = b);

        public static void DrawStuckThreshold()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.NavResetThreshold;
            if (ImGui.DragFloat("スタック判定しきい値", ref tmp, 0.1f, 0.1f, 10f))
            {
                GatherBuddy.Config.AutoGatherConfig.NavResetThreshold = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip("The time in seconds before the navigation system will consider you stuck.");
        }

        public static void DrawSortingMethodCombo()
        {
            var v = GatherBuddy.Config.AutoGatherConfig.SortingMethod;
            var current = v switch
            {
                AutoGatherConfig.SortingType.Location => "場所順",
                AutoGatherConfig.SortingType.None     => "並び替えなし",
                _                                     => v.ToString(),
            };
            ImGui.SetNextItemWidth(150);

            using var combo = ImRaii.Combo("アイテム並び替え方法", current);
            ImGuiUtil.HoverTooltip("自動採集リスト内のアイテムをどの順番で処理するかを選びます。");
            if (!combo)
                return;

            if (ImGui.Selectable("場所順", v == AutoGatherConfig.SortingType.Location))
            {
                GatherBuddy.Config.AutoGatherConfig.SortingMethod = AutoGatherConfig.SortingType.Location;
                GatherBuddy.Config.Save();
            }

            if (ImGui.Selectable("並び替えなし", v == AutoGatherConfig.SortingType.None))
            {
                GatherBuddy.Config.AutoGatherConfig.SortingMethod = AutoGatherConfig.SortingType.None;
                GatherBuddy.Config.Save();
            }
        }

        // General Config
        public static void DrawOpenOnStartBox()
            => DrawCheckbox("起動時に画面を開く",
                "ゲーム開始後にGatherBuddy JPの画面を開きます。",
                GatherBuddy.Config.OpenOnStart, b => GatherBuddy.Config.OpenOnStart = b);

        public static void DrawLockPositionBox()
            => DrawCheckbox("画面位置をロック",
                "GatherBuddy JP画面の移動をロックします。",
                GatherBuddy.Config.MainWindowLockPosition, b =>
                {
                    GatherBuddy.Config.MainWindowLockPosition = b;
                    _base.UpdateFlags();
                });

        public static void DrawLockResizeBox()
            => DrawCheckbox("画面サイズをロック",
                "GatherBuddy JP画面のサイズ変更をロックします。",
                GatherBuddy.Config.MainWindowLockResize, b =>
                {
                    GatherBuddy.Config.MainWindowLockResize = b;
                    _base.UpdateFlags();
                });

        public static void DrawRespectEscapeBox()
            => DrawCheckbox("Escapeで閉じる",
                "画面にフォーカスがある時、Escapeキーで閉じます。",
                GatherBuddy.Config.CloseOnEscape, b =>
                {
                    GatherBuddy.Config.CloseOnEscape = b;
                    _base.UpdateFlags();
                });

        public static void DrawGearChangeBox()
            => DrawCheckbox("ギアセット変更を有効化",
                "採集対象に合わせて採掘師/園芸師のギアセットへ変更します。",
                GatherBuddy.Config.UseGearChange, b => GatherBuddy.Config.UseGearChange = b);

        public static void DrawTeleportBox()
            => DrawCheckbox("テレポを有効化",
                "選択した採集場へ自動テレポします。",
                GatherBuddy.Config.UseTeleport, b => GatherBuddy.Config.UseTeleport = b);

        public static void DrawMapOpenBox()
            => DrawCheckbox("場所をマップで開く",
                "選択した採集場所をマップで開きます。",
                GatherBuddy.Config.UseCoordinates, b => GatherBuddy.Config.UseCoordinates = b);

        public static void DrawPlaceMarkerBox()
            => DrawCheckbox("マップにフラッグを立てる",
                "選択した採集場所付近にフラッグを立てます。",
                GatherBuddy.Config.UseFlag, b => GatherBuddy.Config.UseFlag = b);

        public static void DrawMapMarkerPrintBox()
            => DrawCheckbox("マップ位置を表示",
                "Toggle whether to automatically write a map link to the approximate location of the chosen node to chat.",
                GatherBuddy.Config.WriteCoordinates, b => GatherBuddy.Config.WriteCoordinates = b);

        public static void DrawPlaceWaymarkBox()
            => DrawCheckbox("カスタムウェイマークを置く",
                "手動設定した場所にカスタムウェイマークを置きます。",
                GatherBuddy.Config.PlaceCustomWaymarks, b => GatherBuddy.Config.PlaceCustomWaymarks = b);

        public static void DrawPrintUptimesBox()
            => DrawCheckbox("採集場の出現時間を表示",
                "Print the uptimes of nodes you try to /gather in the chat if they are not always up.",
                GatherBuddy.Config.PrintUptime, b => GatherBuddy.Config.PrintUptime = b);

        public static void DrawSkipTeleportBox()
            => DrawCheckbox("近距離テレポをスキップ",
                "同じマップ内で目的地に近い場合、テレポを省略します。",
                GatherBuddy.Config.SkipTeleportIfClose, b => GatherBuddy.Config.SkipTeleportIfClose = b);

        public static void DrawShowStatusLineBox()
            => DrawCheckbox("ステータス行を表示",
                "Show a status line below the gatherables and fish tables.",
                GatherBuddy.Config.ShowStatusLine, v => GatherBuddy.Config.ShowStatusLine = v);

        public static void DrawHideClippyBox()
            => DrawCheckbox("GatherClippyボタンを非表示",
                "Permanently hide the GatherClippy Button in the Gatherables and Fish tabs.",
                GatherBuddy.Config.HideClippy, v => GatherBuddy.Config.HideClippy = v);

        private const string ChatInformationString =
            "Note that the message only gets printed to your chat log, regardless of the selected channel"
          + " - other people will not see your 'Say' message.";

        public static void DrawPrintTypeSelector()
            => DrawChatTypeSelector("通常メッセージのチャット種類",
                "The chat type used to print regular messages issued by GatherBuddy.\n"
              + ChatInformationString,
                GatherBuddy.Config.ChatTypeMessage, t => GatherBuddy.Config.ChatTypeMessage = t);

        public static void DrawErrorTypeSelector()
            => DrawChatTypeSelector("エラーのチャット種類",
                "The chat type used to print error messages issued by GatherBuddy.\n"
              + ChatInformationString,
                GatherBuddy.Config.ChatTypeError, t => GatherBuddy.Config.ChatTypeError = t);

        public static void DrawContextMenuBox()
            => DrawCheckbox("ゲーム内右クリックメニューを追加",
                "採集アイテムの右クリックメニューに採集関連の項目を追加します。",
                GatherBuddy.Config.AddIngameContextMenus, b =>
                {
                    GatherBuddy.Config.AddIngameContextMenus = b;
                    if (b)
                        _plugin.ContextMenu.Enable();
                    else
                        _plugin.ContextMenu.Disable();
                });

        public static void DrawPreferredJobSelect()
        {
            var v       = GatherBuddy.Config.PreferredGatheringType;
            var current = v switch
            {
                GatheringType.Miner    => "採掘師",
                GatheringType.Botanist => "園芸師",
                _                      => "指定なし",
            };
            ImGui.SetNextItemWidth(SetInputWidth);
            using var combo = ImRaii.Combo("優先ジョブ", current);
            ImGuiUtil.HoverTooltip(
                "Choose your job preference when gathering items that can be gathered by miners as well as botanists.\n"
              + "This effectively turns the regular gather command to /gathermin or /gatherbtn when an item can be gathered by both, "
              + "ignoring the other options even on successive tries.");
            if (!combo)
                return;

            if (ImGui.Selectable("指定なし", v == GatheringType.Multiple) && v != GatheringType.Multiple)
            {
                GatherBuddy.Config.PreferredGatheringType = GatheringType.Multiple;
                GatherBuddy.Config.Save();
            }

            if (ImGui.Selectable("採掘師", v == GatheringType.Miner) && v != GatheringType.Miner)
            {
                GatherBuddy.Config.PreferredGatheringType = GatheringType.Miner;
                GatherBuddy.Config.Save();
            }

            if (ImGui.Selectable("園芸師", v == GatheringType.Botanist) && v != GatheringType.Botanist)
            {
                GatherBuddy.Config.PreferredGatheringType = GatheringType.Botanist;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawPrintClipboardBox()
            => DrawCheckbox("クリップボード情報を表示",
                "クリップボードへ保存した時にチャットへ通知します。失敗時は常に通知されます。",
                GatherBuddy.Config.PrintClipboardMessages, b => GatherBuddy.Config.PrintClipboardMessages = b);

        // Weather Tab
        public static void DrawWeatherTabNamesBox()
            => DrawCheckbox("Show Names in Weather Tab",
                "Toggle whether to write the names in the table for the weather tab, or just the icons with names on hover.",
                GatherBuddy.Config.ShowWeatherNames, b => GatherBuddy.Config.ShowWeatherNames = b);

        // Alarms
        public static void DrawAlarmToggle()
            => DrawCheckbox("Enable Alarms", "Toggle all alarms on or off.", GatherBuddy.Config.AlarmsEnabled,
                b =>
                {
                    if (b)
                        _plugin.AlarmManager.Enable();
                    else
                        _plugin.AlarmManager.Disable();
                });

        public static void DrawAlarmsInDutyToggle()
            => DrawCheckbox("Enable Alarms in Duty", "Set whether alarms should trigger while you are bound by a duty.",
                GatherBuddy.Config.AlarmsInDuty,     b => GatherBuddy.Config.AlarmsInDuty = b);

        public static void DrawAlarmsOnlyWhenLoggedInToggle()
            => DrawCheckbox("Enable Alarms Only In-Game",  "Set whether alarms should trigger while you are not logged into any character.",
                GatherBuddy.Config.AlarmsOnlyWhenLoggedIn, b => GatherBuddy.Config.AlarmsOnlyWhenLoggedIn = b);

        private static void DrawAlarmPicker(string label, string description, Sounds current, Action<Sounds> setter)
        {
            var cur = (int)current;
            ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
            if (ImGui.Combo(new ImU8String(label), ref cur, AlarmCache.SoundIdNames))
                setter((Sounds)cur);
            ImGuiUtil.HoverTooltip(description);
        }

        public static void DrawWeatherAlarmPicker()
            => DrawAlarmPicker("Weather Change Alarm", "Choose a sound that is played every 8 Eorzea hours on regular weather changes.",
                GatherBuddy.Config.WeatherAlarm,       _plugin.AlarmManager.SetWeatherAlarm);

        public static void DrawHourAlarmPicker()
            => DrawAlarmPicker("Eorzea Hour Change Alarm", "Choose a sound that is played every time the current Eorzea hour changes.",
                GatherBuddy.Config.HourAlarm,              _plugin.AlarmManager.SetHourAlarm);

        // Fish Timer
        public static void DrawFishTimerBox()
            => DrawCheckbox("Show Fish Timer",
                "Toggle whether to show the fish timer window while fishing.",
                GatherBuddy.Config.ShowFishTimer, b => GatherBuddy.Config.ShowFishTimer = b);

        public static void DrawFishTimerEditBox()
            => DrawCheckbox("Edit Fish Timer",
                "Enable editing the fish timer window.",
                GatherBuddy.Config.FishTimerEdit, b => GatherBuddy.Config.FishTimerEdit = b);

        public static void DrawFishTimerClickthroughBox()
            => DrawCheckbox("Enable Fish Timer Clickthrough",
                "Allow clicking through the fish timer and disabling the context menus instead.",
                GatherBuddy.Config.FishTimerClickthrough, b => GatherBuddy.Config.FishTimerClickthrough = b);

        public static void DrawFishTimerHideBox()
            => DrawCheckbox("Hide Uncaught Fish in Fish Timer",
                "Hide all fish from the fish timer window that have not been recorded with the given combination of snagging and bait.",
                GatherBuddy.Config.HideUncaughtFish, b => GatherBuddy.Config.HideUncaughtFish = b);

        public static void DrawFishTimerHideBox2()
            => DrawCheckbox("Hide Unavailable Fish in Fish Timer",
                "Hide all fish from the fish timer window that have have known requirements that are unfulfilled, like Fisher's Intuition or Snagging.",
                GatherBuddy.Config.HideUnavailableFish, b => GatherBuddy.Config.HideUnavailableFish = b);

        public static void DrawFishTimerUptimesBox()
            => DrawCheckbox("Show Uptimes in Fish Timer",
                "Show the uptimes for restricted fish in the fish timer window.",
                GatherBuddy.Config.ShowFishTimerUptimes, b => GatherBuddy.Config.ShowFishTimerUptimes = b);

        public static void DrawKeepRecordsBox()
            => DrawCheckbox("Keep Fish Records",
                "Store Fish Records on your computer. This is necessary for bite timings for the fish timer window.",
                GatherBuddy.Config.StoreFishRecords, b => GatherBuddy.Config.StoreFishRecords = b);

        public static void DrawShowLocalTimeInRecordsBox()
            => DrawCheckbox("Use Local Time in Records",
                "When displaying timestamps in the Fish Records Tab, use local time instead of Unix time.",
                GatherBuddy.Config.UseUnixTimeFishRecords, b => GatherBuddy.Config.UseUnixTimeFishRecords = b);
        
        public static void DrawFishTimerScale()
        {
            var value = GatherBuddy.Config.FishTimerScale / 1000f;
            ImGui.SetNextItemWidth(SetInputWidth);
            var ret = ImGui.DragFloat("Fish Timer Bite Time Scale", ref value, 0.1f, FishRecord.MinBiteTime / 500f,
                FishRecord.MaxBiteTime / 1000f,
                "%2.3f Seconds");

            ImGuiUtil.HoverTooltip("The fishing timer window bite times are scaled to this value.\n"
              + "If your bite time exceeds the value, the progress bar and bite windows will not be displayed.\n"
              + "You should probably keep this as high as your highest bite window and as low as possible. About 40 seconds is usually enough.");

            if (!ret)
                return;

            var newValue = (ushort)Math.Clamp((int)(value * 1000f + 0.9), FishRecord.MinBiteTime * 2, FishRecord.MaxBiteTime);
            if (newValue == GatherBuddy.Config.FishTimerScale)
                return;

            GatherBuddy.Config.FishTimerScale = newValue;
            GatherBuddy.Config.Save();
        }

        public static void DrawFishTimerIntervals()
        {
            int value = GatherBuddy.Config.ShowSecondIntervals;
            ImGui.SetNextItemWidth(SetInputWidth);
            var ret = ImGui.DragInt("Fish Timer Interval Separators", ref value, 0.01f, 0, 16);
            ImGuiUtil.HoverTooltip("The fishing timer window can show a number of interval lines and corresponding seconds between 0 and 16.\n"
              + "Set to 0 to turn this feature off.");
            if (!ret)
                return;

            var newValue = (byte)Math.Clamp(value, 0, 16);
            if (newValue == GatherBuddy.Config.ShowSecondIntervals)
                return;

            GatherBuddy.Config.ShowSecondIntervals = newValue;
            GatherBuddy.Config.Save();
        }
        
        public static void DrawFishTimerIntervalsRounding()
        {
            var value = GatherBuddy.Config.SecondIntervalsRounding;
            ImGui.SetNextItemWidth(SetInputWidth);
            var ret = ImGui.DragInt("Fish Timer Interval Rounding", ref value, 0.01f, 0, 3);
            ImGuiUtil.HoverTooltip("Round the displayed second value to this number of digits past the decimal. \n"
                + "Set to 0 to display only whole numbers.");
            if (!ret)
                return;

            var newValue = (byte)Math.Clamp(value, 0, 3);
            if (newValue == GatherBuddy.Config.SecondIntervalsRounding)
                return;

            GatherBuddy.Config.SecondIntervalsRounding = newValue;
            GatherBuddy.Config.Save();
        }

        public static void DrawHideFishPopupBox()
            => DrawCheckbox("Hide Catch Popup",
                "Prevents the popup window that shows you your caught fish and its size, amount and quality from being shown.",
                GatherBuddy.Config.HideFishSizePopup, b => GatherBuddy.Config.HideFishSizePopup = b);

        public static void DrawCollectableHintPopupBox()
            => DrawCheckbox("Show Collectable Hints",
                "Show if a fish is collectable in the fish timer window.",
                GatherBuddy.Config.ShowCollectableHints, b => GatherBuddy.Config.ShowCollectableHints = b);

        public static void DrawDoubleHookHintPopupBox()
            => DrawCheckbox("Show Multi Hook Hints",
                "Show if a fish can be double or triple hooked in Cosmic Exploration and Ocean Fishing",
                GatherBuddy.Config.ShowMultiHookHints, b => GatherBuddy.Config.ShowMultiHookHints = b);
        public static void DrawOceanTypeHintPopupBox()
            => DrawCheckbox("Show Ocean Type Hints",
                "Show what type of fish in Ocean Fishing",
                GatherBuddy.Config.ShowOceanTypeHints, b => GatherBuddy.Config.ShowOceanTypeHints = b);
        
        // Fish Stats Window
        public static void DrawEnableFishStats()
            => DrawCheckbox("Enable Fish Stats",
                "New tab for aggregating and reporting fish stats based on local records. Currently in testing.",
                GatherBuddy.Config.EnableFishStats, b => GatherBuddy.Config.EnableFishStats = b);
        public static void DrawEnableReportTime()  
            => DrawCheckbox("Copy Time Stats when reporting.",
                "When copying the report, add min and max times to the report.",
                GatherBuddy.Config.EnableReportTime, b => GatherBuddy.Config.EnableReportTime = b);
        public static void DrawEnableReportSize()  
            => DrawCheckbox("Copy Sizes Stats when reporting.",
                "When copying the report, add min and max sizes to the report.",
                GatherBuddy.Config.EnableReportSize, b => GatherBuddy.Config.EnableReportSize = b);
        public static void DrawEnableReportMulti() 
            => DrawCheckbox("Copy Multi Hook Stats when reporting.",
                "When copying the report, add stats about multi-hook yields to the report.",
                GatherBuddy.Config.EnableReportMulti, b => GatherBuddy.Config.EnableReportMulti = b);
        public static void DrawEnableGraphs()      
            => DrawCheckbox("Enable Graphs.",
                "When viewing a fishing spot, enable visualization of fish report data. Extreme Testing!",
                GatherBuddy.Config.EnableFishStatsGraphs, b => GatherBuddy.Config.EnableFishStatsGraphs = b);

        // Spearfishing Helper
        public static void DrawSpearfishHelperBox()
            => DrawCheckbox("Show Spearfishing Helper",
                "Toggle whether to show the Spearfishing Helper while spearfishing.",
                GatherBuddy.Config.ShowSpearfishHelper, b => GatherBuddy.Config.ShowSpearfishHelper = b);

        public static void DrawSpearfishNamesBox()
            => DrawCheckbox("Show Fish Name Overlay",
                "Toggle whether to show the identified names of fish in the spearfishing window.",
                GatherBuddy.Config.ShowSpearfishNames, b => GatherBuddy.Config.ShowSpearfishNames = b);

        public static void DrawAvailableSpearfishBox()
            => DrawCheckbox("Show List of Available Fish",
                "Toggle whether to show the list of fish available in your current spearfishing spot on the side of the spearfishing window.",
                GatherBuddy.Config.ShowAvailableSpearfish, b => GatherBuddy.Config.ShowAvailableSpearfish = b);

        public static void DrawSpearfishSpeedBox()
            => DrawCheckbox("Show Speed of Fish in Overlay",
                "Toggle whether to show the speed of fish in the spearfishing window in addition to their names.",
                GatherBuddy.Config.ShowSpearfishSpeed, b => GatherBuddy.Config.ShowSpearfishSpeed = b);

        public static void DrawSpearfishCenterLineBox()
            => DrawCheckbox("Show Center Line",
                "Toggle whether to show a straight line up from the center of the spearfishing gig in the spearfishing window.",
                GatherBuddy.Config.ShowSpearfishCenterLine, b => GatherBuddy.Config.ShowSpearfishCenterLine = b);

        public static void DrawSpearfishIconsAsTextBox()
            => DrawCheckbox("Show Speed and Size as Text",
                "Toggle whether to show the speed and size of available fish as text instead of icons.",
                GatherBuddy.Config.ShowSpearfishListIconsAsText, b => GatherBuddy.Config.ShowSpearfishListIconsAsText = b);

        public static void DrawSpearfishFishNameFixed()
            => DrawCheckbox("Show Fish Names in Fixed Position",
                "Toggle whether to show the identified names of fish on the moving fish themselves or in a fixed position.",
                GatherBuddy.Config.FixNamesOnPosition, b => GatherBuddy.Config.FixNamesOnPosition = b);

        public static void DrawSpearfishFishNamePercentage()
        {
            if (!GatherBuddy.Config.FixNamesOnPosition)
                return;

            var tmp = (int)GatherBuddy.Config.FixNamesPercentage;
            ImGui.SetNextItemWidth(SetInputWidth);
            if (!ImGui.DragInt("Fish Name Position Percentage", ref tmp, 0.1f, 0, 100, "%i%%"))
                return;

            tmp = Math.Clamp(tmp, 0, 100);
            if (tmp == GatherBuddy.Config.FixNamesPercentage)
                return;

            GatherBuddy.Config.FixNamesPercentage = (byte)tmp;
            GatherBuddy.Config.Save();
        }

        // Gather Window
        public static void DrawShowGatherWindowBox()
            => DrawCheckbox("Show Gather Window",
                "Show a small window with pinned Gatherables and their uptimes.",
                GatherBuddy.Config.ShowGatherWindow, b => GatherBuddy.Config.ShowGatherWindow = b);

        public static void DrawGatherWindowAnchorBox()
            => DrawCheckbox("Anchor Gather Window to Bottom Left",
                "Lets the Gather Window grow to the top and shrink from the top instead of the bottom.",
                GatherBuddy.Config.GatherWindowBottomAnchor, b => GatherBuddy.Config.GatherWindowBottomAnchor = b);

        public static void DrawGatherWindowTimersBox()
            => DrawCheckbox("Show Gather Window Timers",
                "Show the uptimes for gatherables in the gather window.",
                GatherBuddy.Config.ShowGatherWindowTimers, b => GatherBuddy.Config.ShowGatherWindowTimers = b);

        public static void DrawGatherWindowAlarmsBox()
            => DrawCheckbox("Show Active Alarms in Gather Window",
                "Additionally show active alarms as a last gather window preset, obeying the regular rules for the window.",
                GatherBuddy.Config.ShowGatherWindowAlarms, b =>
                {
                    GatherBuddy.Config.ShowGatherWindowAlarms = b;
                    _plugin.GatherWindowManager.SetShowGatherWindowAlarms(b);
                });

        public static void DrawSortGatherWindowBox()
            => DrawCheckbox("Sort Gather Window by Uptime",
                "Sort the items selected for the gather window by their uptimes.",
                GatherBuddy.Config.SortGatherWindowByUptime, b => GatherBuddy.Config.SortGatherWindowByUptime = b);

        public static void DrawGatherWindowShowOnlyAvailableBox()
            => DrawCheckbox("Show Only Available Items",
                "Show only those items from your gather window setup that are currently available.",
                GatherBuddy.Config.ShowGatherWindowOnlyAvailable, b => GatherBuddy.Config.ShowGatherWindowOnlyAvailable = b);

        public static void DrawHideGatherWindowCompletedItemsBox()
            => DrawCheckbox("Hide Completed Items",
                "Hide items that have the required inventory amount present in inventory.",
                GatherBuddy.Config.HideGatherWindowCompletedItems, b => GatherBuddy.Config.HideGatherWindowCompletedItems = b);

        public static void DrawHideGatherWindowInDutyBox()
            => DrawCheckbox("Hide Gather Window in Duty",
                "Hide the gather window when bound by any duty.",
                GatherBuddy.Config.HideGatherWindowInDuty, b => GatherBuddy.Config.HideGatherWindowInDuty = b);

        public static void DrawGatherWindowHoldKey()
        {
            DrawCheckbox("Only Show Gather Window if Holding Key",
                "Only show the gather window if you are holding your selected key.",
                GatherBuddy.Config.OnlyShowGatherWindowHoldingKey, b => GatherBuddy.Config.OnlyShowGatherWindowHoldingKey = b);

            if (!GatherBuddy.Config.OnlyShowGatherWindowHoldingKey)
                return;

            ImGui.SetNextItemWidth(SetInputWidth);
            Widget.KeySelector("Hotkey to Hold", "Set the hotkey to hold to keep the window visible.",
                GatherBuddy.Config.GatherWindowHoldKey,
                k => GatherBuddy.Config.GatherWindowHoldKey = k, Configuration.ValidKeys);
        }

        public static void DrawGatherWindowLockBox()
            => DrawCheckbox("Lock Gather Window Position",
                "Prevent moving the gather window by dragging it around.",
                GatherBuddy.Config.LockGatherWindow, b => GatherBuddy.Config.LockGatherWindow = b);


        public static void DrawGatherWindowHotkeyInput()
        {
            if (Widget.ModifiableKeySelector("Hotkey to Open Gather Window", "Set a hotkey to open the Gather Window.", SetInputWidth,
                    GatherBuddy.Config.GatherWindowHotkey, k => GatherBuddy.Config.GatherWindowHotkey = k, Configuration.ValidKeys))
                GatherBuddy.Config.Save();
        }

        public static void DrawMainInterfaceHotkeyInput()
        {
            if (Widget.ModifiableKeySelector("Hotkey to Open Main Interface", "Set a hotkey to open the main GatherBuddy interface.",
                    SetInputWidth,
                    GatherBuddy.Config.MainInterfaceHotkey, k => GatherBuddy.Config.MainInterfaceHotkey = k, Configuration.ValidKeys))
                GatherBuddy.Config.Save();
        }


        public static void DrawGatherWindowDeleteModifierInput()
        {
            ImGui.SetNextItemWidth(SetInputWidth);
            if (Widget.ModifierSelector("Modifier to Delete Items on Right-Click",
                    "Set the modifier key to be used while right-clicking items in the gather window to delete them.",
                    GatherBuddy.Config.GatherWindowDeleteModifier, k => GatherBuddy.Config.GatherWindowDeleteModifier = k))
                GatherBuddy.Config.Save();
        }


        public static void DrawAetherytePreference()
        {
            var tmp     = GatherBuddy.Config.AetherytePreference == AetherytePreference.Cost;
            var oldPref = GatherBuddy.Config.AetherytePreference;
            if (ImGui.RadioButton("Prefer Cheaper Aetherytes", tmp))
                GatherBuddy.Config.AetherytePreference = AetherytePreference.Cost;
            var hovered = ImGui.IsItemHovered();
            ImGui.SameLine();
            if (ImGui.RadioButton("Prefer Less Travel Time", !tmp))
                GatherBuddy.Config.AetherytePreference = AetherytePreference.Distance;
            hovered |= ImGui.IsItemHovered();
            if (hovered)
                ImGui.SetTooltip(
                    "Specify whether you prefer aetherytes that are closer to your target (less travel time)"
                  + " or aetherytes that are cheaper to teleport to when scanning through all available nodes for an item."
                  + " Only matters if the item is not timed and has multiple sources.");

            if (oldPref != GatherBuddy.Config.AetherytePreference)
            {
                GatherBuddy.UptimeManager.ResetLocations();
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawAlarmFormatInput()
            => DrawFormatInput("Alarm Chat Format",
                "Keep empty to have no chat output.\nCan replace:\n- {Alarm} with the alarm name in brackets.\n- {Item} with the item link.\n- {Offset} with the alarm offset in seconds.\n- {DurationString} with 'will be up for the next ...' or 'is currently up for ...'.\n- {場所順} with the map flag link and location name.",
                GatherBuddy.Config.AlarmFormat, Configuration.DefaultAlarmFormat, s => GatherBuddy.Config.AlarmFormat = s);

        public static void DrawIdentifiedGatherableFormatInput()
            => DrawFormatInput("Identified Gatherable Chat Format",
                "Keep empty to have no chat output.\nCan replace:\n- {Input} with the entered search text.\n- {Item} with the item link.",
                GatherBuddy.Config.IdentifiedGatherableFormat, Configuration.DefaultIdentifiedGatherableFormat,
                s => GatherBuddy.Config.IdentifiedGatherableFormat = s);

        public static void DrawAlwaysMapsBox()
            => DrawCheckbox("Always gather maps when available",      "GBR will always grab maps first if it sees one in a node",
                GatherBuddy.Config.AutoGatherConfig.AlwaysGatherMaps, b => GatherBuddy.Config.AutoGatherConfig.AlwaysGatherMaps = b);

        public static void DrawUseExistingAutoHookPresetsBox()
        {
            DrawCheckbox("Use existing AutoHook presets",
                "Use your own AutoHook presets instead of GBR-generated ones.\n"
              + "Name your preset using the fish's Item ID (e.g., '46188' for Goldentail).\n"
              + "Find Fish IDs by hovering over fish in the Fish tab.\n"
              + "Ignored when 'Use AutoHook Global Preset' is enabled.\n"
              + "Your presets will never be deleted - only GBR-generated presets are cleaned up.",
                GatherBuddy.Config.AutoGatherConfig.UseExistingAutoHookPresets,
                b => GatherBuddy.Config.AutoGatherConfig.UseExistingAutoHookPresets = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("AutoHook")]);
        }

        public static void DrawUseAutoHookGlobalPresetBox()
        {
            DrawCheckbox("Use AutoHook Global Preset",
                "Clear AutoHook's selected custom preset and let AutoHook use its built-in Global Preset for rod fishing.\n"
              + "This takes precedence over both GBR-generated presets and fish-ID AutoHook presets.\n"
              + "Spearfishing still uses GBR-generated AutoGig presets.",
                GatherBuddy.Config.AutoGatherConfig.UseAutoHookGlobalPreset,
                b => GatherBuddy.Config.AutoGatherConfig.UseAutoHookGlobalPreset = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("AutoHook")]);
        }

        public static void DrawSurfaceSlapConfig()
        {
            DrawCheckbox("Enable automatic Surface Slap",
                "Automatically enable Surface Slap for non-target fish that share the same bite type as your target fish.\n"
              + "This helps remove unwanted fish to increase catch rates of your target.",
                GatherBuddy.Config.AutoGatherConfig.EnableSurfaceSlap,
                b => GatherBuddy.Config.AutoGatherConfig.EnableSurfaceSlap = b);
            
            if (GatherBuddy.Config.AutoGatherConfig.EnableSurfaceSlap)
            {
                ImGui.Indent();
                
                var gpAbove = GatherBuddy.Config.AutoGatherConfig.SurfaceSlapGPAbove;
                if (ImGui.RadioButton("Use Surface Slap when GP is Above", gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.SurfaceSlapGPAbove = true;
                    GatherBuddy.Config.Save();
                }
                
                ImGui.SameLine();
                if (ImGui.RadioButton("Below", !gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.SurfaceSlapGPAbove = false;
                    GatherBuddy.Config.Save();
                }
                
                var gpThreshold = GatherBuddy.Config.AutoGatherConfig.SurfaceSlapGPThreshold;
                ImGui.SetNextItemWidth(SetInputWidth);
                if (ImGui.DragInt("GP Threshold", ref gpThreshold, 1, 0, 10000))
                {
                    GatherBuddy.Config.AutoGatherConfig.SurfaceSlapGPThreshold = Math.Max(0, gpThreshold);
                    GatherBuddy.Config.Save();
                }
                ImGuiUtil.HoverTooltip("Surface Slap will be used when your GP is above/below this threshold.");
                
                ImGui.Unindent();
            }
        }

        public static void DrawIdenticalCastConfig()
        {
            DrawCheckbox("Enable automatic Identical Cast",
                "Automatically enable Identical Cast for your target fish to increase catch rates.\n"
              + "Identical Cast improves catch rate when used on the same fishing hole.",
                GatherBuddy.Config.AutoGatherConfig.EnableIdenticalCast,
                b => GatherBuddy.Config.AutoGatherConfig.EnableIdenticalCast = b);
            
            if (GatherBuddy.Config.AutoGatherConfig.EnableIdenticalCast)
            {
                ImGui.Indent();
                
                var gpAbove = GatherBuddy.Config.AutoGatherConfig.IdenticalCastGPAbove;
                if (ImGui.RadioButton("Use Identical Cast when GP is Above", gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.IdenticalCastGPAbove = true;
                    GatherBuddy.Config.Save();
                }
                
                ImGui.SameLine();
                if (ImGui.RadioButton("Below##IdenticalCast", !gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.IdenticalCastGPAbove = false;
                    GatherBuddy.Config.Save();
                }
                
                var gpThreshold = GatherBuddy.Config.AutoGatherConfig.IdenticalCastGPThreshold;
                ImGui.SetNextItemWidth(SetInputWidth);
                if (ImGui.DragInt("GP Threshold##IdenticalCast", ref gpThreshold, 1, 0, 10000))
                {
                    GatherBuddy.Config.AutoGatherConfig.IdenticalCastGPThreshold = Math.Max(0, gpThreshold);
                    GatherBuddy.Config.Save();
                }
                ImGuiUtil.HoverTooltip("Identical Cast will be used when your GP is above/below this threshold.");
                
                ImGui.Unindent();
            }
        }

        public static void DrawAmbitiousLureConfig()
        {
            DrawCheckbox("Enable automatic Ambitious Lure",
                "Automatically enable Ambitious Lure for fish that use Powerful Hookset.",
                GatherBuddy.Config.AutoGatherConfig.EnableAmbitiousLure,
                b => GatherBuddy.Config.AutoGatherConfig.EnableAmbitiousLure = b);
            
            if (GatherBuddy.Config.AutoGatherConfig.EnableAmbitiousLure)
            {
                ImGui.Indent();
                
                var gpAbove = GatherBuddy.Config.AutoGatherConfig.AmbitiousLureGPAbove;
                if (ImGui.RadioButton("Use Ambitious Lure when GP is Above", gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.AmbitiousLureGPAbove = true;
                    GatherBuddy.Config.Save();
                }
                
                ImGui.SameLine();
                if (ImGui.RadioButton("Below##AmbitiousLure", !gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.AmbitiousLureGPAbove = false;
                    GatherBuddy.Config.Save();
                }
                
                var gpThreshold = GatherBuddy.Config.AutoGatherConfig.AmbitiousLureGPThreshold;
                ImGui.SetNextItemWidth(SetInputWidth);
                if (ImGui.DragInt("GP Threshold##AmbitiousLure", ref gpThreshold, 1, 0, 10000))
                {
                    GatherBuddy.Config.AutoGatherConfig.AmbitiousLureGPThreshold = Math.Max(0, gpThreshold);
                    GatherBuddy.Config.Save();
                }
                ImGuiUtil.HoverTooltip("Ambitious Lure will be used when your GP is above/below this threshold.");
                
                ImGui.Unindent();
            }
        }

        public static void DrawModestLureConfig()
        {
            DrawCheckbox("Enable automatic Modest Lure",
                "Automatically enable Modest Lure for fish that use Precision Hookset.",
                GatherBuddy.Config.AutoGatherConfig.EnableModestLure,
                b => GatherBuddy.Config.AutoGatherConfig.EnableModestLure = b);
            
            if (GatherBuddy.Config.AutoGatherConfig.EnableModestLure)
            {
                ImGui.Indent();
                
                var gpAbove = GatherBuddy.Config.AutoGatherConfig.ModestLureGPAbove;
                if (ImGui.RadioButton("Use Modest Lure when GP is Above", gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.ModestLureGPAbove = true;
                    GatherBuddy.Config.Save();
                }
                
                ImGui.SameLine();
                if (ImGui.RadioButton("Below##ModestLure", !gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.ModestLureGPAbove = false;
                    GatherBuddy.Config.Save();
                }
                
                var gpThreshold = GatherBuddy.Config.AutoGatherConfig.ModestLureGPThreshold;
                ImGui.SetNextItemWidth(SetInputWidth);
                if (ImGui.DragInt("GP Threshold##ModestLure", ref gpThreshold, 1, 0, 10000))
                {
                    GatherBuddy.Config.AutoGatherConfig.ModestLureGPThreshold = Math.Max(0, gpThreshold);
                    GatherBuddy.Config.Save();
                }
                ImGuiUtil.HoverTooltip("Modest Lure will be used when your GP is above/below this threshold.");
                
                ImGui.Unindent();
            }
        }

        public static void DrawUseHookTimersBox()
        {
            DrawCheckbox("Use Hook Timers in AutoHook Presets",
                "Enable bite timer windows in generated AutoHook presets.",
                GatherBuddy.Config.AutoGatherConfig.UseHookTimers,
                b => GatherBuddy.Config.AutoGatherConfig.UseHookTimers = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("AutoHook")]);
        }

        public static void DrawAutoCollectablesFishingBox()
            => DrawCheckbox("Auto Collectables",
                "Auto accept/decline collectable fish based on minimum collectability.",
                GatherBuddy.Config.AutoGatherConfig.AutoCollectablesFishing,
                b => GatherBuddy.Config.AutoGatherConfig.AutoCollectablesFishing = b);

        public static void DrawDeferRepairDuringFishingBuffsBox()
            => DrawCheckbox("Defer repairs during fishing buffs",
                "Prevents GBR from stopping fishing for repairs when you have active fishing skill buffs.\n"
              + "Buffs like Patience, Surface Slap, Identical Cast, Prize Catch, etc. will be respected.",
                GatherBuddy.Config.AutoGatherConfig.DeferRepairDuringFishingBuffs,
                b => GatherBuddy.Config.AutoGatherConfig.DeferRepairDuringFishingBuffs = b);

        public static void DrawDeferReductionDuringFishingBuffsBox()
            => DrawCheckbox("Defer aetherial reduction during fishing buffs",
                "Prevents GBR from stopping fishing for aetherial reduction when you have active fishing skill buffs.",
                GatherBuddy.Config.AutoGatherConfig.DeferReductionDuringFishingBuffs,
                b => GatherBuddy.Config.AutoGatherConfig.DeferReductionDuringFishingBuffs = b);

        public static void DrawDeferMateriaExtractionDuringFishingBuffsBox()
            => DrawCheckbox("Defer materia extraction during fishing buffs",
                "Prevents GBR from stopping fishing for materia extraction when you have active fishing skill buffs.",
                GatherBuddy.Config.AutoGatherConfig.DeferMateriaExtractionDuringFishingBuffs,
                b => GatherBuddy.Config.AutoGatherConfig.DeferMateriaExtractionDuringFishingBuffs = b);

        public static void DrawFishingCordialConfig()
        {
            DrawCheckbox("Use Cordial",
                "Automatically use cordials in generated fishing presets when GP falls below the minimum threshold.",
                GatherBuddy.Config.AutoGatherConfig.UseCordialForFishing,
                b => GatherBuddy.Config.AutoGatherConfig.UseCordialForFishing = b);

            if (GatherBuddy.Config.AutoGatherConfig.UseCordialForFishing)
            {
                ImGui.Indent();
                ImGui.SetNextItemWidth(150);
                var gpThreshold = GatherBuddy.Config.AutoGatherConfig.CordialForFishingGPThreshold;
                if (ImGui.DragInt("GP Threshold", ref gpThreshold, 1, 0, 10000))
                {
                    GatherBuddy.Config.AutoGatherConfig.CordialForFishingGPThreshold = Math.Max(0, gpThreshold);
                    GatherBuddy.Config.Save();
                }
                ImGuiUtil.HoverTooltip("Use cordial when GP falls below this threshold (prevents overcapping).");
                ImGui.Unindent();
            }
        }

        public static void DrawUsePatienceBox()
            => DrawCheckbox("Use Patience/Patience II",
                "Automatically use Patience/Patience II in generated fishing presets when fishing for:\n"
              + "• Fish requiring mooch chains\n"
              + "• Collectable fish\n"
              + "• Fish that can be used for aetherial reduction",
                GatherBuddy.Config.AutoGatherConfig.UsePatience,
                b => GatherBuddy.Config.AutoGatherConfig.UsePatience = b);

        public static void DrawPrizeCatchConfig()
        {
            DrawCheckbox("Use Prize Catch",
                "Automatically use Prize Catch in generated fishing presets.\n"
              + "Recommended for mooching or Surface Slap fishing.",
                GatherBuddy.Config.AutoGatherConfig.UsePrizeCatch,
                b => GatherBuddy.Config.AutoGatherConfig.UsePrizeCatch = b);
            
            if (GatherBuddy.Config.AutoGatherConfig.UsePrizeCatch)
            {
                ImGui.Indent();
                
                var gpAbove = GatherBuddy.Config.AutoGatherConfig.PrizeCatchGPAbove;
                if (ImGui.RadioButton("Use Prize Catch when GP is Above", gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.PrizeCatchGPAbove = true;
                    GatherBuddy.Config.Save();
                }
                
                ImGui.SameLine();
                if (ImGui.RadioButton("Below##PrizeCatch", !gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.PrizeCatchGPAbove = false;
                    GatherBuddy.Config.Save();
                }
                
                var gpThreshold = GatherBuddy.Config.AutoGatherConfig.PrizeCatchGPThreshold;
                ImGui.SetNextItemWidth(SetInputWidth);
                if (ImGui.DragInt("GP Threshold##PrizeCatch", ref gpThreshold, 1, 0, 10000))
                {
                    GatherBuddy.Config.AutoGatherConfig.PrizeCatchGPThreshold = Math.Max(0, gpThreshold);
                    GatherBuddy.Config.Save();
                }
                ImGuiUtil.HoverTooltip("Prize Catch will be used when your GP is above/below this threshold.");
                
                ImGui.Unindent();
            }
        }

        public static void DrawChumConfig()
        {
            DrawCheckbox("Use Chum",
                "Automatically use Chum in generated fishing presets.",
                GatherBuddy.Config.AutoGatherConfig.UseChum,
                b => GatherBuddy.Config.AutoGatherConfig.UseChum = b);
            
            if (GatherBuddy.Config.AutoGatherConfig.UseChum)
            {
                ImGui.Indent();
                
                var gpAbove = GatherBuddy.Config.AutoGatherConfig.ChumGPAbove;
                if (ImGui.RadioButton("Use Chum when GP is Above", gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.ChumGPAbove = true;
                    GatherBuddy.Config.Save();
                }
                
                ImGui.SameLine();
                if (ImGui.RadioButton("Below##Chum", !gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.ChumGPAbove = false;
                    GatherBuddy.Config.Save();
                }
                
                var gpThreshold = GatherBuddy.Config.AutoGatherConfig.ChumGPThreshold;
                ImGui.SetNextItemWidth(SetInputWidth);
                if (ImGui.DragInt("GP Threshold##Chum", ref gpThreshold, 1, 0, 10000))
                {
                    GatherBuddy.Config.AutoGatherConfig.ChumGPThreshold = Math.Max(0, gpThreshold);
                    GatherBuddy.Config.Save();
                }
                ImGuiUtil.HoverTooltip("Chum will be used when your GP is above/below this threshold.");
                
                ImGui.Unindent();
            }
        }

        public static void DrawFishingConsumablesConfig()
        {
            DrawCheckbox("Use Food",
                "Automatically use configured food when food buff expires (only when NOT fishing or no active fishing buffs).",
                GatherBuddy.Config.AutoGatherConfig.UseFood,
                b => GatherBuddy.Config.AutoGatherConfig.UseFood = b);

            if (GatherBuddy.Config.AutoGatherConfig.UseFood)
            {
                ImGui.Indent();
                ImGui.SetNextItemWidth(150);
                DrawConsumableCombo("Select food", AutoGather.AutoGather.PossibleFoods, 
                    GatherBuddy.Config.AutoGatherConfig.FoodItemId, 
                    id => 
                    {
                        GatherBuddy.Config.AutoGatherConfig.FoodItemId = id;
                        GatherBuddy.Config.Save();
                    });
                ImGui.Unindent();
            }

            DrawCheckbox("Use Medicine",
                "Automatically use configured medicine (like Draft of Spiritbond) when medicine buff expires (only when NOT fishing or no active fishing buffs).",
                GatherBuddy.Config.AutoGatherConfig.UseMedicine,
                b => GatherBuddy.Config.AutoGatherConfig.UseMedicine = b);

            if (GatherBuddy.Config.AutoGatherConfig.UseMedicine)
            {
                ImGui.Indent();
                ImGui.SetNextItemWidth(150);
                DrawConsumableCombo("Select medicine", AutoGather.AutoGather.PossiblePotions, 
                    GatherBuddy.Config.AutoGatherConfig.MedicineItemId, 
                    id => 
                    {
                        GatherBuddy.Config.AutoGatherConfig.MedicineItemId = id;
                        GatherBuddy.Config.Save();
                    });
                ImGui.Unindent();
            }
        }

        private static void DrawConsumableCombo(string label, Lumina.Excel.Sheets.Item[] items, uint currentItemId, Action<uint> onChanged)
        {
            var list = items
                .SelectMany(item => new[]
                {
                    (item, rowid: item.RowId, isHq: false),
                    (item, rowid: item.RowId + 1_000_000, isHq: true)
                })
                .Where(x => !x.isHq || x.item.CanBeHq)
                .Select(x => (name: ItemUtil.GetItemName(x.rowid, includeIcon: true).ExtractText(), x.rowid, count: AutoGather.AutoGather.GetInventoryItemCount(x.rowid)))
                .Where(x => !string.IsNullOrEmpty(x.name))
                .OrderBy(x => x.count == 0)
                .ThenBy(x => x.name)
                .Select(x => x with { name = $"{x.name} ({x.count})" })
                .ToList();

            var selected = (currentItemId > 0 ? list.FirstOrDefault(x => x.rowid == currentItemId).name : null) ?? string.Empty;
            using var combo = ImRaii.Combo(label, selected);
            if (combo)
            {
                if (ImGui.Selectable(string.Empty, currentItemId <= 0))
                {
                    onChanged(0);
                }

                bool? separatorState = null;
                foreach (var (itemname, rowid, count) in list)
                {
                    if (count != 0)
                        separatorState = true;
                    else if (separatorState ?? false)
                    {
                        ImGui.Separator();
                        separatorState = false;
                    }

                    if (ImGui.Selectable(itemname, currentItemId == rowid))
                    {
                        onChanged(rowid);
                    }
                }
            }
        }
        
        public static void DrawDiademAutoAetherCannonBox()
            => DrawCheckbox("Diadem Auto-Aethercannon",
                "Automatically target and fire aethercannon at nearby enemies when gauge is ready (≥200).\n"
              + "Only fires while not pathing/navigating. 2-second cooldown between uses.",
                GatherBuddy.Config.AutoGatherConfig.DiademAutoAetherCannon,
                b => GatherBuddy.Config.AutoGatherConfig.DiademAutoAetherCannon = b);

        public static void DrawDiademWindmireJumps()
            => DrawCheckbox("Diadem Windmire Jumps",
                "Allows the use of Windmires for jumping between islands in the Diadem.\n" +
                "Windmires will only be used when they provide a significant distance advantage over normal movement.",
                GatherBuddy.Config.AutoGatherConfig.DiademWindmireJumps,
                b => GatherBuddy.Config.AutoGatherConfig.DiademWindmireJumps = b);
        
        public static void DrawDiademFarmCloudedNodes()
            => DrawCheckbox("Re-enter The Diadem to Reset Clouded Nodes",
                "After gathering umbral items from a clouded node, re-enter the instance to make the node reappear.",
                GatherBuddy.Config.AutoGatherConfig.DiademFarmCloudedNodes,
                b => GatherBuddy.Config.AutoGatherConfig.DiademFarmCloudedNodes = b);

        public static void DrawManualPresetGenerator()
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Manual Preset Generator");
            ImGui.Spacing();
            
            var availableFish = GatherBuddy.GameData.Fishes.Values.Where(f => !f.IsSpearFish).ToList();
            
            ImGui.TextUnformatted("Select Target Fish:");
            ImGui.SetNextItemWidth(SetInputWidth);
            
            if (ImGui.BeginCombo("###FishSelector", _selectedFish?.Name[GatherBuddy.Language] ?? "並び替えなし"))
            {
                ImGui.SetNextItemWidth(SetInputWidth - 20);
                ImGui.InputTextWithHint("###FishFilter", "Search...", ref _fishFilterText, 100);
                ImGui.Separator();
                
                using (var child = ImRaii.Child("###FishList", new Vector2(0, 200 * ImGuiHelpers.GlobalScale), false))
                {
                    for (int i = 0; i < availableFish.Count; i++)
                    {
                        var fish = availableFish[i];
                        var fishName = fish.Name[GatherBuddy.Language];
                        
                        if (_fishFilterText.Length > 0 && !fishName.ToLower().Contains(_fishFilterText.ToLower()))
                            continue;
                        
                        using var id = ImRaii.PushId($"{fish.ItemId}###{i}");
                        if (ImGui.Selectable(fishName, _selectedFish?.ItemId == fish.ItemId))
                        {
                            _selectedFish = fish;
                            _presetName = fish.ItemId.ToString();
                            _fishFilterText = "";
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }
                
                ImGui.EndCombo();
            }
            
            if (_selectedFish != null)
            {
                ImGui.Spacing();
                ImGui.TextUnformatted("Preset Name:");
                ImGui.SetNextItemWidth(SetInputWidth);
                ImGui.InputText("###PresetNameInput", ref _presetName, 64);
                ImGuiUtil.HoverTooltip("The preset name should match the fish's Item ID for GBR to use it automatically.");
                
                ImGui.Spacing();
                if (ImGui.Button("Generate Preset"))
                {
                    GenerateManualPreset(_selectedFish, _presetName);
                }
            }
        }
        
        private static void GenerateManualPreset(Fish fish, string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
                presetName = fish.ItemId.ToString();
            
            var success = AutoHookIntegration.AutoHookService.ExportPresetToAutoHook(presetName, [fish], _base.MatchConfigPreset(fish));
            
            if (success)
            {
                if (fish.Predators.Length > 0 && fish.Predators.All(p => !p.Item1.IsSpearFish))
                {
                    Dalamud.Chat.Print($"[GatherBuddy] Generated 2 presets for {fish.Name[GatherBuddy.Language]}: '{presetName}_Predators' and '{presetName}_Target'");
                }
                else
                {
                    Dalamud.Chat.Print($"[GatherBuddy] Generated preset '{presetName}' for {fish.Name[GatherBuddy.Language]}");
                }
            }
            else
            {
                Dalamud.Chat.PrintError($"[GatherBuddy] Failed to generate preset for {fish.Name[GatherBuddy.Language]}");
            }
        }
    }

    private string _configSearch       = string.Empty;
    private int    _selectedConfigPage  = 0;

    private readonly record struct ConfigEntry(string SearchText, Action<ConfigLayout> Draw)
    {
        public ConfigEntry(string searchText, Action draw)
            : this(searchText, _ => draw())
        { }
    }
    private readonly record struct ConfigPage(string Category, string Name, ConfigEntry[] Entries);
    private readonly record struct ConfigLayout(int Depth)
    {
        public static ConfigLayout Root { get; } = new(0);

        public ConfigLayout Child => new(Depth + 1);

        public void Draw(ConfigEntry entry)
        {
            using var indent = PushConfigIndent(Depth);
            entry.Draw(this);
        }

        public void Draw(Action draw)
        {
            using var indent = PushConfigIndent(Depth);
            draw();
        }
    }

    private readonly struct ConfigIndentScope : IDisposable
    {
        private readonly float _amount;

        public ConfigIndentScope(float amount)
        {
            _amount = amount;
            ImGui.Indent(amount);
        }

        public void Dispose()
        {
            if (_amount > 0f)
                ImGui.Unindent(_amount);
        }
    }

    private static readonly ConfigPage[] ConfigPages = BuildConfigPages();

    private static ConfigIndentScope PushConfigIndent(int depth)
    {
        if (depth <= 0)
            return default;

        return new ConfigIndentScope(depth * ImGui.GetStyle().IndentSpacing);
    }

    private static ConfigPage[] BuildConfigPages() =>
    [
        new("自動採集", "基本",
        [
            new("マウント選択",                                   AutoGatherUI.DrawMountSelector),
            new("マウント開始距離",                              ConfigFunctions.DrawMountUpDistance),
            new("着地距離",                                      ConfigFunctions.DrawLandingDistance),
            new("マウント中も移動",                              ConfigFunctions.DrawMoveWhileMounting),
            new("採集完了時の効果音と音量",
                layout =>
                {
                    ConfigFunctions.DrawHonkModeBox();
                    if (GatherBuddy.Config.AutoGatherConfig.HonkMode)
                        layout.Child.Draw(ConfigFunctions.DrawHonkVolumeSlider);
                }),
            new("リテイナー所持品を確認",                         ConfigFunctions.DrawCheckRetainersBox),
            new("次の時間限定アイテムへテレポ",                   ConfigFunctions.DrawTeleportToNextNodeBox),
            new("完了時・待機時に帰宅",                           ConfigFunctions.DrawGoHomeBox),
            new("大地の恵みのリキャスト中にクリスタルを採集",     ConfigFunctions.DrawUseGivingLandOnCooldown),
            new("予備アイテムにもスキルを使用",                   ConfigFunctions.DrawUseSkillsForFallabckBox),
            new("必要アイテムが無い採集場を放棄",                 ConfigFunctions.DrawAbandonNodesBox),
            new("地図がある場合は常に採集",                       ConfigFunctions.DrawAlwaysMapsBox),
        ]),
        new("自動採集", "詳細",
        [
            new("必要時に装備修理・修理しきい値",
                layout =>
                {
                    ConfigFunctions.DrawRepairBox();
                    if (GatherBuddy.Config.AutoGatherConfig.DoRepair)
                        layout.Child.Draw(ConfigFunctions.DrawRepairThreshold);
                }),
            new("マテリア精製を有効化",                           ConfigFunctions.DrawMaterialExtraction),
            new("精選を有効化・全アイテムを常に精選",
                layout =>
                {
                    ConfigFunctions.DrawAetherialReduction();
                    if (GatherBuddy.Config.AutoGatherConfig.DoReduce)
                        layout.Child.Draw(ConfigFunctions.DrawAlwaysReduceAllItemsBox);
                }),
            new("AutoRetainerマルチモード待機・しきい値・時間限定採集場での遅延",
                layout =>
                {
                    ConfigFunctions.DrawAutoretainerBox();
                    if (GatherBuddy.Config.AutoGatherConfig.AutoRetainerMultiMode)
                    {
                        layout.Child.Draw(ConfigFunctions.DrawAutoretainerThreshold);
                        layout.Child.Draw(ConfigFunctions.DrawAutoretainerTimedNodeDelayBox);
                    }
                }),
            new("アイテム並び替え方法",                           ConfigFunctions.DrawSortingMethodCombo),
            new("Lifestreamコマンド",                             ConfigFunctions.DrawLifestreamCommandTextInput),
            new("スタック対策クールダウン",                       ConfigFunctions.DrawAntiStuckCooldown),
            new("スタック判定しきい値",                           ConfigFunctions.DrawStuckThreshold),
            new("時間限定採集場の先読み",                         ConfigFunctions.DrawTimedNodePrecog),
            new("実行遅延ミリ秒",                                 ConfigFunctions.DrawExecutionDelay),
            new("採集ウィンドウ操作を有効化",                     ConfigFunctions.DrawAutoGatherBox),
            new("マップマーカー移動を無効化",                     ConfigFunctions.DrawUseFlagBox),
            new("vnavmesh移動を使用",                             ConfigFunctions.DrawUseNavigationBox),
            new("歩き移動を強制",                                 ConfigFunctions.DrawForceWalkingBox),
            new("ランダム着地点を無効化",                         ConfigFunctions.DrawDisableRandomLandingPositionsBox),
        ]),
        new("一般", "採集コマンド",
        [
            new("優先ジョブ・指定なし・採掘師・園芸師",           ConfigFunctions.DrawPreferredJobSelect),
            new("ギアセット変更を有効化",                         ConfigFunctions.DrawGearChangeBox),
            new("テレポを有効化",                                 ConfigFunctions.DrawTeleportBox),
            new("場所をマップで開く",                             ConfigFunctions.DrawMapOpenBox),
            new("マップにフラッグを立てる",                       ConfigFunctions.DrawPlaceMarkerBox),
            new("カスタムウェイマークを置く",                     ConfigFunctions.DrawPlaceWaymarkBox),
            new("安いエーテライト優先・移動時間優先",             ConfigFunctions.DrawAetherytePreference),
            new("近距離テレポをスキップ",                         ConfigFunctions.DrawSkipTeleportBox),
            new("ゲーム内コンテキストメニューを追加",             ConfigFunctions.DrawContextMenuBox),
        ]),
        new("一般", "ギアセット名",
        [
            new("採掘師セット", () => ConfigFunctions.DrawSetInput("採掘師", GatherBuddy.Config.MinerSetName,    s => GatherBuddy.Config.MinerSetName    = s)),
            new("園芸師セット", () => ConfigFunctions.DrawSetInput("園芸師", GatherBuddy.Config.BotanistSetName, s => GatherBuddy.Config.BotanistSetName = s)),
        ]),
        new("一般", "メッセージ",
        [
            new("通常メッセージのチャット種類",                   ConfigFunctions.DrawPrintTypeSelector),
            new("エラーのチャット種類",                           ConfigFunctions.DrawErrorTypeSelector),
            new("マップ位置を表示",                               ConfigFunctions.DrawMapMarkerPrintBox),
            new("採集時に採集場の出現時間を表示",                 ConfigFunctions.DrawPrintUptimesBox),
            new("クリップボード情報を表示",                       ConfigFunctions.DrawPrintClipboardBox),
            new("識別した採集物のチャット形式",                   ConfigFunctions.DrawIdentifiedGatherableFormatInput),
        ]),
        new("UI", "設定ウィンドウ",
        [
            new("起動時に設定UIを開く",                           ConfigFunctions.DrawOpenOnStartBox),
            new("Escapeでメインウィンドウを閉じる",               ConfigFunctions.DrawRespectEscapeBox),
            new("設定UIの移動をロック",                           ConfigFunctions.DrawLockPositionBox),
            new("設定UIのサイズをロック",                         ConfigFunctions.DrawLockResizeBox),
            new("ステータス行を表示",                             ConfigFunctions.DrawShowStatusLineBox),
            new("GatherClippyボタンを非表示",                     ConfigFunctions.DrawHideClippyBox),
            new("メイン画面を開くホットキー",                     ConfigFunctions.DrawMainInterfaceHotkeyInput),
        ]),
        new("UI", "採集ウィンドウ",
        [
            new("採集ウィンドウを表示",                           ConfigFunctions.DrawShowGatherWindowBox),
            new("採集ウィンドウを左下に固定",                     ConfigFunctions.DrawGatherWindowAnchorBox),
            new("採集ウィンドウにタイマーを表示",                 ConfigFunctions.DrawGatherWindowTimersBox),
            new("採集ウィンドウを出現時間順で並べ替え",           ConfigFunctions.DrawSortGatherWindowBox),
            new("出現中アイテムのみ表示",                         ConfigFunctions.DrawGatherWindowShowOnlyAvailableBox),
            new("完了アイテムを非表示",                           ConfigFunctions.DrawHideGatherWindowCompletedItemsBox),
            new("コンテンツ中は採集ウィンドウを非表示",           ConfigFunctions.DrawHideGatherWindowInDutyBox),
            new("キー押下中のみ採集ウィンドウを表示",             ConfigFunctions.DrawGatherWindowHoldKey),
            new("採集ウィンドウ位置をロック",                     ConfigFunctions.DrawGatherWindowLockBox),
            new("採集ウィンドウを開くホットキー",                 ConfigFunctions.DrawGatherWindowHotkeyInput),
            new("右クリック削除に使う修飾キー",                   ConfigFunctions.DrawGatherWindowDeleteModifierInput),
        ]),
    ];

    private static void DrawAllColors()
    {
        foreach (var color in Enum.GetValues<ColorId>())
        {
            var (defaultColor, name, description) = color.Data();
            var currentColor = GatherBuddy.Config.Colors.TryGetValue(color, out var current) ? current : defaultColor;
            if (Widget.ColorPicker(name, description, currentColor, c => GatherBuddy.Config.Colors[color] = c, defaultColor))
                GatherBuddy.Config.Save();
        }

        ImGui.NewLine();

        if (Widget.PaletteColorPicker("Names in Chat",         Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorNames,
                Configuration.DefaultSeColorNames,    Configuration.ForegroundColors, out var idx))
            GatherBuddy.Config.SeColorNames = idx;
        if (Widget.PaletteColorPicker("Commands in Chat",      Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorCommands,
                Configuration.DefaultSeColorCommands, Configuration.ForegroundColors, out idx))
            GatherBuddy.Config.SeColorCommands = idx;
        if (Widget.PaletteColorPicker("Arguments in Chat",     Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorArguments,
                Configuration.DefaultSeColorArguments, Configuration.ForegroundColors, out idx))
            GatherBuddy.Config.SeColorArguments = idx;
        if (Widget.PaletteColorPicker("Alarm Message in Chat", Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorAlarm,
                Configuration.DefaultSeColorAlarm,    Configuration.ForegroundColors, out idx))
            GatherBuddy.Config.SeColorAlarm = idx;
    }


    private void DrawConfigTab()
    {
        using var id = ImRaii.PushId("Config");
        var       selectFromHeader = _selectConfigTab;
        var       dummy            = true;
        using var tab = selectFromHeader
            ? ImRaii.TabItem("設定", ref dummy, ImGuiTabItemFlags.SetSelected)
            : ImRaii.TabItem("設定");
        if (selectFromHeader)
            _selectConfigTab = false;
        ImGuiUtil.HoverTooltip("GatherBuddy JP の動作を設定します。\n"
          + "自動採集、テレポ、移動、表示、釣り支援などを調整できます。");

        if (!tab)
            return;

        ConfigFunctions._base = this;

        var leftPanelWidth = 175f * Scale;

        {
            using var leftChild = ImRaii.Child("##ConfigLeft", new Vector2(leftPanelWidth, 0), true);
            if (leftChild)
            {
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##ConfigSearch", "設定を検索...", ref _configSearch, 256);
                ImGui.Separator();
                DrawConfigPageSelector();
            }
        }

        ImGui.SameLine();

        using var rightChild = ImRaii.Child("##ConfigRight", Vector2.Zero, false);
        if (!rightChild)
            return;
        var padding = ImGui.GetStyle().WindowPadding;
        ImGui.SetCursorPosY(padding.Y);

        if (!string.IsNullOrWhiteSpace(_configSearch))
            DrawConfigSearchResults();
        else
            DrawConfigPage(ConfigPages[_selectedConfigPage]);
    }

    private void DrawConfigPageSelector()
    {
        var lastCategory = string.Empty;
        for (var i = 0; i < ConfigPages.Length; i++)
        {
            var page = ConfigPages[i];
            if (page.Category != lastCategory)
            {
                if (lastCategory.Length > 0)
                    ImGui.Spacing();
                if (page.Category.Length > 0)
                {
                    ImGui.TextDisabled(page.Category.ToUpperInvariant());
                    ImGui.Separator();
                }
                lastCategory = page.Category;
            }

            var isSelected = _selectedConfigPage == i;
            if (ImGui.Selectable(page.Name, isSelected) && !isSelected)
            {
                _selectedConfigPage = i;
                _configSearch       = string.Empty;
            }
        }
    }

    private void DrawConfigSearchResults()
    {
        var query = _configSearch.Trim();
        var any   = false;
        var layout = ConfigLayout.Root;

        foreach (var page in ConfigPages)
        {
            var hasMatch = false;
            foreach (var entry in page.Entries)
            {
                if (entry.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    hasMatch = true;
                    break;
                }
            }

            if (!hasMatch) continue;

            if (any)
                ImGui.Spacing();
            any = true;

            var header = page.Category.Length > 0 ? $"{page.Category}: {page.Name}" : page.Name;
            DrawConfigSearchHeader(header);

            foreach (var entry in page.Entries)
                if (entry.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
                    layout.Draw(entry);
        }

        if (!any)
        {
            var startY = ImGui.GetCursorPosY();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("一致する設定がありません。");
            var targetY = startY + ImGui.GetFrameHeightWithSpacing();
            if (ImGui.GetCursorPosY() < targetY)
                ImGui.SetCursorPosY(targetY);
        }
    }

    private static void DrawConfigSearchHeader(string header)
    {
        var startY = ImGui.GetCursorPosY();
        var startX = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(header.ToUpperInvariant());
        var targetY = startY + ImGui.GetFrameHeightWithSpacing();
        if (ImGui.GetCursorPosY() < targetY)
            ImGui.SetCursorPosY(targetY);
        ImGui.Separator();
        ImGui.SetCursorPosX(startX);
    }

    private static void DrawConfigPage(ConfigPage page)
    {
        var layout = ConfigLayout.Root;
        foreach (var entry in page.Entries)
            layout.Draw(entry);
    }
}


