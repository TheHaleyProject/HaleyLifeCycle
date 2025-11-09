using System;

namespace Haley.Enums {

    [Flags]
    public enum LifeCycleTransitionFlag {
        None = 0,
        IsSystem = 1 << 0, // 1  - triggered internally by the system
        IsAuto = 1 << 1, // 2  - executes automatically without external event
        IsManual = 1 << 2, // 4  - requires explicit user action
        NeedsApproval = 1 << 3,
        CanRetry = 1 << 4,
        IsCritical = 1 << 5, // 8  - critical transition (e.g., requires approval)
        IsRollback = 1 << 6  // 16 - represents rollback transition
    }
}