using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.MiniApps.UserMiniApp.Storage;

// Явно создает storage-схему user mini-app в общей Postgres-базе.
public static class UserMiniAppStorageSchema
{
    public static void EnsureSchema(UserMiniAppDbContext context)
    {
        context.Database.ExecuteSqlRaw(
            @"
            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" uuid NOT NULL,
                ""ExternalId"" text NOT NULL,
                ""Payload"" text NOT NULL,
                ""DateCreated"" timestamp with time zone NOT NULL,
                ""DateLastModified"" timestamp with time zone NOT NULL,
                CONSTRAINT ""PK_Users"" PRIMARY KEY (""Id"")
            );

            CREATE TABLE IF NOT EXISTS ""UserProperties"" (
                ""Id"" uuid NOT NULL,
                ""DateCreated"" timestamp with time zone NOT NULL,
                ""DateLastModified"" timestamp with time zone NOT NULL,
                ""ParentEntityId"" uuid NOT NULL,
                ""PropertyType"" integer NOT NULL,
                ""Data"" text NOT NULL,
                ""Metadata"" text NOT NULL,
                CONSTRAINT ""PK_UserProperties"" PRIMARY KEY (""Id"")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_ExternalId"" ON ""Users"" (""ExternalId"");
            CREATE INDEX IF NOT EXISTS ""IX_UserProperties_ParentEntityId"" ON ""UserProperties"" (""ParentEntityId"");
            CREATE INDEX IF NOT EXISTS ""IX_UserProperties_ParentEntityId_PropertyType"" ON ""UserProperties"" (""ParentEntityId"", ""PropertyType"");
            ");
    }
}
