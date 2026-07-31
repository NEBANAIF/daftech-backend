using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

public record SessionActivityDto(
    SessionAccountType AccountType,
    Guid AccountId,
    string AccountName,
    bool OnlineStatus,
    DateTimeOffset LastSeen,
    string? MostRecentIpAddress
);

public record LoginSessionDto(Guid Id, string IpAddress, DateTimeOffset LoginTime, DateTimeOffset? LogoutTime, bool OnlineStatus, DateTimeOffset LastSeen);
