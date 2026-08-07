const MacroResult = (a,b,c,d) => {
    return {
        result: a,
        reason: b,
        array: c || [],
        property: d,
    };
}

const hikaku = (a,b) => {
    if( [a,b].find(c => !Array.isArray(c)) )return false;
    for( var i=0;i<a.length;i++ ){
        if( a[i] != b[i] ) return false; 
    }
    return true;
}

const kenchi_repeat = (a,b,e) => {
    let result = [];
    for( var c=0;c<a.length;c++ ){
        let kaisu = Math.floor(b/a[c]) - 1;
        if( !kaisu ) kaisu=1;
        let res_rep = [];
        for( var d=0;d<kaisu;d++ ){
            let hik_a = e.slice(a[c]*(d+0), a[c]*(d+1));
            let hik_b = e.slice(a[c]*(d+1), a[c]*(d+2));
            let res = hikaku( hik_a, hik_b );
            res_rep.push(res);
        }
        result.push(res_rep);
    }
    return result;
}

const kenchi_random = (a) => {
    let max = 0;
    let min = 0;
    let heikin = 0;
    let center = 0;
    for(var i=0;i<a.length;i++){
        if( a[i]>max || !max ) max = a[i];
        if( a[i]<min || !min ) min = a[i];
        heikin += a[i];
    }
    heikin = Math.round(heikin / a.length);
    center = Math.round((max + min) / 2);
    return {
        max: max,
        min: min,
        heikin: heikin,
        center: center,
        rounded: a,
    }
}

const timestampExchange = (a,fir) => {
    let b = [];
    a.forEach((c,i) => {
        if( !i ) return b.push(c - fir);
        b.push(c - a[i-1]);
    });
    return b;
}

const findfalse = (a,b) => {
    let d = [];
    a.forEach(f => {
        d = d.concat(f);
    });
    let c = Math.floor(d.length/b)-1;
    let e = d.filter(g => g);

    // console.log(`必要一致数:${c} 配列:[${d.join(", ")}]`);

    return e.length>=c;
}

const macrofn = (val,fir,exc) => {
    let arr = val;
    if( exc ) arr = timestampExchange(val,fir);

    let max = 18;
    let keisoku = [2,3,5,6];

    let rounded = [];
    let rounded2 = [];
    arr.forEach(a => {
        rounded.push(Math.round(parseInt(a)/100) * 100);
        rounded2.push(Math.round(parseInt(a)/200) * 200);
    });

    if( rounded.length<max || rounded2.length<max ){
        return MacroResult(false, "18未満の判定回数", rounded2);
    }

    // console.log(`ex: [検知する配列]`);
    // console.log(rounded.join(","));

    // ランダムマクロ検知のための検証
    let res_random = kenchi_random(rounded);

    // 繰り返しを検知
    let res_repeat = kenchi_repeat(keisoku, max, rounded2);
    for( var i=0;i<res_repeat.length;i++ ){
        if( Array.isArray(res_repeat[i]) ){
            if( findfalse(res_repeat[i], 0.9) ){
                return MacroResult(true, `${keisoku[i]}回の動作の繰り返しを検知`, rounded2, res_random);
            }
        }
    }

    return MacroResult(false, "正常なプレイ", rounded2, res_random);
}

module.exports = macrofn;