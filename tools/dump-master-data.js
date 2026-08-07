#!/usr/bin/env node
/*
 * legacy/mmo/*.js → data/*.json
 *
 * 敵 77 体・技 90 個・状態異常 37 種を手で書き写すと必ず転記ミスが出るため、
 * 元の JavaScript モジュールをそのまま require() して JSON に落とす。
 *
 *   node tools/dump-master-data.js
 *
 * 冪等。出力に差分が出た場合は legacy/ 側が変わったということなので、
 * data/*.json をコミットし直すこと。
 *
 * 各モジュールは末尾の forEach で既定値を埋めてから module.exports している
 * (例: enemy.js の `if(!e.hp) e.hp=1;`)。よってここで得られるのは
 * 「既定値適用済み」のデータであり、C# 側で既定値を再実装する必要はない。
 */

const fs = require("fs");
const path = require("path");

const LEGACY = path.join(__dirname, "..", "legacy", "mmo");
const OUT = path.join(__dirname, "..", "data");

/** enemy.js が pic に前置する定数。C# 側では設定値にしたいので剥がして保存する。 */
const ENEMY_IMAGE_BASE = "http://bgm.hisyaku.com/mmo/";

/**
 * JSON.stringify は Infinity を null にするが、その挙動に依存すると
 * 「意図した null」と区別が付かないので明示的に変換する。
 * skill.learn の Infinity は「習得不可能」を意味し、C# では int? の null になる。
 */
const replacer = (key, value) => (value === Infinity ? null : value);

function load(name) {
  // require キャッシュを効かせない (同一プロセスで再実行しても素直に読み直せるように)
  const p = path.join(LEGACY, name);
  delete require.cache[require.resolve(p)];
  return require(p);
}

function write(name, value) {
  const file = path.join(OUT, name);
  fs.writeFileSync(file, JSON.stringify(value, replacer, 2) + "\n", "utf8");
  const count = Array.isArray(value) ? value.length : Object.keys(value).length;
  console.log(`  ${name.padEnd(22)} ${String(count).padStart(3)} 件`);
}

/**
 * 旧 skill.js には外側の配列を書き忘れた定義が 2 件ある。
 *
 *   虚ろな悪夢:  self:   ["豊穣の宣告", 1, 4, 100]     ← [[...]] であるべき
 *   ノーライフ:  effect: ["即死",       1, 1, 18]      ← 同上
 *
 * 旧 ea.js は `arr.forEach(e => { if (random(1,100) <= e[3]) ... })` で読むため、
 * この形だと e が文字列や数値になり、e[3] は "宣" か undefined になる。
 * 数値との比較は必ず false なので、これらの効果は一度も発動しない
 * (10 万回試行して 0 回であることを確認済み)。
 *
 * つまり「見た目の動作」は "効果なし" である。C# 側の型を汚さないよう、
 * ここで空配列に正規化して旧挙動を保つ。
 * 効果を実際に発動させたい場合は legacy 側のデータを [[...]] に直すこと
 * (ただしそれはゲームバランスの変更になる)。
 */
function normalizeSkills(skills) {
  return skills.map((s) => {
    const fixField = (field) => {
      const v = s[field];
      if (Array.isArray(v) && v.length && !Array.isArray(v[0])) {
        console.warn(
          `  [注意] 技「${s.name}」の ${field} が入れ子になっていないため空にしました ` +
            `(旧実装でも発動しません): ${JSON.stringify(v)}`
        );
        return [];
      }
      return v;
    };

    return {
      ...s,
      effect: fixField("effect"),
      self: fixField("self"),
      pair: fixField("pair"),
    };
  });
}

/**
 * enemy.js は読み込み時に `e.pic = base + e.pic` を実行する。
 * pic 未指定の敵は "…/undefined" になってしまうので、そこは null に落とす。
 */
function normalizeEnemies(enemies) {
  return enemies.map((e) => {
    let pic = e.pic;
    if (typeof pic === "string" && pic.startsWith(ENEMY_IMAGE_BASE)) {
      pic = pic.slice(ENEMY_IMAGE_BASE.length);
    }
    if (!pic || pic === "undefined" || pic === "null") {
      pic = null;
    }
    return { ...e, pic };
  });
}

fs.mkdirSync(OUT, { recursive: true });
console.log("legacy/mmo/*.js → data/*.json");

write("enemies.json", normalizeEnemies(load("enemy.js")));
write("skills.json", normalizeSkills(load("skill.js")));
write("effects.json", load("effect.js"));
write("abilities.json", load("ability.js"));
write("fields.json", load("field.js"));
write("field-requirements.json", load("fieldrequire.js"));
write("difficulties.json", load("difficultity.js"));
write("affinities.json", load("aisyou.js"));
write("shops.json", load("shop.js"));
write("tips.json", load("tips.js"));
write("item-info.json", load("iteminfo.js"));
write("items.json", load("item.json"));
write("fix.json", load("fix.json"));

// newdata.js は関数なので 3 種類の初期状態を個別に取り出す。
// 新規プレイヤー / 新規戦場 / 新規ペットの初期値 (Phase 3 の既定値として使う)。
const newdata = load("newdata.js");
write("defaults.json", {
  channel: newdata("channel"),
  user: newdata("user"),
  pet: newdata("pet"),
});

console.log("完了");
