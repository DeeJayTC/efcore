// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;

// ReSharper disable InconsistentNaming
namespace Microsoft.EntityFrameworkCore;

public class StoreGeneratedSqlServerTest : StoreGeneratedTestBase<StoreGeneratedSqlServerTest.StoreGeneratedSqlServerFixture>
{
    public StoreGeneratedSqlServerTest(StoreGeneratedSqlServerFixture fixture)
        : base(fixture)
    {
    }

    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseTransaction(transaction.GetDbTransaction());

    [ConditionalFact]
    public virtual void Exception_in_SaveChanges_causes_store_values_to_be_reverted()
    {
        var entities = new List<Darwin>();
        for (var i = 0; i < 100; i++)
        {
            entities.Add(
                new Darwin
                {
                    Species = new Species { Name = "Goldfish (with legs)" },
                    MixedMetaphors = new List<Species>
                    {
                        new() { Name = "Large ground finch" },
                        new() { Name = "Medium ground finch" },
                        new() { Name = "Small tree finch" },
                        new() { Name = "Green warbler-finch" }
                    }
                });
        }

        entities.Add(
            new Darwin
            {
                Id = 1777,
                Species = new Species { Name = "Goldfish (with legs)" },
                MixedMetaphors = new List<Species>
                {
                    new() { Name = "Large ground finch" },
                    new() { Name = "Medium ground finch" },
                    new() { Name = "Small tree finch" },
                    new() { Name = "Green warbler-finch" }
                }
            });

        for (var i = 0; i < 2; i++)
        {
            ExecuteWithStrategyInTransaction(
                context =>
                {
                    context.AddRange(entities);

                    foreach (var entity in entities.Take(100))
                    {
                        Assert.Equal(0, entity.Id);
                        Assert.Null(entity._id);
                    }

                    Assert.Equal(1777, entities[100].Id);

                    var tempValueIdentityMap = entities.ToDictionary(
                        e => context.Entry(e).Property(p => p.Id).CurrentValue,
                        e => e);

                    var stateManager = context.GetService<IStateManager>();
                    var key = context.Model.FindEntityType(typeof(Darwin)).FindPrimaryKey();

                    foreach (var entity in entities)
                    {
                        Assert.Same(
                            entity,
                            stateManager.TryGetEntry(
                                key,
                                new object[] { context.Entry(entity).Property(p => p.Id).CurrentValue }).Entity);
                    }

                    // DbUpdateException : An error occurred while updating the entries. See the
                    // inner exception for details.
                    // SqlException : Cannot insert explicit value for identity column in table
                    // 'Blog' when IDENTITY_INSERT is set to OFF.
                    var updateException = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
                    Assert.Single(updateException.Entries);

                    foreach (var entity in entities.Take(100))
                    {
                        Assert.Equal(0, entity.Id);
                        Assert.Null(entity._id);
                        Assert.Null(entity.Species.DarwinId);
                        foreach (var species in entity.MixedMetaphors)
                        {
                            Assert.Null(species.MetaphoricId);
                        }
                    }

                    Assert.Equal(1777, entities[100].Id);
                    Assert.Equal(1777, entities[100].Species.DarwinId);
                    foreach (var species in entities[100].MixedMetaphors)
                    {
                        Assert.Equal(1777, species.MetaphoricId);
                    }

                    foreach (var entity in entities)
                    {
                        Assert.Same(
                            entity,
                            tempValueIdentityMap[context.Entry(entity).Property(p => p.Id).CurrentValue]);
                    }

                    foreach (var entity in entities)
                    {
                        Assert.Same(
                            entity,
                            stateManager.TryGetEntry(
                                key,
                                new object[] { context.Entry(entity).Property(p => p.Id).CurrentValue }).Entity);
                    }
                });
        }
    }

