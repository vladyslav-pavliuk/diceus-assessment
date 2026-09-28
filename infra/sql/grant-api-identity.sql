-- Database user for the API's managed identity (D-44). Run by the deploy workflow after the migrations, as the SQL
-- server's Entra admin (the deployment service principal), with go-sqlcmd. Idempotent: every statement is safe to repeat.
--
-- Variables (sqlcmd -v):
--   ApiIdentityName      display name of the user-assigned managed identity, e.g. id-claims-api
--   ApiIdentityClientId  its client (application) id
--   ReaderName           optional read-only Entra user for the portal query editor, or '-' for none
--   ReaderObjectId       that user's object id, or '-'
--
-- Why WITH SID ... TYPE = E rather than FROM EXTERNAL PROVIDER: FROM EXTERNAL PROVIDER makes the server look the name up
-- in Microsoft Graph, which, when the admin is a service principal, needs a server identity with Directory Readers. The SID
-- of a service principal or managed identity is its client id, and that of a user is its object id; with the SID given,
-- no Graph lookup happens.
--
-- What the API may do: read and write rows (db_datareader, db_datawriter); claims_app adds DENY UPDATE, DELETE on
-- ClaimAuditLog (D-14, InitialCreate). It is not dbo and has no ALTER on the dbo schema, so it cannot change the
-- application schema or drop the audit trigger. It owns the [HangFire] schema, plus CREATE TABLE, so Hangfire can install
-- and upgrade its own tables (D-41 item 14) there and nowhere else.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @apiName sysname = N'$(ApiIdentityName)';
DECLARE @apiClientId uniqueidentifier = CONVERT(uniqueidentifier, N'$(ApiIdentityClientId)');
DECLARE @sql nvarchar(max);

IF DATABASE_PRINCIPAL_ID(@apiName) IS NULL
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@apiName)
        + N' WITH SID = ' + CONVERT(nvarchar(100), CONVERT(varbinary(16), @apiClientId), 1)
        + N', TYPE = E;';
    EXEC (@sql);
    PRINT N'Created user ' + @apiName + N'.';
END;

-- ALTER ROLE ... ADD MEMBER is a no-op for an existing member.
SET @sql = N'ALTER ROLE [db_datareader] ADD MEMBER ' + QUOTENAME(@apiName) + N';'
    + N'ALTER ROLE [db_datawriter] ADD MEMBER ' + QUOTENAME(@apiName) + N';'
    + N'ALTER ROLE [claims_app] ADD MEMBER ' + QUOTENAME(@apiName) + N';'
    + N'GRANT CREATE TABLE TO ' + QUOTENAME(@apiName) + N';';
EXEC (@sql);

IF SCHEMA_ID(N'HangFire') IS NULL
    SET @sql = N'CREATE SCHEMA [HangFire] AUTHORIZATION ' + QUOTENAME(@apiName) + N';';
ELSE
    SET @sql = N'ALTER AUTHORIZATION ON SCHEMA::[HangFire] TO ' + QUOTENAME(@apiName) + N';';
EXEC (@sql);

PRINT N'API identity ' + @apiName + N': db_datareader, db_datawriter, claims_app, CREATE TABLE, owner of [HangFire].';

-- Optional: a read-only login for a person, so the audit log and Hangfire tables can be shown in the portal query editor.
DECLARE @readerName sysname = N'$(ReaderName)';
DECLARE @readerObjectId nvarchar(36) = N'$(ReaderObjectId)';

IF @readerName <> N'-' AND @readerObjectId <> N'-'
BEGIN
    IF DATABASE_PRINCIPAL_ID(@readerName) IS NULL
    BEGIN
        SET @sql = N'CREATE USER ' + QUOTENAME(@readerName)
            + N' WITH SID = ' + CONVERT(nvarchar(100), CONVERT(varbinary(16), CONVERT(uniqueidentifier, @readerObjectId)), 1)
            + N', TYPE = E;';
        EXEC (@sql);
    END;

    SET @sql = N'ALTER ROLE [db_datareader] ADD MEMBER ' + QUOTENAME(@readerName) + N';';
    EXEC (@sql);
    PRINT N'Reader ' + @readerName + N': db_datareader.';
END;
