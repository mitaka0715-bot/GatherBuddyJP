using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using GatherBuddy.Crafting;
using GatherBuddy.Enums;
using GatherBuddy.Plugin;
using GatherBuddy.Time;

namespace GatherBuddy;

public partial class GatherBuddy
{
    public const string IdentifyCommand       = "identify";
    public const string GearChangeCommand     = "gearchange";
    public const string TeleportCommand       = "teleport";
    public const string MapMarkerCommand      = "mapmarker";
    public const string AdditionalInfoCommand = "information";
    public const string SetWaymarksCommand    = "waymarks";
    public const string AutoCommand           = "auto";
    public const string AutoOnCommand         = "auto on";
    public const string AutoOffCommand        = "auto off";
    public const string MainCommand           = "/gbjp";
    public const string FullIdentify          = $"{MainCommand} {IdentifyCommand}";
    public const string FullGearChange        = $"{MainCommand} {GearChangeCommand}";
    public const string FullTeleport          = $"{MainCommand} {TeleportCommand}";
    public const string FullMapMarker         = $"{MainCommand} {MapMarkerCommand}";
    public const string FullAdditionalInfo    = $"{MainCommand} {AdditionalInfoCommand}";
    public const string FullSetWaymarks       = $"{MainCommand} {SetWaymarksCommand}";
    public const string FullAuto              = $"{MainCommand} {AutoCommand}";
    public const string FullAutoOn            = $"{MainCommand} {AutoOnCommand}";
    public const string FullAutoOff           = $"{MainCommand} {AutoOffCommand}";

    private readonly Dictionary<string, CommandInfo> _commands = new();

    private void InitializeCommands()
    {
        _commands["/gatherbuddyjp"] = new CommandInfo(OnGatherBuddy)
        {
            HelpMessage = "GatherBuddy JP の画面を開きます。",
            ShowInHelp  = false,
        };

        _commands[MainCommand] = new CommandInfo(OnGatherBuddy)
        {
            HelpMessage = "GatherBuddy JP の画面を開きます。",
            ShowInHelp  = true,
        };

        _commands["/gbjpgather"] = new CommandInfo(OnGather)
        {
            HelpMessage = "指定アイテムを含む最寄り採集場所を探し、最寄りエーテライトへテレポし、適切な装備に変更します。\n"
              + "'alarm' で最後に通知されたアラーム対象、'next' で前回と同じアイテムの次候補を指定できます。",
            ShowInHelp = true,
        };

        _commands["/gbjpbtn"] = new CommandInfo(OnGatherBtn)
        {
            HelpMessage =
                "指定アイテムを含む最寄り園芸採集場所を探し、最寄りエーテライトへテレポし、適切な装備に変更します。",
            ShowInHelp = true,
        };

        _commands["/gbjpmin"] = new CommandInfo(OnGatherMin)
        {
            HelpMessage =
                "指定アイテムを含む最寄り採掘場所を探し、最寄りエーテライトへテレポし、適切な装備に変更します。",
            ShowInHelp = true,
        };

        _commands["/gbjpfish"] = new CommandInfo(OnGatherFish)
        {
            HelpMessage =
                "指定魚の釣り場を探し、最寄りエーテライトへテレポし、釣り装備に変更します。",
            ShowInHelp = true,
        };

        _commands["/gbjpgroup"] = new CommandInfo(OnGatherGroup)
        {
            HelpMessage = "現在時刻に対応するグループ内の採集場所へテレポします。詳細は /gbjpgroup を実行してください。",
            ShowInHelp  = true,
        };

        _commands["/gbjpc"] = new CommandInfo(OnGatherBuddyShort)
        {
            HelpMessage = "設定項目を素早く切り替えます。引数なしでヘルプを表示します。",
            ShowInHelp  = true,
        };

        _commands["/gbjpdebug"] = new CommandInfo(OnGatherDebug)
        {
            ShowInHelp = false,
        };

        _commands["/vulcan"] = new CommandInfo(OnVulcan)
        {
            HelpMessage = "Open the Vulcan crafting interface. Use with a list ID/name to jump to that list, or 'craft <recipeId|name> [qty]' to start the full gather+craft pipeline immediately.",
            ShowInHelp  = true,
        };

        _commands["/vvendor"] = new CommandInfo(OnVendor)
        {
            HelpMessage = "Open the Vulcan Vendors tab.",
            ShowInHelp  = true,
        };

        _commands["/vulcanmb"] = new CommandInfo(OnVulcanMarketboard)
        {
            HelpMessage = "Open the Vulcan Marketboard tab.",
            ShowInHelp  = true,
        };

        _commands["/vcollect"] = new CommandInfo(OnCollectablesWindow)
        {
            HelpMessage = "Open the Collectables turn-in and purchase window.",
            ShowInHelp  = true,
        };

        foreach (var (command, info) in _commands)
            Dalamud.Commands.AddHandler(command, info);
    }

