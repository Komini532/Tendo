# Tendo — Extend Adventure

Discord ゲーム Bot「Extend Adventure」の C# (.NET 8) 移植。
移植元は 6〜7 年前に手書きされた JavaScript 実装 (discord.js v11 / SQLite / glitch.com ホスト)。
参照用の原典は [`legacy/`](legacy/) に置いてある。

見た目の動作は変えない方針で移植している。
「元々可能だった入力ができる」「元々得られた出力が得られる」ことが基準で、
入力方法そのもの (リアクション → ボタン interaction など) は現行 API に合わせて更新する。

## 構成

| プロジェクト | 役割 |
|---|---|
| `src/Tendo.Bot` | Discord 層。ホスト、イベント購読、スラッシュコマンド、embed 組み立て |
| `src/Tendo.Game` | ゲームロジック。Discord にも DB にも依存しないので単体テストできる |
| `src/Tendo.Data` | MySQL 9.7 永続化 (旧 `db.js` / SQLite の置き換え) |
| `tests/Tendo.Game.Tests` | `Tendo.Game` の単体テスト |
| `legacy/` | 移植元 JavaScript (参照専用・ビルド対象外) |
| `data/` | マスターデータ JSON (`tools/dump-master-data.js` が `legacy/mmo/*.js` から生成) |

## 動かす

必要なもの: .NET 8 SDK、MySQL 9.7。

### 設定

**トークンと接続文字列はリポジトリに置かない。** `appsettings.json` の該当項目は空のままにし、
環境変数か user-secrets で与える。

環境変数 (前置詞 `TENDO_`、階層は `__` で区切る):

```bash
export TENDO_Discord__Token="＜Bot トークン＞"
export TENDO_Discord__OwnerId="＜/mod を使えるユーザー ID＞"
export TENDO_Database__ConnectionString="Server=localhost;Port=3306;Database=tendo;User ID=tendo;Password=＜略＞;CharSet=utf8mb4"

# 開発中はギルド限定登録にするとスラッシュコマンドが即反映される
export TENDO_Discord__TestGuildId="＜テストサーバーの ID＞"
```

user-secrets を使う場合:

```bash
dotnet user-secrets --project src/Tendo.Bot set "Discord:Token" "＜Bot トークン＞"
```

### データベース

ローカル開発用に MySQL 9.7 の `docker-compose.yml` を同梱している。

```bash
docker compose up -d
```

スキーマは起動時に自動で適用される (`src/Tendo.Data/Migrations/*.sql`)。
適用済みは `schema_migrations` に記録されるので、二重に流れることはない。

### ビルドと実行

```bash
dotnet build
dotnet test
dotnet run --project src/Tendo.Bot
```

設定が足りない場合はスタックトレースではなく、
どの項目をどこに設定すればよいかだけを出して終了する。

### 永続化層のテスト

`Tendo.Data.Tests` の往復テストは実際の MySQL を使う。
接続先が与えられたときだけ実行し、無ければ skip する
(無言で成功したことにはしない)。

```bash
docker compose up -d
export TENDO_TEST_MYSQL="Server=127.0.0.1;Port=3306;Database=tendo;User ID=tendo;Password=tendo;CharSet=utf8mb4"
dotnet test
```

テストは実データを書き込むので、本番の接続先を指定しないこと。

### 戦闘計算の差分テスト

移植で一番壊れやすいのが戦闘計算なので、旧 JavaScript との差分テストで固定している。

`tools/reference-battle.js` と `tools/reference-reward.js` が参照実装で、
`ea.js` から式と処理順をそのまま写してある (行番号を併記)。
`Math.random` を mulberry32 に差し替え、C# 側も同じ実装・同じ種を使うので、
**乱数が引かれる順序まで一致していないと結果がずれて検出される**。

固定ケースは `tests/Tendo.Game.Tests/Fixtures/` にあり、再生成はこの 2 つ。

```bash
node tools/reference-battle.js 1000 400 > tests/Tendo.Game.Tests/Fixtures/battle-reference.json
node tools/reference-reward.js 2000 200 > tests/Tendo.Game.Tests/Fixtures/reward-reference.json
```

比較対象は最終的な体力・魔力・所持金・状態異常・フィールドに加え、**戦闘ログの全行**。

JS と C# で意味が違って踏みやすい点は `Tendo.Game/Engine/JsMath.cs` に閉じ込めてある。

| JS | C# の落とし穴 |
|---|---|
| `Math.round` | C# 既定は銀行家丸め。`Math.Round(2.5)` が 2 になる |
| `r.random(a,b)` | 一様分布ではなく両端の当選幅が半分。均せば発動率が変わる |
| `arr.choice(i)` | 添字が範囲外**または値が falsy** ならランダムに落ちる |

### Bot に必要な権限

特権インテント (Message Content / Server Members) は**使わない**。
スラッシュコマンドのみで動作するため、Developer Portal での有効化は不要。

## 移植の進め方

規模が大きいためフェーズに分けて進めている。

- [x] **Phase 1 — 基盤**: ソリューション構成、ホスト、Discord.Net 配線、`/ping` `/info`
- [x] **Phase 2 — マスターデータ**: 敵 77 / 技 90 / 状態異常 37 などを JSON 化して読み込む
- [x] **Phase 3 — 永続化**: MySQL スキーマとリポジトリ
- [x] **Phase 4 — 戦闘エンジン**: `ea.js` の戦闘処理 (最大の山)
- [ ] **Phase 5 — コマンド群**: 残りのコマンドとページネーション等の UI 部品
- [ ] **Phase 6 — 管理コマンドと仕上げ**: `/mod`、README、CI

## ライセンス

[MIT](LICENSE)
