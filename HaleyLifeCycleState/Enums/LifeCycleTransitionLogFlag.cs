using System;

namespace Haley.Enums {

    [Flags]
    public enum LifeCycleTransitionLogFlag : int {
        None = 0,
        System = 1 << 0,
        Manual = 1 << 1,
        Retry = 1 << 2,
        Rollback = 1 << 3
    }
}
