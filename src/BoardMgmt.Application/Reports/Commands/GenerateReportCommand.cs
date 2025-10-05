using MediatR;

namespace BoardMgmt.Application.Reports.Commands;

public record GenerateReportCommand(
    string Type,              // attendance | voting | documents | performance | custom
    string Period,            // last-month | last-quarter | last-year | custom
    DateTimeOffset? Start,
    DateTimeOffset? End,
    bool IncludeCharts,
    bool IncludeData,
    bool IncludeSummary,
    bool IncludeRecommendations,
    string Format             // pdf | excel | powerpoint | html
) : IRequest<Guid>;
