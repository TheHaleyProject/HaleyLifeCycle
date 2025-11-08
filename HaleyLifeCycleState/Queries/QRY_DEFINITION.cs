using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal class QRY_DEFINITION {
        public const string INSERT = $@"INSERT IGNORE INTO definition (display_name, description, env) VALUES ({DISPLAY_NAME}, {DESCRIPTION}, {ENV}); SELECT id FROM definition WHERE display_name = {DISPLAY_NAME} AND env = {ENV} LIMIT 1;";
        public const string GET_BY_ID = $@"SELECT * FROM definition WHERE id = {ID};";
        public const string GET_ALL = "SELECT * FROM definition;";
        public const string DELETE = $@"DELETE FROM definition WHERE id = {ID};";
    }
}