    private void DisposeCommands()
    {
        foreach (var command in _commands.Keys)
            Dalamud.Commands.RemoveHandler(command);
    }

    private void OnGatherBuddy(string command, string arguments)
    {
        if (arguments.Equals("vulcan", StringComparison.OrdinalIgnoreCase))
        {
            _vulcanWindow?.Toggle();
            return;
        }


        if (!Executor.DoCommand(arguments))
            Interface.Toggle();
    }

    private void OnGather(string command, string arguments)
    {
        if (arguments.Length == 0)
            Communicator.NoItemName(command, "item");
        else
            Executor.GatherItemByName(arguments);
    }

    private void OnGatherBtn(string command, string arguments)
    {
        if (arguments.Length == 0)
            Communicator.NoItemName(command, "item");
        else
            Executor.GatherItemByName(arguments, GatheringType.Botanist);
    }

    private void OnGatherMin(string command, string arguments)
    {
        if (arguments.Length == 0)
            Communicator.NoItemName(command, "item");
        else
            Executor.GatherItemByName(arguments, GatheringType.Miner);
    }

    private void OnGatherFish(string command, string arguments)
    {
        if (arguments.Length == 0)
            Communicator.NoItemName(command, "fish");
        else
            Executor.GatherFishByName(arguments);
    }

    private void OnGatherGroup(string command, string arguments)
    {
        if (arguments.Length == 0)
        {
            Communicator.Print(GatherGroupManager.CreateHelp());
            return;
        }

        var argumentParts = arguments.Split();
        var minute = (Time.EorzeaMinuteOfDay + (argumentParts.Length < 2 ? 0 : int.TryParse(argumentParts[1], out var offset) ? offset : 0))
          % RealTime.MinutesPerDay;
        if (!GatherGroupManager.TryGetValue(argumentParts[0], out var group))
        {
            Communicator.NoGatherGroup(argumentParts[0]);
            return;
        }

        var node = group.CurrentNode((uint)minute);
        if (node == null)
        {
            Communicator.NoGatherGroupItem(argumentParts[0], minute);
        }
        else
        {
            if (node.Annotation.Any())
                Communicator.Print(node.Annotation);
            if (node.PreferLocation == null)
                Executor.GatherItem(node.Item);
            else
                Executor.GatherLocation(node.PreferLocation);
        }
    }

