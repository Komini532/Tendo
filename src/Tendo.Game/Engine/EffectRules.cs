using Tendo.Game.Master;
using Tendo.Game.State;

namespace Tendo.Game.Engine;

/// <summary>旧 <c>ea.js</c> 冒頭の小さなヘルパー群。</summary>
public static class EffectRules
{
    /// <summary>
    /// 旧 <c>isprotected(a, b)</c>。
    /// <code>
    /// const isprotected = (a,b) => {
    ///   let c=null;
    ///   a.forEach((d,e) => {
    ///     if( c )return;
    ///     let f = ef.find(g => g.name==d[0]);
    ///     if(!f)return;
    ///     if( f.protect.indexOf(b)!=-1 ) c = d[0];
    ///   });
    ///   return c;
    /// }
    /// </code>
    ///
    /// 付いている状態異常のうち、<paramref name="incoming"/> を防ぐものの名前を返す。
    /// 先頭から探して最初に見つかったものを返す (順序が表示に出る)。
    /// </summary>
    /// <returns>防いだ状態異常の名前。防げないなら null。</returns>
    public static string? FindProtector(
        MasterData data,
        IReadOnlyList<ActiveEffect> current,
        string incoming)
    {
        foreach (var active in current)
        {
            var definition = data.FindEffect(active.Name);
            if (definition is null)
            {
                continue;
            }

            if (definition.Protects.Contains(incoming, StringComparer.Ordinal))
            {
                return active.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// 旧 <c>skillselect(arr, nores)</c>。
    /// <code>
    /// const skillselect = (arr,nores) => {
    ///   let ski = null;
    ///   arr.forEach((s,i)=>{
    ///     if( ski )return;
    ///     if( r.random(1,100)&lt;=s.per ){ ski = s.name; }
    ///   });
    ///   return ski || (nores ? null : skillselect(arr));
    /// }
    /// </code>
    ///
    /// 先頭から順に確率判定し、最初に当たった技を使う。
    /// 誰も当たらなければ <paramref name="allowNoResult"/> が false のかぎり引き直す。
    ///
    /// 旧実装は再帰なので、全ての確率が 0 だと無限再帰でスタックを溢れさせる。
    /// 実データではそうならないが、落ちるより諦める方が安全なので上限を設ける。
    /// </summary>
    public static string? SelectSkill<T>(
        IRandomSource random,
        IReadOnlyList<T> candidates,
        Func<T, string> nameSelector,
        Func<T, int> percentSelector,
        bool allowNoResult = false)
    {
        const int maxAttempts = 1000;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            string? selected = null;

            foreach (var candidate in candidates)
            {
                // 旧 if(ski) return; — 空文字は falsy なので探索が続く点まで合わせる。
                if (!string.IsNullOrEmpty(selected))
                {
                    break;
                }

                if (JsMath.Random(random, 1, 100) <= percentSelector(candidate))
                {
                    selected = nameSelector(candidate);
                }
            }

            if (!string.IsNullOrEmpty(selected))
            {
                return selected;
            }

            if (allowNoResult)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>旧 <c>skillselect(einfo.skill)</c>。敵の行動抽選。</summary>
    public static string? SelectEnemySkill(
        IRandomSource random,
        IReadOnlyList<EnemySkillRef> skills)
        => SelectSkill(random, skills, s => s.Name, s => s.Percent);

    /// <summary>
    /// 旧 <c>ctrl.perchoice(a, b)</c>。
    /// 各要素に同じ確率 <paramref name="percent"/> を与えて 1 つ選ぶ。
    /// 引き直しはしない (誰も当たらなければ null)。
    /// チョコレートを与えた人から「懐く相手」を決めるのに使う。
    /// </summary>
    public static string? SelectByEqualChance(
        IRandomSource random,
        IReadOnlyList<string> candidates,
        int percent)
        => SelectSkill(random, candidates, c => c, _ => percent, allowNoResult: true);
}
