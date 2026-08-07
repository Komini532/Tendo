#!/usr/bin/env node
/*
 * 戦闘計算の参照実装 (差分テスト用)。
 *
 *   node tools/reference-battle.js <seed> <caseCount> > cases.json
 *
 * 目的は「C# 版が旧 JavaScript と同じ数値を出すか」を機械的に確かめること。
 *
 * ea.js はそのままでは動かない (discord.js v11 が要る、r.sto/r.ots が存在しない、
 * DB とチャンネルに依存する) ため、戦闘計算に関わる部分だけをここへ写した。
 * **式と処理順は ea.js から一字一句そのまま持ってきており、行番号を併記してある。**
 * писать し直すと差分テストの意味が無くなるので、変更するときは必ず ea.js を確認すること。
 *
 * Math.random は mulberry32 に差し替える。C# 側も同じ実装・同じ種を使うので、
 * 乱数の「引かれる順序」まで一致していないと結果がずれる = 検出できる。
 */

const path = require("path");
const mulberry32 = require("./mulberry32.js");

const DATA = path.join(__dirname, "..", "data");
const load = (f) => require(path.join(DATA, f));

const sk = load("skills.json");
const en = load("enemies.json");
const ef = load("effects.json");
const ab = load("abilities.json");
const fi = load("fields.json");
const df = load("difficulties.json");
const as = load("affinities.json");
const fix = load("fix.json");

// --- 乱数 -------------------------------------------------------------------
let rnd = mulberry32(1);

// ea.js が使う r.random (fn.js)
const random = (a, b) => {
  const c = b - a;
  const d = Math.round(rnd() * c);
  return d + a;
};

// ea.js:38-40 Array.prototype.choice
const choice = (arr, c) => arr[c] || arr[Math.floor(rnd() * arr.length)];

// ea.js:44-53 skillselect
const skillselect = (arr, nores) => {
  let ski = null;
  arr.forEach((s) => {
    if (ski) return;
    if (random(1, 100) <= s.per) ski = s.name;
  });
  return ski || (nores ? null : skillselect(arr));
};

// ea.js:54-66 isprotected
const isprotected = (a, b) => {
  let c = null;
  a.forEach((d) => {
    if (c) return;
    const f = ef.find((g) => g.name == d[0]);
    if (!f) return;
    if (f.protect.indexOf(b) != -1) c = d[0];
  });
  return c;
};

/*
 * ea.js:663-819 hitevent
 * 戻り値の代わりに state を直接書き換える。ログは行の配列として貯める。
 */
