# 開発者向けの説明

ChohyoPdf のソースコードを改造する人向けに、ビルド・テスト・ドキュメントの作り直し方をまとめています。ライブラリやデザイナーを使うだけなら、[ChohyoPdf のサイト](https://ringoacid.github.io/ChohyoPdf/) を見てください。

## フォルダの構成

| フォルダ | 内容 |
| --- | --- |
| `src/ChohyoPdf` | ライブラリ本体（NuGet パッケージになるプロジェクト） |
| `tests/ChohyoPdf.Tests` | ライブラリのテスト |
| `tools/ChohyoPdf.Designer` | GUI デザイナー（WPF）。説明は [tools/ChohyoPdf.Designer/README.md](tools/ChohyoPdf.Designer/README.md) |
| `tools/ChohyoPdf.Designer.Core` | デザイナーのモデル・PDF 出力・C# コードの生成 |
| `tests/ChohyoPdf.Designer.Tests` | デザイナーのテスト |
| `samples/ChohyoPdf.Samples` | 請求書・レポート・受領書のサンプル |
| `docs` | 説明書（`manual.md`・`designer.md`）と、その生成に使うサンプル・スクリプト。`docs/api` は API リファレンスの設定 |

## ビルドとテスト

```
dotnet build ChohyoPdf.sln
dotnet test tests/ChohyoPdf.Tests
dotnet test tests/ChohyoPdf.Tests -f net8.0 -p:LibraryTargetFramework=netstandard2.0   # netstandard2.0 版のテスト
dotnet test tests/ChohyoPdf.Designer.Tests          # デザイナーのテスト
```

- ソリューションには WPF のデザイナーが含まれるため、`ChohyoPdf.sln` 全体のビルドは Windows で行ってください。ライブラリだけなら、`dotnet build src/ChohyoPdf` でほかの OS でもビルドできます。
- テストは net8.0 / net10.0 / net48 で実行します（net48 では net462 版のライブラリを使います）。netstandard2.0 版は、上のように `LibraryTargetFramework` を指定して net8.0 で実行します。
- 日本語フォントを使うテストのうち、フォントが見つからない環境では検証を省略するものがあります。
- GitHub Actions（`.github/workflows/ci.yml`）でも、push とプルリクエストのたびに同じビルドとテストを実行します。

## ソースコードから使う

NuGet パッケージを使わずに、ほかのプロジェクトからソースコードを直接参照するときは、`src/ChohyoPdf/ChohyoPdf.csproj` をプロジェクト参照に追加します。

```xml
<ItemGroup>
  <ProjectReference Include="..\ChohyoPdf\src\ChohyoPdf\ChohyoPdf.csproj" />
</ItemGroup>
```

手元で NuGet パッケージ（.nupkg）を作って参照するときは、次のようにします。

```
dotnet pack src/ChohyoPdf -c Release -o artifacts
dotnet add package ChohyoPdf --source ./artifacts
```

## サンプルとデザイナーの実行

```
dotnet run --project samples/ChohyoPdf.Samples -f net8.0 -- 出力先フォルダ
dotnet run --project tools/ChohyoPdf.Designer
```

## ドキュメントとサイトの作り直し方

説明書（`docs/manual.md`）、`docs/images` のサンプルの画像、GitHub Pages で公開するサイト（`_site`）は、スクリプトで生成しています。`docs/manual.md` は直接編集しないでください。

| 直したいもの | 編集するファイル |
| --- | --- |
| 説明書の文章 | `docs/manual.template.md` |
| 説明書のサンプルコード | `docs/examples/ChohyoPdf.DocExamples` の `.cs` |
| デザイナーの説明書 | `docs/designer.md`（直接編集します）。画面写真と動画は [デザイナーの README](tools/ChohyoPdf.Designer/README.md#説明書の画像と動画) |
| API リファレンス | `src/ChohyoPdf` のドキュメントコメント（`///`）。設定は `docs/api/docfx.json` |
| サイトの見た目（CSS・JavaScript） | `docs/tools/site/page.html`（全ページ共通）、`docs/tools/site/home.html`（トップページの本文） |

編集したら、次のコマンドを実行します。

```
pip install pillow markdown-it-py pygments
python docs/tools/build_docs.py
```

- 実行には、.NET SDK、Python 3 と上の 3 つのパッケージ、`pdftoppm` と `pdfinfo`（Poppler または xpdf）が必要です。API リファレンスを作る DocFX は、`.config/dotnet-tools.json` のローカルツールで、スクリプトが `dotnet tool restore` で入れます。
- スクリプトは、サンプルを実行して PDF を出力し、`pdftoppm` で画像にしてから、テンプレートの `<!-- example: 名前 -->` をサンプルのコードに置き換えます。サンプルのコードのうち、`// #example 名前` から `// #end` までが説明書に載ります。
- 続けて `_site` にサイトを作ります。トップページ（`index.html`）、説明書（`manual.html`）、デザイナーの説明書（`designer.html`）、API リファレンス（`api/`）です。`python -m http.server -d _site` で配信すると、ブラウザーで確かめられます。
- 文章だけを直したときは `--skip-run` で、サンプルの実行と画像の生成を省けます。`--skip-api` では API リファレンスを作りません。
- リンク先の見出し・画像・動画がないといった誤りがあると、スクリプトはエラーで止まります。
- 公開するクラスとメンバーには、ドキュメントコメントを書いてください。ないとビルドで警告（CS1591）が出ます。

README の「コードの例」は、`docs/examples/ChohyoPdf.DocExamples/Examples.QuickStart.cs` と同じコードで、画像（`docs/images/quick-start.png`）はそのコードで出力したものです。README は NuGet のパッケージのページにも表示されるので、画像やリンクは `https://` から始まる URL で書いています。コードを変えるときは、サンプルと README の両方を直してください（違っていると、スクリプトがエラーで知らせます）。

### サイトの公開

`main` に push すると、GitHub Actions（`.github/workflows/pages.yml`）が `python docs/tools/build_docs.py --skip-run` でサイトを作り、GitHub Pages（https://ringoacid.github.io/ChohyoPdf/）に公開します。画像は作り直さないので、サンプルを変えたときは、手元でスクリプトを実行して `docs/images` の画像もコミットしてください。

## リリース

NuGet への公開と、デザイナーの ZIP の公開の手順は [RELEASING.md](RELEASING.md) にあります。
