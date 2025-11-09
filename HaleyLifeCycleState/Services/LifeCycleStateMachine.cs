using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;

namespace Haley.Services {
    public class LifeCycleStateMachine : ILifeCycleStateMachine {
        private readonly ILifeCycleStateRepository _repo;
        // dictionary: transition_name (or event_name) -> guard delegate
        private readonly Dictionary<string, Func<object, Task<bool>>> _guards;
        public event Func<TransitionEventArgs, Task>? OnBeforeTransition;
        public event Func<TransitionEventArgs, Task>? OnAfterTransition;
        public event Func<TransitionEventArgs, Task>? OnTransitionFailed;


        public LifeCycleStateMachine(ILifeCycleStateRepository repo) { 
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _guards = new Dictionary<string, Func<object, Task<bool>>>(StringComparer.OrdinalIgnoreCase);
        }

        #region Helpers

        private static string GetRefType<TEntity>() => typeof(TEntity).Name.ToLowerInvariant();
        private static int ToInt(object v) => v == null ? 0 : Convert.ToInt32(v);
        private static long ToLong(object v) => v == null ? 0L : Convert.ToInt64(v);
        private static string? ToStr(object v) => v?.ToString();

        private static LifeCycleState MapState(Dictionary<string, object> row) => new LifeCycleState {
            Id = ToInt(row["id"]),
            DisplayName = ToStr(row["display_name"]),
            Flags = (LifeCycleStateFlag)ToInt(row["flags"]),
            DefinitionVersion = ToInt(row["def_version"]),
            Category = row.ContainsKey("category") ? ToStr(row["category"]) : null
        };

        private static LifeCycleInstance MapInstance(Dictionary<string, object> row) => new LifeCycleInstance {
            Id = ToLong(row["id"]),
            Guid = Guid.Parse(ToStr(row["guid"]) ?? Guid.Empty.ToString()),
            CurrentState = ToInt(row["current_state"]),
            LastEvent = ToInt(row["last_event"]),
            ExternalRef = ToStr(row["external_ref"]),
            ExternalType = ToStr(row["external_type"]),
            DefinitionVersion = ToInt(row["def_version"]),
            Flags = (LifeCycleInstanceFlag)ToInt(row["flags"])
        };

        private async Task ThrowIfFailed<T>(IFeedback<T> feedback, string context) {
            if (feedback == null || !feedback.Status) {
                if (_repo.ThrowExceptions)
                    throw new InvalidOperationException($"{context} failed: {feedback?.Message}");
            }
        }

        private async Task RaiseAsync(Func<TransitionEventArgs, Task>? handler, LifeCycleTransitionLog? log = null, Exception? ex = null) {
            if (handler != null)
                await handler(new TransitionEventArgs(log, ex));
        }

        #endregion

        public async Task<bool> ReceiveAckAsync(string messageId) {
            var fb = await _repo.Ack_MarkReceived(messageId);
            return fb != null && fb.Status;
        }

        public async Task RetryUnackedAsync(int retryAfterMinutes = 2) {
            var fb = await _repo.Ack_GetPending(retryAfterMinutes);
            if (fb?.Result == null) return;

            foreach (var row in fb.Result) {
                var ackId = Convert.ToInt64(row["id"]);
                var transitionLogId = Convert.ToInt64(row["transition_log"]);
                var messageId = row["message_id"]?.ToString();

                // Re-publish the same notification (idempotent)
                //await _notifier.PublishTransition(messageId, transitionLogId);

                await _repo.Ack_Bump(ackId);
            }
        }

        public void RegisterGuard(string transitionKey, Func<object, Task<bool>> guardFunc) {
            _guards[transitionKey] = guardFunc;
        }

        #region Instance Retrieval

        public async Task<LifeCycleInstance?> GetInstanceAsync(string externalRefType, Guid externalRefId) {
            var fb = await _repo.GetInstancesByRef(externalRefId.ToString());
            if (fb == null || !fb.Status || fb.Result == null || fb.Result.Count == 0) return null;
            var match = fb.Result.Find(x => string.Equals(ToStr(x["external_type"]), externalRefType, StringComparison.OrdinalIgnoreCase));
            return match != null ? MapInstance(match) : null;
        }

