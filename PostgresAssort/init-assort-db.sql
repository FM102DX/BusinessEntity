-- Initialize mall database schema
-- Based on AssortmentApi models and DbContext configuration

-- Create CommodityItem table
CREATE TABLE "CommodityItems" (
    "Id" uuid NOT NULL,
    "Name" varchar(250) NOT NULL,
    "Description" text,
    "Price" integer NOT NULL,
    "SupplierId" uuid NOT NULL,
    "SysMessage" text,
    "CreatedDateTime" timestamp without time zone NOT NULL,
    "LastModifiedDatTime" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_CommodityItems" PRIMARY KEY ("Id")
);

-- Create Supplier table
CREATE TABLE "Suppliers" (
    "Id" uuid NOT NULL,
    "Name" varchar(250) NOT NULL,
    "SysMessage" text,
    "CreatedDateTime" timestamp without time zone NOT NULL,
    "LastModifiedDatTime" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Suppliers" PRIMARY KEY ("Id")
);

-- Add foreign key constraint
ALTER TABLE "CommodityItems" 
ADD CONSTRAINT "FK_CommodityItems_Suppliers_SupplierId" 
FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE CASCADE;