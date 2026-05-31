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

            CREATE TABLE IF NOT EXISTS ""UserRoles"" (
                ""Id"" uuid NOT NULL,
                ""Name"" text NOT NULL,
                ""Permissions"" text NOT NULL,
                ""IsSystem"" boolean NOT NULL,
                ""DateCreated"" timestamp with time zone NOT NULL,
                ""DateLastModified"" timestamp with time zone NOT NULL,
                CONSTRAINT ""PK_UserRoles"" PRIMARY KEY (""Id"")
            );

            CREATE TABLE IF NOT EXISTS ""UserGroups"" (
                ""Id"" uuid NOT NULL,
                ""Name"" text NOT NULL,
                ""DateCreated"" timestamp with time zone NOT NULL,
                ""DateLastModified"" timestamp with time zone NOT NULL,
                CONSTRAINT ""PK_UserGroups"" PRIMARY KEY (""Id"")
            );

            CREATE TABLE IF NOT EXISTS ""UserGroupMembers"" (
                ""Id"" uuid NOT NULL,
                ""UserId"" uuid NOT NULL,
                ""GroupId"" uuid NOT NULL,
                ""DateCreated"" timestamp with time zone NOT NULL,
                ""DateLastModified"" timestamp with time zone NOT NULL,
                CONSTRAINT ""PK_UserGroupMembers"" PRIMARY KEY (""Id"")
            );

            CREATE TABLE IF NOT EXISTS ""UserRoleAssignments"" (
                ""Id"" uuid NOT NULL,
                ""SpaceId"" uuid NOT NULL,
                ""Subject"" text NOT NULL DEFAULT 'Space',
                ""SubjectId"" uuid NOT NULL,
                ""AssignmentType"" text NOT NULL,
                ""RoleId"" uuid NOT NULL,
                ""DateCreated"" timestamp with time zone NOT NULL,
                ""DateLastModified"" timestamp with time zone NOT NULL,
                CONSTRAINT ""PK_UserRoleAssignments"" PRIMARY KEY (""Id"")
            );

            ALTER TABLE ""UserRoleAssignments""
                ADD COLUMN IF NOT EXISTS ""Subject"" text NOT NULL DEFAULT 'Space';

            UPDATE ""UserRoleAssignments""
                SET ""Subject"" = 'Space'
                WHERE ""Subject"" IS NULL OR ""Subject"" = '';

            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_ExternalId"" ON ""Users"" (""ExternalId"");
            CREATE INDEX IF NOT EXISTS ""IX_UserProperties_ParentEntityId"" ON ""UserProperties"" (""ParentEntityId"");
            CREATE INDEX IF NOT EXISTS ""IX_UserProperties_ParentEntityId_PropertyType"" ON ""UserProperties"" (""ParentEntityId"", ""PropertyType"");
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_UserRoles_Name"" ON ""UserRoles"" (""Name"");
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_UserGroups_Name"" ON ""UserGroups"" (""Name"");
            CREATE INDEX IF NOT EXISTS ""IX_UserGroupMembers_GroupId"" ON ""UserGroupMembers"" (""GroupId"");
            CREATE INDEX IF NOT EXISTS ""IX_UserGroupMembers_UserId"" ON ""UserGroupMembers"" (""UserId"");
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_UserGroupMembers_GroupId_UserId"" ON ""UserGroupMembers"" (""GroupId"", ""UserId"");
            CREATE INDEX IF NOT EXISTS ""IX_UserRoleAssignments_SpaceId"" ON ""UserRoleAssignments"" (""SpaceId"");
            CREATE INDEX IF NOT EXISTS ""IX_UserRoleAssignments_Subject"" ON ""UserRoleAssignments"" (""Subject"");
            CREATE INDEX IF NOT EXISTS ""IX_UserRoleAssignments_SubjectId"" ON ""UserRoleAssignments"" (""SubjectId"");
            CREATE INDEX IF NOT EXISTS ""IX_UserRoleAssignments_RoleId"" ON ""UserRoleAssignments"" (""RoleId"");
            DROP INDEX IF EXISTS ""IX_UserRoleAssignments_SpaceId_SubjectId_AssignmentType_RoleId"";
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_UserRoleAssignments_SpaceId_Subject_SubjectId_AssignmentType_RoleId""
                ON ""UserRoleAssignments"" (""SpaceId"", ""Subject"", ""SubjectId"", ""AssignmentType"", ""RoleId"");
            ");
    }
}