        public Task<LifeCycleInstance?> GetInstanceAsync<TEntity>(Guid externalRefId) => GetInstanceAsync(GetRefType<TEntity>(), externalRefId);

        #endregion

        #region Initialization

        public async Task InitializeAsync(string externalRefType, Guid externalRefId, int definitionVersion) {
            var initFb = await _repo.GetInitialState(definitionVersion);
            await ThrowIfFailed(initFb, "GetInitialState");
            if (initFb.Result == null) throw new InvalidOperationException($"No initial state for def_version {definitionVersion}");

            int initStateId = ToInt(initFb.Result["id"]);
            var regFb = await _repo.RegisterInstance(definitionVersion, initStateId, 0, externalRefId.ToString(), externalRefType, LifeCycleInstanceFlag.Active);
            await ThrowIfFailed(regFb, "RegisterInstance");
        }

        public Task InitializeAsync<TEntity>(Guid externalRefId, int definitionVersion) => InitializeAsync(GetRefType<TEntity>(), externalRefId, definitionVersion);

        #endregion

        #region Trigger

        public async Task<bool> TriggerAsync(string externalRefType, Guid externalRefId, Guid toStateId, string comment = null, object? context = null) {
            LifeCycleTransitionLog? log = null;
            try {
                var instance = await GetInstanceAsync(externalRefType, externalRefId);
                if (instance == null) throw new InvalidOperationException("Instance not found.");

                var fromState = instance.CurrentState;
                var transitionFb = await _repo.GetOutgoingTransitions(fromState, instance.DefinitionVersion);
                var transition = transitionFb.Result?.Find(x => Convert.ToInt32(x["to_state"]) == Convert.ToInt32(toStateId));
                if (transition == null) throw new InvalidOperationException($"Invalid transition {fromState} → {toStateId}");

                var transitionName = transition["event"]?.ToString() ?? $"T_{fromState}_{toStateId}";

                // Guard lookup
                if (_guards.TryGetValue(transitionName, out var guardFunc)) {
                    bool allowed = await guardFunc(context);
                    if (!allowed) throw new InvalidOperationException($"Guard condition failed for transition {transitionName}");
                }

                // Prepare transition log (in-memory model)
                log = new LifeCycleTransitionLog {
                    InstanceId = instance.Id,
                    FromState = fromState,
                    ToState = Convert.ToInt32(toStateId),
                    Event = Convert.ToInt32(transition["event"]),
                    Actor = "system",
                    Flags = LifeCycleTransitionLogFlag.Manual,
                    Metadata = comment,
                    Created = DateTime.UtcNow
                };

                // Before hook (unchanged)
                await RaiseAsync(OnBeforeTransition, log);

                // Persist transition + update instance
                var logIdFb = await _repo.LogTransition(instance.Id, fromState, log.ToState, log.Event, log.Actor, log.Flags, log.Metadata);
                await ThrowIfFailed(logIdFb, "LogTransition"); // INSERT ...; SELECT LAST_INSERT_ID(); returns id
                var updFb = await _repo.UpdateInstanceState(instance.Id, log.ToState, log.Event, instance.Flags);
                await ThrowIfFailed(updFb, "UpdateInstanceState");

                // --- ACK: mark SENT for this transition (minimal blue-tick record) ---
                // messageId lets the client send back an acknowledgement later
                var messageId = Guid.NewGuid().ToString();
                // NOTE: requires repo method: Ack_Insert(messageId, transitionLogId)
                // ack_log table already exists in schema
                await _repo.Ack_Insert(messageId, logIdFb.Result);

                // After hook (unchanged). If you want the client to see messageId via events,
                // you can pass it inside log.Metadata (append) or extend TransitionEventArgs later.
                await RaiseAsync(OnAfterTransition, log);

                return true;
            } catch (Exception ex) {
                await RaiseAsync(OnTransitionFailed, log, ex);
                return false;
            }
        }


        public Task<bool> TriggerAsync<TEntity>(Guid externalRefId, Guid toStateId, string? comment = null, object? context = null) => TriggerAsync(GetRefType<TEntity>(), externalRefId, toStateId, comment,context);

        #endregion

        #region Validation

