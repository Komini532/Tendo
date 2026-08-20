# Tendo — Extend Adventure

Discord ゲーム Bot「Extend Adventure」の C# (.NET 8) 移植。
移植元は 6〜7 年前に手書きされた JavaScript 実装 (discord.js v11 / SQLite / glitch.com ホスト)。
参照用の原典は [`legacy/`](legacy/) に置いてある。

見た目の動作は変えない方針で移植している。
「元々可能だった入力ができる」「元々得られた出力が得られる」ことが基準で、
入力方法そのもの (リアクション → ボタン interaction など) は現行 API に合わせて更新する。

移植が済んだあとのバランス調整だけは、この方針から意図的に外れている。
数値の設計は [バランス設計](#バランス設計) を参照。

## 構成

| プロジェクト | 役割 |
|---|---|
| `src/Tendo.Bot` | Discord 層。ホスト、イベント購読、スラッシュコマンド、embed 組み立て |
| `src/Tendo.Game` | ゲームロジック。Discord にも DB にも依存しないので単体テストできる |
| `src/Tendo.Data` | MySQL 9.7 永続化 (旧 `db.js` / SQLite の置き換え) |
| `tests/` | 単体テスト。`Tendo.Game.Tests` は差分テストを含む |
| `legacy/` | 移植元 JavaScript (参照専用・ビルド対象外。バランスの原典ではない) |
| `data/` | マスターデータ JSON。**手で編集する正のデータ** |

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

移植で一番壊れやすいのが戦闘計算なので、差分テストで固定している。

`tools/reference-battle.js` と `tools/reference-reward.js` が参照実装で、
`ea.js` から式と処理順をそのまま写してある (行番号を併記)。
**参照実装が読むのは `legacy/` ではなく `data/*.json`** なので、
バランス調整で数値を動かしてもこのテストは意味を保つ。
固定するのは「旧 JavaScript と同じ数値」から
「C# エンジンと JS 参照実装が同じ数値 (乱数の消費順を含む)」へ移った。
式そのものを変えたときは参照実装も同じ形に直し、固定ケースを作り直すこと。
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

ただし**招待時の OAuth2 スコープに `applications.commands` が必要**。
これが無いとサーバーにスラッシュコマンドが一切表示されない。

```
https://discord.com/api/oauth2/authorize
  ?client_id=＜アプリの client_id＞
  &permissions=8192
  &scope=bot%20applications.commands
```

移植元の `config.json` にあった招待 URL は `scope=bot` のみだった
(接頭辞コマンド時代のものなので当然だが、そのまま使うと動かない)。
**`applications.commands` が無かった頃から入っている古い Bot を再利用する場合は、
上の URL で入れ直してスコープを付与すること。**

開発中は `TENDO_Discord__TestGuildId` を設定するとギルド限定登録になり、
反映が即時になるので確認が早い。

### Docker

```bash
docker build -t tendo .
docker run --rm \
  -e TENDO_Discord__Token="＜Bot トークン＞" \
  -e TENDO_Discord__OwnerId="＜管理者のユーザー ID＞" \
  -e TENDO_Database__ConnectionString="＜接続文字列＞" \
  tendo
```

## コマンド

旧実装の 23 コマンドをすべて slash command として移植してある。

| 分類 | コマンド |
|---|---|
| 戦闘 | `/attack` `/skill` `/wait` `/reset` `/fix` |
| 情報 | `/status` `/cstatus` `/inventory` `/skills` `/mlist` `/ranking` `/help` `/info` `/tips` `/ping` |
| やりとり | `/use` `/give` `/shop` |
| 移動 | `/go` `/dchange` |
| ペット | `/pstatus` `/rename` `/release` |
| 管理 | `/mod`（`Discord:OwnerId` の 1 人だけ） |

`/mod` のサブコマンドは
`addeff` `remeff` `field` `sum` `gil` `exp` `isk` `update` `clist` `plist`
`ban` `unban` `banlist`。

旧 `-eval` は任意の JavaScript を実行するもので安全な代替が無いため移植していない。
旧 `-macro` はマクロ検知に紐づくもので、検知ごと移植していない
(旧実装でも `ea.js:2144` のカンマ演算子により一度も動いていなかった)。
`unban` は旧実装に無いが、BAN を解除する手段が無いと運用できないため追加した。

## バランス設計

旧実装のインフレは非対称だった。プレイヤー側は「強いスキルの習得」で伸び、
敵側は「個体ステータスの倍率」で伸びる。共通の物差しが無いので手動調整しかできず、
実測すると **フィールドは実質すべて等価** (敵HPはフィールドに依存せず、
レア度 0 の個体倍率はどのフィールドでもほぼ同じ) で、
`fields.json` の経験値倍率 0.95〜1.32 だけが差だった。

いまはフィールドを階層の軸に据えている。`data/fields.json` が 1 フィールドあたり 4 つ持つ。

| キー | 意味 |
|---|---|
| `cap` | このフィールドで通用する敵Lvの上限 |
| `hp` | 敵の最大HP倍率 |
| `atk` | 敵の与ダメージ倍率 |
| `exp` | 獲得経験値倍率 |

最終的な敵の最大HPは `(有効敵Lv × 個体hp × 10 + fix.enemy) × フィールドhp × 難易度hp`。
**有効敵Lv = `min(battle.Level, cap)`** で、上限に達したチャンネルでは
`battle.Level` 自体も伸びなくなる。

### なぜ上限が要るのか

経験値/ターンを式で書くと敵Lvが約分されて消え、

```
経験値/ターン ∝ フィールドexp / フィールドhp
```

になる。つまり **HP倍率だけ上げても上位フィールドは「遅いだけ」** で、
経験値倍率で埋め合わせようとすると倍率が数千まで発散する
(経験値→自Lv→火力→もっとHPが要る、という正のフィードバックが掛かるため)。

1 撃破は最短 1 ターンなので、敵Lv上限は

```
経験値/ターン の天井 = cap × フィールドexp
```

という硬い上限を作る。下位フィールドは構造的に頭打ちになり、
上へ行く以外に進みようが無くなる。同時に 自Lv/有効敵Lv がほぼ 1 に固定されるので、
フィールド倍率が 0.7〜4.8 という扱える範囲に収まる。

### 狙っている値

- 適正スキル 2 回で個体HP倍率 1.0 の敵を倒せる (参入直後がいちばん厳しく、上限に近づくと楽になる)
- 1 体倒すあいだに失う体力は最大体力の 12%〜56%、ティア順に単調増加
- 経験値/ターンの天井は 160 (草原) 〜 15000 (永遠悪夢) で単調増加
- スキル威力は習得レベル順に単調非減少 (旧データでは lv400 常闇 112 と lv1000 シグルイ 444 が突出し、
  lv500〜650 と lv1100〜1600 の技が習得時点で下位互換になっていた)
- 個体倍率はレア度ごとの狭い帯に収める。スケールはフィールド側が持つ

### 検算のしかた

手で確かめる必要はない。`Tendo.Game.Tests` の `BalanceInvariantTests` が
実際のエンジンを走らせて測り、上の条件を全部確かめる。

```bash
dotnet test tests/Tendo.Game.Tests --filter バランス表 --logger "console;verbosity=detailed"
```

でフィールド別の一覧が出る (テストが落ちたときは失敗メッセージにも同じ表が付く)。

## 移植で直した旧実装の不具合

忠実に移植すると壊れたままになるため、以下は直してある。

| 箇所 | 旧挙動 | 移植後 |
|---|---|---|
| `ea.js:1306` | `separate()` が未定義でショップ購入が必ず例外 | 画面の案内どおり買える |
| `ea.js:1820` | 未宣言の変数へ代入し、敵の状態異常が `/cstatus` に出ない | `/status` と同じ形で表示 |
| `ea.js:367` | 参加者がサーバーを抜けていると報酬処理ごと例外 | 名前が引けなければ ID を出して続行 |
| `ea.js:266` | 順位が「登録順で先頭 100 行」の中でのみ算出される | 全件での順位 (100 人以下なら一致) |
| `ctrl.nearest` | snowflake の精度落ちを近い値で誤魔化す | `BIGINT UNSIGNED` で精度が落ちないため不要 |
| `effect.js:375` | 「被ダメージ上昇」だけレベル配列のキーが `effect` (単数) で、効果が丸ごと無効 | `effects` に揃えて発動するように (メルトンの主効果も生き返る) |
| `shop.js` | 重複除去の条件を誤り「湖」「秘境」「永遠悪夢」でポーション類が買えない | フィールドごとに 1 件へ統合 |

一方、バグに見えても**出力が変わるものは旧挙動のまま**にしている
(`/inventory` が 4 種しか表示しない、アイテム `r` が効果を持たない、
ペットアビリティの `eff`/`appear` が未実装、レア度 4 の敵が既定色で出る、など)。

## 移植の進め方

規模が大きいためフェーズに分けて進めた。

- [x] **Phase 1 — 基盤**: ソリューション構成、ホスト、Discord.Net 配線、`/ping` `/info`
- [x] **Phase 2 — マスターデータ**: 敵 77 / 技 90 / 状態異常 37 などを JSON 化して読み込む
- [x] **Phase 3 — 永続化**: MySQL スキーマとリポジトリ
- [x] **Phase 4 — 戦闘エンジン**: `ea.js` の戦闘処理 (最大の山)
- [x] **Phase 5 — コマンド群**: 残りのコマンドとページネーション等の UI 部品
- [x] **Phase 6 — 管理コマンドと仕上げ**: `/mod`、README、CI

## ライセンス

[MIT](LICENSE)
