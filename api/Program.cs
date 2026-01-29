using TGitApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register storage service based on configuration
var storageType = builder.Configuration["Storage:Type"]?.ToLowerInvariant() ?? "json";
if (storageType == "cosmos")
{
    builder.Services.AddSingleton<IStorageService, CosmosStorageService>();
}
else
{
    builder.Services.AddSingleton<IStorageService, JsonStorageService>();
}

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TGit API v1");
});

app.UseCors();

app.MapControllers();

app.Run();
