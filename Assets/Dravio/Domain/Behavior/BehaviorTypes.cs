using System;

namespace Dravio.Domain.Behavior
{
    // U2.9 — Sinh học, cá tính và hành vi (ADR 0203, đề xuất). Toàn bộ module này CHỈ ĐỌC GameState:
    // không có lệnh, không đổi save, không ảnh hưởng vàng / XP / bond / vitals.
    // Chuỗi wire (camelCase) dùng cho i18n `behavior.<wire>` và cho tầng Sanctuary; không lưu trong save.

    /// <summary>11 hành vi của spec U2.9. Thứ tự cố định (dùng làm thứ tự duyệt và thứ tự trong DTO).</summary>
    public enum DragonBehavior { Idle, Rest, Sleep, Explore, SeekFood, Eat, Play, Bask, Swim, Fly, Burrow }

    /// <summary>
    /// Vai trò của MỘT quyết định (không phải của loại hành vi):
    /// Gameplay = suy ra từ state gameplay có thật (thức ăn rơi, vừa được cho ăn, ca chăm sóc, nơi trú, vitals thấp) và có thể dẫn tới lệnh có sẵn;
    /// Presentation = chọn ngẫu nhiên có trọng số chỉ để trình diễn, không bao giờ dẫn tới lệnh.
    /// </summary>
    public enum BehaviorRole { Gameplay, Presentation }

    /// <summary>
    /// Pha ngày đêm, trùng ranh giới dữ liệu Environment (OriginSanctuaryLiving / OceanLiving / SkyLiving.json):
    /// dawn 5:00–7:00, day 7:00–17:30, dusk 17:30–19:30, night 19:30–5:00. Domain không giữ đồng hồ thế giới:
    /// Unity truyền pha hiện tại (EnvironmentTimeOfDay.Phase) hoặc giờ (<see cref="DayCycle.PhaseOfMinute"/>).
    /// </summary>
    public enum DayPhase { Dawn, Day, Dusk, Night }

    /// <summary>Gợi ý hiển thị cho tầng Sanctuary. Tên trùng <c>Dravio.Character.DragonLocomotion</c> nhưng Domain không tham chiếu Character.</summary>
    public enum LocomotionHint { Idle, Walk, Run, Swim, Fly, Burrow, Sleep }

    /// <summary>Tương tác một lần, trùng <c>Dravio.Character.DragonInteraction</c> (None / Eat / Happy).</summary>
    public enum InteractionHint { None, Eat, Happy }

    public static class DayCycle
    {
        public const int DawnStartMinute = 5 * 60;
        public const int DayStartMinute = 7 * 60;
        public const int DuskStartMinute = 17 * 60 + 30;
        public const int NightStartMinute = 19 * 60 + 30;
        public const int MinutesPerDay = 24 * 60;

        /// <summary>Phút trong ngày (bất kỳ số nguyên nào, tự quấn 0–1439) → pha.</summary>
        public static DayPhase PhaseOfMinute(int minuteOfDay)
        {
            int m = ((minuteOfDay % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;
            if (m >= DawnStartMinute && m < DayStartMinute) return DayPhase.Dawn;
            if (m >= DayStartMinute && m < DuskStartMinute) return DayPhase.Day;
            if (m >= DuskStartMinute && m < NightStartMinute) return DayPhase.Dusk;
            return DayPhase.Night;
        }

        /// <summary>
        /// Mức hoạt động 0–100 của loài theo pha (ActivityCycle G0): ban ngày 100/60/10 (ngày/chạng vạng/đêm),
        /// ban đêm đối xứng, "both" 80 ngày và đêm, 100 lúc chạng vạng. Đề xuất ADR 0203 B2; chỉ ảnh hưởng hành vi trình diễn.
        /// </summary>
        public static int Activeness(ActivityCycle cycle, DayPhase phase)
        {
            bool twilight = phase == DayPhase.Dawn || phase == DayPhase.Dusk;
            switch (cycle)
            {
                case ActivityCycle.Day: return phase == DayPhase.Day ? 100 : twilight ? 60 : 10;
                case ActivityCycle.Night: return phase == DayPhase.Night ? 100 : twilight ? 60 : 10;
                case ActivityCycle.Both: return twilight ? 100 : 80;
                default: throw new ArgumentOutOfRangeException(nameof(cycle));
            }
        }
    }
}
