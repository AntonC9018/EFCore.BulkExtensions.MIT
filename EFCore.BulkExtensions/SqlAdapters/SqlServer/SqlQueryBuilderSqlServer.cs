﻿using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace EFCore.BulkExtensions.SqlAdapters.SqlServer;

/// <summary>
/// Contains a compilation of SQL queries used in EFCore.
/// </summary>
public class SqlQueryBuilderSqlServer : SqlAdapters.QueryBuilderExtensions
{
    /// <inheritdoc/>
    public override DbParameter CreateParameter(SqlParameter sqlParameter)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public override object Dbtype()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public override string RestructureForBatch(string sql, bool isDelete = false)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public override string SelectFromOutputTable(TableInfo tableInfo)
    {
        return EFCore.BulkExtensions.SqlQueryBuilder.SelectFromOutputTable(tableInfo);
    }

    /// <inheritdoc/>
    public override void SetDbTypeParam(object npgsqlParameter, object dbType)
    {
        throw new NotImplementedException();
    }
}
