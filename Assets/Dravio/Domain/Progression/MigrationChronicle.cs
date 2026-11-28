using System;
using System.Collections.Generic;
using System.Globalization;
using Dravio.Domain.Core;

namespace Dravio.Domain.Progression
{
    public static class MigrationChronicle
    {
        static readonly DateTime OpeningDay = new DateTime(2026, 11, 28);
        static readonly long OpeningMs = new DateTimeOffset(OpeningDay, TimeSpan.FromHours(7)).ToUnixTimeMilliseconds();
        // Leave room for displaying next year's celebrations for extremely large imported clocks.
        static readonly long LastMs = new DateTimeOffset(9998, 12, 30, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        // Each chapter uses a permanent milestone so returning players keep their place.
        public static int ChaptersCleared(GameState state)
        {
            if (state.Player.Stats.TotalFeedClicks == 0) return 0;
            if (!state.FindZone("origin").UnlockedHabitatIds.Contains("origin_pondbank")) return 1;
            if (state.Player.Stats.TotalHatched == 0) return 2;
            return state.ExpeditionsClaimed.TryGetValue("origin_forest", out int trips) && trips > 0 ? 4 : 3;
        }

        public sealed class Visit
        {
            public readonly string Id, Vietnamese, English;
            public readonly DateTime Start, End;
            public readonly string Action;
            internal Visit(string id, string vi, string en, DateTime start, int days, string action)
            {
                Id = id; Vietnamese = vi; English = en; Start = start; End = start.AddDays(days); Action = action;
            }
            public string Stamp => Id + "/" + Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            public bool Active(DateTime day) => day >= Start && day < End;
        }

        public static DateTime LocalDay(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(0, Math.Min(ms, LastMs))).UtcDateTime.AddHours(7).Date;

        public static List<Visit> Agenda(DateTime day)
        {
            if (day < OpeningDay) day = OpeningDay;
            var visits = new List<Visit>();
            var saturday = day.Date.AddDays(-(((int)day.DayOfWeek + 1) % 7));
            if (day >= saturday.AddDays(2)) saturday = saturday.AddDays(7);
            visits.Add(new Visit("picnic", "Bữa ăn bên suối", "Creekside picnic", saturday, 2, "foodConsumed"));
            var first = new DateTime(day.Year, day.Month, 1);
            if (day >= first.AddDays(3)) first = first.AddMonths(1);
            visits.Add(new Visit("tracks", "Ba ngày tìm dấu chân", "Three days of tracks", first, 3, "dragonObserved"));
            Annual("newyear", "Cánh bay đầu năm", "First flight of the year", 1, 1, 3, "expeditionClaimed");
            Annual("national", "Đàn rồng mừng Quốc khánh", "National Day gathering", 9, 2, 2, "foodConsumed");
            Annual("winter", "Đêm canh tổ Giáng sinh", "Christmas nest watch", 12, 24, 3, "dragonHatched");
            var special = new Visit("first-flock", "Đợt di cư đầu tiên", "The first migration",
                OpeningDay, 9, "dragonObserved");
            if (day < special.End) visits.Add(special);
            visits.Sort((a,b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : string.CompareOrdinal(a.Id,b.Id));
            return visits;

            void Annual(string id, string vi, string en, int month, int date, int length, string action)
            {
                var start = new DateTime(day.Year, month, date);
                if (day >= start.AddDays(length)) start = start.AddYears(1);
                visits.Add(new Visit(id, vi, en, start, length, action));
            }
        }

        public static Visit ReadStamp(string stamp)
        {
            if (string.IsNullOrEmpty(stamp) || stamp.Length > 48) return null;
            int split = stamp.IndexOf('/');
            if (split < 1 || !DateTime.TryParseExact(stamp.Substring(split+1), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) || day < OpeningDay || day.Year > 9998) return null;
            return Agenda(day).Find(v => v.Stamp == stamp);
        }

        public static void Record(GameState state, long nowMs, List<DomainEvent> events)
        {
            // The calendar launches with the first migration. Ignore non-calendar simulation clocks.
            if (nowMs < OpeningMs || nowMs > LastMs) return;
            List<Visit> agenda = null;
            foreach (var e in events)
            {
                if (!(e is FoodConsumed || e is DragonObserved || e is ExpeditionClaimed || e is DragonHatched)) continue;
                var day = LocalDay(nowMs);
                if (agenda == null) agenda = Agenda(day);
                foreach (var visit in agenda)
                    if (visit.Active(day) && visit.Action == e.Type && !state.FestivalStamps.Contains(visit.Stamp))
                        state.FestivalStamps.Add(visit.Stamp);
            }
        }
    }
}
