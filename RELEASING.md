# リリース手順

ChohyoPdf を nuget.org に公開する手順です。バージョン番号のタグを GitHub に push すると、GitHub Actions（`.github/workflows/release.yml`）がテストを実行し、NuGet に公開します。

## 公開の手順

1. 公開する変更を `main` に入れ、GitHub Actions の CI が成功していることを確認します。
2. バージョン番号を決めます（下の「バージョン番号の付け方」を参照）。
3. タグを付けて push します。

   ```
   git tag -a v0.2.0 -m "ChohyoPdf 0.2.0"
   git push origin v0.2.0
   ```

4. GitHub の「Actions」タブで、Release ワークフローが成功したことを確認します。
5. nuget.org の自動チェックが終わると、数分から 1 時間ほどで https://www.nuget.org/packages/ChohyoPdf に表示されます。

パッケージのバージョンはタグから決まります（`v0.2.0` → `0.2.0`）。`ChohyoPdf.csproj` の `<Version>` を書き換える必要はありません。

## バージョン番号の付け方

`メジャー.マイナー.パッチ` の形にします。

| 変更の内容 | 例 |
| --- | --- |
| 不具合の修正だけ | 0.1.0 → 0.1.1 |
| 機能の追加（既存のコードはそのまま動く） | 0.1.1 → 0.2.0 |
| 既存のコードが動かなくなる変更 | 0.2.0 → 1.0.0（1.0.0 以降は 1.x → 2.0.0） |
| 試用版 | `v0.2.0-preview.1` のように付けると、プレリリース版として公開されます |

## 注意

- **公開したバージョンは、削除も上書きもできません。** 問題があった場合は、nuget.org のパッケージ管理画面で非表示（Unlist）にし、番号を上げて公開し直します。
- ワークフローが nuget.org への push より前（テストやパッケージ作成）で失敗した場合は、まだ何も公開されていません。原因を直してから、タグを付け直して push します。

  ```
  git tag -d v0.2.0
  git push origin :refs/tags/v0.2.0
  git tag -a v0.2.0 -m "ChohyoPdf 0.2.0"
  git push origin v0.2.0
  ```

## 公開に使っている設定

公開には nuget.org の Trusted Publishing を使っており、API キーは保存していません。

- **nuget.org の Trusted Publishing ポリシー**
  - Repository Owner：`Ringoacid`
  - Repository：`ChohyoPdf`
  - Workflow File：`release.yml`
  - Environment：空欄
  - Scopes：Push only new package versions
  - Glob Patterns：`ChohyoPdf`
- **GitHub のリポジトリのシークレット**
  - `NUGET_USER`：nuget.org のユーザー名（`Ringoacid`）

`release.yml` のファイル名を変えたら、ポリシーの Workflow File も合わせて変えてください。新しい ID のパッケージを追加で公開するときは、ポリシーの Scopes を「Push」（新しいパッケージを含む）に戻し、Glob Patterns にその ID を追加します。
