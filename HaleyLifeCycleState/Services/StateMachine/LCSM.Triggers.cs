using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;

namespace Haley.Services {
    public partial class LifeCycleStateMachine {
        private async Task RaiseTransitionAsync(TransitionOccurred occurred) {
            var handler = TransitionRaised;
            if (handler == null) return;
            var delegates = handler.GetInvocationList();
            foreach (var d in delegates) {
                try {
                    if (d is Func<TransitionOccurred, Task> asyncHandler) await asyncHandler(occurred).ConfigureAwait(false);
                    else d.DynamicInvoke(occurred);
                } catch {
                    // Swallow handler exceptions to avoid breaking SM flow; 
                }
            }
        }

        public async Task<bool> TriggerAsync(int definitionVersion, string externalRef, int eventCode, string? actor = null, string? comment = null, object? context = null) {
            if (definitionVersion <= 0) throw new ArgumentOutOfRangeException(nameof(definitionVersion));
            var normalizedRef = externalRef.Normalize();
            if (string.IsNullOrWhiteSpace(normalizedRef)) throw new ArgumentNullException(nameof(externalRef));

            try {
                // 1) Load instance by def_version + external_ref
                var instFb = await Repository.GetInstancesByRef(normalizedRef).ConfigureAwait(false);
                EnsureSuccess(instFb, "GetInstancesByRef");
                var instRows = instFb.Result ?? new List<Dictionary<string, object>>();
                var instRow = instRows.Find(r => r.GetLong("def_version") == definitionVersion);
                if (instRow == null) throw new InvalidOperationException($"Instance not found for def_version={definitionVersion}, externalRef='{normalizedRef}'.");

                var instance = new LifeCycleInstance {
                    Id = instRow.GetLong("id"),
                    DefinitionVersion = instRow.GetInt("def_version"),
                    CurrentState = instRow.GetInt("current_state"),
                    LastEvent = instRow.GetInt("last_event"),
                    ExternalRef = instRow.GetString("external_ref") ?? normalizedRef,
                    Flags = (LifeCycleInstanceFlag)instRow.GetInt("flags"),
                    Created = DateTime.UtcNow
                };

                // 2) Resolve event by code for this definition version
                var evFb = await Repository.GetEventsByVersion(definitionVersion).ConfigureAwait(false);
                EnsureSuccess(evFb, "GetEventsByVersion");
                var evRows = evFb.Result ?? new List<Dictionary<string, object>>();
                var evRow = evRows.Find(r => r.GetInt("code") == eventCode);
                if (evRow == null) throw new InvalidOperationException($"Event with code {eventCode} not found for def_version={definitionVersion}.");

                var eventId = evRow.GetInt( "id");
                var eventName = evRow.GetString( "display_name") ?? evRow.GetString("name") ?? eventCode.ToString();

                // 3) Resolve transition: current_state + eventId -> next state
                var trFb = await Repository.GetOutgoingTransitions(instance.CurrentState, definitionVersion).ConfigureAwait(false);
                EnsureSuccess(trFb, "GetOutgoingTransitions");
                var trRows = trFb.Result ?? new List<Dictionary<string, object>>();
                var candidates = trRows.FindAll(t => t.GetInt("event") == eventId);

                if (candidates.Count == 0) throw new InvalidOperationException($"No transition found for state={instance.CurrentState}, eventId={eventId}, def_version={definitionVersion}.");
                if (candidates.Count > 1) throw new InvalidOperationException($"Multiple transitions found for state={instance.CurrentState}, eventId={eventId}, def_version={definitionVersion}. Guard logic not implemented.");

                var trRow = candidates[0];
                var fromStateId = trRow.GetInt("from_state");
                var toStateId = trRow.GetInt("to_state");

                // 4) Prepare log data
                var actorValue = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
                var metadata = BuildMetadata(comment, context);

                // 5) Write transition_log
                var logFb = await Repository.LogTransition(instance.Id, fromStateId, toStateId, eventId, actorValue, metadata).ConfigureAwait(false);
                EnsureSuccess(logFb, "LogTransition");
                var logId = logFb.Result;

                // 6) Update instance state
                var updFb = await Repository.UpdateInstanceState(instance.Id, toStateId, eventId, instance.Flags).ConfigureAwait(false);
                EnsureSuccess(updFb, "UpdateInstanceState");

                // 7) Insert ack row (event bus)
                var messageId = Guid.NewGuid().ToString("N");
                var ackFb = await Repository.Ack_InsertWithMessage(logId, 1, messageId, 1).ConfigureAwait(false); // consumer=1 for now
                EnsureSuccess(ackFb, "Ack_InsertWithMessage");

                // 8) Raise in-process event (optional)
                var occurred = new TransitionOccurred {
                    TransitionLogId = logId,
                    InstanceId = instance.Id,
                    DefinitionVersion = instance.DefinitionVersion,
                    ExternalRef = instance.ExternalRef ?? normalizedRef,
                    FromStateId = fromStateId,
                    ToStateId = toStateId,
                    EventId = eventId,
                    EventCode = eventCode,
                    EventName = eventName,
                    Actor = actorValue,
                    Metadata = metadata,
                    Created = DateTime.UtcNow
                };

                await RaiseTransitionAsync(occurred).ConfigureAwait(false);
                return true;
            } catch {
                if (Repository.ThrowExceptions) throw;
                return false;
            }
        }

        public Task<bool> TriggerAsync(int definitionVersion, Guid externalRefId, int eventCode, string? actor = null, string? comment = null, object? context = null) {
            return TriggerAsync(definitionVersion, externalRefId.ToString("D"), eventCode, actor, comment, context);
        }

        public async Task<bool> TriggerByNameAsync(int definitionVersion, string externalRef, string eventName, string? actor = null, string? comment = null, object? context = null) {
            if (definitionVersion <= 0) throw new ArgumentOutOfRangeException(nameof(definitionVersion));
            if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentNullException(nameof(eventName));
            var normalizedRef = externalRef.Normalize();
            if (string.IsNullOrWhiteSpace(normalizedRef)) throw new ArgumentNullException(nameof(externalRef));
            var normalizedName = eventName.Trim();

            try {
                // Find event by name or display_name
                var evFb = await Repository.GetEventsByVersion(definitionVersion).ConfigureAwait(false);
                EnsureSuccess(evFb, "GetEventsByVersion");
                var evRows = evFb.Result ?? new List<Dictionary<string, object>>();
                var evRow = evRows.Find(r =>
                    string.Equals(r.GetString("name"), normalizedName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.GetString("display_name"), normalizedName, StringComparison.OrdinalIgnoreCase));

                if (evRow == null) throw new InvalidOperationException($"Event with name '{normalizedName}' not found for def_version={definitionVersion}.");

                var eventCode = evRow.GetInt("code");
                if (eventCode == 0) eventCode = evRow.GetInt("id"); // fallback

                return await TriggerAsync(definitionVersion, normalizedRef, eventCode, actor, comment, context).ConfigureAwait(false);
            } catch {
                if (Repository.ThrowExceptions) throw;
                return false;
            }
        }

        public Task<bool> TriggerByNameAsync(int definitionVersion, Guid externalRefId, string eventName, string? actor = null, string? comment = null, object? context = null) {
            return TriggerByNameAsync(definitionVersion, externalRefId.ToString("D"), eventName, actor, comment, context);
        }
    }
}
