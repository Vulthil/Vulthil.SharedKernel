using Microsoft.EntityFrameworkCore;
using WebApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<WebApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("postgres")));

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapOpenApi();

await app.RunAsync();

public partial class Program;
