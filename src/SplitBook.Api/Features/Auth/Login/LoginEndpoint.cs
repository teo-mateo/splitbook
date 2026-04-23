using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void MapLogin(this RouteGroupBuilder group)
    {
        group.MapPost("/login", LoginHandler.HandleAsync);
    }
}
