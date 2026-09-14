INSERT IGNORE INTO 4cows_v2.Medicine (Medicine_Name)
SELECT DISTINCT TRIM(Medicine_Name) AS Medicine_Name
FROM 4cows.Cow_Treatment;

-- Klauenbefunde. Seit der Migration AddClawFinding steht der Befund nicht mehr
-- als Freitext an der Behandlung, sondern als eigene Zeile - wie Medicine oben.
-- Ein leerer Befund heißt "an dieser Klaue nichts erfasst" und bekommt KEINE
-- Zeile; an der Behandlung bleibt die Spalte dann NULL.
INSERT IGNORE INTO 4cows_v2.Claw_Finding (Claw_Finding_Name)
SELECT DISTINCT name FROM (
              SELECT TRIM(Claw_Finding_LV) AS name FROM 4cows.Claw_Treatment
    UNION ALL SELECT TRIM(Claw_Finding_LH)         FROM 4cows.Claw_Treatment
    UNION ALL SELECT TRIM(Claw_Finding_RV)         FROM 4cows.Claw_Treatment
    UNION ALL SELECT TRIM(Claw_Finding_RH)         FROM 4cows.Claw_Treatment
) cells
WHERE name <> '';

-- Klauenbehandlungen
INSERT INTO 4cows_v2.Claw_Treatment (
    Ear_Tag_Number,
    Treatment_Date,
    Claw_Finding_LV_ID,
    Bandage_LV,
    Block_LV,
    Claw_Finding_LH_ID,
    Bandage_LH,
    Block_LH,
    Claw_Finding_RV_ID,
    Bandage_RV,
    Block_RV,
    Claw_Finding_RH_ID,
    Bandage_RH,
    Block_RH,
    IsBandageRemoved
)
SELECT 
    c.Ear_Tag_Number,
    ct.Treatment_Date,
    flv.Claw_Finding_ID,
    ct.Bandage_LV,
    ct.Block_LV,
    flh.Claw_Finding_ID,
    ct.Bandage_LH,
    ct.Block_LH,
    frv.Claw_Finding_ID,
    ct.Bandage_RV,
    ct.Block_RV,
    frh.Claw_Finding_ID,
    ct.Bandage_RH,
    ct.Block_RH,
    ct.IsBandageRemoved
FROM 
    4cows.Claw_Treatment ct
LEFT JOIN 
    4cows_v2.Cow c
ON 
    ct.Collar_Number = c.Collar_Number
-- Je Klaue ein eigener Alias; der Vergleich läuft über die Standard-Kollation
-- und ist damit case-insensitiv, "mortellaro" findet also "Mortellaro".
LEFT JOIN 4cows_v2.Claw_Finding flv ON flv.Claw_Finding_Name = TRIM(ct.Claw_Finding_LV)
LEFT JOIN 4cows_v2.Claw_Finding flh ON flh.Claw_Finding_Name = TRIM(ct.Claw_Finding_LH)
LEFT JOIN 4cows_v2.Claw_Finding frv ON frv.Claw_Finding_Name = TRIM(ct.Claw_Finding_RV)
LEFT JOIN 4cows_v2.Claw_Finding frh ON frh.Claw_Finding_Name = TRIM(ct.Claw_Finding_RH)
WHERE 
    c.Ear_Tag_Number IS NOT NULL; -- Nur Zeilen mit gültiger Ear_Tag_Number einfügen




-- Kuh Behandlungen
INSERT INTO 4cows_v2.Cow_Treatment (
    Ear_Tag_Number,
    Medicine_ID,
    Administration_Date,
    Medicine_Dosage,
    WhereHow
)
SELECT 
    c.Ear_Tag_Number,
    m.Medicine_ID,
    ct.Administration_Date,
    ct.Medicine_Dosage,
    ct.WhereHow
FROM 
    4cows.Cow_Treatment ct
LEFT JOIN 
    4cows_v2.Cow c
ON 
    ct.Collar_Number = c.Collar_Number
LEFT JOIN 
    4cows_v2.Medicine m
ON 
    TRIM(ct.Medicine_Name) COLLATE utf8mb3_general_ci = TRIM(m.Medicine_Name) COLLATE utf8mb3_general_ci
WHERE 
    c.Ear_Tag_Number IS NOT NULL 
    AND m.Medicine_ID IS NOT NULL; 
    
