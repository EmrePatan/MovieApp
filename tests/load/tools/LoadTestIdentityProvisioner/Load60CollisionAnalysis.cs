using MovieApp.Domain.Entities;

namespace MovieApp.LoadTestIdentityProvisioner;

public enum Load60SlotStatus
{
    EligibleForCreation,
    ExistingDedicated,
    BlockedCollision,
}

public sealed record Load60SlotPlan(Load60IdentitySlot Slot, Load60SlotStatus Status, string? Detail);

public sealed record Load60CollisionAnalysis(
    IReadOnlyList<Load60SlotPlan> Slots,
    bool CanWrite)
{
    public bool HasBlockedCollision => Slots.Any(s => s.Status == Load60SlotStatus.BlockedCollision);
}

public static class Load60CollisionAnalyzer
{
    public static Load60CollisionAnalysis Analyze(
        IReadOnlyList<Load60IdentitySlot> plannedSlots,
        IReadOnlyDictionary<string, User> existingByNormalizedEmail,
        string emailDomain)
    {
        var plans = new List<Load60SlotPlan>(plannedSlots.Count);
        foreach (var slot in plannedSlots)
        {
            if (existingByNormalizedEmail.TryGetValue(slot.NormalizedEmail, out var existing))
            {
                if (Load60IdentityFormats.IsDedicatedLoad60NormalizedEmail(existing.NormalizedEmail, emailDomain) &&
                    string.Equals(existing.Email, slot.Email, StringComparison.OrdinalIgnoreCase))
                {
                    plans.Add(new Load60SlotPlan(slot, Load60SlotStatus.ExistingDedicated, "Already provisioned."));
                }
                else
                {
                    plans.Add(new Load60SlotPlan(
                        slot,
                        Load60SlotStatus.BlockedCollision,
                        "Normalized email is owned by a non-LOAD60 user."));
                }
            }
            else
            {
                plans.Add(new Load60SlotPlan(slot, Load60SlotStatus.EligibleForCreation, null));
            }
        }

        var canWrite = !plans.Any(p => p.Status == Load60SlotStatus.BlockedCollision);
        return new Load60CollisionAnalysis(plans, canWrite);
    }
}
