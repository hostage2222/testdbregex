using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace TestDbRegex;

public class RegexContext : DbContext
{
    public DbSet<RegexEntity> RegexEntities { get; set; } = null!;

    //The connection string is for the default postgres database
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql(
            "Host=localhost;Database=postgres;Username=postgres;Password=postgres");

    // create table injection_regex_table (field text unique);
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<RegexEntity>(options => options.HasNoKey());
        modelBuilder.Entity<RegexEntity>().ToTable("injection_regex_table")
            .Property(e => e.Field).HasColumnName("field");
    }
}

public record RegexEntity(string Field);

public static class Program
{
    public static void Main()
    {
        using var dbContext = new RegexContext();

        CreateDatabaseTable(dbContext);

        var userInput = "';delete from injection_regex_table;select '";

        var equalExpression = CreateEqualExpression(userInput);
        var equalQuery = dbContext.RegexEntities.Where(equalExpression);

        Console.WriteLine("Equality expression:");
        TestQuery(dbContext, equalQuery);

        var regexExpression = CreateRegexExpression(userInput);
        var regexQuery = dbContext.RegexEntities.Where(regexExpression);

        Console.WriteLine("Regex expression:");
        TestQuery(dbContext, regexQuery);
    }

    private static void TestQuery(RegexContext context, IQueryable<RegexEntity> query)
    {
        Console.WriteLine($"Query: \n{query.ToQueryString()}\n");
        Console.WriteLine($"Count before executing the query = {context.Set<RegexEntity>().Count()}");
        var _ = query.ToList();
        Console.WriteLine($"Count after executing the query = {context.Set<RegexEntity>().Count()}\n");
    }

    //Create expression regexEntity => regexEntity.Field == "userInput"
    private static Expression<Func<RegexEntity, bool>> CreateEqualExpression(string userInput)
    {
        Expression<Func<RegexEntity, string>> expressionToSearch = regexEntity => regexEntity.Field;
        var parameter = Expression.Parameter(typeof(RegexEntity));
        var exprInvoke = Expression.Invoke(expressionToSearch, parameter);

        var argument = Expression.Constant(userInput);
        var equalExpression = Expression.Equal(exprInvoke, argument);

        return (Expression<Func<RegexEntity, bool>>)Expression.Lambda(equalExpression, parameter);
    }

    //Create expression Regex.IsMatch(regexEntity => regexEntity.Field, "userInput", RegexOptions.IgnoreCase)
    private static Expression<Func<RegexEntity, bool>> CreateRegexExpression(string userInput)
    {
        Expression<Func<RegexEntity, string>> expressionToSearch = regexEntity => regexEntity.Field;
        var parameter = Expression.Parameter(typeof(RegexEntity));
        var exprInvoke = Expression.Invoke(expressionToSearch, parameter);

        var regexIsMatchMethod = typeof(Regex).GetMethod(nameof(Regex.IsMatch),
            [typeof(string), typeof(string), typeof(RegexOptions)])!;
        //The problem is here!
        var argument = Expression.Constant(userInput);
        var regOptions = Expression.Constant(RegexOptions.IgnoreCase);
        var regexIsMatchCall = Expression.Call(regexIsMatchMethod, exprInvoke, argument, regOptions);

        return (Expression<Func<RegexEntity, bool>>)Expression.Lambda(regexIsMatchCall, parameter);
    }

    private static void CreateDatabaseTable(RegexContext context)
    {
        context.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS public.injection_regex_table (field TEXT UNIQUE)");
        context.Database.ExecuteSqlRaw(
            "INSERT INTO public.injection_regex_table (field) VALUES ('a'), ('b'), ('c') ON CONFLICT DO NOTHING");
    }
}