    public class StoreGeneratedSqlServerFixture : StoreGeneratedFixtureBase
    {
        protected override ITestStoreFactory TestStoreFactory
            => SqlServerTestStoreFactory.Instance;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => builder
                .EnableSensitiveDataLogging()
                .ConfigureWarnings(
                    b => b.Default(WarningBehavior.Throw)
                        .Ignore(CoreEventId.SensitiveDataLoggingEnabledWarning)
                        .Ignore(RelationalEventId.BoolWithDefaultWarning));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.Entity<Gumball>(
                b =>
                {
                    b.Property(e => e.Id).UseIdentityColumn();
                    b.Property(e => e.Identity).HasDefaultValue("Banana Joe");
                    b.Property(e => e.IdentityReadOnlyBeforeSave).HasDefaultValue("Doughnut Sheriff");
                    b.Property(e => e.IdentityReadOnlyAfterSave).HasDefaultValue("Anton");
                    b.Property(e => e.AlwaysIdentity).HasDefaultValue("Banana Joe");
                    b.Property(e => e.AlwaysIdentityReadOnlyBeforeSave).HasDefaultValue("Doughnut Sheriff");
                    b.Property(e => e.AlwaysIdentityReadOnlyAfterSave).HasDefaultValue("Anton");
                    b.Property(e => e.Computed).HasDefaultValue("Alan");
                    b.Property(e => e.ComputedReadOnlyBeforeSave).HasDefaultValue("Carmen");
                    b.Property(e => e.ComputedReadOnlyAfterSave).HasDefaultValue("Tina Rex");
                    b.Property(e => e.AlwaysComputed).HasDefaultValue("Alan");
                    b.Property(e => e.AlwaysComputedReadOnlyBeforeSave).HasDefaultValue("Carmen");
                    b.Property(e => e.AlwaysComputedReadOnlyAfterSave).HasDefaultValue("Tina Rex");
                });

            modelBuilder.Entity<Anais>(
                b =>
                {
                    b.Property(e => e.OnAdd).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddUseBeforeUseAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddIgnoreBeforeUseAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddThrowBeforeUseAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddUseBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddIgnoreBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddThrowBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddUseBeforeThrowAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddIgnoreBeforeThrowAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddThrowBeforeThrowAfter).HasDefaultValue("Rabbit");

                    b.Property(e => e.OnAddOrUpdate).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddOrUpdateUseBeforeUseAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddOrUpdateIgnoreBeforeUseAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddOrUpdateThrowBeforeUseAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddOrUpdateUseBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddOrUpdateIgnoreBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddOrUpdateThrowBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddOrUpdateUseBeforeThrowAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddOrUpdateIgnoreBeforeThrowAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnAddOrUpdateThrowBeforeThrowAfter).HasDefaultValue("Rabbit");

                    b.Property(e => e.OnUpdate).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnUpdateUseBeforeUseAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnUpdateIgnoreBeforeUseAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnUpdateThrowBeforeUseAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnUpdateUseBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnUpdateIgnoreBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnUpdateThrowBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnUpdateUseBeforeThrowAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnUpdateIgnoreBeforeThrowAfter).HasDefaultValue("Rabbit");
                    b.Property(e => e.OnUpdateThrowBeforeThrowAfter).HasDefaultValue("Rabbit");
                });

            modelBuilder.Entity<WithBackingFields>(
                b =>
                {
                    b.Property(e => e.NullableAsNonNullable).HasComputedColumnSql("1");
                    b.Property(e => e.NonNullableAsNullable).HasComputedColumnSql("1");
                });

            modelBuilder.Entity<WithNullableBackingFields>(
                b =>
                {
                    b.Property(e => e.NullableBackedBoolTrueDefault).HasDefaultValue(true);
                    b.Property(e => e.NullableBackedIntNonZeroDefault).HasDefaultValue(-1);
                    b.Property(e => e.NullableBackedBoolFalseDefault).HasDefaultValue(false);
                    b.Property(e => e.NullableBackedIntZeroDefault).HasDefaultValue(0);
                });

            modelBuilder.Entity<WithObjectBackingFields>(
                b =>
                {
                    b.Property(e => e.NullableBackedBoolTrueDefault).HasDefaultValue(true);
                    b.Property(e => e.NullableBackedIntNonZeroDefault).HasDefaultValue(-1);
                    b.Property(e => e.NullableBackedBoolFalseDefault).HasDefaultValue(false);
                    b.Property(e => e.NullableBackedIntZeroDefault).HasDefaultValue(0);
                });

            modelBuilder.Entity<NonStoreGenDependent>().Property(e => e.HasTemp).HasDefaultValue(777);

            modelBuilder.Entity<CompositePrincipal>().Property(e => e.Id).UseIdentityColumn();

            base.OnModelCreating(modelBuilder, context);
        }
    }
}
