-- Initialize logger_db database schema
-- Based on WebLogger migrations

-- Create WebLoggerMessage table
CREATE TABLE "WebLoggerMessage" (
    "Id" uuid NOT NULL,
    "Sender" text,
    "Message" text,
    "LogLevel" integer,
    "SysMessage" text,
    "CreatedDateTime" timestamp without time zone NOT NULL,
    "LastModifiedDatTime" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_WebLoggerMessage" PRIMARY KEY ("Id")
);

-- Create WebLoggerSettings table
CREATE TABLE "WebLoggerSettings" (
    "Id" uuid NOT NULL,
    "RefreshPeriodMs" integer NOT NULL,
    "ItemsPerPage" integer NOT NULL,
    "LogsKeepingPeriod" interval NOT NULL,
    "SysMessage" text,
    "CreatedDateTime" timestamp without time zone NOT NULL,
    "LastModifiedDatTime" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_WebLoggerSettings" PRIMARY KEY ("Id")
);