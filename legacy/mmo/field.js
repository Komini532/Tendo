/* フィールド情報
{
    name: フィールド名
    exp: 経験値倍率
}
*/

const field = [
    {
        name:"草原",
        exp: 0.95,
    },
    {
        name:"洞窟",
        exp: 1.05,
    },
    {
        name:"氷原",
        exp: 0.95,
    },
    {
        name:"峡谷",
        exp: 1.05,
    },
    {
        name:"火山",
        exp: 1.15,
    },
    {
        name: "沼",
        exp: 1.1,
    },
    {
        name: "毒沼",
        exp: 1.15,
    },
    {
        name: "湖",
        exp: 1.2,
    },
    {
        name: "氷山",
        exp: 1.18,
    },
    {
        name: "天界",
        exp: 1.3,
    },
    {
        name: "冥界",
        exp: 1.25,
    },
    {
        name: "秘境",
        exp: 1.32,
    },
    {
        name: "地獄",
        exp: 1.25,
    },
    {
        name: "遺跡",
        exp: 1.32,
    },
    {
        name: "竜洞",
    },
    {
        name: "永遠悪夢",
        exp: 1.5,
    },
];

field.forEach((f,i) => {
    if(!f.name) f.name = "";
    if(!f.exp) f.exp = 1;

    field[i] = f;
});

module.exports = field;