    private void OnGatherBuddyShort(string command, string arguments)
    {
        switch (arguments.ToLowerInvariant())
        {
            case "window":
                Config.ShowGatherWindow = !Config.ShowGatherWindow;
                break;
            case "alarm":
                if (Config.AlarmsEnabled)
                    AlarmManager.Disable();
                else
                    AlarmManager.Enable();
                break;
            case "spear":
                Config.ShowSpearfishHelper = !Config.ShowSpearfishHelper;
                break;
            case "fish":
                Config.ShowFishTimer = !Config.ShowFishTimer;
                break;
            case "edit":
                if (!Config.FishTimerEdit)
                {
                    Config.ShowFishTimer = true;
                    Config.FishTimerEdit = true;
                }
                else
                {
                    Config.FishTimerEdit = false;
                }

                break;
            case "unlock":
                Config.MainWindowLockPosition = false;
                Config.MainWindowLockResize   = false;
                break;
            case "collect":
                CollectableManager.Start();
                return;
            case "collectstop":
                CollectableManager.Stop();
                return;
            default:
                var shortHelpString = new SeStringBuilder().AddText("Use ").AddColoredText(command, Config.SeColorCommands)
                    .AddText(" with one of the following arguments:\n")
                    .AddColoredText("        window", Config.SeColorArguments).AddText(" - Toggle the Gather Window on or off.\n")
                    .AddColoredText("        alarm",  Config.SeColorArguments).AddText(" - Toggle Alarms on or off.\n")
                    .AddColoredText("        spear",  Config.SeColorArguments).AddText(" - Toggle the Spearfishing Helper on or off.\n")
                    .AddColoredText("        fish",   Config.SeColorArguments).AddText(" - Toggle the Fish Timer window on or off.\n")
                    .AddColoredText("        edit",   Config.SeColorArguments).AddText(" - Toggle edit mode for the fish timer.\n")
                    .AddColoredText("        unlock", Config.SeColorArguments).AddText(" - Unlock the main window position and size.")
                    .BuiltString;
                Communicator.Print(shortHelpString);
                return;
        }

        Config.Save();
    }

    private void OnVulcan(string command, string arguments)
    {
        var trimmed = arguments.Trim();
        if (trimmed.Length == 0)
        {
            _vulcanWindow?.Toggle();
            return;
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts[0].Equals("craft", StringComparison.OrdinalIgnoreCase))
        {
            OnVulcanCraft(parts);
            return;
        }

        _vulcanWindow?.OpenToList(trimmed);
    }

    private void OnVendor(string command, string arguments)
        => _vulcanWindow?.OpenToVendors();

    private void OnVulcanMarketboard(string command, string arguments)
        => _vulcanWindow?.OpenToMarketboard();

    private void OnCollectablesWindow(string command, string arguments)
        => CollectablesWindow?.Open();

    private void OnVulcanCraft(string[] parts)
    {
        if (parts.Length < 2)
        {
            Communicator.Print("/vulcan craft <recipeId | itemName> [quantity]");
            return;
        }

        var rest = parts[1..];
        int quantity = 1;
        string recipeArg;

        if (rest.Length >= 2 && int.TryParse(rest[^1], out var qty) && qty > 0)
        {
            quantity = qty;
            recipeArg = string.Join(" ", rest[..^1]);
        }
        else
        {
            recipeArg = string.Join(" ", rest);
        }

        Lumina.Excel.Sheets.Recipe? recipe = null;
        if (uint.TryParse(recipeArg, out var recipeId))
        {
            recipe = RecipeManager.GetRecipe(recipeId);
            if (recipe == null)
            {
                Communicator.Print($"No recipe found with ID {recipeId}.");
                return;
            }
        }
        else
        {
            var matches = RecipeManager.FindByItemName(recipeArg);
            if (matches.Count == 0)
            {
                Communicator.Print($"No recipe found matching '{recipeArg}'.");
                return;
            }

            if (matches.Count > 1)
            {
                Communicator.Print($"Multiple recipes match '{recipeArg}'. Use the recipe ID:");
                var classJobSheet = Dalamud.GameData.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
                foreach (var m in matches)
                {
                    var jobAbbr = classJobSheet?.GetRow(m.CraftType.RowId + 8).Abbreviation.ExtractText() ?? "??";
                    Communicator.Print($"  {m.ItemResult.Value.Name.ExtractText()} [{jobAbbr}] - ID: {m.RowId}");
                }
                return;
            }

            recipe = matches[0];
        }

        var itemName = recipe.Value.ItemResult.Value.Name.ExtractText();
        var tempList = new CraftingListDefinition
        {
            ID   = -1,
            Name = $"Command: {itemName} x{quantity}",
        };
        tempList.Recipes.Add(new CraftingListItem(recipe.Value.RowId, quantity));

        GatherBuddy.Log.Information($"[Commands] /vulcan craft: {itemName} x{quantity} (recipe {recipe.Value.RowId})");
        Communicator.Print($"Starting: {itemName} x{quantity}");
        _vulcanWindow?.StartCraftingList(tempList);
    }

