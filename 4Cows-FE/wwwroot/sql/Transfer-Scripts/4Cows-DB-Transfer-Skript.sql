INSERT IGNORE INTO 4cows_v2.Medicine (Medicine_Name)
SELECT DISTINCT TRIM(Medicine_Name) AS Medicine_Name
FROM 4cows.Cow_Treatment;

-- Klauenbehandlungen
INSERT INTO 4cows_v2.Claw_Treatment (
    Ear_Tag_Number,
    Treatment_Date,
    Claw_Finding_LV,
    Bandage_LV,
    Block_LV,
    Claw_Finding_LH,
    Bandage_LH,
    Block_LH,
    Claw_Finding_RV,
    Bandage_RV,
    Block_RV,
    Claw_Finding_RH,
    Bandage_RH,
    Block_RH,
    IsBandageRemoved
)
SELECT 
    c.Ear_Tag_Number,
    ct.Treatment_Date,
    ct.Claw_Finding_LV,
    ct.Bandage_LV,
    ct.Block_LV,
    ct.Claw_Finding_LH,
    ct.Bandage_LH,
    ct.Block_LH,
    ct.Claw_Finding_RV,
    ct.Bandage_RV,
    ct.Block_RV,
    ct.Claw_Finding_RH,
    ct.Bandage_RH,
    ct.Block_RH,
    ct.IsBandageRemoved
FROM 
    4cows.Claw_Treatment ct
LEFT JOIN 
    4cows_v2.Cow c
ON 
    ct.Collar_Number = c.Collar_Number
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
    
