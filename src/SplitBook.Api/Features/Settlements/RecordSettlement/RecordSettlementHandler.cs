using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Domain;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Settlements.RecordSettlement;

public static class RecordSettlementHandler
{
    public static async Task<Results<Created<SettlementDto>, ProblemHttpResult>> HandleAsync(
        Guid groupId,
        RecordSettlementRequest request,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        AppDbContext context)
    {
        // Idempotency check (with 24h window)
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await context.Settlements
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.IdempotencyKey == idempotencyKey);
            if (existing != null && existing.CreatedAt > DateTimeOffset.UtcNow.AddHours(-24))
            {
                return MapToCreated(existing, groupId);
            }
        }

        // Validate caller is a member of the group
        var currentUser = currentUserAccessor.GetCurrentUser(httpContext);
        var membership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == currentUser.Id && m.RemovedAt == null);
        if (membership == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        // Fetch group for currency validation
        var group = await context.Groups
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        // Validate request
        var validator = new RecordSettlementValidator();
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return TypedResults.Problem(
                title: "Validation Failed",
                detail: string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")),
                statusCode: 400
            );
        }

        // Validate currency matches group currency
        if (request.Currency.ToUpperInvariant() != group.Currency)
        {
            return TypedResults.Problem(
                title: "Currency Mismatch",
                detail: $"Settlement currency must match group currency '{group.Currency}'",
                statusCode: 400
            );
        }

        // Validate fromUserId != toUserId
        if (request.FromUserId == request.ToUserId)
        {
            return TypedResults.Problem(
                title: "Validation Failed",
                detail: "fromUserId and toUserId must be different",
                statusCode: 400
            );
        }

        // Validate both fromUserId and toUserId are group members
        var participantUserIds = new[] { request.FromUserId, request.ToUserId };
        var activeMemberships = await context.Memberships
            .Where(m => m.GroupId == groupId && participantUserIds.Contains(m.UserId) && m.RemovedAt == null)
            .ToListAsync();

        var activeMemberIds = new HashSet<Guid>(activeMemberships.Select(m => m.UserId));

        if (!activeMemberIds.Contains(request.FromUserId))
        {
            return TypedResults.Problem(
                title: "From user is not a member of the group",
                statusCode: 400
            );
        }

        if (!activeMemberIds.Contains(request.ToUserId))
        {
            return TypedResults.Problem(
                title: "To user is not a member of the group",
                statusCode: 400
            );
        }

        // Build settlement entity
        var settlement = new Settlement
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            FromUserId = request.FromUserId,
            ToUserId = request.ToUserId,
            AmountMinor = request.AmountMinor,
            Currency = group.Currency,
            OccurredOn = request.OccurredOn,
            CreatedAt = DateTimeOffset.UtcNow,
            IdempotencyKey = idempotencyKey,
        };

        context.Settlements.Add(settlement);
        await context.SaveChangesAsync();

        return MapToCreated(settlement, groupId);
    }

    private static Created<SettlementDto> MapToCreated(Settlement settlement, Guid groupId)
    {
        var dto = new SettlementDto(
            settlement.Id,
            settlement.GroupId,
            settlement.FromUserId,
            settlement.ToUserId,
            settlement.AmountMinor,
            settlement.Currency,
            settlement.OccurredOn,
            settlement.CreatedAt
        );

        return TypedResults.Created($"/groups/{groupId}/settlements/{settlement.Id}", dto);
    }
}
