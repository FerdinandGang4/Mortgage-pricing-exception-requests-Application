using Microsoft.EntityFrameworkCore;
using PricingExceptions.API.Data;
using PricingExceptions.API.Services;
using PricingExceptions.API.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InMemoryWriteLock>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPricingExceptionService, PricingExceptionService>();
builder.Services.AddScoped<IApprovedDiscountService, ApprovedDiscountService>();
builder.Services.AddDbContext<PricingExceptionsDbContext>(options =>
    options.UseInMemoryDatabase("PricingExceptions"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
    await database.Database.EnsureCreatedAsync();
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

app.UseAuthorization();

app.MapControllers();

app.Run();

