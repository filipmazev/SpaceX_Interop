using spacexinterop.api._Common.Utility.Validators.Interfaces;
using spacexinterop.api._Common.Utility.Factories.Interfaces;
using spacexinterop.api._Common.Utility.Clients.Interfaces;
using spacexinterop.api._Common.Utility.Mapper.Interfaces;
using spacexinterop.api._Common.Utility.Validators;
using spacexinterop.api._Common.Utility.Factories;
using spacexinterop.api.Repositories.Interfaces;
using spacexinterop.api._Common.Utility.Clients;
using spacexinterop.api._Common.Utility.Mapper;
using spacexinterop.api.Services.Interfaces;
using spacexinterop.api.Repositories;
using spacexinterop.api.Services;

namespace spacexinterop.api.Infrastructure;

public static class DependenciesBuilder
{
    public static void AddDependencies(this IServiceCollection services)
    {
        //#region Infrastructure

        services.AddSingleton<Microsoft.AspNetCore.Identity.ILookupProtectorKeyRing, LookupProtectorKeyRing>();
        services.AddSingleton<Microsoft.AspNetCore.Identity.ILookupProtector, LookupProtector>();
        services.AddSingleton<IMapper, Mapper>();

        services.AddScoped<IResultFactory, ResultFactory>();
        services.AddScoped<IValidators, Validators>();

        services.AddScoped<ISpaceXClient, SpaceXClient>();

        services.AddTransient<IMappingConfig, MappingConfig>();

        //#endregion

        //#region Services
        
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISpaceXService, SpaceXService>();

        //#endregion

        //#region Repositories

        services.AddScoped<ISpaceXLaunchesRepository, SpaceXLaunchesRepository>();

        //#endregion
    }
}