using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;
using PricingExceptions.API.Authentication;
using PricingExceptions.API.Data;
using PricingExceptions.API.Data.Seed;
using PricingExceptions.API.Middleware;
using PricingExceptions.API.Services;
using PricingExceptions.API.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddOpenApi(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = DevelopmentAuthenticationHandler.HeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description = "Development demo user ID. See the README for manager, reviewer and mortgage process IDs.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" }
            });
            return Task.CompletedTask;
        });
    }
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InMemoryWriteLock>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPricingExceptionService, PricingExceptionService>();
builder.Services.AddScoped<IApprovedDiscountService, ApprovedDiscountService>();
builder.Services.AddDbContext<PricingExceptionsDbContext>(options =>
    options.UseInMemoryDatabase("PricingExceptions"));

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
    await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context.HttpContext });
});

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
    await database.Database.EnsureCreatedAsync();
    if (app.Environment.IsDevelopment())
        await DevelopmentDataSeeder.SeedAsync(database);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Mortgage Pricing Exceptions API v1");
    });
}

// The HTTP launch profile has no HTTPS listener to redirect to.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;

