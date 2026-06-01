# GatherBuddy JP

GatherBuddy JP is a Japanese-first fork/custom build of GatherBuddy for FFXIV Dalamud.

This build focuses on Japanese UI text and convenience adjustments for gathering navigation, auto-gather lists, teleport-assisted movement, and vnavmesh-assisted travel.

## Features

- Japanese-oriented gather item search and UI labels
- Auto-gather list handling
- Teleport-assisted route movement
- vnavmesh-assisted movement
- Gather window and auto-gather UI adjustments
- Optional behavior to prioritize only the currently selected auto-gather list

## Required Plugins

- vnavmesh

Some movement features require vnavmesh to be installed and working.

## Install

Add this URL to Dalamud custom plugin repositories:

```text
https://raw.githubusercontent.com/mitaka0715-bot/GatherBuddyJP/main/pluginmaster.json
```

Then install `GatherBuddy JP` from the Dalamud Plugin Installer.

## Usage Notes

### Auto-gather lists

If you stop or interrupt auto-gathering before the requested quantity is completed, the unfinished auto-gather list may remain enabled.

When an unfinished list remains enabled, GatherBuddy JP may continue gathering items from that old list the next time auto-gathering is started.

Before starting a new gathering target, delete or disable the previous unfinished list if you do not want it to continue.

### 日本語メモ

設定した個数を取り切る前に中断した場合、前回の自動採取リストが残ったままになることがあります。

その状態で次の採取を始めると、前回残っているリストのアイテムを先に採取しに行く場合があります。

新しいアイテムを採取し直す場合は、前回の未完了リストを削除するか、無効化してから開始してください。

## Disclaimer

This project is an unofficial personal/custom plugin build.

It is not affiliated with, endorsed by, or supported by Square Enix, XIVLauncher, Dalamud, the original GatherBuddy project, or any related plugin authors.

Use this plugin at your own risk. Third-party tools, automation, custom plugins, and modified plugin builds may violate game rules, service terms, or community/server policies depending on how they are used. You are responsible for understanding and accepting those risks before installing or using this plugin.

No warranty is provided. The author is not responsible for account actions, game crashes, data loss, plugin conflicts, broken behavior after game/plugin updates, or any other damage caused directly or indirectly by this plugin.

## Distribution Files

- `pluginmaster.json`: Dalamud custom repository metadata
- `latest.zip`: Plugin package downloaded by Dalamud
- `GatherBuddyJP.json`: Plugin manifest included in the package

## Author

General Headquarters
