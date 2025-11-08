using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Internal {
    internal class QueryFields {
        // Generic Identifiers
        public const string ID = "@ID";
        public const string GUID = "@GUID";
        public const string DEF_VERSION = "@DEF_VERSION";
        public const string PARENT = "@PARENT";
        public const string VERSION = "@VERSION";

        // Common Metadata
        public const string DISPLAY_NAME = "@DISPLAY_NAME";
        public const string NAME = "@NAME";
        public const string DESCRIPTION = "@DESCRIPTION";
        public const string CATEGORY = "@CATEGORY";
        public const string DATA = "@DATA";
        public const string ENV = "@ENV";

        // State / Event / Transition Fields
        public const string FROM_STATE = "@FROM_STATE";
        public const string TO_STATE = "@TO_STATE";
        public const string EVENT = "@EVENT";
        public const string FLAGS = "@FLAGS";
        public const string GUARD_CONDITION = "@GUARD_CONDITION";

        // Instance Fields
        public const string CURRENT_STATE = "@CURRENT_STATE";
        public const string EXTERNAL_REF = "@EXTERNAL_REF";
        public const string EXTERNAL_TYPE = "@EXTERNAL_TYPE";

        // Transition Log Fields
        public const string INSTANCE_ID = "@INSTANCE_ID";
        public const string ACTOR = "@ACTOR";
        public const string METADATA = "@METADATA";

        // Audit / Timestamp Fields
        public const string CREATED = "@CREATED";
        public const string MODIFIED = "@MODIFIED";
    }
}
