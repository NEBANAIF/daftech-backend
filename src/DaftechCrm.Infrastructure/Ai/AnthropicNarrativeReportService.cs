using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Infrastructure.Ai;

/// <summary>
/// Calls the Anthropic Messages API directly over HttpClient — no SDK
/// dependency, since this is a small, single-purpose call. See
/// IAiNarrativeReportService for the graceful-degradation contract this
/// must honor: any failure here returns Available=false, never throws
/// out to the caller.
/// </summary>
public class AnthropicNarrativeReportService : IAiNarrativeReportService
{
    private readonly HttpClient _http;
    private readonly AiReportingOptions _options;
    private readonly ILogger<AnthropicNarrativeReportService> _logger;

    public AnthropicNarrativeReportService(HttpClient http, IOptions<AiReportingOptions> options, ILogger<AnthropicNarrativeReportService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiPerformanceSummaryResult> SummarizeEmployeePerformanceAsync(EmployeePerformanceMetrics metrics, CancellationToken ct = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return new AiPerformanceSummaryResult(false, null, "AI reporting is not configured.");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var prompt = BuildPrompt(metrics);

            var requestBody = new
            {
                model = _options.Model,
                max_tokens = 400,
                messages = new[] { new { role = "user", content = prompt } },
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiBaseUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("x-api-key", _options.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("AI reporting call failed with {StatusCode}: {Body}", response.StatusCode, body);
                return new AiPerformanceSummaryResult(false, null, $"AI service returned {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            var narrative = ExtractNarrative(json);

            return narrative is null
                ? new AiPerformanceSummaryResult(false, null, "AI service returned an unexpected response shape.")
                : new AiPerformanceSummaryResult(true, narrative, null);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AI reporting call timed out after {Seconds}s", _options.TimeoutSeconds);
            return new AiPerformanceSummaryResult(false, null, "AI service timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI reporting call failed unexpectedly.");
            return new AiPerformanceSummaryResult(false, null, "AI service is unavailable.");
        }
    }

    private static string BuildPrompt(EmployeePerformanceMetrics m)
    {
        return $"""
            Write a brief (3-5 sentence) narrative performance summary for a support technician,
            based only on the following metrics. Be factual and neutral — note strengths and one
            area for improvement if the data suggests one, but do not invent numbers not given here.

            Employee: {m.EmployeeName}
            Tickets assigned: {m.TicketsAssigned}
            Tickets resolved: {m.TicketsResolved}
            Average resolution time (hours): {m.AverageResolutionHours?.ToString("0.0") ?? "no data"}
            On-time resolution rate: {m.OnTimeRate:0}%
            Average client satisfaction score: {m.AverageSatisfactionScore?.ToString("0") ?? "no ratings yet"} / 100
            Total hours worked (period): {m.TotalHoursWorked:0.0}
            """;
    }

    private static string? ExtractNarrative(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return null;

        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) && type.GetString() == "text"
                && block.TryGetProperty("text", out var text))
            {
                sb.Append(text.GetString());
            }
        }

        var result = sb.ToString().Trim();
        return result.Length > 0 ? result : null;
    }
}
