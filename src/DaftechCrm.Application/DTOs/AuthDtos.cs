using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

public record RefreshRequest(string RefreshToken);

public record RefreshResult(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);

public record LogoutRequest(SessionAccountType AccountType, Guid AccountId);
