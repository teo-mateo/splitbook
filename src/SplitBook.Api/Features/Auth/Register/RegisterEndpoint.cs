using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Auth.Register;

public static class RegisterEndpoint
{
    public static void MapRegister(this RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterHandler.HandleAsync);
    }
}
