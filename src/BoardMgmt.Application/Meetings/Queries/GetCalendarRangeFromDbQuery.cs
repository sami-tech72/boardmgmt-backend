// Application/Calendars/Queries/GetCalendarRangeFromDbQuery.cs
using BoardMgmt.Application.Calendars;
using MediatR;
using System;

namespace BoardMgmt.Application.Calendars.Queries;

public sealed record GetCalendarRangeFromDbQuery(DateTimeOffset StartUtc, DateTimeOffset EndUtc)
  : IRequest<IReadOnlyList<CalendarEventDto>>;
