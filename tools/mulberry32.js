// mulberry32。C# 側 Tendo.Game.Engine.Mulberry32 と同一の実装。
// 差分テストで JS と C# に同じ乱数列を流すために使う。
function mulberry32(seed) {
  let a = seed >>> 0;
  return function () {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
module.exports = mulberry32;
