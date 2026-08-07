const Discord = require("discord.js");
const r = require("./fn.js");
const mac = require("./mmo/macro.js");

const config = require("./mmo/config.json");
const fix = require("./mmo/fix.json");
const im = require("./mmo/item.json");

const sk = require("./mmo/skill.js");
const en = require("./mmo/enemy.js");
const ef = require("./mmo/effect.js");
const as = require("./mmo/aisyou.js");
const nd = require("./mmo/newdata.js");
const fi = require("./mmo/field.js");
const sh = require("./mmo/shop.js");
const fr = require("./mmo/fieldrequire.js");
const ti = require("./mmo/tips.js");
const ab = require("./mmo/ability.js");
const ii = require("./mmo/iteminfo.js");
const df = require("./mmo/difficultity.js");

const fm = [1, 1.5, 1, 1.5, 2, 2, 3];
const resetable = ["即死", "死の宣告"];
const learnable = sk.filter(s => s.learn!=Infinity);

const REACTION = new Map();
const AWAIT = new Map();
const MACRO = new Map();
const REN = new Map();
const BANLIST = [];

const ea = new Discord.Client();
const prefix = config.prefix;
const version = "???";

const embed = a => new Discord.RichEmbed(a);
const code = (a,b) => "```"+(b||"C")+"\n"+a+"\n```"; 
Array.prototype.choice = function(c) {
  return this[c] || this[Math.floor(Math.random() * this.length)];
}
Array.prototype.lastIndex = function(c) {
  return this[this.length-1];
}
const skillselect = (arr,nores) => {
  let ski = null;
  arr.forEach((s,i)=>{
    if( ski )return;
    if( r.random(1,100)<=s.per ){
      ski = s.name;
    }
  });
  return ski || (nores ? null : skillselect(arr));
}
const isprotected = (a,b) => {
  let c=null;
  a.forEach((d,e) => {
    if( c )return;
    let f = ef.find(g => g.name==d[0]);
    if(!f)return;
    let h = f.protect;
    if( h.indexOf(b)!=-1 ){
      c = d[0];
    }
  });
  return c;
}

const sqlite = require("./db.js"),
      db = sqlite.init(`./db/${config.filename || "mmo"}.sqlite3`);

db.serialize(() => {
  db.run('create table if not exists mmo_channel(id INT, name TEXT)');
  db.run('create table if not exists mmo_user(id INT, name TEXT)');
  db.run('create table if not exists mmo_others(id INT, name TEXT)');
});

const ctrl = {
  type: (id) => {
    if (ea.channels.get(id)) {
      return "channel";
    } else if (ea.users.get(id)) {
      return "user";
    } else {
      return "others";
    }
  },
  new: (a, b, c) => {
    db.serialize(() => {
      const ins = db.prepare(`INSERT INTO mmo_${c || ctrl.type(a)} VALUES(?,?)`);

      ins.run(a, b);

      ins.finalize();
    });
  },
  read: (a,b) => {
    let type = ctrl.type(a);
    return new Promise(function(resolve, reject) {
      db.serialize(function() {
        db.get(`SELECT id,name FROM mmo_${b || type} WHERE id = ${a}`,
          function(err, res) {
            if (err) return reject(err);

            if( res ){
              res.id = a;
              res.name = String(res.name).replace(/\n/g, "%n");
            }else{
              try{
                res.id = a;
              }catch(e){
                console.log(`001: [res id change failed]`);
              }
            }

            resolve(res);
          });
      });
    });
  },
  write: (a, b, c) => {
    let type = ctrl.type(a);
    if( type=="command" )return;
    return new Promise((res,rej) => {
      db.serialize(() => {
        const upd = db.prepare(`UPDATE mmo_${c || type} SET name = $name WHERE id = $id`);
        let str = String(b).replace(/\n/g, "%n");

        upd.run({
          $id: a,
          $name: str,
        });
  
        upd.finalize();
        res();
      });
    });
  },
  multi: (a, b) => {
    const e = {
      dat: [],
      count: 0,
      limit: a.length,
    }
    return new Promise((resolve, reject) => {
      const c = (d) => {
        return new Promise((res, rej) => {
          d(a[e.count]).then(f => {
            e.count++;
            e.dat.push(f);
            if (e.count >= e.limit) {
              res();
              resolve(e.dat);
            } else {
              res(c(d));
            }
          }).catch(e => {
            rej();
          });
        });
      };
      c(b);
    });
  },
  page: (_1, _2, _3) => {
    const channel = _1.channel || _1,
      user = _1.user || _2,
      pages = _1.pages || _3;
    const c = {
      nowpage: 0,
      lastmoved: Number(new Date()),
    };

    channel.send(pages[0]).then(M => {
      let move = ["◀", "▶"];
      r.repeat(move, val => M.react(val));
      REACTION.set(M.id, (R, U) => {
        if (U != user) return;
        if(channel.guild) R.remove(U);
        let order = R.emoji.name;
        let movetime = Number(new Date());
        if (movetime - c.lastmoved > 15000) {
          REACTION.delete(M.id);
          return;
        }
        if (order == move[0]) {
          c.nowpage--;
        } else if (order == move[1]) {
          c.nowpage++;
        } else {
          return;
        }
        if (c.nowpage > pages.length - 1) c.nowpage = 0;
        if (c.nowpage < 0) c.nowpage = pages.length - 1;
        M.edit(pages[c.nowpage]);
        c.lastmoved = movetime;
      });
    });
  },
  react: (o) => {
    o.channel.send(o.msg || embed({
      title: o.name,
      description: o.value,
    })).then(M => {
      r.repeat(o.reactions, r => M.react(r));
      REACTION.set(M.id, (R, U) => {
        if( U.bot && !o.bot ) return;

        if (!o.global && o.user != U) return;
        if (U == ea.user) return;
        if( o.undelete ) R.remove(U);
        const a = R.emoji.name;
        const i = o.reactions.indexOf(a);

        o.event(M, a, i, U);

        if (o.repeat) return;
        REACTION.delete(M.id);
        if( o.undelete ) M.delete().catch(()=>{});
      });
    });
  },
  collector: (o) => {
    return new Promise((res, rej) => {
      o.channel.send(o.msg || embed({
        title: o.name,
        description: `${o.value}\n（制限時間：${o.time/1000}秒、キャンセル：\`cancel\`）`,
      })).then(M => {
        const c = o.channel.createMessageCollector(F => F.author == o.user, {
          time: o.time,
        });

        c.on("collect", M2 => {
          if (M2.content == "cancel") {
            if (typeof o.cancel == "function") {
              o.cancel();
            } else if (o.cancel) {
              o.channel.send(o.cancel).then(ctrl.delete);
            }
            return c.stop();
          }
          if( M2.author.bot ) return;

          o.collect(M2, c);
          if( !o.undelete ) M2.delete().catch(()=>{});
        });

        c.on("end", collection => {
          if ( !M.deleted ) M.delete().catch(()=>{});
          res(collection);
        });
      });
    });
  },
  delete: (M) => {
    setTimeout(() => {
      if (!M.deleted) M.delete().catch(()=>{});
    }, 4500);
  },
  prank: (id) => {
    return new Promise((res,rej) => {
      db.serialize(() => {
        db.all(`SELECT * FROM mmo_user`, (err, row) => {
          if( err ) return rej(`[REJECT] ${err}`);
          if( !row ) return rej("[REJECT] DataBase Error");

          row = row.slice(0, 100);

          let sort = [];
          row.forEach((a,i) => {
            sort.push({
              id: a.id,
              name: r.sto(a.name),
            });
          });
          sort = sort.sort((a,b) => b.name.xp - a.name.xp);
        
          let ijud = sort.find(s => s.id == id ) || sort.find(s => s.id == ctrl.nearest(parseInt(id), ea.users.array()));
          if( !ijud )return rej("[REJECT] User Data Not Found");

          let rank = sort.indexOf(ijud)+1;
          res(rank);
        });
      });
    });
  },
  nearest: (id, arr) => {
    if( typeof id!="number" ) id = Number(id);

    let ids = [];
    arr.forEach(u => ids.push(u.id));

    let diff = [];
    let index = 0;

    ids.forEach((u,i) => {
      diff[i] = Math.abs(id - u);
      index = (diff[index] < diff[i]) ? index : i;
    });

    return ids[index];
  },
  whodm : (id) => {
    let channel = ea.channels.get(id);
    let result;

    ea.users.forEach(u => {
      if( u.dmChannel == channel ) result = u;
    });

    return result;
  },
  all: (a) => {
    return new Promise((res, rej) => {
      db.serialize(() => {
        db.all(`SELECT * FROM ${a}`, (err, row) => {
          if( err ) return rej(err);

          res(row);
        });
      });
    });
  },
  perchoice: (a,b) => {
    let fixed = [];
    a.forEach(c => {
      fixed.push({
        name: c,
        per: b,
      });
    });
    let result = skillselect(fixed, true);
    return result;
  },
};

