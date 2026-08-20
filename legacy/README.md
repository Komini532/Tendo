# legacy — 移植元の JavaScript 実装 (参照専用)

`extend_adventure_old-main` をほぼそのまま置いたもの。**ビルド対象ではない。**
C# 実装の正解を確認するための参照であり、以下の 2 つの用途で実際に使う。

1. **Phase 2** — `tools/dump-master-data.js` がここの `mmo/*.js` を `require()` して
   `data/*.json` を生成する。敵 77 体などのマスターデータを手写ししないため。
2. **Phase 4** — 戦闘計算の差分テスト。同じ乱数列を与えたときに C# 実装と
   同じダメージ・同じ抽選結果になることを確認する。

## 元のアーカイブからの変更点

| 変更 | 理由 |
|---|---|
| `mmo/config.json` を削除 | 実在形式の Bot トークンが入っていたため。代わりに `mmo/config.example.json` を置いた |
| `mmo/config_alpha.json` を削除 | 同上 |
| `.glitch-assets` を削除 | 中身が空。glitch.com (サービス終了) の残骸 |

それ以外のファイルは一切変更していない。

## 移植時に把握しておくべき点

- **`fn.js` は移植元と世代が違う。** `ea.js` は `r.sto` / `r.ots` / `r.date` / `r.repeat` を
  呼ぶが、この `fn.js` (discord.js v12/13 期の書き直し版) にはどれも存在しない。
  `ea.js` 単体では動かない。契約は呼び出し側から復元してある。
- **`r.sto` / `r.ots` が eval によるデシリアライズ。** DB の TEXT 列に eval 可能な
  JS 文字列を保存していた。C# 版では正規化テーブルに置き換わり、消滅する。
- **`mmo/macro.js` (マクロ検知) は C# に移植しない。** `ea.js:2144` の
  `let MacroKenchi = (MACRO.get(d.user.id), false);` がカンマ演算子で常に `false` を返すため、
  検知処理は元から一度も動いていない。参照用にファイルは残してある。
- **`ea.js:1306` の `separate()` は未定義。** ショップ購入が例外で必ず失敗する。
  C# 版では画面表示 (`[番号] [個数] で購入　例： 1 10`) 通りに動くよう直してある。
- **`main.js` の HTTP サーバーは glitch.com のスリープ回避用。** 移植しない。
