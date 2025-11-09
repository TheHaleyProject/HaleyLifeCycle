using System;

namespace Haley.Models {
    public class LifeCycleDefinition {
        public int Id { get; set; }
        public Guid Guid { get; set; }
        public string DisplayName { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public int Env { get; set; }  // 0=Dev,1=Test,2=UAT,3=Prod
        public DateTime Created { get; set; }
    }
}
