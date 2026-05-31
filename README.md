# GatherBuddy JP

GatherBuddy JP は、GatherBuddy Reborn JP fork を元にした採掘・園芸向けの Dalamud プラグインです。

## 概要

- 採掘師 / 園芸師向けの採集アイテム検索
- 自動採集リスト
- vnavmesh を使ったテレポ後の移動
- 採集ウィンドウの操作支援
- 天候、アラーム、採集グループ、採集ウィンドウ機能

この版では、漁師向けの画面やウィンドウを通常UIから外しています。

## 必須プラグイン

- vnavmesh

自動移動を使う場合は vnavmesh が必要です。

## Dalamud への追加方法

Dalamud の設定画面を開きます。

1. `/xlsettings`
2. `Experimental`
3. `Custom Plugin Repositories`
4. 下のURLを追加

```text
https://raw.githubusercontent.com/mitaka0715-bot/GatherBuddyJP/main/repo.json
```

追加後、Plugin Installer の一覧から `GatherBuddy JP` をインストールできます。

## 配布ファイル

- `repo.json`: Dalamud カスタムリポジトリ用JSON
- `latest.zip`: Dalamud がインストール時に取得するプラグインZIP
- `GatherBuddyJP.json`: プラグインmanifest

## Author

General Headquarters