const result = (d, dat) => {
  let einfo = dat[0],
      enemy = dat[1],
      player = dat[2];
  let fdata = fi.find(f => f.name==enemy.f);
  let ddata = df.find(d => d.name==enemy.d);
  let exp = Math.round(einfo.exp*enemy.lv*(fdata ? fdata.exp : 1)*(ddata ? ddata.exp : 1));
  let gil = Math.round(einfo.g*Math.round(enemy.lv/3));
  let description = [code(`< RESULT >`, "HTML")];
  description.push(code(`\` ${exp}の経験値を獲得！`, "JS"));
  description.push(code(`\` ${gil}のギルを獲得！`, "JS"));
  let item = [];
  let lim = 0;
  let match = Array.isArray(enemy.pet) ? enemy.pet : [];
  einfo.i.forEach(e => {
    if( r.random(1,100) <= e[2] ){
      item.push(e.slice(0,2));

      description.push(code(`\` ${im[e[0]]}を手に入れた！`, "JS"));
      lim++;
    }
  });
  
  let update = [];
  ctrl.multi(enemy.n, i => ctrl.read(i)).then(source => {
    source.forEach(ud => {
      if(!ud)return;
      let m = d.guild ? d.guild.members.get(ud.id) : ea.users.get(ud.id);
      let p = r.sto(ud.name);
      let up = 0;

      let username = m.displayName || m.username;

      item.forEach((a) => {
        p.i[a[0]] += a[1];
      });

      p.g += gil;
      p.xp += exp;
      while(p.xp >= (p.lv+1)**2){
        p.lv += 1;
        up += 1;
        p.mhp = p.lv*10+fix.player;
        p.mmp = Math.round(p.lv*2.22);
      }

      learnable.forEach(l => {
        if( l.learn<=p.lv && p.sk.indexOf(l.name)==-1 ){
          p.sk.push(l.name);
          update.push(code(`+ ${username}は${l.name}を習得した！`, "DIFF"));
        }
      });

      if( up>=1 ){
        update.push(code(`+ ${username}が${p.lv}にレベルアップした！`, "DIFF"));
      }

      let pet = p.p[0];
      let pup = 0;
      if( pet ){
        pet.xp += exp;
        while(pet.xp >= (pet.lv+1)**2){
          pet.lv += 1;
          pup += 1;
        }
        if( pup>=1 ){
          update.push(code(`+ ${pet.n}が${pet.lv}にレベルアップした！`, "DIFF"));
        }
        p.p[0] = pet;
      }

      p.hp = p.mhp;
      p.n = "0";
      p.eff = p.eff.filter(e => resetable.indexOf(e[0])==-1);

      ctrl.write(ud.id, r.ots(p));
    });
  }).then(() => {
    d.channel.send(embed({
      description : description.concat(update).join(""),
    }));

    let nxevt = () => {
      let selector = en.filter(e => e.field.indexOf(enemy.f)!=-1);
      let next = (() => {
        let random = r.random(1,100);
        let random2 = r.random(1,777);
        let random3 = r.random(1,100);
        let random4 = r.random(1,2048);

        if( random2==777 && selector.find(e => e.rare==2) ){
          return selector.filter(e => e.rare==2).choice();
        }
        if( random4==2048 && selector.find(e => e.rare==4) ){
          return selector.filter(e => e.rare==4).choice();
        }
        if( random3<=7 && selector.find(e => e.rare==3) ){
          return selector.filter(e => e.rare==3).choice();
        }
        if( random<=3 && selector.find(e => e.rare==1) ){
          return selector.filter(e => e.rare==1).choice();
        }
        return selector.filter(e => e.rare==0).choice();
      })();
      let ninfo = en.find(e => e.code==next.code);
      let lv = enemy.lv+1;
      let hp = Math.round(((lv*next.hp*10)+fix.enemy) * (ddata ? ddata.hp : 1));
  
      ctrl.write(d.channel.id, r.ots({
        f: enemy.f,
        c: next.code,
        lv: lv,
        mhp: hp,
        hp: hp,
        mmp: next.mp,
        mp: next.mp,
        eff: next.effect,
        n: [],
        cs: d.channel.id,
        d: enemy.d,
      })).then(() => {
        d.channel.send(embed({
          description: code(`${ninfo.name}が現れた！`, ["C", "fix", "fix", "fix"][ninfo.rare])+code([`[レベル] ${lv}`, `[体力] ${hp}`].join("\n"), "CSS"),
          image: {
            url: ninfo.pic,
          },
        })).then(() => {
          AWAIT.set(d.channel.id, false);
        }).catch(() => {
          AWAIT.set(d.channel.id, false);
        });
      });
    }

    if( match.length ){
      let choiced = ctrl.perchoice(match, 12);
      let first = ea.users.get(choiced);
      console.log(match);
      console.log(choiced);
      if( first ){
        let yes = () => {
          ctrl.read(first.id).then(fd => {
            if( !fd ) return nxevt();
            let userdata = r.sto(fd.name);

            let ability = ab.filter(a => einfo.ability.indexOf(a.rare)!=-1);
            let choice = () => {
              let random = r.random(1, 1000);
              if( ability.find(a => a.rare=="UR") && random<=5 ){
                return ability.filter(a => a.rare=="UR").choice();
              }else if( ability.find(a => a.rare=="SR") && random<=120 ){
                return ability.filter(a => a.rare=="SR").choice();
              }else if( ability.find(a => a.rare=="R") && random<=512 ){
                return ability.filter(a => a.rare=="R").choice();
              }else if( ability.find(a => a.rare=="N") ){
               return ability.filter(a => a.rare=="N").choice();
              }
              return ab[0];
            }
            let aname = choice();

            let npet = nd("pet");
            npet.n = einfo.name;
            npet.c = einfo.code;
            npet.p = r.random(5, 95);
            npet.a = aname ? aname.name : ab[0].name;
            let beforepet = userdata.p[0];
            if( beforepet ){
              npet.lv = beforepet.lv;
              npet.xp = beforepet.xp;
            }
            userdata.p[0] = npet;

            ctrl.write(fd.id, r.ots(userdata));

            d.channel.send(embed({
              description: `${first}は\`${einfo.name}\`を仲間にしました。`,
            }));
            nxevt();
          });
        }
        let no = () => {
          d.channel.send(embed({
            description: `${first}は\`${einfo.name}\`を仲間にしませんでした。`,
          }));
          nxevt();
        }
        let timeout = setTimeout(() => {
          no();
        }, 10000);
        ctrl.react({
          reactions: ["👍", "👎"],
          channel: d.channel,
          user: first,
          undelete: !d.guild,
          msg: embed({
            description: `\`${einfo.name}\`が${first}に懐いたようだ...\n仲間にしますか？`,
          }),
          event: (m,a,i) => {
            switch(i){
              case 0:
                clearTimeout(timeout);
                yes();
                REACTION.delete(m.id);
                break;
              case 1:
                clearTimeout(timeout);
                no();
                REACTION.delete(m.id);
                break;
            }
          },
        });
        return;
      }
    }
    nxevt();
  });
}

