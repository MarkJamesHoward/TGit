using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TGitApi.Data;
using TGitApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder
    .Services.AddOpenTelemetry()
    .WithTracing(tracing =>
        tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter()
    )
    .WithMetrics(metrics =>
        metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter()
    );

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Register storage service based on configuration
var storageType = builder.Configuration["Storage:Type"]?.ToLowerInvariant() ?? "json";
if (storageType == "sql")
{
    builder.Services.AddDbContextFactory<TGitDbContext>(options =>
        options.UseSqlServer(builder.Configuration["Sql:ConnectionString"])
    );
    builder.Services.AddSingleton<IStorageService, SqlStorageService>();
}
else
{
    builder.Services.AddSingleton<IStorageService, JsonStorageService>();
}

var app = builder.Build();

// Auto-create SQL tables if using SQL storage
if (storageType == "sql")
{
    using var scope = app.Services.CreateScope();
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TGitDbContext>>();
    using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TGit API v1");
});

app.UseCors();

app.MapControllers();

app.Run();
