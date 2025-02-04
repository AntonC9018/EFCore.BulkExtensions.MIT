using EFCore.BulkExtensions.SqlAdapters;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;


namespace EFCore.BulkExtensions.Tests.BulkInsertOrUpdate;

public class EfCoreBulkInsertOrUpdateTests : IClassFixture<EfCoreBulkInsertOrUpdateTests.DatabaseFixture>, IAssemblyFixture<DbAssemblyFixture>
{
    private readonly ITestOutputHelper _writer;
    private readonly DatabaseFixture _dbFixture;

    public EfCoreBulkInsertOrUpdateTests(ITestOutputHelper writer, DatabaseFixture dbFixture)
    {
        _writer = writer;
        _dbFixture = dbFixture;
    }

    /// <summary>
    /// Covers issue: https://github.com/videokojot/EFCore.BulkExtensions.MIT/issues/46
    /// Original: https://github.com/borisdj/EFCore.BulkExtensions/issues/1249
    /// </summary>
    [Theory]
    [InlineData(DbServerType.SQLServer)]
    public void BulkInsertOrUpdate_CustomUpdateBy_WithOutputIdentity_NullKeys(DbServerType dbType)
    {
        using var db = _dbFixture.GetDb(dbType);

        var newItem = new SimpleItem()
        {
            StringProperty = null,
            GuidProperty = new Guid("ac53ed8d-5ee4-43cb-98c0-f12939949a49"),
        };

        var ensureList = new[] { newItem, };

        db.BulkInsertOrUpdate(ensureList, c =>
        {
            c.SetOutputIdentity = true;
            c.UpdateByProperties = new List<string> { nameof(SimpleItem.StringProperty) };
            c.PreserveInsertOrder = true;
        });

        var fromDb = db.SimpleItems.Single(x => x.GuidProperty == newItem.GuidProperty);

        Assert.NotNull(fromDb); // Item was inserted

        // Ids were correctly filled
        Assert.Equal(fromDb.Id, newItem.Id);
        Assert.NotEqual(0, newItem.Id);
    }

    /// <summary>
    /// Covers issue: https://github.com/videokojot/EFCore.BulkExtensions.MIT/issues/45
    /// Original: https://github.com/borisdj/EFCore.BulkExtensions/issues/1248
    /// </summary>
    [Theory]
    [InlineData(DbServerType.SQLServer)]
    public void BulkInsertOrUpdate_InsertOnlyNew_SetOutputIdentity(DbServerType dbType)
    {
        var bulkId = Guid.NewGuid();
        var existingItemId = "existingId";

        var initialItem = new SimpleItem()
        {
            StringProperty = existingItemId,
            Name = "initial1",
            BulkIdentifier = bulkId,
            GuidProperty = Guid.NewGuid(),
        };

        using (var db = _dbFixture.GetDb(dbType))
        {
            db.SimpleItems.Add(initialItem);
            db.SaveChanges();
        }

        using (var db = _dbFixture.GetDb(dbType))
        {
            var newItem = new SimpleItem()
            {
                StringProperty = "insertedByBulk",
                BulkIdentifier = bulkId,
                GuidProperty = Guid.NewGuid(),
            };

            var updatedItem = new SimpleItem()
            {
                StringProperty = existingItemId,
                BulkIdentifier = bulkId,
                Name = "updated by Bulks",
                GuidProperty = Guid.NewGuid(),
            };

            var ensureList = new[] { newItem, updatedItem };

            db.BulkInsertOrUpdate(ensureList, c =>
            {
                c.PreserveInsertOrder = true;
                c.UpdateByProperties =
                    new List<string> { nameof(SimpleItem.StringProperty), nameof(SimpleItem.BulkIdentifier) };
                c.PropertiesToIncludeOnUpdate = new List<string> { "" };
                c.SetOutputIdentity = true;
            });

            var dbItems = GetItemsOfBulk(bulkId, dbType).OrderBy(x => x.GuidProperty).ToList();

            var updatedDb = dbItems.Single(x => x.GuidProperty == initialItem.GuidProperty);
            var newDb = dbItems.Single(x => x.GuidProperty == newItem.GuidProperty);

            Assert.Equal(updatedDb.Id, updatedItem.Id); // output identity was set

            // Rest of properties were not updated:
            Assert.Equal(updatedDb.Name, initialItem.Name);
            Assert.Equal(updatedDb.StringProperty, initialItem.StringProperty);
            Assert.Equal(updatedDb.BulkIdentifier, initialItem.BulkIdentifier);

            Assert.Equal(newDb.Id, newItem.Id);
            Assert.Equal(newDb.Name, newItem.Name);
            Assert.Equal(newDb.StringProperty, newItem.StringProperty);
            Assert.Equal(newDb.BulkIdentifier, newItem.BulkIdentifier);
        }
    }

