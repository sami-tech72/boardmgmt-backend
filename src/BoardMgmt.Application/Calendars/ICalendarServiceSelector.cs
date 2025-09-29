// Application/Calendars/ICalendarServiceSelector.cs
namespace BoardMgmt.Application.Calendars;


public interface ICalendarServiceSelector
{
    ICalendarService For(string provider);
}