using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

// Yeni bir tablo yanlışlıkla public şemaya düşerse canlı Supabase'te
// uygulama hesabı o tabloya erişemez (42501). Bu test, modeldeki her
// tablonun growth şemasına eşlendiğini CI'da kanıtlar.
public sealed class SchemaMappingTests
{
    [Fact]
    public async Task Open_evaluations_are_unique_per_brand()
    {
        await using var factory = new WorkflowApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var index = db.Model.FindEntityType(typeof(BrandEvaluation))!.GetIndexes()
            .SingleOrDefault(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["BrandId"]));
        Assert.NotNull(index);
        Assert.Contains("0, 1, 2", index.GetFilter());
    }
    [Fact]
    public async Task Every_mapped_table_lives_in_growth_schema()
    {
        await using var factory = new WorkflowApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tables = db.Model.GetEntityTypes()
            .Where(t => !t.IsOwned())
            .Select(t => new { Entity = t.ClrType.Name, Schema = t.GetSchema(), Table = t.GetTableName() })
            .ToList();
        Assert.NotEmpty(tables);
        Assert.True(tables.Count >= 50, $"Modelde en az 50 tablo bekleniyordu, {tables.Count} tablo bulundu.");
        foreach (var t in tables)
            Assert.True(t.Schema == "growth", $"{t.Entity} → {t.Schema}.{t.Table} growth şemasında olmalı.");
    }
}
