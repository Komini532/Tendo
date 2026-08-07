-- Extend Adventure — 初期スキーマ (MySQL 9.7 / InnoDB / utf8mb4)
--
-- 旧実装は SQLite に 3 テーブルしか持たず、いずれも (id INT, name TEXT) という形で、
-- name 列には「eval 可能な JavaScript の文字列」が丸ごと入っていた。
--
--   mmo_user    プレイヤー
--   mmo_channel 戦場
--   mmo_others  id=0 の 1 行だけ。BAN リスト
--
-- eval によるデシリアライズは危険なうえ、集計もできない (ランキングは全件読み出し)。
-- ここでは中身を展開して正規化する。
--
-- ID について: Discord の snowflake は 64bit。旧実装は INT 列 + JS の Number (2^53) で
-- 精度が落ちており、それを誤魔化すために「一番近い ID を探す」処理まで入っていた。
-- BIGINT UNSIGNED なら欠落しないので、その回避策は移植しない。

CREATE TABLE IF NOT EXISTS players (
    user_id           BIGINT UNSIGNED NOT NULL,
    hp                BIGINT          NOT NULL DEFAULT 100,
    max_hp            BIGINT          NOT NULL DEFAULT 100,
    mana              BIGINT          NOT NULL DEFAULT 0,
    max_mana          BIGINT          NOT NULL DEFAULT 0,
    speed             DOUBLE          NOT NULL DEFAULT 1,
    level             INT             NOT NULL DEFAULT 1,
    experience        BIGINT          NOT NULL DEFAULT 0,
    gil               BIGINT          NOT NULL DEFAULT 0,
    -- 旧 n。戦闘中のチャンネル。旧仕様の "0" (非戦闘中) は NULL で表す。
    battle_channel_id BIGINT UNSIGNED     NULL,
    created_at        DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at        DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                          ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (user_id),
    -- /status の順位と /mod plist は経験値降順。旧実装は全件読んで JS 側で
    -- ソートしていたが、索引があれば SQL で解決できる。
    KEY ix_players_experience (experience DESC)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_bin;

-- 旧 sk。習得済みの技。
CREATE TABLE IF NOT EXISTS player_skills (
    user_id    BIGINT UNSIGNED NOT NULL,
    skill_name VARCHAR(64)     NOT NULL,
    PRIMARY KEY (user_id, skill_name),
    CONSTRAINT fk_player_skills_player FOREIGN KEY (user_id)
        REFERENCES players (user_id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_bin;

-- 旧 i。アイテム所持数。
CREATE TABLE IF NOT EXISTS player_items (
    user_id  BIGINT UNSIGNED NOT NULL,
    item_id  VARCHAR(8)      NOT NULL,
    quantity INT             NOT NULL DEFAULT 0,
    PRIMARY KEY (user_id, item_id),
    CONSTRAINT fk_player_items_player FOREIGN KEY (user_id)
        REFERENCES players (user_id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_bin;

-- 旧 eff。状態異常は配列の順序がそのまま表示順になるので slot で並びを保つ。
CREATE TABLE IF NOT EXISTS player_effects (
    user_id    BIGINT UNSIGNED NOT NULL,
    slot       INT             NOT NULL,
    name       VARCHAR(32)     NOT NULL,
    level      INT             NOT NULL,
    turns_left INT             NOT NULL,
    PRIMARY KEY (user_id, slot),
    CONSTRAINT fk_player_effects_player FOREIGN KEY (user_id)
        REFERENCES players (user_id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_bin;

-- 旧 p[0]。1 人 1 体まで。
CREATE TABLE IF NOT EXISTS pets (
    user_id       BIGINT UNSIGNED NOT NULL,
    name          VARCHAR(50)     NOT NULL DEFAULT '',
    enemy_code    VARCHAR(64)     NOT NULL DEFAULT '',
    level         INT             NOT NULL DEFAULT 1,
    experience    BIGINT          NOT NULL DEFAULT 0,
    attack_chance INT             NOT NULL DEFAULT 0,
    ability       VARCHAR(64)     NOT NULL DEFAULT '',
    PRIMARY KEY (user_id),
    CONSTRAINT fk_pets_player FOREIGN KEY (user_id)
        REFERENCES players (user_id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_bin;

-- 旧 mmo_channel。フィールド・難易度といった場の情報と、今出ている敵の情報が同居する。
-- 旧データ構造がそうなっているので、そのまま移す。
CREATE TABLE IF NOT EXISTS battles (
    channel_id          BIGINT UNSIGNED NOT NULL,
    field               VARCHAR(32)     NOT NULL DEFAULT '草原',
    difficulty          VARCHAR(16)     NOT NULL DEFAULT 'NORMAL',
    enemy_code          VARCHAR(64)     NOT NULL DEFAULT '',
    level               INT             NOT NULL DEFAULT 1,
    hp                  BIGINT          NOT NULL DEFAULT 0,
    max_hp              BIGINT          NOT NULL DEFAULT 0,
    mana                BIGINT          NOT NULL DEFAULT 0,
    max_mana            BIGINT          NOT NULL DEFAULT 0,
    -- 敵が入れ替わると 0 に戻る (旧実装が turn を含まないオブジェクトを書いていたため)。
    turn                INT             NOT NULL DEFAULT 0,
    -- 旧 cs。/mod clist の CLEAR / FAIL 表示に使う。
    checksum_channel_id BIGINT UNSIGNED     NULL,
    created_at          DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at          DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                            ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (channel_id),
    -- /ranking と /mod clist は敵レベル降順。
    KEY ix_battles_level (level DESC)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_bin;

CREATE TABLE IF NOT EXISTS battle_effects (
    channel_id BIGINT UNSIGNED NOT NULL,
    slot       INT             NOT NULL,
    name       VARCHAR(32)     NOT NULL,
    level      INT             NOT NULL,
    turns_left INT             NOT NULL,
    PRIMARY KEY (channel_id, slot),
    CONSTRAINT fk_battle_effects_battle FOREIGN KEY (channel_id)
        REFERENCES battles (channel_id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_bin;

-- 旧 n。この敵に手を出した人。撃破報酬の配布対象。
CREATE TABLE IF NOT EXISTS battle_participants (
    channel_id BIGINT UNSIGNED NOT NULL,
    user_id    BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (channel_id, user_id),
    CONSTRAINT fk_battle_participants_battle FOREIGN KEY (channel_id)
        REFERENCES battles (channel_id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_bin;

-- 旧 pet。チョコレートを食べさせた人。撃破時の「懐いた」抽選対象。
CREATE TABLE IF NOT EXISTS battle_chocolate_feeders (
    channel_id BIGINT UNSIGNED NOT NULL,
    user_id    BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (channel_id, user_id),
    CONSTRAINT fk_battle_chocolate_battle FOREIGN KEY (channel_id)
        REFERENCES battles (channel_id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_bin;

-- 旧 mmo_others の id=0 に入っていた { list: [...] }。
CREATE TABLE IF NOT EXISTS bans (
    user_id    BIGINT UNSIGNED NOT NULL,
    created_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (user_id)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_bin;