function hitevent(o, state, log) {
  const { ainfo, dinfo, attack, defence, skill, pre } = o;
  let damage = o.damage;
  const zinfo = as.find((a) => a.name == dinfo.zokusei);
  let nomove = "";

  if (zinfo) {
    if (zinfo.high.indexOf(skill.zokusei) != -1) damage = Math.round(damage * 1.333);
    else if (zinfo.low.indexOf(skill.zokusei) != -1) damage = Math.round(damage * 0.666);
  }

  if (skill.g) {
    const reqg = Math.round(skill.g * Math.round(attack.lv / 10));
    if (reqg <= attack.g) {
      attack.g -= reqg;
      log.push(`${pre}${ainfo.name}は${reqg}ギルを投げつけた！`);
      damage = Math.round(damage * 0.01 * reqg);
    } else {
      log.push(`${pre}${ainfo.name}は${reqg}ギルを持っていなかった！`);
      damage = 0;
    }
  }

  attack.eff.forEach((e) => {
    const teff = ef.find((f) => f.name == e[0]);
    if (!teff) return;
    const level = teff.effects[e[1] - 1]; // choice ではない
    if (!level) return;
    const amanojaku = attack.eff.find((x) => x[0] == "天邪鬼");
    if (amanojaku) damage = Math.round(damage / level.atk);
    else damage = Math.round(damage * level.atk);
    if (random(1, 100) <= level.nomove && !nomove) nomove = teff.name;
  });

  defence.eff.forEach((e) => {
    const teff = ef.find((f) => f.name == e[0]);
    if (!teff) return;
    const level = teff.effects[e[1] - 1];
    if (!level) return;
    damage = Math.round(damage / level.def);
    if (teff.nodamage.indexOf(skill.zokusei) != -1) damage = 0;
    if (teff.inv && skill.type == 0) damage = 0;
    if (teff.minv && skill.type == 1) damage = 0;
  });

  if (nomove) {
    log.push(`${pre}${ainfo.name}は${nomove}で動けない！`);
    return;
  }

  const mp1 = skill.mp;
  if (!(mp1 <= attack.mp || !skill.mp)) {
    log.push(`${pre}${ainfo.name}の${skill.name}！魔力が足りない！`);
    return;
  }

  defence.hp -= damage;
  if (defence.hp < 0) defence.hp = 0;

  if (damage) {
    log.push(`${pre}${ainfo.name}の${skill.name}！${dinfo.name}に${damage}ダメージ！`);
  } else if (skill.type != 2) {
    log.push(`${pre}${ainfo.name}の${skill.name}！${dinfo.name}には効いていない！`);
  } else {
    log.push(`${pre}${ainfo.name}の${skill.name}！`);
  }

  if (attack.mp) attack.mp -= mp1;

  const noeffect = !damage && skill.type != 2;
  if (noeffect) return;

  skill.pair.forEach((e) => {
    const per = random(1, 100) <= e[2];
    const tgef = defence.eff.find((f) => f[0] == e[0]);
    if (per && tgef) {
      if (tgef[1] <= e[1]) {
        defence.eff = defence.eff.filter((f) => f != tgef);
        log.push(`${pre}${dinfo.name}の${e[0]}状態が解除された！`);
      } else {
        log.push(`${pre}${dinfo.name}の${e[0]}状態を解除できない！`);
      }
    }
  });

  skill.self.forEach((e) => {
    if (random(1, 100) <= e[3]) {
      const prot = isprotected(attack.eff, e[0]);
      if (prot) log.push(`${pre}${ainfo.name}は${prot}で${e[0]}状態にならない！`);
      else {
        attack.eff.push(e.slice(0, 3));
        log.push(`${pre}${ainfo.name}は${e[0]}状態になった！`);
      }
    }
  });

  skill.effect.forEach((e) => {
    if (random(1, 100) <= e[3]) {
      const prot = isprotected(defence.eff, e[0]);
      if (prot) log.push(`${pre}${dinfo.name}は${prot}で${e[0]}状態にならない！`);
      else {
        defence.eff.push(e.slice(0, 3));
        log.push(`${pre}${dinfo.name}は${e[0]}状態になった！`);
      }
    }
  });

  if (skill.move && state.enemy.f != skill.move) {
    if (["nightmare", "daydream"].indexOf(state.enemy.c) != -1) {
      log.push(`${pre}${dinfo.name}は吸いこめない！`);
    } else {
      state.moved = true;
      state.enemy.f = skill.move;
      log.push(`${pre}${dinfo.name}の体が${skill.move}に吸い込まれる！`);
    }
  }
}

// ea.js:921-973 joutai
function joutai(info, attack, log) {
  const amanojaku = attack.eff.find((e) => e[0] == "天邪鬼");

  attack.eff.forEach((e, i) => {
    const effect = ef.find((f) => f.name == e[0]);
    if (!effect) return;
    const level = choice(effect.effects, e[1] - 1); // choice なのでレベル超過はランダム

    if (!effect.end || (effect.end && !(e[2] - 1))) {
      const heal = Math.round(attack.mhp * level.heal);
      const damage = Math.round(attack.mhp * level.damage);

      if (attack.hp >= 1) {
        if (amanojaku) {
          if (heal) {
            attack.hp -= heal;
            log.push(`+ ${info.name}は${e[0]}で${heal}ダメージ受けた！`);
          }
          if (damage) {
            attack.hp += damage;
            log.push(`- ${info.name}は${e[0]}で体力を${damage}回復した！`);
          }
        } else {
          if (heal) {
            attack.hp += heal;
            log.push(`+ ${info.name}は${e[0]}で体力を${heal}回復した！`);
          }
          if (damage) {
            attack.hp -= damage;
            log.push(`- ${info.name}は${e[0]}で${damage}ダメージ受けた！`);
          }
        }
      }
    }

    e[2]--;
    if (!e[2]) {
      log.push(`${info.name}の「${e[0]}」状態が切れた！`);
      attack.eff[i] = null;
    } else {
      attack.eff[i] = e;
    }
  });

  attack.eff = attack.eff.filter((e) => !!e);
}

