/* ペットアビリティデータ
{
    name: アビリティ名 デフォルト=
    atk: ダメージ上昇補正 デフォルト=1
    repeat: 連続攻撃回数 デフォルト=1
    self: エフェクト効果 デフォルト=[]
    effect: エフェクト付与 デフォルト=[]
    rare: レアリティ デフォルト="EX"
    g: ギル補正 デフォルト=1
    appear: レア出現確率補正 デフォルト=1
}
*/

const ability = [
    {
        name: "なし",
        des: "-",
        rare: "EX",
    },
    {
        name: "不動",
        des: "特に何もしません。",
        rare: "N",
    },
    {
        name: "勇奮",
        des: "攻撃力が少し上昇します。",
        atk: 1.2,
        rare: "N",
    },
    {
        name: "荒くれ",
        des: "攻撃力がとても上昇します。",
        atk: 1.45,
        rare: "R",
    },
    {
        name: "チェーン",
        des: "2回攻撃が発生します。",
        repeat: 2,
        rare: "R",
    },
    {
        name: "ポイズンLv.1",
        des: "高確率でLv.1の毒状態を敵に与えます。",
        eff: [["毒", 1, 3, 60]],
        rare: "R",
    },
    {
        name: "ポイズンLv.2",
        des: "中確率でLv.2の毒状態を敵に与えます。",
        eff: [["毒", 2, 3, 40]],
        rare: "R",
    },
    {
        name: "マキナ",
        des: "中確率で時間停止を敵に与えます。",
        eff: [["時間停止", 1, 5, 40]],
        rare: "SR",
    },
    {
        name: "EXポイズンLv.3",
        des: "中確率でLv.3の猛毒状態を敵に与えます。",
        eff: [["猛毒", 3, 6, 50]],
        rare: "SR",
    },
    {
        name: "トリプルチェーン",
        des: "3回攻撃が発生します。",
        repeat: 3,
        rare: "SR",
    },
    {
        name: "フリーズ・ザ・ワールド",
        des: "中確率で凍結を敵に与えます。",
        eff: [["凍結", 2, 4, 50]],
        rare: "SR",
    },
    {
        name: "英雄",
        des: "攻撃力がかなり上昇します。",
        atk: 2.4,
        rare: "SR",
    },
    {
        name: "デッド・オア・ダイ",
        des: "超高確率で敵に即死を与えます。",
        eff: [["即死", 1, 1, 70], ["即死", 1, 1, 70]],
        rare: "UR",
    },
    {
        name: "狂運",
        des: "レア★2以上の出現確率が4倍になります。",
        rare: "UR",
        appear: 4,
    },
    {
        name: "破壊神",
        des: "攻撃力が異常なほど上昇します。",
        atk: 8.1,
        rare: "UR",
    },
    {
        name: "孔明の罠",
        des: "低確率で敵に即死を与えます。",
        eff: [["即死", 1, 1, 5]],
        rare: "EX",
    },
    {
        name: "最強の槍",
        des: "攻撃力が大幅に上昇します。",
        atk: 4,
        rare: "EX",
    },
];

ability.forEach((a,i) => {
    if(!a.name) a.name=null;
    if(!a.des) a.des="-";
    if(!a.atk) a.atk=1;
    if(!a.repeat) a.repeat=1;
    if(!a.self) a.self=[];
    if(!a.effect) a.effect=[];
    if(!a.rare) a.rare="EX";

    ability[i] = a;
});

module.exports = ability;