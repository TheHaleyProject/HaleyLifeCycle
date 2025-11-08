using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal class QRY_INSTANCE {
        public const string INSERT = $@"INSERT IGNORE INTO instance (last_event, current_state, external_ref, external_type, flags, def_version) VALUES ({EVENT}, {CURRENT_STATE}, {EXTERNAL_REF}, {EXTERNAL_TYPE}, {FLAGS}, {DEF_VERSION}); SELECT id FROM instance WHERE external_ref = {EXTERNAL_REF} AND def_version = {DEF_VERSION} LIMIT 1;";
        public const string GET_BY_ID = $@"SELECT * FROM instance WHERE id = {ID};";
        public const string GET_BY_REF = $@"SELECT * FROM instance WHERE external_ref = {EXTERNAL_REF};";
        public const string DELETE = $@"DELETE FROM instance WHERE id = {ID};";
    }
}
