using Haley.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haley.Models;

namespace Haley.Abstractions {
    /// <summary>
    /// Represents the abstraction for executing lifecycle state transitions,
    /// initialization, and instance management across multiple entity types.
    /// </summary>
    public interface ILifeCycleStateMachine {
        /// <summary>
        /// Gets the lifecycle instance by external reference type and ID.
        /// </summary>
        Task<LifeCycleInstance?> GetInstanceAsync(string externalRefType, Guid externalRefId);

        /// <summary>
        /// Initializes a new lifecycle instance to its initial state.
        /// </summary>
        Task InitializeAsync(string externalRefType, Guid externalRefId, int definitionVersion);

        /// <summary>
        /// Triggers a transition for a given entity instance.
        /// </summary>
        Task<bool> TriggerAsync(string externalRefType, Guid externalRefId, Guid toStateId, string comment = null);

        /// <summary>
        /// Validates if a transition between two states is allowed.
        /// </summary>
        Task<bool> ValidateTransitionAsync(Guid fromStateId, Guid toStateId);

        /// <summary>
        /// Retrieves the current state for a given instance.
        /// </summary>
        Task<LifeCycleState> GetCurrentStateAsync(string externalRefType, Guid externalRefId);

        /// <summary>
        /// Retrieves the full transition history (log) for a given instance.
        /// </summary>
        Task<IReadOnlyList<LifeCycleTransitionLog?>> GetTransitionHistoryAsync(string externalRefType, Guid externalRefId);

        /// <summary>
        /// Forcefully updates the current state of an instance (admin/system use).
        /// </summary>
        Task ForceUpdateStateAsync(string externalRefType, Guid externalRefId, Guid newStateId, LifeCycleTransitionLogFlag flags = LifeCycleTransitionLogFlag.System);

        /// <summary>
        /// Checks if the given state is final (cannot transition further).
        /// </summary>
        Task<bool> IsFinalStateAsync(Guid stateId);

        /// <summary>
        /// Checks if the given state is an initial (entry) state.
        /// </summary>
        Task<bool> IsInitialStateAsync(Guid stateId);
    }
}
