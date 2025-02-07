CREATE DATABASE IF NOT EXISTS `4cows_v3`;
USE `4cows_v3`;

CREATE TABLE IF NOT EXISTS Cow (
                                           Ear_Tag_Number NVARCHAR(64) UNIQUE NOT NULL PRIMARY KEY,
                                           Collar_Number INT NOT NULL,
                                           IsGone BOOL default false
);

CREATE TABLE IF NOT EXISTS Medicine (
                                            Medicine_ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                                            Medicine_Name NVARCHAR(64) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS WhereHow(
                                            WhereHow_ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                                            WhereHow_Name NVARCHAR(64) NOT NULL UNIQUE,
                                            ShowDialog BOOL NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS Udder (
                                            UDDER_ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                                            Quarter_LV BOOL NOT NULL DEFAULT FALSE,
                                            Quarter_LH BOOL NOT NULL DEFAULT FALSE,
                                            Quarter_RV BOOL NOT NULL DEFAULT FALSE,
                                            Quarter_RH BOOL NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS Cow_Treatment (
                                             Cow_Treatment_ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                                             Ear_Tag_Number NVARCHAR(64) NOT NULL,
                                             Medicine_ID INT NOT NULL,
                                             Administration_Date DATETIME NOT NULL,
                                             Medicine_Dosage FLOAT NOT NULL,
                                             WhereHow_ID INT NULL,
                                             COW_QUARTER_ID INT NOT NULL,
                                             FOREIGN KEY (Ear_Tag_Number) REFERENCES Cow(Ear_Tag_Number) ON DELETE CASCADE,
                                             FOREIGN KEY (Medicine_ID) REFERENCES Medicine(Medicine_ID) ON DELETE CASCADE,
                                             FOREIGN KEY (WhereHow_ID) REFERENCES WhereHow(WhereHow_ID) ON DELETE CASCADE,
                                             FOREIGN KEY (COW_QUARTER_ID) REFERENCES Udder(UDDER_ID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Claw_Treatment (
                                              Claw_Treatment_ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                                              Ear_Tag_Number NVARCHAR(64) NOT NULL,
                                              Treatment_Date DATETIME NOT NULL,

                                              Claw_Finding_LV NVARCHAR(32) NOT NULL,
                                              Bandage_LV BIT NOT NULL,
                                              Block_LV BIT NOT NULL,

                                              Claw_Finding_LH NVARCHAR(32) NOT NULL,
                                              Bandage_LH BIT NOT NULL,
                                              Block_LH BIT NOT NULL,

                                              Claw_Finding_RV NVARCHAR(32) NOT NULL,
                                              Bandage_RV BIT NOT NULL,
                                              Block_RV BIT NOT NULL,

                                              Claw_Finding_RH NVARCHAR(32) NOT NULL,
                                              Bandage_RH BIT NOT NULL,
                                              Block_RH BIT NOT NULL,
                                              IsBandageRemoved BOOLEAN NOT NULL,
                                              FOREIGN KEY (Ear_Tag_Number) REFERENCES Cow(Ear_Tag_Number) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Planned_Cow_Treatment (
                                             Planned_Cow_Treatment_ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                                             Ear_Tag_Number NVARCHAR(64) NOT NULL,
                                             Medicine_ID INT NOT NULL,
                                             Administration_Date DATETIME NOT NULL,
                                             Medicine_Dosage FLOAT NOT NULL,
                                             WhereHow_ID INT NOT NULL,
                                             IsFound BOOLEAN NOT NULL,
                                             IsTreatet BOOLEAN NOT NULL,
                                             Udder_ID INT NOT NULL,
                                             FOREIGN KEY (Ear_Tag_Number) REFERENCES Cow(Ear_Tag_Number) ON DELETE CASCADE,
                                             FOREIGN KEY (Medicine_ID) REFERENCES Medicine(Medicine_ID) ON DELETE CASCADE,
                                             FOREIGN KEY (WhereHow_ID) REFERENCES WhereHow(WhereHow_ID) ON DELETE CASCADE,
                                             FOREIGN KEY (Udder_ID) REFERENCES Udder(UDDER_ID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Planned_Claw_Treatment (
                                              Planned_Claw_Treatment_ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                                              Ear_Tag_Number NVARCHAR(64) NOT NULL,
                                              Treatment_Date DATETIME NOT NULL,
                                              Desciption NVARCHAR(1028) NULL,
                                              Claw_Finding_LV BOOLEAN NOT NULL,
                                              Claw_Finding_LH BOOLEAN NOT NULL,
                                              Claw_Finding_RV BOOLEAN NOT NULL,
                                              Claw_Finding_RH BOOLEAN NOT NULL,
                                              FOREIGN KEY (Ear_Tag_Number) REFERENCES Cow(Ear_Tag_Number) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS KPI (
                                   KPI_ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                                   Title NVARCHAR(64) NOT NULL,
                                    Url NVARCHAR(64) NOT NULL,
                                    Script NVARCHAR(5098) NOT NULL,
                                    Sort_Order INT NOT NULL UNIQUE
    );