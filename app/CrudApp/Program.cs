using CrudApp.Data;
using CrudApp.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration from environment variables ----
var port       = Environment.GetEnvironmentVariable("APP_PORT")    ?? "8080";
var dbHost     = Environment.GetEnvironmentVariable("DB_HOST")     ?? "localhost";
var dbPort     = Environment.GetEnvironmentVariable("DB_PORT")     ?? "3306";
var dbName     = Environment.GetEnvironmentVariable("DB_NAME")     ?? "cruddb";
var dbUser     = Environment.GetEnvironmentVariable("DB_USER")     ?? "cruduser";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "crudpass";

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var connectionString =
    $"Server={dbHost};Port={dbPort};Database={dbName};User={dbUser};Password={dbPassword};";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 4, 0))));

var app = builder.Build();

// ---- Create the table on startup (retry while the DB is still starting) ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    const int maxAttempts = 10;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            logger.LogInformation("Database ready.");
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}",
                attempt, maxAttempts, ex.Message);
            Thread.Sleep(3000);
        }
    }
}

// ---- Health endpoints ----
// /health    -> is the app process alive?
// /health/db -> can the app reach MariaDB?
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/health/db", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "ok", database = "reachable" })
        : Results.Json(new { status = "error", database = "unreachable" }, statusCode: 503));

// ---- CRUD endpoints ----
app.MapPost("/items", async (ItemInput input, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(input.Name))
        return Results.BadRequest(new { error = "name is required" });

    var item = new Item { Name = input.Name, Description = input.Description };
    db.Items.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/items/{item.Id}", item);
});

app.MapGet("/items", async (AppDbContext db) =>
    Results.Ok(await db.Items.AsNoTracking().OrderBy(i => i.Id).ToListAsync()));

app.MapGet("/items/{id:int}", async (int id, AppDbContext db) =>
    await db.Items.FindAsync(id) is Item item
        ? Results.Ok(item)
        : Results.NotFound(new { error = "Item not found" }));

app.MapPut("/items/{id:int}", async (int id, ItemInput input, AppDbContext db) =>
{
    var item = await db.Items.FindAsync(id);
    if (item is null) return Results.NotFound(new { error = "Item not found" });

    if (!string.IsNullOrWhiteSpace(input.Name)) item.Name = input.Name;
    if (input.Description is not null) item.Description = input.Description;

    await db.SaveChangesAsync();
    return Results.Ok(item);
});

app.MapDelete("/items/{id:int}", async (int id, AppDbContext db) =>
{
    var item = await db.Items.FindAsync(id);
    if (item is null) return Results.NotFound(new { error = "Item not found" });

    db.Items.Remove(item);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Item deleted", item });
});

app.Run();