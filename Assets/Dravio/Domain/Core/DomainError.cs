using System;
using System.Collections.Generic;

namespace Dravio.Domain.Core
{
    /// <summary>
    /// Mã lỗi có kiểu của command bus (src/engine/core/errors.ts). Giữ đủ 30 mã của bản TS để khóa i18n
    /// <c>commandError.&lt;code&gt;</c> không đổi. U2.1 phát các mã của feed/consumeFood/pet/rename/advanceTime;
    /// U2.3 thêm moveDragon/setProductionSlot/unlockHabitat/surveyHabitat/viewBiologyProfile.
    /// </summary>
    public enum DomainErrorCode
    {
        [WireName("invalid-command")] InvalidCommand,
        [WireName("unknown-dragon")] UnknownDragon,
        [WireName("dragon-unavailable")] DragonUnavailable,
        [WireName("zone-locked")] ZoneLocked,
        [WireName("habitat-locked")] HabitatLocked,
        [WireName("food-too-fast")] FoodTooFast,
        [WireName("food-limit")] FoodLimit,
        [WireName("unknown-food")] UnknownFood,
        [WireName("food-expired")] FoodExpired,
        [WireName("wrong-diet")] WrongDiet,
        [WireName("wrong-place")] WrongPlace,
        [WireName("feed-cooldown")] FeedCooldown,
        [WireName("pet-cooldown")] PetCooldown,
        [WireName("name-empty")] NameEmpty,
        [WireName("name-too-long")] NameTooLong,
        [WireName("name-invalid-chars")] NameInvalidChars,
        // Mã của world/habitat (U2.3) và của gói trứng/hướng dẫn (chưa port: tutorial-pack-used,
        // pack-pending, no-pending-pack, not-enough-gold). Giữ đủ để danh mục mã và khóa i18n khớp bản TypeScript.
        [WireName("zone-full")] ZoneFull,
        [WireName("habitat-unsuitable")] HabitatUnsuitable,
        [WireName("already-there")] AlreadyThere,
        [WireName("not-placed")] NotPlaced,
        [WireName("production-budget-full")] ProductionBudgetFull,
        [WireName("unknown-habitat")] UnknownHabitat,
        [WireName("habitat-already-unlocked")] HabitatAlreadyUnlocked,
        [WireName("unlock-requirement-missing")] UnlockRequirementMissing,
        [WireName("discovery-already-known")] DiscoveryAlreadyKnown,
        [WireName("survey-requirement-missing")] SurveyRequirementMissing,
        [WireName("tutorial-pack-used")] TutorialPackUsed,
        [WireName("pack-pending")] PackPending,
        [WireName("no-pending-pack")] NoPendingPack,
        [WireName("not-enough-gold")] NotEnoughGold,
    }

    /// <summary>Lỗi nghiệp vụ trả về từ dispatch. Không phải exception: lệnh bị từ chối là luồng bình thường.</summary>
    public sealed class DomainError
    {
        public DomainError(DomainErrorCode code, string detail = null)
        {
            Code = code;
            Detail = detail;
        }

        public DomainErrorCode Code { get; }

        /// <summary>Mã dạng chuỗi giống TypeScript, ví dụ "pet-cooldown".</summary>
        public string WireCode => Code.ToWire();

        /// <summary>Khóa i18n để UI hiển thị: <c>commandError.&lt;code&gt;</c>.</summary>
        public string MessageKey => "commandError." + WireCode;

        public string Detail { get; }

        public override string ToString() => Detail == null ? WireCode : WireCode + ": " + Detail;
    }

    /// <summary>
    /// Vi phạm bất biến domain (dữ liệu hỏng, ID catalog không tồn tại, tràn số tiền).
    /// Khác <see cref="DomainError"/>: đây là lỗi lập trình hoặc save hỏng, không phải lệnh bị từ chối.
    /// </summary>
    public sealed class DomainInvariantException : Exception
    {
        public DomainInvariantException(string message) : base(message) { }
    }

    /// <summary>Kết quả kiểm tra GameState: danh sách lỗi dạng "path: lý do" giống validateGameState của TS.</summary>
    public sealed class ValidationResult
    {
        public ValidationResult(IReadOnlyList<string> errors) { Errors = errors ?? Array.Empty<string>(); }
        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
        public override string ToString() => IsValid ? "valid" : string.Join("; ", Errors);
    }
}
