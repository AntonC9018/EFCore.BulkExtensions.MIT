using EFCore.BulkExtensions.SqlAdapters;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkExtensions;

internal static class DbContextBulkTransaction
{
    public static void Execute<T>(
        DbContext context,
        Type? type,
        IList<T> entities,
        OperationType operationType,
        BulkConfig? bulkConfig,
        Action<decimal>? progress) 
        
        where T : class
    {
        type ??= typeof(T);

        using var activity = ActivitySources.StartExecuteActivity(operationType, entities.Count);

        bool wontDeleteEntities = operationType 
            is not OperationType.InsertOrUpdateOrDelete
            and not OperationType.Truncate
            and not OperationType.SaveChanges;
        if (entities.Count == 0 && 
            wontDeleteEntities &&
            (bulkConfig == null || bulkConfig.CustomSourceTableName == null))
        {
            return;
        }

        if (operationType == OperationType.SaveChanges)
        {
            DbContextBulkTransactionSaveChanges.SaveChanges(context, bulkConfig, progress);
        }
        else if (bulkConfig?.IncludeGraph == true)
        {
            DbContextBulkTransactionGraphUtil.ExecuteWithGraph(context, entities, operationType, bulkConfig, progress);
        }
        else
        {
            TableInfo tableInfo = TableInfo.CreateInstance(context, type, entities, operationType, bulkConfig);

            switch (operationType)
            {
                case OperationType.Insert when
                    !tableInfo.BulkConfig.SetOutputIdentity &&
                    tableInfo.BulkConfig.CustomSourceTableName == null:
                {
                    SqlBulkOperation.Insert(context, type, entities, tableInfo, progress);
                    break;
                }
                case OperationType.Read:
                {
                    SqlBulkOperation.Read(context, type, entities, tableInfo, progress);
                    break;
                }
                case OperationType.Truncate:
                {
                    SqlBulkOperation.Truncate(context, tableInfo);
                    break;
                }
                default:
                {
                    SqlBulkOperation.Merge(context, type, entities, tableInfo, operationType, progress);
                    break;
                }
            }
        }
    }

    public static async Task ExecuteAsync<T>(DbContext context, Type? type, IList<T> entities, OperationType operationType, BulkConfig? bulkConfig, Action<decimal>? progress, CancellationToken cancellationToken = default) where T : class
    {
        type ??= typeof(T);

        using (ActivitySources.StartExecuteActivity(operationType, entities.Count))
        {
            if (entities.Count == 0 && operationType != OperationType.InsertOrUpdateOrDelete && operationType != OperationType.Truncate && operationType != OperationType.SaveChanges)
            {
                return;
            }

            if (operationType == OperationType.SaveChanges)
            {
                await DbContextBulkTransactionSaveChanges.SaveChangesAsync(context, bulkConfig, progress, cancellationToken).ConfigureAwait(false);
            }
            else if(bulkConfig?.IncludeGraph == true)
            {
                await DbContextBulkTransactionGraphUtil.ExecuteWithGraphAsync(context, entities, operationType, bulkConfig, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                TableInfo tableInfo = TableInfo.CreateInstance(context, type, entities, operationType, bulkConfig);

                if (operationType == OperationType.Insert && !tableInfo.BulkConfig.SetOutputIdentity)
                {
                    await SqlBulkOperation.InsertAsync(context, type, entities, tableInfo, progress, cancellationToken).ConfigureAwait(false);
                }
                else if (operationType == OperationType.Read)
                {
                    await SqlBulkOperation.ReadAsync(context, type, entities, tableInfo, progress, cancellationToken).ConfigureAwait(false);
                }
                else if (operationType == OperationType.Truncate)
                {
                    await SqlBulkOperation.TruncateAsync(context, tableInfo, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SqlBulkOperation.MergeAsync(context, type, entities, tableInfo, operationType, progress, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
