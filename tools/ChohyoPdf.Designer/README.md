# ChohyoPdf Designer（開発者向け）

ChohyoPdf Designer のソースコードを改造する人向けの説明です。使い方は [ChohyoPdf Designer 説明書](../../docs/designer.md) を見てください。

## ソースコードから起動する

- Windows 10（1809 以降）または Windows 11
- .NET 10 SDK

```
dotnet run --project tools/ChohyoPdf.Designer
```

デザインファイル（`.chohyo`）のパスを引数に渡すと、そのファイルを開いて起動します（`dotnet run --project tools/ChohyoPdf.Designer -- 請求書.chohyo`）。

## プロジェクトの構成

| プロジェクト | 内容 |
| --- | --- |
| `tools/ChohyoPdf.Designer.Core` | デザインのモデル、保存形式、PDF 出力（`ReportRenderer`）、C# コードの生成（`CSharpGenerator`）、C# の式の計算（`ScriptEvaluator`） |
| `tools/ChohyoPdf.Designer` | WPF の画面 |
| `tests/ChohyoPdf.Designer.Tests` | テスト（生成したコードを Roslyn でコンパイル・実行し、PDF 出力と比べるテストを含む） |
| `docs/tools/DesignerCapture` | 説明書の画面写真と動画を撮るツール（下の「説明書の画像と動画」を参照） |

`ReportRenderer` と `CSharpGenerator` は、同じ順序・同じ計算で描くように作っています。描き方を変えるときは両方を変え、`dotnet test tests/ChohyoPdf.Designer.Tests` で一致を確かめてください。

## 配布する ZIP ファイル

配布するのは、.NET を含めた自己完結の版（win-x64）を ZIP にしたものです。インストーラーは作りません。

```
dotnet publish tools/ChohyoPdf.Designer -c Release -r win-x64 --self-contained -o artifacts/ChohyoPdfDesigner
```

- `artifacts/ChohyoPdfDesigner` の中身を ZIP にすると、配布するファイル（約 70 MB）になります。
- 単一の実行ファイル（`PublishSingleFile`）にはしません。C# の式の計算（`ScriptEvaluator`）は、Roslyn に渡す参照を、実行時に読み込んだ DLL のファイルから作るためです。単一の実行ファイルでは DLL がファイルとして存在しないので、C# の式が計算できなくなります。
- Roslyn などのリソースは、日本語の分だけを含めています（csproj の `SatelliteResourceLanguages`）。
- GitHub Actions（`.github/workflows/designer-release.yml`）が、`designer-v` で始まるタグを push したときに、この ZIP を作って GitHub の Releases に公開します。手順は [RELEASING.md](../../RELEASING.md) にあります。

## 説明書の画像と動画

[説明書](../../docs/designer.md) の画面写真と動画（`docs/images/designer`）は、`docs/tools/DesignerCapture` で撮っています。デザイナーを起動し、UI Automation で部品を探して、実際のマウスとキーボードで操作しながら、ffmpeg で画面を録画します。

```
dotnet publish tools/ChohyoPdf.Designer -c Release -r win-x64 --self-contained -o artifacts/ChohyoPdfDesigner
dotnet run --project docs/tools/DesignerCapture -- artifacts/ChohyoPdfDesigner/ChohyoPdfDesigner.exe docs/images/designer
```

- 撮影中（数分間）はマウスとキーボードが動くので、PC に触らないでください。通知などが写ったら撮り直します。
- 名前を付けると、その場面だけを撮ります（例: `... docs/images/designer tutorial place-text`）。場面の一覧は `docs/tools/DesignerCapture/Scenes.List.cs` にあります。
- 画面の拡大率が 100% で、1440 × 900 のウィンドウが収まる画面で撮ってください。ffmpeg（libx264 と libwebp を含むもの）が必要です。
- 短い動画はアニメーション WebP（12 fps）、チュートリアルのように長いものは MP4（H.264）にしています。MP4 には、説明書に載せるポスター画像（PNG）も作ります。
- 画面の部品の名前を調べるときは、`... <出力フォルダ> dump [請求書] [click:部品の名前]` で、部品の名前と位置の一覧を出せます。
