using Haley.Abstractions;
using Haley.Enums;
using Haley.Internal;
using Haley.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Haley.Internal.QueryFields;

namespace Haley.Services {
    public partial class LifeCycleStateMariaDB {

        // ----------------------------------------------------------
        // Acknowledgement Management
        // ----------------------------------------------------------
        public async Task<IFeedback<long>> Ack_Insert(string messageId, long transitionLogId) {
            var fb = new Feedback<long>();
            try {
                var result = await _agw.Scalar(new AdapterArgs(_key) { Query = QRY_ACK_LOG.INSERT },
                    (MESSAGE_ID, messageId),
                    (TRANSITION_LOG, transitionLogId));

                if (result == null || !long.TryParse(result.ToString(), out var id))
                    return fb.SetMessage("Unable to insert ack_log.");

                return fb.SetStatus(true).SetResult(id);
            } catch (Exception ex) {
                _logger?.LogError(ex, "Ack_Insert failed.");
                if (ThrowExceptions) throw;
                return fb.SetMessage(ex.Message);
            }
        }

        // ACK: mark message as received
        public async Task<IFeedback<bool>> Ack_MarkReceived(string messageId) {
            var fb = new Feedback<bool>();
            try {
                await _agw.NonQuery(new AdapterArgs(_key) { Query = QRY_ACK_LOG.ACK },
                    (MESSAGE_ID, messageId));
                return fb.SetStatus(true).SetResult(true);
            } catch (Exception ex) {
                _logger?.LogError(ex, "Ack_MarkReceived failed.");
                if (ThrowExceptions) throw;
                return fb.SetMessage(ex.Message);
            }
        }

        // RETRY QUEUE: read messages still SENT and older than retryAfterMinutes
        public async Task<IFeedback<List<Dictionary<string, object>>>> Ack_GetPending(int retryAfterMinutes) {
            var fb = new Feedback<List<Dictionary<string, object>>>();
            try {
                var result = await _agw.Read(new AdapterArgs(_key) { Query = QRY_ACK_LOG.RETRYQ },
                    (RETRY_AFTER_MIN, retryAfterMinutes));

                if (result is not List<Dictionary<string, object>> list || list.Count == 0)
                    return fb.SetMessage("No pending ACK messages.");

                return fb.SetStatus(true).SetResult(list);
            } catch (Exception ex) {
                _logger?.LogError(ex, "Ack_GetPending failed.");
                if (ThrowExceptions) throw;
                return fb.SetMessage(ex.Message);
            }
        }

        // BUMP: increment retry_count and update last_retry
        public async Task<IFeedback<bool>> Ack_Bump(long ackId) {
            var fb = new Feedback<bool>();
            try {
                await _agw.NonQuery(new AdapterArgs(_key) { Query = QRY_ACK_LOG.BUMP },
                    (ID, ackId));
                return fb.SetStatus(true).SetResult(true);
            } catch (Exception ex) {
                _logger?.LogError(ex, "Ack_Bump failed.");
                if (ThrowExceptions) throw;
                return fb.SetMessage(ex.Message);
            }
        }
    }
}
