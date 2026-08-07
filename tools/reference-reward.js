#!/usr/bin/env node
/*
 * 撃破報酬と次の敵の抽選の参照実装 (差分テスト用)。
 *
 *   node tools/reference-reward.js <seed> <caseCount> > cases.json
 *
 * reference-battle.js と同じ方針で、ea.js の result() と nxevt() から
 * 式と処理順をそのまま写している。行番号は ea.js のもの。
 *
 * 特に nxevt は「4 つの乱数を判定より前に全て引く」ため、順序を取り違えると
 * 以降の乱数列がずれる。そこを検出できるようにするのがこのファイルの主目的。
 */

const path = require("path");
const mulberry32 = require("./mulberry32.js");

const DATA = path.join(__dirname, "..", "data");
const load = (f) => require(path.join(DATA, f));

const sk = load("skills.json");
const en = load("enemies.json");
const ab = load("abilities.json");
const fi = load("fields.json");
const df = load("difficulties.json");
const im = load("items.json");
const fix = load("fix.json");

const learnable = sk.filter((s) => s.learn !== null);
const resetable = ["即死", "死の宣告"];

let rnd = mulberry32(1);
const random = (a, b) => Math.round(rnd() * (b - a)) + a;
const choice = (arr, c) => arr[c] || arr[Math.floor(rnd() * arr.length)];

// ea.js:336-357 result() の報酬計算部分
function computeReward(einfo, enemy) {
  const fdata = fi.find((f) => f.name == enemy.f);
  const ddata = df.find((d) => d.name == enemy.d);

  const exp = Math.round(einfo.exp * enemy.lv * (fdata ? fdata.exp : 1) * (ddata ? ddata.exp : 1));
  const gil = Math.round(einfo.g * Math.round(enemy.lv / 3));

  const lines = ["< RESULT >", `\` ${exp}の経験値を獲得！`, `\` ${gil}のギルを獲得！`];
  const item = [];

  einfo.i.forEach((e) => {
    if (random(1, 100) <= e[2]) {
      item.push(e.slice(0, 2));
      lines.push(`\` ${im[e[0]]}を手に入れた！`);
    }
  });

  return { exp, gil, item, lines };
}

// ea.js:360-412 参加者への反映
function applyToPlayer(p, username, exp, gil, item, update) {
  let up = 0;

  item.forEach((a) => {
    p.i[a[0]] = (p.i[a[0]] || 0) + a[1];
  });

  p.g += gil;
  p.xp += exp;

  while (p.xp >= (p.lv + 1) ** 2) {
    p.lv += 1;
    up += 1;
    p.mhp = p.lv * 10 + fix.player;
    p.mmp = Math.round(p.lv * 2.22);
  }

  learnable.forEach((l) => {
    if (l.learn <= p.lv && p.sk.indexOf(l.name) == -1) {
      p.sk.push(l.name);
      update.push(`+ ${username}は${l.name}を習得した！`);
    }
  });

  if (up >= 1) update.push(`+ ${username}が${p.lv}にレベルアップした！`);

  const pet = p.p[0];
  let pup = 0;
  if (pet) {
    pet.xp += exp;
    while (pet.xp >= (pet.lv + 1) ** 2) {
      pet.lv += 1;
      pup += 1;
    }
    if (pup >= 1) update.push(`+ ${pet.n}が${pet.lv}にレベルアップした！`);
    p.p[0] = pet;
  }

  p.hp = p.mhp;
  p.n = "0";
  p.eff = p.eff.filter((e) => resetable.indexOf(e[0]) == -1);
}

// ea.js:418-439 nxevt の抽選。4 つの乱数を判定より前に全て引く。
function chooseNext(field) {
  const selector = en.filter((e) => e.field.indexOf(field) != -1);

  const r1 = random(1, 100);
  const r2 = random(1, 777);
  const r3 = random(1, 100);
  const r4 = random(1, 2048);

  if (r2 == 777 && selector.find((e) => e.rare == 2)) return choice(selector.filter((e) => e.rare == 2));
  if (r4 == 2048 && selector.find((e) => e.rare == 4)) return choice(selector.filter((e) => e.rare == 4));
  if (r3 <= 7 && selector.find((e) => e.rare == 3)) return choice(selector.filter((e) => e.rare == 3));
  if (r1 <= 3 && selector.find((e) => e.rare == 1)) return choice(selector.filter((e) => e.rare == 1));
  return choice(selector.filter((e) => e.rare == 0));
}

