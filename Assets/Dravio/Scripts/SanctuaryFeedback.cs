using System;
using System.Globalization;
using Dravio.Domain.Catalog;
using Dravio.Domain;

namespace Dravio
{
    public static class SanctuaryFeedback
    {
        public static int FeedWaitSeconds(long? lastMeal, long now)
        {
            if (!lastMeal.HasValue) return 0;
            long remaining = CareRules.FeedXpCooldownMs - (now - lastMeal.Value);
            return (int)Math.Ceiling(Math.Max(0L, remaining) / 1000.0);
        }

        public static string UpgradePrice(long? price, long balanceMicro, bool vi)
        {
            if (!price.HasValue) return vi ? "Đã tối đa" : "Max level";
            string label = price.Value.ToString(CultureInfo.InvariantCulture) + " G";
            decimal missing = price.Value - balanceMicro / (decimal)MicroGold.MicroPerGold;
            if (missing <= 0) return label;
            string amount = missing.ToString("0.######", CultureInfo.InvariantCulture);
            if (vi) amount = amount.Replace('.', ',');
            return label + (vi ? " · Thiếu " : " · Need ") + amount + " G";
        }
    }
}