    /// <summary>
    /// Covers: https://github.com/borisdj/EFCore.BulkExtensions/issues/1250
    /// </summary>
    [Theory]
    [InlineData(DbServerType.SQLServer)]
    public void BulkInsertOrUpdate_SetOutputIdentity_SetOutputNonIdentityColumns(DbServerType dbType)
    {
        using var db = _dbFixture.GetDb(dbType);

        var newItem = new SimpleItem()
        {
            StringProperty = "newItem",
            GuidProperty = new Guid("9f71ff93-2326-44d3-acb6-95b5d0566d68"),
            Name = "newName",
        };

        var ensureList = new[] { newItem, };

        // OutputIdentity == true && SetOutputNonIdentityColumns == false
        db.BulkInsertOrUpdate(ensureList,
                              c =>
                              {
                                  c.SetOutputIdentity = true;
                                  c.UpdateByProperties = new List<string> { nameof(SimpleItem.StringProperty), nameof(SimpleItem.Name) };
                                  c.PreserveInsertOrder = true;
                              });

        var fromDb = db.SimpleItems.SingleOrDefault(x => x.GuidProperty == newItem.GuidProperty);
        Assert.NotNull(fromDb); // Item was inserted!

        Assert.NotEqual(0, newItem.Id);
    }

    /// <summary>
    /// Covers: https://github.com/videokojot/EFCore.BulkExtensions.MIT/issues/48
    /// Original issue: https://github.com/borisdj/EFCore.BulkExtensions/issues/1251
    /// </summary>
    [Theory]
    [InlineData(DbServerType.SQLServer)]
    public void BulkInsertOrUpdate_WillNotSetOutputIdentityIfThereIsConflict(DbServerType dbType)
    {
        var bulkId = Guid.NewGuid();
        var existingItemId = "existingDuplicateOf";

        var initialItem = new SimpleItem()
        {
            StringProperty = existingItemId,
            Name = "initial1",
            BulkIdentifier = bulkId,
        };

        var duplicateInitial = new SimpleItem()
        {
            StringProperty = existingItemId,
            Name = "initial2",
            BulkIdentifier = bulkId,
        };

        using (var db = _dbFixture.GetDb(dbType))
        {
            db.SimpleItems.Add(initialItem);
            db.SimpleItems.Add(duplicateInitial);
            db.SaveChanges();

            Assert.NotEqual(0, initialItem.Id);
            Assert.NotEqual(0, duplicateInitial.Id);
        }

        using (var db = _dbFixture.GetDb(dbType))
        {
            var updatingItem = new SimpleItem()
            {
                StringProperty = existingItemId,
                BulkIdentifier = bulkId,
                Name = "updates multiple rows",
            };

            var ensureList = new[] { updatingItem, };

            // We throw we cannot set output identity deterministically.
            var exception = Assert.Throws<BulkExtensionsException>(
                () => db.BulkInsertOrUpdate(ensureList, c =>
                {
                    c.UpdateByProperties = new List<string> { nameof(SimpleItem.StringProperty), nameof(SimpleItem.BulkIdentifier) };
                    c.SetOutputIdentity = true;
                    c.PreserveInsertOrder = true;
                }));

            Assert.Equal(BulkExtensionsExceptionType.CannotSetOutputIdentityForNonUniqueUpdateByProperties, exception.ExceptionType);
            Assert.StartsWith("Items were Inserted/Updated successfully in db, but we cannot set output identity correctly since single source row(s) matched multiple rows in db.", exception.Message);

            var bulkItems = GetItemsOfBulk(bulkId, dbType);

            Assert.Equal(2, bulkItems.Count);

            // Item updated both of the matching rows
            foreach (var dbItem in bulkItems)
            {
                Assert.Equal(updatingItem.Name, dbItem.Name);
            }
        }
    }