/*
 * ea.js:618-1080 の 1 ターン分。
 * 難易度の初回付与 → spdevent → spdevent2 → 決着 の順。
 */
function runTurn(state, actSkill) {
  const { player, enemy } = state;
  const einfo = en.find((e) => e.code == enemy.c);
  const ddata = df.find((d) => d.name == enemy.d) || df[0];
  const tinfo = { name: state.username, zokusei: "無" };
  const log = [];

  if (!enemy.turn) enemy.turn = 0;
  enemy.turn++;

  if (enemy.turn == 1 && ddata.eff) {
    ddata.eff.forEach((e) => {
      if (random(1, 100) <= e[3]) enemy.eff.push(e.slice(0, 3));
    });
  }

  const spd = player.spd * player.lv - einfo.spd * enemy.lv;
  const jun = spd >= 0;

  const playerActs = () => {
    if (player.hp <= 0) return false;
    const pdamage = Math.round(
      ((actSkill.atk * (player.lv * 10 + fix.player)) / 3 / 30) * (random(85, 100) / 100)
    );
    hitevent(
      { pre: "+ ", ainfo: tinfo, dinfo: einfo, attack: player, defence: enemy, skill: actSkill, damage: pdamage },
      state,
      log
    );

    // ペット攻撃 (ea.js:845-886)
    const pinfo = player.p[0];
    if (pinfo) {
      const pdata = { lv: pinfo.lv, hp: Infinity, mp: Infinity, eff: [] };
      const pab = ab.find((a) => a.name == pinfo.a);
      if (random(1, 100) <= pinfo.p && pab) {
        for (let i = 0; i < pab.repeat; i++) {
          const pst = en.find((e) => e.code == pinfo.c);
          const psk = skillselect(pst.skill);
          const ska = sk.find((s) => s.name == psk);
          const kab = ab.find((a) => a.name == pst.only);
          if (ska) {
            let petdamage = Math.round(
              ((ska.atk * (pinfo.lv * 10 + fix.player)) / 3 / 30) * (random(85, 100) / 100)
            );
            petdamage = Math.round(petdamage * pab.atk);
            if (kab) petdamage = Math.round(petdamage * kab.atk);
            hitevent(
              { pre: "+ ", ainfo: { name: pinfo.n }, dinfo: einfo, attack: pdata, defence: enemy, skill: ska, damage: petdamage },
              state,
              log
            );
          }
        }
      }
    }
    return true;
  };

  const enemyActs = () => {
    if (enemy.hp <= 0) return false;
    for (let i = 0; i < (einfo.repeat || 1); i++) {
      const eskname = skillselect(einfo.skill);
      const enemysk = sk.find((s) => s.name == eskname) || sk.find((s) => s.name == "攻撃");
      const edamage = Math.round(
        ((enemysk.atk * (enemy.lv * 10 + fix.enemy) * einfo.atk * (ddata ? ddata.atk : 1)) / 3 / 30) *
          (random(85, 100) / 100)
      );
      hitevent(
        { pre: "- ", ainfo: einfo, dinfo: tinfo, attack: enemy, defence: player, skill: enemysk, damage: edamage },
        state,
        log
      );
    }
    return true;
  };

  // spdevent: 速い方から 1 回ずつ。途中で return すると後攻も動かない。
  const order = jun ? [playerActs, enemyActs] : [enemyActs, playerActs];
  for (const act of order) {
    if (!act()) break;
  }

  // spdevent2: 状態異常の継続効果も同じ順・同じ打ち切り。
  const tickPlayer = () => {
    if (player.hp <= 0) return false;
    joutai(tinfo, player, log);
    return true;
  };
  const tickEnemy = () => {
    if (enemy.hp <= 0) return false;
    joutai(einfo, enemy, log);
    return true;
  };
  const order2 = jun ? [tickPlayer, tickEnemy] : [tickEnemy, tickPlayer];
  for (const tick of order2) {
    if (!tick()) break;
  }

  if (enemy.hp <= 0) {
    log.push(einfo.next ? `- ${einfo.next.msg}` : `\` ${einfo.name}を倒した！`);
  } else if (player.hp <= 0) {
    log.push(`${state.username}は倒れた…`);
  }

  return log;
}

