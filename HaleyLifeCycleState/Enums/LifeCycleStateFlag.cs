using System;
using System.Collections.Generic;
using System.Text;

namespace Haley.Enums {
    [Flags]
    public enum LifeCycleStateFlag : int {
        None = 0,
        IsInitial = 1 << 0,
        IsFinal = 1 << 1,
        IsSystem = 1 << 2,
        IsError = 1 << 3
    }
}