const fn = (d, act) => {
  if(!act)return;
  let id = [d.channel.id, d.user.id];
  ctrl.multi(id, a => ctrl.read(a)).then(ids => {
    if( !ids[0] ){
      let cd = nd("channel");
      let enemy = en.filter(e => e.field.indexOf("草原")!=-1 && e.rare!=2).choice();

      cd.c = enemy.code;
      cd.mhp = (1*enemy.hp*10)+fix.enemy;
      cd.hp = cd.mhp;
      cd.mmp = enemy.mp;
      cd.mp = cd.mmp;
      cd.eff = enemy.effect;
      cd.cs = d.channel.id;
      cd.d = "NORMAL";

      ctrl.new(id[0], r.ots(cd));

      return fn(d, act);
    }
    if( !ids[1] ){
      let ud = nd("user");

      ctrl.new(id[1], r.ots(ud));

      return fn(d, act);
    }

    let enemy = r.sto(ids[0].name);
    let player = r.sto(ids[1].name);

    let username = d.member ? d.member.displayName : d.user.username;

    let einfo = en.find(e => e.code==enemy.c);
    const tinfo = {
      name: username,
      zokusei: "無",
    }
    let ddata = df.find(d => d.name==enemy.d) || df[0];

    if( !enemy.cs ) enemy.cs = d.channel.id;
    if( !act.macro ) act.macro = [false, false];

    let penaltyenemy = ["nightmare", "daydream"];
    const penaltyevent = () => {
      let number = act.macro.indexOf(true);
      console.log("011: [MACRO PENALTY!]");
      if( number!=-1 && penaltyenemy.indexOf(enemy.c)==-1 ){
        d.channel.send(embed({
          description: code(`- 「仕事の多いことは決して良い事ではないな...困ったものだ。」`, "DIFF"),
        }));
        fn(d, {
          do: "rs",
          absolute: true,
          summon: penaltyenemy.choice(),
        });
      }else{
        AWAIT.set(d.channel.id, false);
      }
    }

    switch(act.do){
      case "sk":
        if( player.sk.indexOf(act.sk.name)==-1 && act.sk.name!="攻撃" ){
          AWAIT.set(d.channel.id, false);
          return d.channel.send(embed({
            description: `${d.user}さんはまだ「${act.sk.name}」を習得していません。`,
          }));
        }
      case "isk":
        if( player.n!="0" && player.n!=d.channel.id ){
          let ch = ea.channels.get(player.n);
          if( ch ){
            AWAIT.set(d.channel.id, false);
            return d.channel.send(embed({
              description: `${d.user}さんは「${ch}」で戦闘中です。`,
            }));
          }else{
            player.hp = player.mhp;
          }
        }
        if ( act.macro.indexOf(true)!=-1 ) return penaltyevent();
        if( player.hp<=0 ){
          AWAIT.set(d.channel.id, false);
          return d.channel.send(embed({
            description: `${d.user}さんは既にやられています...`,
          }));
        }

        if( !enemy.turn )enemy.turn=0;
        enemy.turn++;

        player.n = d.channel.id;
        if( enemy.n.indexOf(d.user.id)==-1 ) enemy.n.push(d.user.id);

        let description = "";
        let moved = null;

        if( enemy.turn==1 && ddata.eff ){
          ddata.eff.forEach(e => {
            if( r.random(1, 100) <= e[3] ){
              enemy.eff.push(e.slice(0, 3));
            }
          });
        }

        let hitevent = (options) => {
          let ainfo = options.ainfo;
          let dinfo = options.dinfo;
          let attack = options.attack;
          let defence = options.defence;
          let skill = options.skill;
          let damage = options.damage;
          let pre = options.pre;
          let pet = options.pet;
          let zinfo = as.find(a => a.name==dinfo.zokusei);
          let nomove = "";

          if( zinfo ){
            if( zinfo.high.indexOf(skill.zokusei)!=-1 ){
              damage = Math.round(damage * 1.333);
            }else if( zinfo.low.indexOf(skill.zokusei)!=-1 ){
              damage = Math.round(damage * 0.666);
            }
          }
          if( skill.g ){
            let reqg = Math.round(skill.g * Math.round(attack.lv/10));
            if( reqg <= attack.g ){
              attack.g -= reqg;
              console.log(attack.g);
              description += code(`${pre}${ainfo.name}は${reqg}ギルを投げつけた！`);
              damage = Math.round((damage*0.01) * reqg);
            }else{
              description += code(`${pre}${ainfo.name}は${reqg}ギルを持っていなかった！`);
              damage = 0;
            }
          }
          attack.eff.forEach(e => {
            let teff = ef.find(f => f.name==e[0]);
            if( !teff )return;
            let level = teff.effects[e[1]-1];
            if( !level )return;

            let amanojaku = attack.eff.find(e => e[0]=="天邪鬼");

            if(amanojaku){
              damage = Math.round(damage / level.atk);
            }else{
              damage = Math.round(damage * level.atk);
            }

            if( r.random(1,100)<=level.nomove && !nomove ){
              nomove = teff.name;
            }
          });
          defence.eff.forEach(e => {
            let teff = ef.find(f => f.name==e[0]);
            if( !teff )return;
            let level = teff.effects[e[1]-1];
            if( !level )return;

            damage = Math.round(damage / level.def);

            if( teff.nodamage.indexOf(skill.zokusei)!=-1 ){
              damage = 0;
            }
            if( teff.inv && skill.type==0 ){
              damage = 0;
            }
            if( teff.minv && skill.type==1 ){
              damage = 0;
            }
          });

          if( !nomove ){
          let mp1 = skill.mp;
          if( mp1<=attack.mp || !skill.mp ){
            console.log(`[${ainfo.name}'s AMAZING Damage] ${damage}`);
            defence.hp -= damage;
            if( defence.hp<0 )defence.hp = 0;
            if( damage ){
              description += code(`${pre}${ainfo.name}の${skill.name}！${dinfo.name}に${damage}ダメージ！`, "diff");
            }else{
              if( skill.type!=2 ){
                description += code(`${pre}${ainfo.name}の${skill.name}！${dinfo.name}には効いていない！`, "diff");
              }else{
                description += code(`${pre}${ainfo.name}の${skill.name}！`, "diff");
              }
            }
            if( attack.mp ) attack.mp -= mp1;

            let noeffect = !damage && skill.type!=2;
            if( !noeffect ){
              // console.log(`[${ainfo.name} -> ${dinfo.name}]`);
              // console.log(skill.effect);
              let _pair = skill.pair;
              let _self = skill.self;
              let _effect = skill.effect;

              _pair.forEach(e => {
                let per = r.random(1,100) <= e[2];
                let tgef = defence.eff.find(f => f[0] == e[0]);

                if( per && tgef ){
                  if( tgef[1] <= e[1] ){
                    // console.log(`013: [Paired!]`);
                    defence.eff = defence.eff.filter(f => f != tgef);
                    description += code(`${pre}${dinfo.name}の${e[0]}状態が解除された！`, "CSS");
                  }else{
                    description += code(`${pre}${dinfo.name}の${e[0]}状態を解除できない！`, "CSS");
                  }
                }
              });
              _self.forEach(e => {
                let per = r.random(1,100) <= e[3];
  
                if( per ){
                  let protected = isprotected(attack.eff, e[0]);
                  if( protected ){
                    description += code(`${pre}${ainfo.name}は${protected}で${e[0]}状態にならない！`, "CSS");
                  }else{
                    attack.eff.push(e.slice(0, 3));
                    description += code(`${pre}${ainfo.name}は${e[0]}状態になった！`, "CSS");
                  }
                }
              });
              _effect.forEach(e => {
                let per = r.random(1,100) <= e[3];

                if( per ){
                  let protected = isprotected(defence.eff, e[0]);
                  if( protected ){
                    description += code(`${pre}${dinfo.name}は${protected}で${e[0]}状態にならない！`);
                  }else{
                    defence.eff.push(e.slice(0, 3));
                    description += code(`${pre}${dinfo.name}は${e[0]}状態になった！`);
                  }
                }
              });
              if( skill.move && enemy.f!=skill.move ){
                if( penaltyenemy.indexOf(enemy.c)!=-1 ){
                  description += code(`${pre}${dinfo.name}は吸いこめない！`);
                }else{
                  moved = true;
                  enemy.f = skill.move;
                  description += code(`${pre}${dinfo.name}の体が${skill.move}に吸い込まれる！`);
                }
              }
            }
          }else{
            description += code(`${pre}${ainfo.name}の${skill.name}！魔力が足りない！`, "diff");
          }
          }else{
            description += code(`${pre}${ainfo.name}は${nomove}で動けない！`);
          }
          return {
            ainfo: ainfo,
            attack: attack,
            dinfo: dinfo,
            defence: defence,
            skill: skill,
          };
        }
        let spd = player.spd*player.lv - einfo.spd*enemy.lv;
        let jun = spd>=0;
        let mov = 0;
        let spdevent = (sp) => {
          mov++;
          let cm = sp ? !jun : jun;

          if( cm ){
            if( player.hp<=0 ){
              return;
            }
            let pdamage = Math.round((act.sk.atk*(player.lv*10+fix.player)/3) / (30) * (r.random(85, 100) / 100));
            let pattack = hitevent({
              pre: `+ `,
              ainfo: tinfo,
              attack: player,
              dinfo: einfo,
              defence: enemy,
              skill: act.sk,
              damage: pdamage,
            });

            player = pattack.attack;
            enemy = pattack.defence;

            let pinfo = player.p[0];
            if( pinfo ){
              let pdata = {
                lv: pinfo.lv,
                hp: Infinity,
                mp: Infinity,
                eff: [],
              }
              let pab = ab.find(a => a.name==pinfo.a);
              if( r.random(1, 100) <= pinfo.p && pab ){
                // console.log(`014: [Pet Attacks!]`);
                for( var i=0;i<pab.repeat;i++ ){
                  let pst = en.find(e => e.code==pinfo.c);
                  let psk = skillselect(pst.skill);
                  let ska = sk.find(s => s.name==psk);
                  let kab = ab.find(a => a.name==pst.only);
                  if( ska ){
                    let petdamage = Math.round((ska.atk*(pinfo.lv*10+fix.player)/3) / (30) * (r.random(85, 100) / 100));
                    petdamage = Math.round(petdamage * pab.atk);
                    if( kab ){
                      petdamage = Math.round(petdamage * kab.atk);
                    }
                    let petattack = hitevent({
                      pre: `+ `,
                      ainfo: { name:pinfo.n },
                      attack: pdata,
                      dinfo: einfo,
                      defence: enemy,
                      skill: ska,
                      damage: petdamage,
                      tamer: player,
                      pet: {
                        info: pst,
                        ab1: pab,
                      },
                    });

                    enemy = petattack.defence;
                  }
                }
              }
            }

            if( mov!=2 ){
              spdevent(true);
            }
          }else{
            if( enemy.hp<=0 ){
              return;
            }
            for(var i=0;i<(einfo.repeat || 1);i++){
              let eskname = skillselect(einfo.skill);
              let enemysk = sk.find(s => s.name == eskname) || sk.find(s => s.name=="攻撃");

              let edamage = Math.round((enemysk.atk*(enemy.lv*10+fix.enemy)/3*einfo.atk*(ddata ? ddata.atk : 1)) / (30) * (r.random(85, 100) / 100));
              let eattack = hitevent({
                pre: `- `,
                ainfo: einfo,
                attack: enemy,
                dinfo: tinfo,
                defence: player,
                skill: enemysk,
                damage: edamage,
              });

              enemy = eattack.attack;
              player = eattack.defence;
            }

            if( mov!=2 ){
              spdevent(true);
            }
          }
        }
        spdevent();

        let joutai = (options) => {
          let ainfo = options.info;
          let attack = options.attack;

          let amanojaku = attack.eff.find(e => e[0]=="天邪鬼");

          attack.eff.forEach((e,i) => {
            let effect = ef.find(f => f.name==e[0]);
            if( !effect ) return;
            let level = effect.effects.choice(e[1]-1);

            if( !effect.end || (effect.end && !(e[2]-1)) ){
              let heal = Math.round(attack.mhp * level.heal);
              let damage = Math.round(attack.mhp * level.damage);

              if( attack.hp>=1 ){
                if( amanojaku ){
                  if( heal ){
                    attack.hp -= heal;
                    description += code(`+ ${ainfo.name}は${e[0]}で${heal}ダメージ受けた！`, "diff");
                  }
                  if( damage ){
                    attack.hp += damage;
                    description += code(`- ${ainfo.name}は${e[0]}で体力を${damage}回復した！`, "diff");
                  }
                }else{
                  if( heal ){
                    attack.hp += heal;
                    description += code(`+ ${ainfo.name}は${e[0]}で体力を${heal}回復した！`, "diff");
                  }
                  if( damage ){
                    attack.hp -= damage;
                    description += code(`- ${ainfo.name}は${e[0]}で${damage}ダメージ受けた！`, "diff");
                  }
                }
              }
            }
            e[2]--;
            if( !e[2] ) {
              description += code(`${ainfo.name}の「${e[0]}」状態が切れた！`, "fix");
              attack.eff[i] = null;
            }else{
              attack.eff[i] = e;
            }
          });

          attack.eff = attack.eff.filter(e => !!e);

          return {
            ainfo: ainfo,
            attack: attack,
          }
        }

        let imov = 0;
        let spdevent2 = (ae) => {
          imov++;
          let cm = ae ? !jun : jun;
          if( cm ){
            if( player.hp<=0 ){
              return;
            }
            let pevent = joutai({
              info: tinfo,
              attack: player,
            });

            player = pevent.attack;

            if( imov!=2 ){
              spdevent2(true);
            }
          }else{
            if( enemy.hp<=0 ){
              return;
            }
            let eevent = joutai({
              info: einfo,
              attack: enemy,
            });

            enemy = eevent.attack;

            if( imov!=2 ){
              spdevent2(true);
            }
          }
        }
        spdevent2();

        if( enemy.hp<=0 ){
          if( einfo.next ){
            description += code(`- ${einfo.next.msg}`, "DIFF");
          }else{
            description += code(`\` ${einfo.name}を倒した！`, "JS");
          }
        }else if( player.hp<=0 ){
          description += code(`${username}は倒れた…`, "BrainFuck");
        }

        let efflistup = (a) => {
          let list = [];
          a.forEach(e => {
            list.push(`[${e[0]}] Lv.${e[1]} (${e[2]} left)`);
          });
          return list.join("\n");
        }

        let php = `[体力] ${player.hp}/${player.mhp}\n[魔力] ${player.mp}/${player.mmp}`;
        let pef = player.eff.length ? code(efflistup(player.eff), "CSS") : code(`状態異常なし`, "BrainFuck");
        let ehp = `[体力] ${enemy.hp}/${enemy.mhp}\n[魔力] ${enemy.mp}/${enemy.mmp}`;
        let eef = enemy.eff.length ? code(efflistup(enemy.eff), "CSS") : code(`状態異常なし`, "BrainFuck");

        ctrl.write(d.channel.id, r.ots(enemy));
        ctrl.write(d.user.id, r.ots(player));

        d.channel.send(embed({
          title: `Turn ${enemy.turn}`,
          description: description,
          fields: [{
            name: `${username}`,
            value: code(php, "CSS") + pef,
            inline: true,
          },{
            name: `${einfo.name}`,
            value: code(ehp, "CSS") + eef,
            inline: true,
          }],
        })).then(() => {
          if( enemy.hp>=1 ) AWAIT.set(d.channel.id, false);
        });

        if(act.macro){
          if( act.macro.indexOf(true)!=-1 ){
            return penaltyevent();
          }
        }
        if( enemy.hp<=0 ){
          if( einfo.next ){
            fn(d, {
              do: "rs",
              absolute: true,
              summon: einfo.next.code,
            });
            return;
          }

          result(d, [einfo, enemy, player]);
        }
        if( player.hp && moved ){
          d.channel.send(embed({
            description: `${d.user}は${enemy.f}に吸い込まれた！`,
          }));
          fn(d, {
            do: "rs",
            absolute: true,
          });
          return;
        }
        break;
      case "sl":
        (() => {
        let slist = [];
        learnable.forEach(s => {
          let tgsk = sk.find(a => a.name==s.name);
          if( !tgsk )return;
          if( player.sk.indexOf(s.name)!=-1 ){
            let selfs = [];
            let effects = [];
            let pairs = [];
            tgsk.self.forEach(e => {
              selfs.push(`[自:${e[0]}] Lv.${e[1]} ${e[2]}ターン ${e[3]}%`);
            });
            tgsk.effect.forEach(e => {
              effects.push(`[敵:${e[0]}] Lv.${e[1]} ${e[2]}ターン ${e[3]}%`);
            });
            tgsk.pair.forEach(e => {
              pairs.push(`[解:${e[0]}] Lv.${e[1]} ${e[2]}%`);
            });
            let e1 = selfs.length ? `\n${selfs.join("\n")}` : "";
            let e2 = effects.length ? `\n${effects.join("\n")}` : "";
            let e3 = pairs.length ? `\n${pairs.join("\n")}` : "";

            slist.push(code(`[${tgsk.name}] ${tgsk.zokusei}属性 ${["物理", "魔法", "特殊"][tgsk.type]} 威力${tgsk.atk} 魔力${tgsk.mp} 習得Lv.${tgsk.learn}\n${tgsk.des}${e1||e2||e3 ?"\n":""}${e1}${e2}${e3}`, "CSS"));
          }else{
            slist.push(code(`[${"？".repeat(s.name.length)}] ${tgsk.zokusei}属性 ${["物理", "魔法", "特殊"][tgsk.type]} 威力？ 魔力？ 習得Lv.${tgsk.learn}`, "BrainFuck"));
          }
        });
        let pages = [];
        let pnum = Math.ceil(slist.length/5);

        for(var i=0;i<pnum;i++){
          pages.push(embed({
            description: code(`${username}の技一覧 [${i+1} / ${pnum}]`, "CSS") + slist.slice( (i*5), 5 + (i*5) ).join(""),
          }));
        }

        ctrl.page(d.channel, d.user, pages);
        })(); 
        break;
      case "ml":
        (() => {
        let nfi = en.filter(e => e.field.indexOf(enemy.f)!=-1);
        if( !nfi.length ){
          return d.channel.send(embed({
            description: "このフィールドには敵がいないようです。",
          }));
        }
        let elist = [];
        nfi.forEach((e) => {
          let text = code(`[${e.name}] ${e.zokusei}属性 レア度:${["★☆☆", "★★☆", "★★★", "★★☆", "★★★★"][e.rare]}`, "CSS");

          if( e.rare==2 || e.rare==4 ) text = `||${text}||`;

          elist.push(text);
        });

        let pages = [];
        let pnum = Math.ceil(elist.length/5);
        for(var i=0;i<pnum;i++){
          pages.push(embed({
            description: code(`${enemy.f}の出現モンスター一覧 [${i+1} / ${pnum}]`, "CSS") + elist.slice( (i*5), 5 + (i*5) ).join(""),
            footer: {
              text: "レア度3以上のモンスターはスポイラーを開くと見ることが出来ます。",
            },
          }));
        }

        ctrl.page(d.channel, d.user, pages);
        })();
        break;
      case "u":
        if( player.hp<=0 ){
          return d.channel.send(embed({
            description: `${d.user}さんは既にやられています...`,
          }));
        }

        let target = act.target || d.user;
        if( !target )return;

        let iname = im[act.item];
        if( !iname )return;

        if( !player.i[act.item] ){
          return d.channel.send(embed({
            description: `${d.user}さんは${iname}を持っていません。`,
          }));
        }

        ctrl.read(target.id).then(ud => {
          if( !ud )return;
          let userdata = r.sto(ud.name);

          player.i[act.item]--;
          let msg = [];
          msg.push(`${d.user}は\`${iname}\`を使った！`);

          let hheal = 0;
          let mheal = 0;
          let aeff = [];
          let canheal = false;
          let pet = false;

          switch(act.item){
            case "p":
              hheal += Math.round(userdata.mhp * (1/5));
              break;
            case "t":
              mheal += Math.round(userdata.mmp * (1/4));
              break;
            case "e":
              canheal = true;
              hheal = userdata.mhp;
              break;
            case "c":
              pet = true;
              break;
            case "i":
              if( userdata.eff.find(f => f[0] == "透明化") ) return;
              msg.push(`${target}は透明化状態になった！`);

              aeff.push(["透明化", 1, 8]);
              break;
            case "a":
              msg.push(`${target}はダメージ上昇状態を得た！`);

              aeff.push(["ダメージ上昇", 3, 8]);
              break;
            case "d":
              msg.push(`${target}はダメージ軽減状態を得た！`);

              aeff.push(["ダメージ軽減", 3, 8]);
              break;
          }

          if( userdata.hp<=0 && !canheal ){
            return d.channel.send(embed({
              description: `${target}さんは既にやられています...`,
            }));
          }

          if( target.id==d.user.id ){
            if( pet ){
              msg.push(`${einfo.name}はチョコレートを食べている...`);
              if( !enemy.pet ) enemy.pet = [];
              enemy.pet.push(d.user.id);
            }
            if( hheal ){
              msg.push(`${target}の体力が\`${hheal}\`回復した！`);
              player.hp += hheal;
            }
            if( mheal ){
              msg.push(`${target}の魔力が\`${mheal}\`回復した！`);
              player.mp += mheal;
            }
            if( aeff.length ){
              aeff.forEach(a => {
                player.eff.push(a);
              });
            }
            if( player.hp>=player.mhp ) player.hp = player.mhp;
            if( player.mp>=player.mmp ) player.mp = player.mmp;
          }else{
            if( pet ){
              msg.push(`${target}は再生状態を得た！`);
              userdata.eff.push(["再生", 2, 25]);
            }
            if( hheal ){
              msg.push(`${target}の体力が\`${hheal}\`回復した！`);
              userdata.hp += hheal;
            }
            if( mheal ){
              msg.push(`${target}の魔力が\`${mheal}\`回復した！`);
              userdata.mp += mheal;
            }
            if( aeff.length ){
              aeff.forEach(a => {
                userdata.eff.push(a);
              });
            }
            if( userdata.hp>=userdata.mhp ) userdata.hp = userdata.mhp;
            if( userdata.mp>=userdata.mmp ) userdata.mp = userdata.mmp;

            ctrl.write(ud.id, r.ots(userdata));
          }
          ctrl.write(d.user.id, r.ots(player));
          ctrl.write(d.channel.id, r.ots(enemy));

          d.channel.send(embed({
            description: msg.join("\n")
          }));
        });
        break;
      case "sh":
        (() => {
        let description = [];
        description.push(code(`[番号] [個数] で購入　例： 1 10`, "CSS"));

        let table = sh.find(s => s.field == enemy.f);
        if( !table ){
          return d.channel.send(embed({
            description: "このフィールドにはショップがないようです。",
          }));
        }
        table.item.forEach((t,i) => {
          description.push(code(`[${i+1}] ${im[t.id]} - ${t.price} ギル`, "CSS"));
        });

        let embed1 = embed({
          title: `${enemy.f}のショップ`,
          description: description.join(""),
          footer: {
            text: `操作制限時間：90秒|「cancel」で終了`,
          },
        });

        ctrl.collector({
          channel: d.channel,
          user: d.user,
          msg: embed1,
          time: 90000,
          undelete: !d.guild,
          collect: (M, C) => {
            if(!M.content)return;
            let len = separate(M.content);
            let count = [Math.floor(parseInt(len[0])), Math.floor(parseInt(len[1]))];
            let itarget = table.item[count[0]-1];

            if( count.find(c => !c || c<0) || !itarget )return;
            
            let price = itarget.price * count[1];
            let iname = im[itarget.id];

            if( price<=player.g ){
              M.delete();

              if( itarget.id=="exp" ){
                let exp = 0;
                for(var i=0;i<count[i];i++){
                  exp += r.random( Math.round(itarget.price/2), Math.round(itarget.price/1.5) );
                }
                player.xp += exp;
                player.g -= price;

                d.channel.send(embed({
                  description: `${d.user}は「${iname}」を買いました！\n${exp}の経験値を手に入れた！(次戦闘後から反映)\n(所持金：${player.g})`,
                })).then(ctrl.delete);
              }else{
                player.i[itarget.id] += count[1];
                player.g -= price;

                d.channel.send(embed({
                  description: `${d.user}は「${iname}」を${count[1]}個買いました！\n(所持金：${player.g})`,
                })).then(ctrl.delete);
              }

              ctrl.write(d.user.id, r.ots(player));
            }
          },
        });
        })();
        break;
      case "gv":
        (() => {
        let target = act.target;
        if( !target )return;

        if( d.user.id == target.id ){
          return d.channel.send(embed({
            description: `${d.user}さん、自分自身にアイテムを渡すことはできません。`,
          }));
        }

        let gitem = im[act.item];
        let isg = act.item=="ギル";
        let length = act.length;

        let has = (isg ? player.g : player.i[act.item]) >= length;
        if( !has ){
          return d.channel.send(embed({
            description: `${d.user}さんは${isg ? "ギル" : gitem}を${length}個も持っていません！`,
          }));
        }

        ctrl.read(target.id).then(ud => {
          if( !ud )return;

          let userdata = r.sto(ud.name);
          if( isg ){
            player.g -= length;
            userdata.g += length;
          }else{
            player.i[act.item] -= length;
            userdata.i[act.item] += length;
          }

          d.channel.send(embed({
            description: `${d.user}さんは<@!${ud.id}>さんに${isg ? "ギル" : gitem}を${length}個渡しました。`,
          }));

          ctrl.write(d.user.id, r.ots(player));
          ctrl.write(ud.id, r.ots(userdata));
        });
        })();
        break;
      case "dc":
        if( einfo.noescape ){
          return d.channel.send(embed({
            description: code(`- ${einfo.noescape.msg}`, "DIFF"),
          }));
        }
        
        let gd = [];
        let difs = [];
        df.forEach(d => {
          if( enemy.lv >= d.require[0] ){
            gd.push(d.name);
            difs.push(code(`[${d.name}] 必要Lv.${d.require[0]}\n${d.des}\n[変更可能]`, "CSS"));
          }else{
            difs.push(code(`[${d.name}] 必要Lv.${d.require[0]}\n${d.des}\n[未開放]`, "BrainFuck"));
          }
        });

        if( gd.length ){
          let embed3 = embed({
            title: `難易度変更`,
            description: code(`[難易度] を宣言で難易度を変更　cancelでキャンセル`, "CSS") + difs.join(""),
          });

          ctrl.collector({
            channel: d.channel,
            user: d.user,
            msg: embed3,
            undelete: !d.guild,
            collect: (M,C) => {
              let dname = String(M.content).toUpperCase();
              if( gd.indexOf(dname)!=-1 ) {
                C.stop();

                enemy.d = dname;
                d.channel.send(embed({
                  description: `難易度を「${dname}」に変更しました！`,
                }));

                ctrl.write(d.channel.id, r.ots(enemy));

                fn(d, {
                  do: "rs",
                  absolute: true,
                });
              }
            },
          });
        }
        break;
      case "fc":
        if( einfo.noescape ){
          return d.channel.send(embed({
            description: code(`- ${einfo.noescape.msg}`, "DIFF"),
          }));
        }
        let requires = fr.filter(f => f.require.indexOf(enemy.f)!=-1);
        let fields = [];
        let move = [];

        requires.forEach(r => {
          let elv = r.elv <= enemy.lv;
          let plv = r.plv <= player.lv;
          if( elv && plv ){
            move.push(r.name);
            fields.push(code(`[${r.name}] 敵Lv.${r.elv} 解放者Lv.${r.plv}\n条件が揃っています。`, "CSS"));
          }else{
            fields.push(code(`[？？] 敵Lv.${r.elv} 解放者Lv.${r.plv}\n条件が揃っていません。`, "CSS"));
          }
        });

        if( move.length ){
          let embed2 = embed({
            title: `移動`,
            description: code(`[フィールド名] を宣言で移動　cancelでキャンセル`, "CSS") + fields.join(""),
          });

          ctrl.collector({
            channel: d.channel,
            user: d.user,
            msg: embed2,
            undelete: !d.guild,
            collect: (M,C) => {
              let fname = M.content;
              if( move.indexOf(fname)!=-1 ) {
                C.stop();

                enemy.f = fname;
                d.channel.send(embed({
                  description: `「${fname}」に移動しました！`,
                }));

                ctrl.write(d.channel.id, r.ots(enemy));

                fn(d, {
                  do: "rs",
                  absolute: true,
                });
              }
            },
          });
        }else{
          d.channel.send(embed({
            title: `移動`,
            description: code(`現在、移動できるフィールドはありません。`, "BrainFuck") + fields.join(""),
          })); 
        }
        break;
      case "fx":
        AWAIT.set(d.channel.id, false);
        d.channel.send(embed({
          description: `連続攻撃制限状態を解除しました。`,
        }));
        break;
      case "rs":
        if( !enemy.n.length && !act.absolute ){
          return;
        }

        if( enemy.n.length ){
          ctrl.multi(enemy.n, id => ctrl.read(id)).then(source => {
            source.forEach(userdata => {
              if( !userdata )return;
              let ud = r.sto(userdata.name);
   
              ud.n = "0";
              ud.hp = ud.mhp;

              ud.eff = ud.eff.filter(e => resetable.indexOf(e[0])==-1);
 
              ctrl.write(userdata.id, r.ots(ud));
            });
          });
        }

        let next = (() => {
          if( einfo.rare!=0 || act.absolute ){
            let sel = en.filter(e => e.field.indexOf(enemy.f)!=-1 && e.rare==0);
            let sel2 = en.find(e => e.name==act.summon||e.code==act.summon);
            if( !sel ) return enemy;
            let select = sel2 || sel.choice();
            let hp = Math.round(((enemy.lv*select.hp*10)+fix.enemy) * (ddata ? ddata.hp : 1));
            return {
              f: enemy.f,
              c: select.code,
              lv: enemy.lv,
              mhp: hp,
              hp: hp,
              mmp: select.mp,
              mp: select.mp,
              eff: select.effect,
              n: [],
              cs: d.channel.id,
              d: enemy.d,
            };
          }
          return enemy;
        })();
        let ninfo = en.find(e => e.code==next.c);

        ctrl.write(d.channel.id, r.ots({
          f: next.f,
          c: next.c,
          lv: next.lv,
          mhp: Math.round(next.mhp),
          hp: Math.round(next.mhp),
          mmp: Math.round(next.mmp),
          mp: Math.round(next.mmp),
          eff: ninfo.effect,
          n: [],
          cs: d.channel.id,
          d: enemy.d,
        })).then(() => {
          d.channel.send(embed({
            description: code(`${ninfo.name}が現れた！`, ["C", "fix", "fix", "fix"][ninfo.rare])+code([`[レベル] ${next.lv}`, `[体力] ${next.mhp}`].join("\n"), "CSS"),
            image: {
              url: ninfo.pic,
            },
          })).then(() => {
            AWAIT.set(d.channel.id, false); 
          });
        });
        return;
      case "rk":
        db.serialize(() => {
          db.all("SELECT * FROM mmo_channel", (err, row) => {
            if( err ) return console.error(err);
            if( !row ) return;

            let sort = [];
            row.forEach((o) => {
              if( !o )return;
              sort.push({
                id: o.id,
                name: r.sto(o.name), 
              });
            });

            sort = sort.sort((a,b) => b.name.lv - a.name.lv);
            let ranks = [];
            let index = 0;
            let channels = ea.channels.array();

            sort.forEach((s,i) => {
              let channel = ea.channels.get(s.name.cs) || ea.channels.get( ctrl.nearest(s.id, channels) );

              if( channel.guild ){
                if( ranks.find(a => a.id == channel.guild.id) )return;
                if( channel.id != s.name.cs ) return;
                index++;
                ranks.push({
                  id: channel.guild.id,
                  text: `[${index}位] ${channel.guild.name} (Lv.${s.name.lv})`,
                  lv: s.name.lv,
                });
              }else{
                if( ranks.find(a => a.id == channel.id) )return;
                if( channel.id != s.name.cs ) return;

                index++;
                let who = ctrl.whodm(channel.id);
                let name = who ? `[${who.tag}'s DM Channel]` : `[Unknown]`;
                ranks.push({
                  id: channel.id,
                  text: `[${index}位] ${name} (Lv.${s.name.lv})`,
                  lv: s.name.lv,
                });
              }
            });

            let pages = [];
            ranks.slice(0, 10).forEach((r) => {
              pages.push(r.text);
            });

            d.channel.send(embed({
              description: code(pages.join("\n"), "CSS"),
            }));
          });
        });
        break;
      case "inv":
        let itemlist = (() => {
          let b = [];
          for(let a in player.i){
            let ides = ii.find(f => f.id==a);
            if(player.i[a] && ides) b.push(code(`[${ides.name}] 所持数：${player.i[a]}個\n${ides.des}\n(短縮：${a})`, "CSS"));
          }
          return b;
        })();
        if( !itemlist.length ){
          itemlist.push(code(`あなたはアイテムを持っていません。`));
        } 
        d.channel.send(embed({
          description: itemlist.join(""),
        }));
        break;
      case "st":
        let items = (() => {
          let b = [];
          for(let a in player.i){
            if(player.i[a]) b.push(`${im[a]} : ${player.i[a]}個`);
          }
          return b;
        })();

        let stevt = (rank) => {
          let air = "** **";
          let description = code(`[USER STATUS] ${username}`, "fix");
          let status = code([rank ? `[順位] ${rank}位` : "", `[レベル] ${player.lv}`, `[経験値] ${player.xp}`, `[ギル] ${player.g}`, `[体力] ${player.hp}/${player.mhp}`, `[魔力] ${player.mp}/${player.mmp}`, `[攻撃力] ${Math.round(player.mhp/3)}`, `[敏捷力] ${player.spd*player.lv}`].join("\n"), "CSS");
          let item = code([`[アイテム]`].concat(items).join("\n"), "CSS");
          let eff = code(`状態異常なし`, "BrainFuck");
          if( player.eff.length ){
            let list = [];
            player.eff.forEach(e => {
              list.push(`[${e[0]}] Lv.${e[1]} (${e[2]} left)`);
            });
 
            eff = code(list.join("\n"), "CSS");
          }

          d.channel.send(embed({
            description: description,
            fields: [{
              name: air,
              value: status,
              inline: true,
            },{
              name: air,
              value: item,
              inline: true,
            },{
              name: air,
              value: eff,
            }],
            thumbnail: {
              url : d.user.avatarURL,
            },
            footer: {
              text: r.date(),
              icon: ea.user.avatarURL || ea.user.defaultAvatarURL,
            },
          }));
        };

        ctrl.prank(d.user.id).then(rank => {
          stevt(rank);
        }).catch(() => {
          stevt();
        })
        break;
      case "pst":
        let pinfo = player.p[0];
        if( !pinfo ){
          return d.channel.send(embed({
            description: `${d.user}さんはペットを飼っていません...`,
          }));
        }
        (() => {
        let ainfo = ab.find(a => a.name==pinfo.a) || ab[0];
        let xinfo = en.find(e => e.code==pinfo.c);
        let oinfo = ab.find(a => a.name==xinfo.only) || ab[0];
        if( !ainfo || !xinfo ) return;
        let description = code(`[PET STATUS] ${username}`, "fix");
        description += code([`[名前] ${pinfo.n}`, `[種族] ${xinfo.name}`, `[レベル] ${pinfo.lv}`, `[経験値] ${pinfo.xp}`, `[属性] ${xinfo.zokusei}`, `[個体能力] ${pinfo.a} #${ainfo.rare}`, ` - ${ainfo.des}`, `[固有能力] ${oinfo.name}`, ` - ${oinfo.des}`, `[攻撃確率] ${pinfo.p}%`].join("\n"), "CSS");

        d.channel.send(embed({
          description: description,
          thumbnail: {
            url: xinfo.pic,
          },
          footer: {
            text: r.date(),
          },
        }));
        })();
        break;
      case "pex":
        (() => {
        let pinfo = player.p[0];
        if( !pinfo ){
          return d.channel.send(embed({
            description: `${d.user}さんはペットを飼っていません...`,
          }));
        }
        switch(act.cmd){
          case "ren":
          case "rename":
            let name = act.args.join(" ");
            if( !name ) return;
            if( String(name).length>50 ){
              return d.channel.send(embed({
                description: `${d.user}さん、ペットの名前は50文字までにしてください。`,
              }));
            }
            if( String(name).match(/\n/) ){
              return d.channel.send(embed({
                description: `${d.user}さん、ペットの名前に改行を含めることはできません。`,
              }));
            }

            ctrl.react({
              reactions: ["👍", "👎"],
              channel: d.channel,
              user: d.user,
              msg: embed({
                description: `\`${pinfo.n}\`の名前を\`${name}\`に変更していいですか？`,
              }),
              undelete: !d.guild,
              event: (m,a,i) => {
                switch(i){
                  case 0:
                    d.channel.send(embed({
                      description: `\`${pinfo.n}\`の名前を\`${name}\`に変更しました。`,
                    }));

                    REACTION.delete(m.id);
                    pinfo.n = name;
                    player.p[0] = pinfo;
                    ctrl.write(d.user.id, r.ots(player));
                    break;
                  default:
                    REACTION.delete(m.id);
                    d.channel.send(embed({
                      description: `\`${pinfo.n}\`の名前を変更しませんでした。`,
                    }));
                }
              }
            });
            break;
          case "rel":
          case "release":
            ctrl.react({
              reactions: ["👍", "👎"],
              channel: d.channel,
              user: d.user,
              msg: embed({
                description: `本当に\`${pinfo.n}\`を逃がしますか...？`,
              }),
              undelete: !d.guild,
              event: (m,a,i) => {
                switch(i){
                  case 0:
                    d.channel.send(embed({
                      description: `${d.user}は\`${pinfo.n}\`を逃がしました...`,
                    }));

                    REACTION.delete(m.id);
                    player.p[0] = undefined;
                    ctrl.write(d.user.id, r.ots(player));
                    break;
                  default:
                    REACTION.delete(m.id);
                    d.channel.send(embed({
                      description: `${d.user}は\`${pinfo.n}\`を逃がしませんでした。`,
                    }));
                }
              }
            });
            break;
        }
        })();
        break;
      case "cst":
        (() => {
        let description = code(`[CHANNEL STATUS] ${d.channel.name || username}`, "fix");
        description += code([`[フィールド] ${enemy.f}`, `[難易度] ${enemy.d || "NORMAL"}`, `[名前] ${einfo.name}`, `[レベル] ${enemy.lv}`, `[体力] ${enemy.hp}/${enemy.mhp}`, `[魔力] ${enemy.mp}/${enemy.mmp}`, `[攻撃力] ${Math.round((enemy.lv*10+fix.enemy)*einfo.atk)}`, `[敏捷力] ${einfo.spd*enemy.lv}`, `[属性] ${einfo.zokusei}`].join("\n"), "CSS");
        if( enemy.eff.length ){
          let list = [];
          enemy.eff.forEach(e => {
            list.push(`[${e[0]}] Lv.${e[1]} (${e[2]} left)`);
          });

          eff = code(list.join("\n"), "CSS");
        }else{
          description += code(`状態異常なし`, "BrainFuck");
        }

        d.channel.send(embed({
          description: description,
          footer: {
            text: r.date(),
          },
        }));
        })();
        break;
      case "pg":
        d.channel.send(embed({
          description: `:ping_pong: \`計測中...\``,
        })).then(M => {
          M.edit(embed({
            description: `:ping_pong: \`計測結果\``,
            fields: [{
              name: "ping",
              value: `:zero: \`${act.ping.createdTimestamp-act.msg.createdTimestamp} ms\`\n:one: \`${M.createdTimestamp-act.msg.createdTimestamp} ms\``,
            }],
          }));
          act.ping.delete();
        });
        break;
      case "md":
        switch(act.k){
          case "-addeff":
            let teff = ef.find(e => e.name==act.args[0]);
            if( !teff )return;
            let lv = parseInt(act.args[1]) || 1;
            let left = parseInt(act.args[2]) || 3;

            player.eff.push([teff.name, lv, left]);
            d.channel.send(embed({
              description: `${d.user}は管理者用コマンドで${teff.name}状態を付与しました。`,
            }));
            break;
          case "-remeff":
            player.eff = [];
            d.channel.send(embed({
              description: `${d.user}は管理者用コマンドで全状態異常を解除しました。`,
            }));
            break;
          case "-field":
            let fname2 = fi.find(f => f.name==act.args[0]);
            let dname2 = df.find(d => d.name==act.args[1]) || df[0];
            if( !fname2 )return;

            d.channel.send(embed({
              description: `\`[DEBUG]\`${act.args[0]}(\`DIF=${act.args[1] || "NORMAL"}\`)に移動します...`,
            }));

            enemy.f = act.args[0];
            enemy.d = dname2.name;
            ctrl.write(d.channel.id, r.ots(enemy));

            fn(d, {
              do: "rs",
              absolute:true,
            });
            break;
          case "-sum":
            let te = en.find(e => e.name==act.args[0]||e.code==act.args[0]);
            if(!te)return;

            d.channel.send(embed({
              description: `\`[DEBUG]\`${act.args[0]}を召還します...`,
            }));

            fn(d, {
              do: "rs",
              absolute:true,
              summon: act.args[0],
            });
            break;
          case "-g":
            let pg = parseInt(act.args[1]);
            if( !pg )return;
            ctrl.read(act.args[0]).then(m => {
              if( !m )return;
              if( m.id==d.user.id )return;
              let dt = r.sto(m.name);
  
              dt.g += pg;
  
              d.channel.send(embed({
                description: `${d.user}は<@!${m.id}>に\`${pg}\`ギルを付与した。`,
              }));
  
              ctrl.write(m.id, r.ots(dt));
            });
            break;
          case "-exp":
            let pexp = parseInt(act.args[1]);
            if( !pexp )return;
            ctrl.read(act.args[0]).then(m => {
              if( !m )return;
              if( m.id==d.user.id )return;

              let dt = r.sto(m.name);

              dt.xp += pexp;

              d.channel.send(embed({
                description: `${d.user}は<@!${m.id}>に\`${pexp}\`経験値を付与した。\n次戦闘後から反映されます。`,
              }));

              ctrl.write(m.id, r.ots(dt));
            })
            break;
          case "-isk":
            fn(d, {
              do: "isk",
              sk: sk.find(s => s.name==act.args[0]) || sk.find(s => s.name=="攻撃"),
            });
            break;
          case "-macro":
            let macroarr = [];
            let arnum = parseInt(act.args[0]);
            if( arnum<0 || isNaN(arnum) ) return;
            macroarr[arnum] = true;
            fn(d, {
              do: "sk",
              sk: sk.find(s => s.name==act.args[1]) || sk.find(s => s.name=="攻撃"),
              macro: macroarr,
            });
            break;
          case "-update":
            let ndch = ea.channels.find(c => c.type=="text"&&c.id==act.args[0]);
            if( !ndch )return;
            ctrl.read(act.args[0]).then(cd => {
              if( !cd ) return;
              let channeldata = r.sto(cd.name);
              let newd = {
                user: d.user,
                channel: ndch,
                member: d.member,
                guild: ndch.guild,
              };

              fn(newd, {
                do: "rs",
                absolute: true,
                summon: channeldata.c,
              });
            });
            break;
          case "-clist":
            ctrl.all("mmo_channel").then(row => {
              let srow = [];
              row.forEach(p => {
                if( !p ) return;
                srow.push({
                  id: p.id,
                  name: r.sto(p.name),
                });
              });

              srow = srow.sort((a,b) => b.name.lv - a.name.lv);

              let rlist = [];
              let channels = ea.channels.array();
              srow.forEach((p,i) => {
                if( !p )return;
                let data = p.name;
                let nearest = ea.channels.get(data.cs) || ea.channels.get(ctrl.nearest(Number(p.id), channels));

                if( nearest.id == data.cs ){
                  rlist.push(embed({
                    title: `[${i+1} / ${srow.length}]`,
                    description: code([`[ CHANNEL DATA No.${i+1} ]`, `[ID] チェックサム:${data.cs}\n取得:${p.id} 判定:${nearest.id} (CLEAR)`, `[フィールド] ${data.f}`, `[敵コード] ${data.c}`, `[レベル] ${data.lv}`, `[体力] ${data.hp} / ${data.mhp}`, `[サーバー] ${nearest.guild ? nearest.guild.name : "DM"}`].join("\n"), "CSS"),
                  }));
                }else{
                  rlist.push(embed({
                    title: `[${i+1} / ${srow.length}]`,
                    description: code(`[ CHANNEL DATA No.${i+1} ]\n[ID] チェックサム:${data.cs}\n取得:${p.id} 最近値:${nearest.id} (FAIL)\n[フィールド] ${data.f}\n[敵コード] ${data.c}\n[レベル] ${data.lv}\n[体力] ${data.hp} / ${data.mhp}\n[サーバー] ${nearest.guild ? nearest.guild.name : "DM"}\n\n[チェックサム通過失敗により無効です。]`, "CSS"),
                  }));
                }
              });

              ctrl.page(d.channel, d.user, rlist);
            });
            return;
          case "-plist":
            ctrl.all("mmo_user").then(row => {
              let srow = [];
              row.forEach(p => {
                if( !p ) return;
                srow.push({
                  id: p.id,
                  name: r.sto(p.name),
                });
              });

              srow = srow.sort((a,b) => b.name.xp - a.name.xp);
              let prank = [];
              let users = ea.users.array();
              srow.forEach((s,i) => {
                let user = ea.users.get(ctrl.nearest(s.id, users));
                let data = s.name;
                let pet = s.name.p[0];
                let petdata = "[ペットは飼っていないようです。]";
                if( pet ){
                  let petenemy = en.find(e => e.code==pet.c);
                  petdata = [`[ Pet Info ]`, `[名前] ${pet.n}`, `[種族] ${petenemy ? petenemy.name : "不明"}`, `[レベル] ${pet.lv}`, `[経験値] ${pet.xp}`, `[攻撃確率] ${pet.p}`].join("\n");
                }
                prank.push(embed({
                  title: `[${i+1} / ${srow.length}]`,
                  description: code([`[ User Info Name=${user ? user.tag : "[ID="+s.id+"]"} ]`, `[順位] ${i+1}位`, `[レベル] ${data.lv}`, `[体力] ${data.hp} / ${data.mhp}`, `[魔力] ${data.mp} / ${data.mmp}`].join("\n"), "CSS") + code(petdata, "CSS"),
                }));
              });

              ctrl.page(d.channel, d.user, prank);
            });
            break;
          case "-ban":
            let bantarget = ea.users.find(u => u.id==act.args[0]||u.tag==act.args[0]);
            if( !bantarget ) return;

            ctrl.read("0", "others").then(bans => {
              if( !bans ) bans = { list:[] };

              bans.list.push(bantarget.id);
              BANLIST.push(bantarget.id);
              d.channel.send(embed({
                description: `[DEBUG] \`${bantarget.tag}\`をBANしました。`,
              }));

              ctrl.write("0", r.ots(bans), "others");
            });
            return;
          case "-banlist":
            let blist = [];
            BANLIST.forEach((b,i) => {
              let user = ea.users.get(b);
              if( !user )return;
              blist.push(`[${i+1}] ${user.tag}`);
            });
            if( !blist.length ){
              blist.push(`[BAN中のユーザーはいません。]`);
            }
            d.channel.send(embed({
              description: code(blist.join("\n"), "CSS"),
            }));
            return;
          case "-eval":
            ctrl.collector({
              channel: d.channel,
              user: d.user,
              msg: embed({
                description: `[DEBUG] JavaScriptを実行します...`,
              }),
              time: 180000,
              collect: (m,c) => {
                if( !m.content )return;

                c.stop();

                let result;
                try{
                  result = eval(m.content);
                }catch(e){
                  result = e;
                }

                d.channel.send(embed({
                  description: `[DEBUG] Eval Result${code(result, "JS")}`,
                }));
              },
            });
            return;
        }
        ctrl.write(d.user.id, r.ots(player));
        break;
    }
  });
}

