using spacexinterop.api._Common.Utility.Validators.Interfaces;
using spacexinterop.api._Common.Utility.Factories.Interfaces;
using spacexinterop.api._Common.Domain.Data.Errors;
using spacexinterop.api._Common.Domain.Data.Result;
using spacexinterop.api.Services.Interfaces;
using spacexinterop.api.Data.Response;
using spacexinterop.api.Data.Request;
using spacexinterop.api.Data.Models;
using Microsoft.AspNetCore.Identity;
using spacexinterop.api._Common;

namespace spacexinterop.api.Services;

public class AuthService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    ILogger<AuthService> logger,
    IValidators validators,
    IResultFactory resultFactory)
    : IAuthService
{
    public async Task<Result<UserResponse?>> Login(LoginRequest request)
    {
        if (!validators.IsEmailValid(request.Email))
            return resultFactory.Failure<UserResponse?>(CommonError.Unauthorized, ResultStatusEnum.Unauthorized);

        try
        {
            User? user = await userManager.FindByEmailAsync(request.Email);

            if (user is null)
                return resultFactory.Failure<UserResponse?>(CommonError.Unauthorized);

            SignInResult result = await signInManager.PasswordSignInAsync(
                user, request.Password, request.RememberMe, lockoutOnFailure: true);

            return !result.Succeeded 
                ? resultFactory.Failure<UserResponse?>(CommonError.Unauthorized, ResultStatusEnum.Unauthorized)
                : resultFactory.Success<UserResponse?>(new UserResponse
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login failed");
            return resultFactory.Exception<UserResponse?>(ex, "An error occurred during login.");
        }
    }

    public async Task<Result> Logout() 
    {         
        try
        {
            await signInManager.SignOutAsync();
            return resultFactory.Success("Logged out");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Logout failed");
            return resultFactory.Exception(ex, "An error occurred during logout.");
        }
    }

    public async Task<Result<UserResponse?>> Register(RegisterRequest request)
    {
        if (!validators.IsEmailValid(request.Email))
            return resultFactory.FromStatus<UserResponse?>(ResultStatusEnum.ValidationFailed);

        User user = new()
        {
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email
        };

        try
        {
            IdentityResult result = await userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                return result.Errors.Any(e => e.Code == Constants.IdentityDuplicateEmailCode) 
                    ? resultFactory.Failure<UserResponse?>(AuthError.EmailAlreadyExists, ResultStatusEnum.EmailAlreadyExists) 
                    : resultFactory.Failure<UserResponse?>(AuthError.RegistrationFailed);
            }
            
            await signInManager.SignInAsync(user, isPersistent: true, authenticationMethod: null);

            return resultFactory.Success<UserResponse?>(new UserResponse
            {
                FirstName = user.FirstName,
                LastName = user.LastName
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Register failed");
            return resultFactory.Exception<UserResponse?>(ex, "An error occurred during registration.");
        }
    }

    public async Task<Result<UserResponse?>> ResolveUserByUserName(string? userName)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(userName))
                return resultFactory.FromStatus<UserResponse?>(ResultStatusEnum.InvalidRequest);

            User? user = await userManager.FindByNameAsync(userName);

            return user is null 
                ? resultFactory.FromStatus<UserResponse?>(ResultStatusEnum.NotFound) 
                : resultFactory.Success<UserResponse?>(new UserResponse
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ResolveUserByUserName failed");
            return resultFactory.Exception<UserResponse?>(ex, "An error occurred during user validation.");
        }
    }
}