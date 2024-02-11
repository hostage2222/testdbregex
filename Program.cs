using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace TestDbRegex;

public class RegexContext : DbContext
{
    public DbSet<RegexEntity> RegexEntities { get; set; } = null!;
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql("Host=localhost;Database=postgres;Username=postgres;Password=postgres");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<RegexEntity>(options => options.HasNoKey());
        modelBuilder.Entity<RegexEntity>().ToTable("regex_table").Property(e => e.Name).HasColumnName("name");
    }
}

public record RegexEntity(string Name);

public static class Program
{
    public static void Main()
    {
        using var db = new RegexContext();

        db.Database.ExecuteSql($"CREATE TABLE IF NOT EXISTS public.regex_table (name TEXT UNIQUE)");
        foreach (var name in (List<string>)["a", "b", "c"])
        {
            db.Database.ExecuteSql($"INSERT INTO public.regex_table (NAME) VALUES ({name}) ON CONFLICT DO NOTHING");
        }
        
        var userInput = "';delete from regex_table;select '";
        
        //Создаём выражение Regex.IsMatch(regexEntity => regexEntity.Name, "userInput", RegexOptions.IgnoreCase);
        Expression<Func<RegexEntity, string>> expressionToSearch = regexEntity => regexEntity.Name;
        var parameter = Expression.Parameter(typeof(RegexEntity));
        var exprInvoke = Expression.Invoke(expressionToSearch, parameter);
        
        var regexIsMatchMethod = typeof(Regex).GetMethod(nameof(Regex.IsMatch),
                [typeof(string), typeof(string), typeof(RegexOptions)])!;
        var argument = Expression.Constant(userInput);
        var regOptions = Expression.Constant(RegexOptions.IgnoreCase);
        var regexIsMatchCall = Expression.Call(regexIsMatchMethod, exprInvoke, argument, regOptions);
        
        var regexIsMatchLambda = (Expression<Func<RegexEntity, bool>>)Expression.Lambda(regexIsMatchCall, parameter);
        
        var query = db.RegexEntities.Where(regexIsMatchLambda);
        Console.WriteLine(query.Count());
        
        Console.WriteLine(db.Set<RegexEntity>().Count());
    }
}