ea.on("ready", () => {
  ea.user.setPresence({
    game: {
      name: `Stopped Developing`,
    },
  });
  console.log("ea start...");
  ctrl.read("0", "others").then(bans => {
    if( !bans ) bans = { list:[] };

    bans.list.forEach(b => {
      BANLIST.push(b);
    });
  });
});

ea.on("messageReactionAdd", (Reaction, User) => {
  let Message = Reaction.message;
  let reactfn = REACTION.get(Message.id);

  if( typeof reactfn == "function" ){
    reactfn(Reaction, User, Message);
  }
});

ea.on("message", Message => {
  if( !Message.content.startsWith(prefix) ) return;
  const owner = ea.users.get(config.owner);
  const d = {
    channel : Message.channel,
    user: Message.author,
    guild: Message.guild,
    member: Message.member,
  }
  if( BANLIST.indexOf(d.user.id)!=-1 ) return console.log(`013: [User Banning Name=${d.user.tag}]`);

  let k = Message.content.split(/\s+/);
  let c = k[0].slice(prefix.length);
  k = k.slice(1);

  let isMacro = false;
  let isMacroRandom = REN.get(d.user.id) === 5;

  let MacroKenchi = (MACRO.get(d.user.id), false);
  if( MacroKenchi ){
    let search = mac(MacroKenchi[0], MacroKenchi[1]);
    let property = search.property;
    isMacro = search.result;
    let highA = search.array.lastIndex() * 4;
    let highB = Math.round((Message.createdTimestamp - MacroKenchi[2])/100)*100;
    console.log(`[${highA} <= ${highB}]`);
    if( highA <= highB ){
      console.log(`017: [No Count Value ${MacroKenchi[2]-Message.createdTimestamp} Name=${d.user.tag}]`);
    }else{
      let val = Message.createdTimestamp - MacroKenchi[2];
      MacroKenchi[0].push(val);
    }
    if( MacroKenchi[0].length>18 ){
      MacroKenchi[1] = MacroKenchi[0][0];
      MacroKenchi[0].shift();
    }
    MacroKenchi[2] = Message.createdTimestamp;
    
    if( property ){
      let heikin = Math.round(property.heikin/100)*100;
      let center = Math.round(property.center/100)*100;
      console.log(`[${d.user.tag}] (${property.min} -- ${property.max} | ${heikin} :: ${center})\n${property.rounded.join(" ")}\n${search.array.join(" ")}\n[REASON] ${search.reason}`);
      if( heikin==center ){
        REN.set(d.user.id, (parseInt(REN.get(d.user.id))||0)+1);
      }else{
        REN.set(d.user.id, 0);
      }
    }else{
      console.log(`[${d.user.tag}] (???? -- ???? | ???? :: ????)\n${search.array.join(" ")}\n[REASON] ${search.reason}`);
    }

    MACRO.set(d.user.id, MacroKenchi);
  }else{
    MACRO.set(d.user.id, [[Message.createdTimestamp], Message.createdTimestamp, Message.createdTimestamp]);
  }

  if( isMacro ){
    MACRO.set(d.user.id, [[Message.createdTimestamp], Message.createdTimestamp]);
    console.log(`[isMacro==true Name=${d.user.tag}]`);
  }
  if( isMacroRandom ){
    console.log(`[isMacroRandom==true Name=${d.user.tag}]`);
  }
  const awaitfn = () => {
    // console.log(`008: [Command Failed name=${d.user.tag}]`);
  }
  let macroarr = [false, isMacroRandom];

  switch(c){
    case "help":
      let commands = [
        "ehelp - このメッセージを表示します。",
        "eattack - 敵に攻撃します。\n(短縮：eatk)",
        "eskill [技名] - 技を発動します。\n(短縮：esk)",
        "euse [アイテム名] - アイテムを使用します。",
        "ewait - 「何もしない」をします。\n(短縮：ewt)",
        "einv - アイテムを確認します。\n(短縮：ei)",
        "estatus - 自分のステータスを表示します。\n(短縮：est)",
        "ecstatus - 戦場のステータスを表示します。\n(短縮：ecst)",
        "eskills - 習得した技一覧を表示します。\n(短縮：esl)",
        "ereset - 戦場をリセットします。\n(短縮：ere、ers)",
        "ego - フィールドを移動します。",
        "edchange - フィールドの難易度を変更します。\n(短縮：dc)",
        "eshop - アイテムを購入するショップ画面を開きます。",
        "emlist - フィールドに出る敵一覧を表示します。",
        "eranking - サーバーランキングを表示します。\n(短縮：erank)",
        "egive [アイテム] [個数] [@メンション]\n - 誰かにアイテムかギルを渡します。",
        "efix - 起動してるのにeatk出来なくなったら試してください。",
        "etips [番号] - このBotに関する豆(ほどもない)知識です。",
        `敵に「チョコレート」を与えて倒すと、\n敵が懐いてついてくることがあります。\nペットは敵の時と同じ技を使うことができます。\n`,
        "epstatus - ペットのステータスを確認します。\n(短縮：epst)",
        "erename - ペットの名前を変更します。\n(短縮：eren)",
        "erelease - ペットを逃がします。\n(短縮：erel)",
      ];
      let links = {
        name: "関連",
        value: `[サーバーに招待](${config.invite})`,
      };
      let footer = {
        text: `開発：Unknown｜現在日時：${r.date()}`,
      }
      let pages = [
        embed({
          title: "Extend Adventure",
          fields: [{
            name: `コマンド一覧 1ページ目 (戦闘関連)`,
            value: code(commands.slice(0,10).join("\n"), `CSS`),
          }, links],
          footer: footer,
        }),
        embed({
          title: "Extend Adventure",
          fields: [{
            name: `コマンド一覧 2ページ目 (戦闘以外)`,
            value: code(commands.slice(10,18).join("\n"), `CSS`),
          }, links],
          footer: footer,
        }),
        embed({
          title: "Extend Adventure",
          fields: [{
            name: `コマンド一覧 3ページ目 (ペット)`,
            value: code(commands.slice(18,24).join("\n"), `CSS`),
          }, links],
          footer: footer,
        }),
      ];

      ctrl.page(d.channel, d.user, pages);
      break;
    case "wt":
    case "wait":
      if( AWAIT.get(d.channel.id) && macroarr.indexOf(true)==-1 ){
        return awaitfn();
      }
      AWAIT.set(d.channel.id, true);
      let dm = sk.find(s => s.name == "何もしない");
      fn(d, {
        do: "isk",
        sk: dm,
        macro: macroarr,
      });
      break;
    case "atk":
    case "attack":
      if( AWAIT.get(d.channel.id) && macroarr.indexOf(true)==-1 ){
        return awaitfn();
      }
      AWAIT.set(d.channel.id, true);
      let atk = sk.find(s => s.name == "攻撃");
      fn(d, {
        do: "sk",
        sk: atk,
        macro: macroarr,
      });
      break;
    case "i":
    case "inv":
      fn(d, {
        do: "inv",
      });
      break;
    case "st":
    case "status":
      fn(d, {
        do: "st",
      });
      break;
    case "pst":
    case "pstatus":
      fn(d, {
        do: "pst",
      });
      break;
    case "cst":
    case "cstatus":
      fn(d, {
        do: "cst",
      });
      break;
    case "sk":
    case "skill":
      if( AWAIT.get(d.channel.id) && macroarr.indexOf(true)==-1  ){
        return awaitfn();
      }
      let skill = sk.find(s => s.name == k[0]);
      if( !skill ) return awaitfn();
      AWAIT.set(d.channel.id, true);
      fn(d, {
        do: "sk",
        sk: skill,
        macro: macroarr,
      });
      break;
    case "sl":
    case "skills":
      fn(d, {
        do: "sl",
      });
      break;
    case "use":
      (() => {
      let sel = k[0];
      if( !im[sel] ){
        let search = (() => {
          for( let b in im ){
            if( im[b]==sel ){
              return b;
            }
          }
        })();

        if( !search )return;
        sel = search;
      }

      fn(d, {
        do: "u",
        item: sel,
        target: Message.mentions.users.first() || d.user,
      });
      })();
      break;
    case "give":
      if( !Message.guild )return;
      (() => {
      let sel = k[0];
      if( !im[sel] && sel!="ギル" ){
        let search = (() => {
          for( let b in im ){
            if( im[b]==sel ){
              return b;
            }
          }
        })();
    
        if( !search )return;
        sel = search;
      }

      let len = Math.floor(parseInt(k[1]));
      if( !len || len<1 )return;
    
      fn(d, {
        do: "gv",
        item: sel,
        length: len,
        target: Message.mentions.members.first() || d.member,
      });
      })();
      break;
    case "ren":
    case "rename":
    case "rel":
    case "release":
      fn(d, {
        do: "pex",
        cmd: c,
        args: k,
      });
      break;
    case "fix":
      fn(d, {
        do: "fx"
      });
      break;
    case "ml":
    case "mlist":
      fn(d, {
        do: "ml",
      });
      break;
    case "shop":
      fn(d, {
        do: "sh",
      });
      break;
    case "go":
      fn(d, {
        do: "fc",
      });
      break;
    case "dc":
    case "dchange":
      fn(d, {
        do: "dc",
      });
      break;
    case "rank":
    case "ranking":
      fn(d, {
        do: "rk",
      });
      break;
    case "re":
    case "rs":
    case "reset":
      fn(d, {
        do: "rs",
      });
      break;
    case "ping":
      d.channel.send(embed({
        description: `[PING]`,
      })).then(M => {
        fn(d, {
          do: "pg",
          msg: Message,
          ping: M,
        });
      });
      break;
    case "tips":
      d.channel.send(embed({
        title: "--- tips ---",
        description: `>>> ${ti.choice(parseInt(k[0])-1)}`,
      }));
      break;
    case "info":
      ctrl.multi(["mmo_user", "mmo_channel"], key => ctrl.all(key)).then((rows) => {
        if( rows.find(a => !a) )return;
        let info = [`[導入サーバー] ${ea.guilds.size}鯖`, `[認識しているユーザー] ${ea.users.size}人`, `[参加しているユーザー] ${rows[0].length}人`, `[認識しているチャンネル] ${ea.channels.size}個`, `[戦場の数] ${rows[1].length}個`];

        d.channel.send(embed({
          title: "EA Info",
          description: code(info.join("\n"), "CSS"),
        }));
      });
      break;
    case "mod":
      if( d.user.id != config.owner )return;

      fn(d, {
        do: "md",
        k: k[0],
        args: k.slice(1),
      });
      break;
  }
});

ea.login(config.token);