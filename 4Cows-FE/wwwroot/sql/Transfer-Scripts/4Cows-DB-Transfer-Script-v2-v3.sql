INSERT into 4cows_v3.Cow
select * from 4cows_v2.Cow;

insert into 4cows_v3.medicine
select * from `4cows_v2`.medicine;

insert into 4cows_v3.planned_claw_treatment
select * from `4cows_v2`.planned_claw_treatment;

insert into 4cows_v3.claw_treatment
select * from `4cows_v2`.claw_treatment;

INSERT INTO `4cows_v3`.wherehow (WhereHow_Name, ShowDialog)
SELECT DISTINCT
    cow_treatment.WhereHow AS WhereHow_Name,
    FALSE AS ShowDialog
FROM
    `4cows_v2`.cow_treatment AS cow_treatment
WHERE
    LOWER(cow_treatment.WhereHow) NOT LIKE '%LH%'
  AND LOWER(cow_treatment.WhereHow) NOT LIKE '%LV%'
  AND LOWER(cow_treatment.WhereHow) NOT LIKE '%RH%'
  AND LOWER(cow_treatment.WhereHow) NOT LIKE '%RV%'
  AND LOWER(cow_treatment.WhereHow) NOT LIKE '%Alle 4%'
  AND NOT EXISTS (
    SELECT 1
    FROM `4cows_v3`.wherehow AS existing
    WHERE LOWER(existing.WhereHow_Name) = LOWER(cow_treatment.WhereHow)
);

-- Alle möglichen Kombinationen für BOOL-Werte generieren und einfügen
INSERT INTO `4cows_v3`.udder (Quarter_LV, Quarter_LH, Quarter_RV, Quarter_RH)
SELECT lv, lh, rv, rh
FROM (
         SELECT b1.val AS lv, b2.val AS lh, b3.val AS rv, b4.val AS rh
         FROM (SELECT TRUE AS val UNION SELECT FALSE AS val) b1
                  CROSS JOIN (SELECT TRUE AS val UNION SELECT FALSE AS val) b2
                  CROSS JOIN (SELECT TRUE AS val UNION SELECT FALSE AS val) b3
                  CROSS JOIN (SELECT TRUE AS val UNION SELECT FALSE AS val) b4
     ) combinations;

#Insert
SET @WhereHowID_IntraZitzenal = (
    SELECT WhereHow_ID
    FROM 4cows_v3.wherehow
    WHERE LOWER(WhereHow_Name) = LOWER('IZ')
);

IF @WhereHowID_IntraZitzenal IS NULL THEN
    INSERT INTO 4cows_v3.wherehow (WhereHow_Name, ShowDialog)
    VALUES ('IZ', TRUE);
    SET @WhereHowID_IntraZitzenal = LAST_INSERT_ID();
END IF;

Insert into 4cows_v3.cow_treatment
SELECT
    cow_treatment.Cow_Treatment_ID,
    cow_treatment.Ear_Tag_Number,
    cow_treatment.Medicine_ID,
    cow_treatment.Administration_Date,
    cow_treatment.Medicine_Dosage,
    CASE
        -- Für bestimmte Werte "IntraZitzenal" verwenden
        WHEN cow_treatment.WhereHow REGEXP '(?i)(rh|rv|lh|lv|Alle 4)'
            THEN @WhereHowID_IntraZitzenal
        ELSE
            -- Andernfalls neuen Eintrag in WhereHow erstellen, wenn nicht vorhanden
            (
                SELECT WhereHow_ID
                FROM 4cows_v3.wherehow
                WHERE LOWER(WhereHow_Name) = LOWER(cow_treatment.WhereHow)
                LIMIT 1
            )
        END AS WhereHow_ID,
    Case
        WHEN cow_treatment.WhereHow REGEXP '(?i)(rh|rv|lh|lv|Alle 4)'
            THEN (
            CASE
                when cow_treatment.WhereHow REGEXP '(?i)(Alle 4)'
                    then(
                    SELECT UDDER_ID from 4cows_v3.udder where (Quarter_LH = True AND Quarter_LV = true AND Quarter_RH = true AND Quarter_RV = true)  LIMIT 1
                )
                ELSE(
                    SELECT UDDER_ID from 4cows_v3.udder where (Quarter_LH = cow_treatment.WhereHow LIKE '%LH%' AND Quarter_LV = cow_treatment.WhereHow LIKE '%LV%' AND Quarter_RH = cow_treatment.WhereHow LIKE '%RH%' AND Quarter_RV = cow_treatment.WhereHow LIKE '%RV%')  LIMIT 1
                )
                END
            )
        ELSE(    SELECT UDDER_ID
                 FROM `4cows_v3`.udder
                 WHERE (Quarter_LH = FALSE AND Quarter_LV = FALSE AND Quarter_RH = FALSE AND Quarter_RV = FALSE)
        )
        END AS COW_QUARTER_ID -- COW_QUARTER_ID bleibt NULL, da keine Details gegeben sind
FROM
    4cows_v2.cow_treatment;