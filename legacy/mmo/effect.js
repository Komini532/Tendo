/* 状態異常情報
{
    name: 状態異常名 デフォルト=null
    emoji: 絵文字ID デフォルト=""
    effects: [{ (n番目がレベルn状態異常の効果)
        heal: ターンごとの体力回復比率(「1/32」なら1ターンに最大の1/32回復) デフォルト=0
        damage: ターンごとのダメージ比率(「1/32」なら1ターンに最大の1/32削る) デフォルト=0
        mheal: ターンごとの魔力回復比率 デフォルト=0
        atk: ダメージ上昇比率 デフォルト=1
        def: ダメージ軽減率上昇比率 デフォルト=1
        nomove: 行動不可能確率 デフォルト=0
    }],
    inv: 物理ダメージ無効 デフォルト=false
    ref: 魔法ダメージ反射 デフォルト=false
    nodamage: 無効にする属性 デフォルト=[]
    protect: 状態異常耐性 [状態異常名 ...] デフォルト=[]
    end: ターン切れ時に効果発動するモード デフォルト=false
}
*/

const effect = [
    {
        name: "毒",
        emoji: "613137409986658332",
        effects: [
            {
                damage: 1/32,
            },
            {
                damage: 1/28,
            },
            {
                damage: 1/24,
            },
            {
                damage: 1/20,
            },
            {
                damage: 1/16,
            },
            {
                damage: 1/12,
            },
            {
                damage: 1/10,
            },
            {
                damage: 1/8,
            },
            {
                damage: 1/6,
            },
            {
                damage: 1/4,
            },
            {
                damage: 1/3,
            },
            {
                damage: 1/2.5,
            },
            {
                damage: 1/2,
            },
            {
                damage: 1/1.5,
            },
            {
                damage: 1,
            },
        ],
        protect: ["毒"],
    },
    {
        name: "猛毒",
        emoji: "613137409986658332",
        effects: [
            {
                damage: 1/16,
            },
            {
                damage: 1/8,
            },
            {
                damage: 1/6,
            },
            {
                damage: 1/4,
            },
            {
                damage: 1/3,
            },
            {
                damage: 1/2,
            },
        ],
        protect: ["猛毒"],
    },
    {
        name: "火傷",
        emoji: "613138999099392016",
        effects: [
            {
                damage: 1/48,
                atk: 0.5,   
            },
            {
                damage: 1/32,
                atk: 0.5,
            },
            {
                damage: 1/24,
                atk: 0.5,   
            },
            {
                damage: 1/16,
                atk: 0.5,   
            },
            {
                damage: 1/12,
                atk: 0.5,   
            },
            {
                damage: 1/8,
                atk: 0.5,   
            },
            {
                damage: 1/6,
                atk: 0.5,   
            },
            {
                damage: 1/4,
                atk: 0.5,   
            },
        ],
        protect: ["火傷"],
    },
    {
        name: "死の宣告",
        emoji: "613138989318144000",
        effects: [
            {
                damage: 7/8,
            },
            {
                damage: 1,
            },
        ],
        end: true,
        protect: ["死の宣告"],
    },
    {
        name: "豊穣の宣告",
        emoji: "613138994796036097",
        effects: [
            {
                heal: 1,
            },
        ],
        end: true,
        protect: ["豊穣の宣告"],
    },
    {
        name: "毒耐性",
        protect: ["毒"],
    },
    {
        name: "猛毒耐性",
        protect: ["毒", "猛毒"],
    },
    {
        name: "火傷耐性",
        protect: ["火傷", "大火傷"],
    },
    {
        name: "宣告耐性",
        protect: ["死の宣告", "豊穣の宣告"],
    },
    {
        name: "再生",
        effects: [
            {
                heal: 1/32,
            },
            {
                heal: 1/24,
            },
            {
                heal: 1/16,
            },
            {
                heal: 1/12,
            },
            {
                heal: 1/10,
            },
            {
                heal: 1/8,
            },
            {
                heal: 1/6,
            },
            {
                heal: 1/4,
            },
            {
                heal: 1/2,
            },
        ],
        protect: ["再生"],
    },
    {
        name: "ダメージ上昇",
        effects: [
            {
                atk: 1.1,
            },
            {
                atk: 1.2,
            },
            {
                atk: 1.3,
            },
            {
                atk: 1.4,
            },
            {
                atk: 1.5,
            },
            {
                atk: 1.65,
            },
            {
                atk: 1.8,
            },
            {
                atk: 2,
            },
            {
                atk: 2.2,
            },
            {
                atk: 2.4,
            },
            {
                atk: 2.8,
            },
            {
                atk: 3,
            },
            {
                atk: 3.5,
            },
            {
                atk: 3.8,
            },
            {
                atk: 4.2,
            },
            {
                atk: 4.8,
            },
            {
                atk: 5.8,
            },
            {
                atk: 6.4,
            },
            {
                atk: 8,
            },
            {
                atk: 10,
            },
        ],
        protect: ["ダメージ上昇"],
    },
    {
        name: "ダメージ軽減",
        effects: [
            {
                def: 1.1,
            },
            {
                def: 1.2,
            },
            {
                def: 1.3,
            },
            {
                def: 1.4,
            },
            {
                def: 1.5,
            },
            {
                def: 1.65,
            },
            {
                def: 1.8,
            },
            {
                def: 2,
            },
            {
                def: 2.5,
            },
            {
                def: 3,
            },
            {
                def: 3.5,
            },
            {
                def: 4.2,
            },
            {
                def: 4.8,
            },
            {
                def: 5.2,
            },
            {
                def: 5.8,
            },
            {
                def: 6.4,
            },
            {
                def: 7,
            },
            {
                def: 8,
            },
            {
                def: 10,
            },
        ],
        protect: ["ダメージ軽減"],
    },
    {
        name: "ダメージ低下",
        effects: [
            {
                atk: 0.9,
            },
            {
                atk: 0.8,
            },
            {
                atk: 0.7,
            },
            {
                atk: 0.6,
            },
            {
                atk: 0.5,
            },
            {
                atk: 0.4,
            },
            {
                atk: 0.3,
            },
            {
                atk: 0.2,
            },
            {
                atk: 0.1,
            },
        ],
        protect: ["ダメージ低下"],
    },
    {
        name: "被ダメージ上昇",
        effect: [
            {
                def: 0.9,
            },
            {
                def: 0.8,
            },
            {
                def: 0.7,
            },
            {
                def: 0.6,
            },
            {
                def: 0.5,
            },
            {
                def: 0.4,
            },
            {
                def: 0.3,
            },
            {
                def: 0.2,
            },
            {
                def: 0.1,
            },
        ],
        protect: ["被ダメージ上昇"],
    },
    {
        name: "心無い幻想",
        protect: ["再生", "ダメージ軽減", "ダメージ上昇"],
    },
    {
        name: "即死",
        effects: [
            {
                damage: 1,
            },
        ],
        end: true,
    },
    {
        name: "落下",
        effects: [
            {
                damage: 0.1,
            },
            {
                damage: 1,
            },
        ],
        end: true,
    },
    {
        name: "自爆",
        effects: [
            {
                damage: 1,
            },
        ],
        end: true,
    },
    {
        name: "即死耐性",
        protect: ["即死", "自爆", "死の宣告"],
    },
    {
        name: "麻痺",
        effects: [
            {
                nomove: 5,
            },
            {
                nomove: 10,
            },
            {
                nomove: 15,
            },
            {
                nomove: 20,
            },
            {
                nomove: 30,
            },
            {
                nomove: 40,
            },
            {
                nomove: 50,
            },
            {
                nomove: 60,
            },
            {
                nomove: 70,
            },
            {
                nomove: 80,
            },
            {
                nomove: 90,
            },
            {
                nomove: 100,
            },
        ],
        protect: ["麻痺"],
    },
    {
        name: "石化",
        effects: [
            {
                nomove: 100,
                def: 50,
            },
            {
                nomove: 100,
                def: 40,
            },
            {
                nomove: 100,
                def: 30,
            },
            {
                nomove: 100,
                def: 25,
            },
            {
                nomove: 100,
                def: 20,
            },
            {
                nomove: 100,
                def: 15,
            },
            {
                nomove: 100,
                def: 10,
            },
            {
                nomove: 100,
                def: 5,
            },
            {
                nomove: 100,
                def: 3,
            },
        ],
        protect: ["石化", "半石化"],
    },
    {
        name: "半石化",
        effects: [
            {
                nomove: 50,
                def: 50,
            },
            {
                nomove: 50,
                def: 25,
            },
            {
                nomove: 65,
                def: 20,
            },
            {
                nomove: 75,
                def: 15,
            },
            {
                nomove: 80,
                def: 10,
            },
            {
                nomove: 85,
                def: 5,
            },
            {
                nomove: 90,
                def: 2.5,
            },
        ],
        protect: ["半石化"],
    },
    {
        name: "凍結",
        effects: [
            {
                nomove: 100,
                def: 15,
                damage: 0.05,
            },
            {
                nomove: 100,
                def: 15,
                damage: 0.08,
            },
            {
                nomove: 100,
                def: 15,
                damage: 0.1,
            },
            {
                nomove: 100,
                def: 15,
                damage: 0.15,
            },
            {
                nomove: 100,
                def: 15,
                damage: 0.2,
            },
        ],
        protect: ["凍結"],
    },
    {
        name: "時間停止",
        effects: [
            {
                nomove: 100,
            },
        ],
        protect: ["時間停止"],
    },
    {
        name: "麻痺耐性",
        protect: ["麻痺"],
    },
    {
        name: "凍結耐性",
        protect: ["凍結"],
    },
    {
        name: "石化耐性",
        protect: ["石化"],
    },
    {
        name: "透明化",
        inv: true,
    },
    {
        name: "魔法障壁",
        minv: true,
    },
    {
        name: "リフレク",
        ref: true,
    },
    {
        name: "ツインウォール",
        inv: true,
        minv: true,
    },
    {
        name: "天邪鬼",
    },
    {
        name: "浮遊",
        nodamage: ["地"],
        protect: ["落下"],
    },
    {
        name: "遮光",
        nodamage: ["光"],
    },
    {
        name: "陽極",
        nodamage: ["闇"],
    },
    {
        name: "理不尽体",
        effects: [
            {
                def: 1.33,
            },
        ],
        protect: ["即死", "石化", "凍結", "麻痺", "自爆", "ダメージ低下", "被ダメージ上昇"],
        nodamage: ["虚"],
    },
    {
        name: "完全耐性",
        protect: ["火傷", "毒", "猛毒", "麻痺", "石化", "凍結", "即死", "自爆", "死の宣告", "ダメージ低下", "被ダメージ上昇"],
    },
];

effect.forEach((e,i) => {
    if(!e.name) e.name="null";
    if(!e.protect) e.protect=[];
    if(!e.end) e.end=false;
    if(!e.inv) e.inv=false;
    if(!e.ref) e.ref=false;
    if(!e.nodamage) e.nodamage=[];

    if(!e.effects) e.effects = [{}];

    e.effects.forEach((eff,ind) => {        
        if(!eff.heal) eff.heal=0;
        if(!eff.damage) eff.damage=0;
        if(!eff.mheal) eff.mheal=0;
        if(!eff.atk) eff.atk=1;
        if(!eff.def) eff.def=1;
        if(!eff.nomove) eff.nomove=0;

        e.effects[ind] = eff;
    });

    effect[i] = e;
});

module.exports = effect;