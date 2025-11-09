using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal class QRY_ACK_LOG {
        public const string INSERT = @$"INSERT INTO ack_log (transition_log, ack_status, message_id) VALUES ({TRANSITION_LOG}, 1, {MESSAGE_ID}); SELECT LAST_INSERT_ID();";
        public const string ACK = @$"UPDATE ack_log SET ack_status = 2, modified = utc_timestamp() WHERE message_id = {MESSAGE_ID};";
        public const string RETRYQ = @$"SELECT * FROM ack_log WHERE ack_status = 1 AND TIMESTAMPDIFF(MINUTE, created, utc_timestamp()) > {RETRY_AFTER_MIN};";
        public const string BUMP = @$"UPDATE ack_log SET retry_count = retry_count + 1, last_retry = utc_timestamp() WHERE id = {ID};";
    }
}