    /// <summary>
    /// Covers: https://github.com/borisdj/EFCore.BulkExtensions/issues/1263
    /// </summary>
    [Theory]
    [InlineData(DbServerType.SQLServer)]
    public void IUD_KeepIdentity_IdentityDifferentFromKey(DbServerType dbType)
    {
        var item = new Entity_KeyDifferentFromIdentity()
        {
            ItemTestGid = Guid.NewGuid(),
            ItemTestIdent = 1234,
            Name = "1234",
        };

        var item2 = new Entity_KeyDifferentFromIdentity()
        {
            ItemTestGid = Guid.NewGuid(),
            ItemTestIdent = 12345678,
            Name = "12345678",
        };

        using (var db = _dbFixture.GetDb(dbType))
        {
            var items = new[] { item, item2 };
            db.BulkInsertOrUpdateOrDelete(items, c => { c.SqlBulkCopyOptions = SqlBulkCopyOptions.Default | SqlBulkCopyOptions.KeepIdentity; });
        }

        using (var db = _dbFixture.GetDb(dbType))
        {
            var insertedItem = db.EntityKeyDifferentFromIdentities.Single(x => x.ItemTestGid == item.ItemTestGid);
            Assert.Equal(1234, insertedItem.ItemTestIdent);
        }
    }

    /// <summary>
    /// Covers: https://github.com/videokojot/EFCore.BulkExtensions.MIT/issues/62
    /// </summary>
    [Theory]
    [InlineData(DbServerType.SQLServer)]
    public void IUD_UpdateByCustomColumns_SetOutputIdentity_CustomColumnNames(DbServerType dbType)
    {
        var item = new Entity_CustomColumnNames()
        {
            CustomColumn = "Value1",
            GuidProperty = Guid.NewGuid(),
        };

        var item2 = new Entity_CustomColumnNames()
        {
            CustomColumn = "Value2",
            GuidProperty = Guid.NewGuid(),
        };

        using (var db = _dbFixture.GetDb(dbType))
        {
            var items = new[] { item, item2 };
            db.BulkInsertOrUpdateOrDelete(items, c =>
            {
                c.SetOutputIdentity = true;
                c.UpdateByProperties = new List<string> { nameof(Entity_CustomColumnNames.CustomColumn) };
            });
        }

        using (var db = _dbFixture.GetDb(dbType))
        {
            var insertedItem = db.EntityCustomColumnNames.Single(x => x.GuidProperty == item.GuidProperty);
            Assert.Equal("Value1", insertedItem.CustomColumn);
        }
    }

    private List<SimpleItem> GetItemsOfBulk(Guid bulkId, DbServerType sqlType)
    {
        using var db = _dbFixture.GetDb(sqlType);

        return db.SimpleItems.Where(x => x.BulkIdentifier == bulkId).ToList();
    }

    public class DatabaseFixture : BulkDbTestsFixture
    {
        public DatabaseFixture() : base(nameof(EfCoreBulkInsertOrUpdateTests))
        {
        }
    }
}
