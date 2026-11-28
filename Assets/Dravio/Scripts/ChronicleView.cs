using System;
using System.Globalization;
using System.Text;
using Dravio.Domain;
using Dravio.Domain.Progression;

namespace Dravio
{
    internal static class ChronicleView
    {
        static readonly string[] ChaptersVi = {
            "I · Tiếng cánh trong sương\nEmberling nghe tiếng gọi ngoài đồng cỏ nhưng đàn rồng còn nấp. Mùi gì có thể kéo chúng ra khỏi bụi cỏ?",
            "II · Vết bùn ở bờ hồ\nEmberling trở về với bùn xanh dưới móng. Dấu chân biến mất ở mép đồng. Đàn đã đi về phía nào?",
            "III · Ký ức trong vỏ trứng\nỞ mép nước có những vỏ trứng rỗng. Giữa đám vỏ cũ có một tiếng gõ. Con rồng tiếp theo sẽ đến từ đâu?",
            "IV · Đường về xuyên rừng\nTiếng gọi giờ vọng lại từ bên kia rừng. Làm sao biết lối ấy vẫn dẫn được cả đàn về nhà?"
        };
        static readonly string[] ChaptersEn = {
            "I · Wings in the mist\nEmberling hears wings beyond the meadow. What scent might draw the flock out of the grass?",
            "II · Tracks by the pond\nEmberling returns with blue mud on its claws. The tracks end at the edge of the meadow. Where did the flock go?",
            "III · Memory in an eggshell\nEmpty shells line the bank. Something taps among the old shells. Where will the next dragon come from?",
            "IV · The forest crossing\nThe answering call comes from beyond the trees. How could you find out whether the flock can still get home that way?"
        };

        static readonly string[] HintsVi = {
            "Thả một phần thức ăn xuống đất ở đồng cỏ.",
            "Trong mục Hướng dẫn, khảo sát Bờ hồ rồi mở sinh cảnh này.",
            "Nhận một rồng mới nở từ gói trứng hoặc tổ lai.",
            "Trong mục Quản lý, cử một rồng trưởng thành phù hợp đi Rừng Khởi Nguyên. Khi rồng về, nhận kết quả chuyến đi."
        };
        static readonly string[] HintsEn = {
            "Drop a portion of food in the meadow.",
            "In the guide, survey the Pondbank, then unlock the habitat.",
            "Claim a hatchling from an egg pack or the breeding nest.",
            "In management, send an eligible adult on the Origin Forest route. Claim the expedition when it returns."
        };

        public static string Story(GameState state, bool vi, bool showHint = false)
        {
            int done = MigrationChronicle.ChaptersCleared(state);
            var chapters = vi ? ChaptersVi : ChaptersEn;
            var text = new StringBuilder(vi ? "ĐƯỜNG DI CƯ ĐÃ MẤT" : "THE LOST MIGRATION");
            text.Append("   ").Append(done).Append("/4\n");
            if (done == chapters.Length)
                text.Append(vi ? "Kết · Khi rồng trinh sát trở về, cả đàn cất cánh theo lối cũ. Mùa sau, rồng non sẽ tự tìm được đường về bờ hồ." :
                    "Epilogue · The scout returns and the flock takes the old crossing. Next season the hatchlings will find their own way home.");
            else
            {
                text.Append(chapters[done]);
                if (showHint) text.Append("\n").Append(vi ? HintsVi[done] : HintsEn[done]);
            }
            if (done > 0)
            {
                text.Append(vi ? "\n\nĐã chinh phục" : "\n\nCompleted");
                for (int i = 0; i < done; i++) text.Append("\n").Append(chapters[i].Split('\n')[0]);
            }
            return text.ToString();
        }

        public static string Calendar(GameState state, DateTime today, bool vi)
        {
            var text = new StringBuilder(vi ? "LỊCH KHU BẢO TỒN · UTC+7" : "SANCTUARY CALENDAR · UTC+7");
            foreach (var visit in MigrationChronicle.Agenda(today))
            {
                text.Append("\n\n").Append(vi ? visit.Vietnamese : visit.English);
                text.Append(" · ").Append(visit.Start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))
                    .Append(" – ").Append(visit.End.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                bool earned = state.FestivalStamps.Contains(visit.Stamp);
                text.Append("\n").Append(earned ? (vi ? "Đã nhận dấu kỷ niệm" : "Stamp collected") :
                    visit.Active(today) ? (vi ? "Đang diễn ra" : "Active") : (vi ? "Sắp tới" : "Upcoming"));
                text.Append(" · ").Append(Objective(visit.Action, vi));
            }
            text.Append(vi ? "\n\nBộ sưu tập dấu · " : "\n\nStamp collection · ").Append(state.FestivalStamps.Count);
            for (int i = state.FestivalStamps.Count - 1; i >= Math.Max(0, state.FestivalStamps.Count - 5); i--)
            {
                var stamp = MigrationChronicle.ReadStamp(state.FestivalStamps[i]);
                if (stamp != null) text.Append("\n").Append(vi ? stamp.Vietnamese : stamp.English)
                    .Append(" · ").Append(stamp.Start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        static string Objective(string action, bool vi)
        {
            switch (action)
            {
                case "foodConsumed": return vi ? "Thả thức ăn và đợi một con rồng ăn." : "Drop food and wait for a dragon to eat.";
                case "dragonObserved": return vi ? "Ghi một quan sát về rồng." : "Record a dragon observation.";
                case "dragonHatched": return vi ? "Nhận một rồng mới nở." : "Claim a hatchling.";
                default: return vi ? "Nhận kết quả một chuyến thám hiểm." : "Claim a returning expedition.";
            }
        }
    }
}
