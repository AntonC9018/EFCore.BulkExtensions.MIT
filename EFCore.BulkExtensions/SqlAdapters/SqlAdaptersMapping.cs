using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel;

namespace EFCore.BulkExtensions.SqlAdapters;

/// <summary>
/// A list of database servers supported by EFCore.BulkExtensions
/// </summary>
public enum DbServerType
{
    /// <summary>
    /// Indicates database is Microsoft's SQL Server
    /// </summary>
    [Description("SqlServer")] SQLServer,

    /// <summary>
    /// Indicates database is SQL Lite
    /// </summary>
    [Description("SQLite")] SQLite,

    /// <summary>
    /// Indicates database is Postgres
    /// </summary>
    [Description("PostgreSql")] PostgreSQL,

    /// <summary>
    ///  Indicates database is MySQL
    /// </summary>
    [Description("MySql")] MySQL,
}

public static class SqlAdaptersMapping
{
    private static IDbServer? _sqlLite;
    private static IDbServer? _msSql;
    private static IDbServer? _mySql;
    private static IDbServer? _postgreSql;

    /// <summary>
    /// Contains a list of methods to generate Adapters and helpers instances
    /// </summary>
    public static IDbServer DbServer(this DbContext dbContext)
    {
        //Context.Database. methods: -IsSqlServer() -IsNpgsql() -IsMySql() -IsSqlite() requires specific provider so instead here used -ProviderName
        
        var providerName = dbContext.Database.ProviderName;
        const string efCoreBulkExtensionsSqlAdaptersText = "EFCore.BulkExtensions.SqlAdapters";
        
        IDbServer dbServerInstance;

        bool CheckName(DbServerType type)
        {
            if (providerName is null)
                return false;
            var name = type.ToString();
            return providerName.EndsWith(name, StringComparison.OrdinalIgnoreCase);
        }

        static IDbServer CreateServerInstance(DbServerType type)
        {
            var nameEnd = type switch
            {
                DbServerType.PostgreSQL => ".PostgreSql.PostgreSqlDbServer",
                DbServerType.MySQL => ".MySql.MySqlDbServer",
                DbServerType.SQLite => ".SQLite.SqlLiteDbServer",
                _ => ".SqlServer.SqlServerDbServer"
            };
            var dbServerType = Type.GetType(efCoreBulkExtensionsSqlAdaptersText + nameEnd);
            return (IDbServer)Activator.CreateInstance(dbServerType ?? typeof(int))!;
        }

        if (CheckName(DbServerType.PostgreSQL))
        {
            dbServerInstance = _postgreSql ??= CreateServerInstance(DbServerType.PostgreSQL);    
        }
        else if (CheckName(DbServerType.MySQL))
        {
            dbServerInstance = _mySql ??= CreateServerInstance(DbServerType.MySQL);

        }
        else if (CheckName(DbServerType.SQLite))
        {
            dbServerInstance = _sqlLite ??= CreateServerInstance(DbServerType.SQLite);
        }
        else
        {
            dbServerInstance = _msSql ??= CreateServerInstance(DbServerType.SQLServer);
        }

        return dbServerInstance;
    }

    /// <summary>
    /// Creates the bulk operations adapter
    /// </summary>
    /// <returns></returns>
    public static ISqlOperationsAdapter CreateBulkOperationsAdapter(this DbContext dbContext) => DbServer(dbContext).Adapter;

    /// <summary>
    /// Returns the Adapter dialect to be used
    /// </summary>
    /// <returns></returns>
    public static IQueryBuilderSpecialization GetAdapterDialect(this DbContext dbContext) => DbServer(dbContext).Dialect;

    /// <summary>
    /// Returns the Database type
    /// </summary>
    /// <returns></returns>
    public static DbServerType GetDatabaseType(this DbContext dbContext) => DbServer(dbContext).Type;
}