    private static void OnGatherDebug(string command, string arguments)
    {
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var subcommand = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
        
        Communicator.Print($"[Debug] Subcommand: '{subcommand}', Args: '{arguments}'");
        
        switch (subcommand)
        {
            case "findrecipe":
                if (parts.Length < 2)
                {
                    Communicator.Print("Usage: /gatherdebug findrecipe <itemName>\n" +
                        "Example: /gatherdebug findrecipe Rarefied Sykon");
                    return;
                }
                
                var searchName = string.Join(" ", parts.Skip(1));
                var recipeSheet = Dalamud.GameData.GetExcelSheet<Lumina.Excel.Sheets.Recipe>();
                if (recipeSheet == null)
                {
                    Communicator.Print("Failed to load recipe sheet");
                    return;
                }
                
                var matches = recipeSheet
                    .Where(r => r.ItemResult.Value.Name.ExtractText()
                        .Contains(searchName, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .ToList();
                
                if (matches.Count == 0)
                {
                    Communicator.Print($"No recipes found matching '{searchName}'");
                }
                else
                {
                    Communicator.Print($"Found {matches.Count} recipe(s) matching '{searchName}':");
                    foreach (var recipe in matches)
                    {
                        var itemName = recipe.ItemResult.Value.Name.ExtractText();
                        Communicator.Print($"  {itemName} - Recipe ID: {recipe.RowId}");
                    }
                }
                return;
                
            case "recipenote":
                if (parts.Length < 3)
                {
                    Communicator.Print("Usage: /gatherdebug recipenote <ingredientIndex> <clickCount> [hq] [open|recipeId]\n" +
                        "Example: /gatherdebug recipenote 0 5 hq       (click ingredient 0, 5 times HQ)\n" +
                        "Example: /gatherdebug recipenote 0 5 hq open  (open Recipe Note, then click)\n" +
                        "Example: /gatherdebug recipenote 0 5 hq 30503 (open recipe 30503, then click)");
                    return;
                }
                
                if (!uint.TryParse(parts[1], out var index))
                {
                    Communicator.Print($"Invalid ingredient index: {parts[1]}");
                    return;
                }
                
                if (!int.TryParse(parts[2], out var count))
                {
                    Communicator.Print($"Invalid click count: {parts[2]}");
                    return;
                }
                
                var isHQ = parts.Length > 3 && parts[3].Equals("hq", StringComparison.OrdinalIgnoreCase);
                var autoOpen = parts.Any(p => p.Equals("open", StringComparison.OrdinalIgnoreCase));
                
                uint recipeId = 0;
                // Check if last arg is a recipe ID (number > 1000)
                if (parts.Length > 3)
                {
                    var lastArg = parts[^1];
                    if (uint.TryParse(lastArg, out var id) && id > 1000)
                    {
                        recipeId = id;
                        autoOpen = true; // Implies open
                    }
                }
                
                Crafting.CraftingGameInterop.DebugClickRecipeNote(index, count, isHQ, autoOpen, recipeId);
                return;
                
            case "wary":
                Dalamud.ToastGui.ShowQuest("The fish have become wary of your presence. It might be time to shift your position...");
                Communicator.Print("Debug: Triggered 'wary' quest toast [EN] (ID 5517)");
                break;
            case "amiss":
                Dalamud.ToastGui.ShowQuest("The fish sense something amiss. Perhaps it is time to try another location.");
                Communicator.Print("Debug: Triggered 'amiss' quest toast [EN] (ID 3516)");
                break;
            case "wary-de":
                Dalamud.ToastGui.ShowQuest("Die Fische in der Umgebung sind auf dich aufmerksam geworden. Besser, du wechselst den Ort ...");
                Communicator.Print("Debug: Triggered 'wary' quest toast [DE] (ID 5517)");
                break;
            case "amiss-de":
                Dalamud.ToastGui.ShowQuest("Die Fische sind misstrauisch und kommen keinen Ilm näher. Versuch es lieber an einer anderen Stelle.");
                Communicator.Print("Debug: Triggered 'amiss' quest toast [DE] (ID 3516)");
                break;
            case "wary-fr":
                Dalamud.ToastGui.ShowQuest("Les poissons des environs commencent à se méfier de vous. Il est temps d'aller voir ailleurs...");
                Communicator.Print("Debug: Triggered 'wary' quest toast [FR] (ID 5517)");
                break;
            case "amiss-fr":
                Dalamud.ToastGui.ShowQuest("Les poissons sont devenus méfiants. Vous devriez aller pêcher dans un autre endroit.");
                Communicator.Print("Debug: Triggered 'amiss' quest toast [FR] (ID 3516)");
                break;
            case "wary-jp":
                Dalamud.ToastGui.ShowQuest("周辺の魚が警戒し始めている。そろそろ移動した方が良さそうだ……");
                Communicator.Print("Debug: Triggered 'wary' quest toast [JP] (ID 5517)");
                break;
            case "amiss-jp":
                Dalamud.ToastGui.ShowQuest("魚たちに警戒されてしまったようだ……。少し場所を変えたほうがいいだろう。");
                Communicator.Print("Debug: Triggered 'amiss' quest toast [JP] (ID 3516)");
                break;
            case "repair":
                Communicator.Print("[Debug] Forcing repair mode for testing...");
                Crafting.CraftingGatherBridge.TestRepairSystem();
                break;
            case "repairstop":
                Communicator.Print("[Debug] Stopping repair test...");
                Crafting.CraftingGatherBridge.StopQueue();
                Crafting.CraftingTasks.StopNavigation();
                break;
            case "repairnpcs":
                var repairNPCs = Crafting.RepairNPCHelper.RepairNPCs;
                if (repairNPCs.Count == 0)
                {
                    Communicator.Print("[Debug] No repair NPCs found. Run /gatherdebug repairpopulate first.");
                }
                else
                {
                    Communicator.Print($"[Debug] Found {repairNPCs.Count} repair NPCs:");
                    foreach (var npc in repairNPCs.Take(10))
                    {
                        var territorySheet = Dalamud.GameData.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
                        var territory = territorySheet?.GetRow(npc.TerritoryType);
                        var placeName = territory?.PlaceName.ValueNullable?.Name.ExtractText() ?? "Unknown";
                        Communicator.Print($"  {npc.Name} - {placeName} ({npc.TerritoryType})");
                    }
                    if (repairNPCs.Count > 10)
                        Communicator.Print($"  ... and {repairNPCs.Count - 10} more");
                }
                break;
            case "repairpopulate":
                Communicator.Print("[Debug] Populating repair NPCs (this may take a moment)...");
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        Crafting.RepairNPCHelper.PopulateRepairNPCs();
                        Communicator.Print($"[Debug] Populated {Crafting.RepairNPCHelper.RepairNPCs.Count} repair NPCs");
                    }
                    catch (Exception ex)
                    {
                        Communicator.Print($"[Debug] Error: {ex.Message}");
                    }
                });
                break;
            default:
                DebugMode = !DebugMode;
                break;
        }
    }
}
