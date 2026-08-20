const fd = require("./field.js");

const field = [];
fd.forEach((f,i) => {
    field.push(f.name);
});

/* ショップ情報
{
    field: フィールド名 デフォルト=草原
    item: [
        {
            id: アイテムID デフォルト=p
            price: 価値 デフォルト=1
        } ...
    ]
}
*/

const shop = [
    {
        field: "湖",
        item: [
            {
                id: "i",
                price: 5000,
            },
        ],
    },
    {
        field: "秘境",
        item: [
            {
                id: "c",
                price: 3000,
            },
        ],
    },
    {
        field: "永遠悪夢",
        item: [
            {
                id: "c",
                price: 3000,
            },
            {
                id: "a",
                price: 2000,
            },
        ],
    },
];

field.forEach(f => {
    if( !shop.find(s => s.name==f) ){
        shop.push({
            field: f,
        });
    }
});

shop.forEach((e,i) => {
    if( !e.item ){
    e.item = [
        {
            id: "p",
            price: 120,
        },
        {
            id: "t",
            price: 300,
        },
        {
            id: "e",
            price: 750,
        },
    ];
    }
    shop[i] = e; 
});

module.exports = shop;