// --- ケース生成 --------------------------------------------------------------
// 状態異常や属性相性を踏むよう、敵と技を satisfyingly 広く散らす。
function buildCase(index) {
  const enemyDef = en[index % en.length];
  const skillDef = sk[(index * 7) % sk.length];
  const difficulty = df[index % df.length];

  const lv = 1 + ((index * 13) % 400);
  const plv = 1 + ((index * 17) % 300);
  const hp = Math.round((lv * enemyDef.hp * 10 + fix.enemy) * difficulty.hp);

  // 敵の出現フィールドが空なら草原扱い (召喚専用の敵)
  const field = enemyDef.field.length ? enemyDef.field[0] : "草原";

  // 半数は本来の魔力量 (「魔力が足りない！」の経路を通す)、
  // 半数は潤沢にして高コストの技 (「吸い込み」など) も実際に発動させる。
  const mana = index % 2 === 0 ? 999999 : Math.round(plv * 2.22);

  const player = {
    hp: plv * 10 + fix.player,
    mhp: plv * 10 + fix.player,
    mp: mana,
    mmp: mana,
    spd: 1,
    lv: plv,
    xp: 0,
    g: 100000,
    sk: [],
    eff: [],
    i: {},
    p: [],
    n: "0",
  };

  // 3 件に 1 件はプレイヤーに状態異常を積んで補正経路を通す。
  if (index % 3 === 0) {
    player.eff.push(["毒", 1 + (index % 5), 3 + (index % 4)]);
  }
  if (index % 7 === 0) {
    player.eff.push(["天邪鬼", 1, 5]);
  }
  if (index % 11 === 0) {
    player.eff.push(["再生", 1 + (index % 3), 4]);
  }

  // 5 件に 1 件はペット同伴。
  if (index % 5 === 0) {
    const species = en[(index * 3) % en.length];
    player.p[0] = {
      n: "ペット" + index,
      c: species.code,
      lv: 1 + (index % 50),
      xp: 0,
      p: 100, // 必ず攻撃させて経路を通す
      a: ab[index % ab.length].name,
    };
  }

  const enemy = {
    f: field,
    c: enemyDef.code,
    lv,
    mhp: hp,
    hp,
    mmp: enemyDef.mp,
    mp: enemyDef.mp,
    eff: enemyDef.effect.map((e) => e.slice(0)),
    n: [],
    cs: "1",
    d: difficulty.name,
  };

  return { player, enemy, username: "テスト", moved: false, skill: skillDef.name };
}

const seed = parseInt(process.argv[2] || "1", 10);
const count = parseInt(process.argv[3] || "200", 10);

const results = [];
for (let i = 0; i < count; i++) {
  // ケースごとに種を変えて、乱数列も散らす。
  rnd = mulberry32(seed + i);

  const state = buildCase(i);
  const actSkill = sk.find((s) => s.name == state.skill);
  const log = runTurn(state, actSkill);

  results.push({
    index: i,
    seed: seed + i,
    skill: state.skill,
    enemyCode: state.enemy.c,
    difficulty: state.enemy.d,
    field: state.enemy.f,
    playerLevel: state.player.lv,
    enemyLevel: state.enemy.lv,
    hasPet: !!state.player.p[0],
    petAbility: state.player.p[0] ? state.player.p[0].a : null,
    // 比較対象
    resultPlayerHp: state.player.hp,
    resultPlayerMp: state.player.mp,
    resultPlayerGil: state.player.g,
    resultEnemyHp: state.enemy.hp,
    resultEnemyMp: state.enemy.mp,
    resultPlayerEffects: state.player.eff.map((e) => e.join(":")),
    resultEnemyEffects: state.enemy.eff.map((e) => e.join(":")),
    resultField: state.enemy.f,
    resultMoved: state.moved,
    log,
  });
}

process.stdout.write(JSON.stringify(results, null, 1));
