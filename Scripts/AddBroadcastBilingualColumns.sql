-- Add missing bilingual columns to BroadcastMessages table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'BodyZH')
BEGIN
    ALTER TABLE [BroadcastMessages] ADD [BodyZH] nvarchar(max) NULL;
END;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'TitleZH')
BEGIN
    ALTER TABLE [BroadcastMessages] ADD [TitleZH] nvarchar(max) NULL;
END;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'SubjectZH')
BEGIN
    ALTER TABLE [BroadcastMessages] ADD [SubjectZH] nvarchar(max) NULL;
END;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'Language')
BEGIN
    ALTER TABLE [BroadcastMessages] ADD [Language] nvarchar(50) NOT NULL CONSTRAINT DF_BroadcastMessages_Language DEFAULT 'English';
END;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'SelectedInterestTagIdsJson')
BEGIN
    ALTER TABLE [BroadcastMessages] ADD [SelectedInterestTagIdsJson] nvarchar(max) NOT NULL CONSTRAINT DF_BroadcastMessages_SelectedInterestTagIdsJson DEFAULT '[]';
END;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'SelectedIndustryTagIdsJson')
BEGIN
    ALTER TABLE [BroadcastMessages] ADD [SelectedIndustryTagIdsJson] nvarchar(max) NOT NULL CONSTRAINT DF_BroadcastMessages_SelectedIndustryTagIdsJson DEFAULT '[]';
END;

PRINT 'Columns added successfully';
