# MultiMonitorTiler

指定したウィンドウを、複数モニターにまたがって「実際にリサイズ」して拡大表示する Windows 用 CLI ツールです。マウス/キーボード操作もそのままそのウィンドウ全体で使えます。

## 仕組みと、境界がズレる問題への対処

ウィンドウを単純に複数モニターにまたがるサイズへリサイズするだけだと、モニターごとに DPI スケーリング(100%/125%/150%など)が異なる場合、ほとんどのアプリは「そのウィンドウが今どの DPI で描画すべきか」を1つしか持てないため、モニターの境界を挟んで表示倍率が不連続になり、境界で絵がズレて見えます。

このツールは次の2段構えで対処します。

1. **本体の処理**: 対象ウィンドウの実際の可視フレーム(`DwmGetWindowAttribute` の `DWMWA_EXTENDED_FRAME_BOUNDS`。Win32 の非表示リサイズ用マージンを除いた「見た目のフレーム」)が、選択したモニター群の外接矩形にピクセル単位でぴったり一致するように `SetWindowPos` で配置・リサイズします。ここは低遅延です — アプリ自身が描画する実ウィンドウをそのまま動かすだけなので、キャプチャ・合成・追加のレンダリングは一切発生しません。
2. **DPI 不整合の解消(任意・`--fix-dpi`)**: 選択したモニター間で DPI スケールが異なる場合、対象アプリの実行ファイルに対して、エクスプローラーの「プロパティ → 互換性 → 高 DPI スケーリングの上書き → システム」と全く同じ Windows 標準の互換フラグ(`HKCU\...\AppCompatFlags\Layers` の `~ DPIUNAWARE`)を設定できます。これは **モニター側の DPI 設定には一切触れず**、対象アプリ1つだけを「DPI 非対応」として扱わせる設定です。これにより Windows がそのウィンドウ全体に対して単一の一括拡大縮小を適用するようになり、モニター境界で倍率が不連続にならず、ズレが解消されます(トレードオフとして、そのアプリの文字などはやや滲んだ描画になります)。この設定はアプリの**次回起動時**から有効になるため、`--relaunch` を付けると本ツールがアプリを閉じて再起動するところまで自動化します(未保存の作業内容は失われる可能性があるため、既定では確認を挟みます)。

## 必要環境

- Windows 10 (1703以降) / Windows 11
- .NET 8 SDK (`dotnet build` に使用。実行だけなら発行した exe と .NET 8 ランタイムのみで可)
- 対象ウィンドウが管理者権限で動いている場合、本ツールも管理者として実行してください(そうでないとプロセス情報の取得・ウィンドウ操作に失敗します)

## ビルド

```powershell
cd src\MultiMonitorTiler
dotnet build -c Release
```

成果物: `src\MultiMonitorTiler\bin\Release\net8.0-windows\MultiMonitorTiler.exe`

単一 exe として発行したい場合:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## 使い方

### 1. モニター一覧を確認

```powershell
MultiMonitorTiler.exe list-monitors
```

番号・位置・解像度・DPI スケール(%)・プライマリかどうかが表示されます。この番号を `expand` の `--monitors` に使います。

### 2. 対象ウィンドウを確認

```powershell
MultiMonitorTiler.exe list-windows
```

PID・プロセス名・ウィンドウタイトルの一覧が表示されます。

### 3. 拡大表示

```powershell
MultiMonitorTiler.exe expand --window "メモ帳" --monitors 1,2,3
```

- `--window` にはタイトルの一部を指定します(部分一致・大文字小文字無視)。
- `--monitors` は `list-monitors` の番号のカンマ区切り、または `all`。
- 選択したモニター間で DPI スケールが異なる場合は警告が出ます。境界のズレを解消したい場合:

```powershell
MultiMonitorTiler.exe expand --window "メモ帳" --monitors 1,2,3 --fix-dpi --relaunch
```

  - `--fix-dpi`: DPI 互換フラグを設定(モニター設定は変更しません)
  - `--relaunch`: フラグ反映のため対象アプリを自動で閉じて再起動(確認プロンプトあり。`--yes` でスキップ可)
  - GDI 系の古いアプリで文字を少しでも鮮明にしたい場合は `--gdi-scaling` を追加

`--fix-dpi` 実行後、フラグは次回起動から有効なため、再起動(自動 or 手動)後にもう一度 `expand` を実行してください。

### 4. 元に戻す

```powershell
MultiMonitorTiler.exe restore --window "メモ帳"
```

`expand` 実行前のウィンドウ位置・サイズ(最大化状態含む)を復元します。

### 5. DPI 互換フラグの解除

```powershell
MultiMonitorTiler.exe clear-dpi-fix --window "メモ帳"
```

## 制限事項

- 動画のフルスクリーン専有モードや DRM 保護コンテンツなど、アプリ自身が特殊なウィンドウモードで描画している場合は、リサイズ自体がアプリ側で無視されることがあります。
- `--relaunch` はコマンドラインを WMI (`Win32_Process.CommandLine`) から取得して再現しますが、アプリ内部のセッション状態(開いていたファイル・タブなど)までは復元しません。
- 対象アプリが管理者権限で実行されている場合、本ツールも管理者として実行する必要があります。

## プロジェクト構成

```
src/MultiMonitorTiler/
  Program.cs            CLI エントリポイント・コマンド処理
  Native.cs              Win32 P/Invoke 宣言
  MonitorService.cs       モニター列挙・外接矩形計算
  WindowService.cs        ウィンドウ列挙・可視フレーム計算・リサイズ
  DpiCompat.cs            DPI 互換フラグ(AppCompatFlags\Layers)の読み書き
  ProcessRelauncher.cs     対象アプリの再起動(WMI 経由でコマンドライン取得)
  WindowStateStore.cs      expand 前の位置・サイズの保存/復元
  app.manifest             本ツール自身を Per-Monitor-V2 DPI aware に設定
```
