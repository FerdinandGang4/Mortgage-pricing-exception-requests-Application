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

// Keep enum values readable in the API contract and reject numeric enum input.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// The header-based scheme is for local demos; its handler refuses non-Development requests.
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
// One shared lock coordinates all scoped workflow services in this API process.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InMemoryWriteLock>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPricingExceptionService, PricingExceptionService>();
builder.Services.AddScoped<IApprovedDiscountService, ApprovedDiscountService>();
// InMemory keeps setup self-contained, but all workflow data is lost on shutdown.
builder.Services.AddDbContext<PricingExceptionsDbContext>(options =>
    options.UseInMemoryDatabase(builder.Configuration["Database:Name"] ?? "PricingExceptions"));

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
    await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context.HttpContext });
});

// Resolve the scoped DbContext before serving requests and seed demo identities only locally.
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

// Authentication builds the user claims before authorization checks endpoint access.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;