// ea.js:481-501 ペット捕獲時のアビリティ抽選
function chooseAbility(einfo) {
  const ability = ab.filter((a) => einfo.ability.indexOf(a.rare) != -1);
  const r = random(1, 1000);

  if (ability.find((a) => a.rare == "UR") && r <= 5) return choice(ability.filter((a) => a.rare == "UR"));
  else if (ability.find((a) => a.rare == "SR") && r <= 120) return choice(ability.filter((a) => a.rare == "SR"));
  else if (ability.find((a) => a.rare == "R") && r <= 512) return choice(ability.filter((a) => a.rare == "R"));
  else if (ability.find((a) => a.rare == "N")) return choice(ability.filter((a) => a.rare == "N"));
  return ab[0];
}

const seed = parseInt(process.argv[2] || "1", 10);
const count = parseInt(process.argv[3] || "200", 10);

const results = [];
for (let i = 0; i < count; i++) {
  rnd = mulberry32(seed + i);

  const einfo = en[i % en.length];
  const difficulty = df[i % df.length];
  const field = einfo.field.length ? einfo.field[0] : "草原";
  const enemy = { f: field, lv: 1 + ((i * 13) % 900), d: difficulty.name, n: [] };

  const plv = 1 + ((i * 17) % 200);
  const player = {
    hp: 1,
    mhp: plv * 10 + fix.player,
    mp: 0,
    mmp: Math.round(plv * 2.22),
    lv: plv,
    // 次のレベルまで僅かに足りない位置から始めて、レベルアップ経路を確実に通す。
    xp: (plv + 1) ** 2 - 1,
    g: 0,
    sk: [],
    eff: [
      ["毒", 1, 5],
      ["即死", 1, 1],
      ["死の宣告", 1, 3],
    ],
    i: {},
    p: [],
  };

  if (i % 4 === 0) {
    player.p[0] = { n: "ペット" + i, c: en[(i * 3) % en.length].code, lv: 1 + (i % 30), xp: 0, p: 50, a: ab[0].name };
  }

  const update = [];
  const reward = computeReward(einfo, enemy);
  applyToPlayer(player, "テスト", reward.exp, reward.gil, reward.item, update);

  const next = chooseNext(enemy.f);
  const nextLevel = enemy.lv + 1;
  const ddata = df.find((d) => d.name == enemy.d);
  const nextHp = next
    ? Math.round((nextLevel * next.hp * 10 + fix.enemy) * (ddata ? ddata.hp : 1))
    : null;

  const petAbility = chooseAbility(einfo);
  const petChance = random(5, 95);

  results.push({
    index: i,
    seed: seed + i,
    enemyCode: einfo.code,
    field: enemy.f,
    difficulty: enemy.d,
    enemyLevel: enemy.lv,
    exp: reward.exp,
    gil: reward.gil,
    drops: reward.item.map((a) => a.join(":")),
    rewardLines: reward.lines,
    updateLines: update,
    playerLevel: player.lv,
    playerXp: player.xp,
    playerGil: player.g,
    playerMaxHp: player.mhp,
    playerMaxMp: player.mmp,
    playerHp: player.hp,
    playerSkills: player.sk,
    playerEffects: player.eff.map((e) => e.join(":")),
    playerItems: Object.keys(player.i).map((k) => `${k}:${player.i[k]}`),
    petLevel: player.p[0] ? player.p[0].lv : null,
    petXp: player.p[0] ? player.p[0].xp : null,
    nextEnemyCode: next ? next.code : null,
    nextEnemyLevel: nextLevel,
    nextEnemyHp: nextHp,
    petAbility: petAbility ? petAbility.name : null,
    petChance,
  });
}

process.stdout.write(JSON.stringify(results, null, 1));
