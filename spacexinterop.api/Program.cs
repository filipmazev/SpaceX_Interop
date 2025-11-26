using spacexinterop.api._Common.Utility.Clients.Interfaces;
using spacexinterop.api._Common.Utility.Mapper.Interfaces;
using spacexinterop.api._Common.Utility.Clients;
using spacexinterop.api._Common.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using spacexinterop.api._Common._Configs;
using spacexinterop.api.Infrastructure;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using spacexinterop.api.Data.Models;
using Microsoft.Extensions.Options;
using spacexinterop.api._Common;
using spacexinterop.api.Data;
using Scalar.AspNetCore;
using Azure.Identity;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

#region Configure Builder

#region Core

builder.Services.AddControllers();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddOpenApi();

builder.Services.AddDependencies();

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}
else
if (builder.Environment.IsProduction())
{
    string keyVaultUrl = builder.Configuration.GetSection("AzureKeyVault:VaultUri").Value!;

    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUrl),
        new DefaultAzureCredential()
    );
}

builder.Services.AddMemoryCache();

builder.Services.Configure<EncryptionKeyConfig>(builder.Configuration.GetSection("EncryptionKeys"));

#region Space X API Interop

builder.Services.Configure<SpaceXApiConfig>(builder.Configuration.GetSection("ExternalApis:SpaceXApi"));

builder.Services.AddHttpClient<ISpaceXClient, SpaceXClient>((sp, client) =>
{
    SpaceXApiConfig options = sp.GetRequiredService<IOptions<SpaceXApiConfig>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

#endregion

#endregion

#region Database Context Setup

if (builder.Environment.IsDevelopment() 
    && (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
{
    builder.Services
        .AddDbContext<MainContext>(options => options
            .UseNpgsql(builder.Configuration.GetConnectionString("PGSQLConnection"))
            .LogTo(Console.WriteLine));  
}
else
{
    builder.Services.AddDbContext<MainContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("MainConnection"))
            .LogTo(Console.WriteLine));
}

#endregion

#region CORS Policy Setup
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        string[]? allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
        if (allowedOrigins is not null && allowedOrigins.Length != 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});
#endregion

#region Authentication & Identity

builder.Services.AddAuthorization();

builder.Services
    .AddIdentity<User, IdentityRole>(options =>
    {
        options.Password.RequiredLength = Constants.MinPasswordLength;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;

        options.User.RequireUniqueEmail = true;

        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(Constants.UserLockoutTimespanInMinutes);
        options.Lockout.MaxFailedAccessAttempts = Constants.MaxFailedAccessAttempts;

        options.Stores.ProtectPersonalData = true;
    })
    .AddEntityFrameworkStores<MainContext>()
    .AddDefaultTokenProviders()
    .AddPersonalDataProtection<LookupProtector, LookupProtectorKeyRing>();

builder.Services
    .ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = "spacexinterop_auth";
        options.Cookie.HttpOnly = true;                                                 // XSS protection (disable JS access)
        options.ExpireTimeSpan = TimeSpan.FromHours(Constants.SessionDurationInHours);  // Session duration
        options.SlidingExpiration = true;                                               // Extend session on activity
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.None;

        options.Cookie.Path = "/";

        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

#endregion

#region Rate Limiter Setup

builder.Services.ConfigureModel<RateLimiterConfig>(builder.Configuration.GetSection("RateLimiter"));
builder.Services.AddConfiguredRateLimiter(builder.Configuration);

#endregion

#endregion

WebApplication app = builder.Build();

#region Configure App

#region Service Scope

using IServiceScope scope = app.Services.CreateScope();

IMappingConfig mappingConfig = app.Services.GetRequiredService<IMappingConfig>();
mappingConfig.RegisterMappings();

#endregion

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("SpaceX Interop API Documentation");
        options.WithTheme(ScalarTheme.BluePlanet);
        options.WithSidebar();
    });

    app.ApplyMigrations();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowedOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.MapControllers();

#endregion

app.Run();