        public async Task<bool> ValidateTransitionAsync(Guid fromStateId, Guid toStateId) {
            var fb = await _repo.GetTransition(ToInt(fromStateId), 0, 0);
            return fb != null && fb.Status && fb.Result != null && ToInt(fb.Result["to_state"]) == ToInt(toStateId);
        }

        #endregion

        #region Current State

        public async Task<LifeCycleState> GetCurrentStateAsync(string externalRefType, Guid externalRefId) {
            var instance = await GetInstanceAsync(externalRefType, externalRefId) ?? throw new InvalidOperationException($"Instance not found for {externalRefType}:{externalRefId}");
            var fb = await _repo.GetStateByName(instance.DefinitionVersion, instance.CurrentState.ToString());
            await ThrowIfFailed(fb, "GetStateByName");
            return fb.Result != null ? MapState(fb.Result) : throw new InvalidOperationException($"State not found for {instance.CurrentState}");
        }

        public Task<LifeCycleState> GetCurrentStateAsync<TEntity>(Guid externalRefId) => GetCurrentStateAsync(GetRefType<TEntity>(), externalRefId);

        #endregion

        #region Transition History

        public async Task<IReadOnlyList<LifeCycleTransitionLog?>> GetTransitionHistoryAsync(string externalRefType, Guid externalRefId) {
            var instance = await GetInstanceAsync(externalRefType, externalRefId);
            if (instance == null) return Array.Empty<LifeCycleTransitionLog>();

            var fb = await _repo.GetLogsByInstance(instance.Id);
            if (fb == null || !fb.Status || fb.Result == null) return Array.Empty<LifeCycleTransitionLog>();

            var list = new List<LifeCycleTransitionLog>();
            foreach (var r in fb.Result) {
                list.Add(new LifeCycleTransitionLog {
                    Id = ToLong(r["id"]),
                    InstanceId = ToLong(r["instance_id"]),
                    FromState = ToInt(r["from_state"]),
                    ToState = ToInt(r["to_state"]),
                    Event = ToInt(r["event"]),
                    Actor = ToStr(r["actor"]),
                    Metadata = ToStr(r["metadata"]),
                    Flags = (LifeCycleTransitionLogFlag)ToInt(r["flags"]),
                    Created = Convert.ToDateTime(r["created"])
                });
            }
            return list;
        }

        public Task<IReadOnlyList<LifeCycleTransitionLog?>> GetTransitionHistoryAsync<TEntity>(Guid externalRefId) => GetTransitionHistoryAsync(GetRefType<TEntity>(), externalRefId);

        #endregion

        #region Force Update

        public async Task ForceUpdateStateAsync(string externalRefType, Guid externalRefId, Guid newStateId, LifeCycleTransitionLogFlag flags = LifeCycleTransitionLogFlag.System) {
            var instance = await GetInstanceAsync(externalRefType, externalRefId) ?? throw new InvalidOperationException($"Instance not found for {externalRefType}:{externalRefId}");
            var logFb = await _repo.LogTransition(instance.Id, instance.CurrentState, ToInt(newStateId), 0, "system", flags, "Force update");
            await ThrowIfFailed(logFb, "LogTransition");
            var updFb = await _repo.UpdateInstanceState(instance.Id, ToInt(newStateId), 0, instance.Flags);
            await ThrowIfFailed(updFb, "UpdateInstanceState");
        }

        public Task ForceUpdateStateAsync<TEntity>(Guid externalRefId, Guid newStateId, LifeCycleTransitionLogFlag flags = LifeCycleTransitionLogFlag.System) => ForceUpdateStateAsync(GetRefType<TEntity>(), externalRefId, newStateId, flags);

        #endregion

        #region State Checks

        public async Task<bool> IsFinalStateAsync(Guid stateId) {
            var fb = await _repo.GetStateByName(0, stateId.ToString());
            return fb != null && fb.Status && fb.Result != null && ((LifeCycleStateFlag)ToInt(fb.Result["flags"])).HasFlag(LifeCycleStateFlag.IsFinal);
        }

        public async Task<bool> IsInitialStateAsync(Guid stateId) {
            var fb = await _repo.GetStateByName(0, stateId.ToString());
            return fb != null && fb.Status && fb.Result != null && ((LifeCycleStateFlag)ToInt(fb.Result["flags"])).HasFlag(LifeCycleStateFlag.IsInitial);
        }

        #endregion
    }
}
