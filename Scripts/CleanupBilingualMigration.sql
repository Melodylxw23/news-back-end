-- Cleanup script for partially applied bilingual broadcast migration
USE News_BackEnd;
GO

-- Drop columns if they exist
IF COL_LENGTH('BroadcastMessages', 'BodyZH') IS NOT NULL
BEGIN
    ALTER TABLE BroadcastMessages DROP COLUMN BodyZH;
    PRINT 'Dropped BodyZH column';
END

IF COL_LENGTH('BroadcastMessages', 'Language') IS NOT NULL
BEGIN
    ALTER TABLE BroadcastMessages DROP COLUMN [Language];
    PRINT 'Dropped Language column';
END

IF COL_LENGTH('BroadcastMessages', 'SubjectZH') IS NOT NULL
BEGIN
    ALTER TABLE BroadcastMessages DROP COLUMN SubjectZH;
    PRINT 'Dropped SubjectZH column';
END

IF COL_LENGTH('BroadcastMessages', 'TitleZH') IS NOT NULL
BEGIN
 ALTER TABLE BroadcastMessages DROP COLUMN TitleZH;
    PRINT 'Dropped TitleZH column';
END

PRINT 'Cleanup complete. Ready for new migration.';
GO
