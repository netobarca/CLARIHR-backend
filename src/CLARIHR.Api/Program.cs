using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CLARIHR.Api.Configuration;
using CLARIHR.Api.Middleware;
using CLARIHR.Application;
using CLARIHR.Infrastructure;
using CLARIHR.Infrastructure.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog logging
Log.Logger = LoggingConfigurationExtensions.CreateLoggingConfiguration(builder.Environment).CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddProblemDetails();
builder.Services
    .AddControllers(options =>
    {
        options.ModelMetadataDetailsProviders.Add(new PublicContractBindingMetadataProvider());
        options.Conventions.Add(new PublicContractRouteConvention());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
            new PublicContractJsonTypeInfoResolver(),
            options.JsonSerializerOptions.TypeInfoResolver);
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type =>
        (type.FullName ?? type.Name).Replace('+', '.'));
    options.SchemaFilter<PublicContractSchemaFilter>();
    options.OperationFilter<PublicContractOperationFilter>();

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CLARIHR API",
        Version = "v1",
        Description = "Development-time API documentation for CLARIHR."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Bearer token. Example: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            }
        ] = Array.Empty<string>()
    });
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthorization();

ConfigureAuthentication(builder.Services, builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

await app.Services.InitializeInfrastructureAsync(app.Logger, app.Environment.IsDevelopment());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CLARIHR API v1");
        options.RoutePrefix = "swagger";
        options.DisplayRequestDuration();
    });
}

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<UnhandledExceptionMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

return;

static void ConfigureAuthentication(IServiceCollection services, IConfiguration configuration)
{
    var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
    if (jwtOptions is not { IsConfigured: true })
    {
        services.AddAuthentication();
        return;
    }

    services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = true;
            options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey!)),
                ClockSkew = TimeSpan.Zero
            };
        });
}

public partial class Program;
