using Haley.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haley.Enums;

namespace Haley.Abstractions {

    /// <summary>
    /// Repository abstraction for managing Lifecycle / State Machine entities.
    /// Provides CRUD, audit, and maintenance operations for definitions, states,
    /// events, transitions, instances, and transition logs.
    /// </summary>
    public interface ILifeCycleStateRepository {
        bool ThrowExceptions { get; }

        // ----------------------------------------------------------
        // DEFINITION & VERSION MANAGEMENT
        // ----------------------------------------------------------
        Task<IFeedback<long>> RegisterDefinition(string displayName, string description, int env);
        Task<IFeedback<long>> RegisterDefinitionVersion(long parentId, int version, string jsonData);
        Task<IFeedback<List<Dictionary<string, object>>>> GetAllDefinitions();
        Task<IFeedback<Dictionary<string, object>>> GetDefinitionById(long id);
        Task<IFeedback<List<Dictionary<string, object>>>> GetVersionsByDefinition(long definitionId);
        Task<IFeedback<Dictionary<string, object>>> GetLatestDefinitionVersion(long definitionId);
        Task<IFeedback<bool>> DefinitionExists(string displayName, int env);
        Task<IFeedback<bool>> UpdateDefinitionDescription(long definitionId, string newDescription);
        Task<IFeedback<bool>> DeleteDefinition(long definitionId);

        // ----------------------------------------------------------
        // STATE MANAGEMENT
        // ----------------------------------------------------------
        Task<IFeedback<long>> RegisterState(string displayName, int defVersion, LifeCycleStateFlag flags, string category = null);
        Task<IFeedback<List<Dictionary<string, object>>>> GetStatesByVersion(int defVersion);
        Task<IFeedback<Dictionary<string, object>>> GetStateByName(int defVersion, string name);
        Task<IFeedback<Dictionary<string, object>>> GetInitialState(int defVersion);
        Task<IFeedback<Dictionary<string, object>>> GetFinalState(int defVersion);
        Task<IFeedback<bool>> UpdateStateFlags(int stateId, LifeCycleStateFlag newFlags);
        Task<IFeedback<bool>> DeleteState(int stateId);

        // ----------------------------------------------------------
        // EVENT MANAGEMENT
        // ----------------------------------------------------------
        Task<IFeedback<long>> RegisterEvent(string displayName, int defVersion);
        Task<IFeedback<List<Dictionary<string, object>>>> GetEventsByVersion(int defVersion);
        Task<IFeedback<Dictionary<string, object>>> GetEventByName(int defVersion, string name);
        Task<IFeedback<bool>> DeleteEvent(int eventId);

        // ----------------------------------------------------------
        // TRANSITION MANAGEMENT
        // ----------------------------------------------------------
        Task<IFeedback<long>> RegisterTransition(int fromState, int toState, int eventId, int defVersion, LifeCycleTransitionFlag flags, string guardCondition = null);
        Task<IFeedback<List<Dictionary<string, object>>>> GetTransitionsByVersion(int defVersion);
        Task<IFeedback<Dictionary<string, object>>> GetTransition(int fromState, int eventId, int defVersion);
        Task<IFeedback<List<Dictionary<string, object>>>> GetOutgoingTransitions(int fromState, int defVersion);
        Task<IFeedback<bool>> DeleteTransition(int transitionId);

        // ----------------------------------------------------------
        // INSTANCE MANAGEMENT
        // ----------------------------------------------------------
        Task<IFeedback<long>> RegisterInstance(long defVersion, int currentState, int lastEvent, string externalRef, string externalType, LifeCycleInstanceFlag flags);
        Task<IFeedback<Dictionary<string, object>>> GetInstanceById(long id);
        Task<IFeedback<List<Dictionary<string, object>>>> GetInstancesByRef(string externalRef);
        Task<IFeedback<List<Dictionary<string, object>>>> GetInstancesByState(int stateId);
        Task<IFeedback<List<Dictionary<string, object>>>> GetInstancesByFlags(LifeCycleInstanceFlag flags);
        Task<IFeedback<bool>> UpdateInstanceState(long instanceId, int newState, int lastEvent, LifeCycleInstanceFlag flags);
        Task<IFeedback<bool>> MarkInstanceCompleted(long instanceId);
        Task<IFeedback<bool>> DeleteInstance(long instanceId);

        // ----------------------------------------------------------
        // TRANSITION LOG / AUDIT
        // ----------------------------------------------------------
        Task<IFeedback<long>> LogTransition(long instanceId, int fromState, int toState, int eventId, string actor, LifeCycleTransitionLogFlag flags, string metadata = null);
        Task<IFeedback<List<Dictionary<string, object>>>> GetLogsByInstance(long instanceId);
        Task<IFeedback<List<Dictionary<string, object>>>> GetLogsByStateChange(int fromState, int toState);
        Task<IFeedback<List<Dictionary<string, object>>>> GetLogsByDateRange(DateTime from, DateTime to);
        Task<IFeedback<Dictionary<string, object>>> GetLatestLogForInstance(long instanceId);

        // ----------------------------------------------------------
        // MAINTENANCE / UTILITIES
        // ----------------------------------------------------------
        Task<IFeedback<int>> PurgeOldLogs(int daysToKeep);
        Task<IFeedback<int>> CountInstances(int defVersion, int flagsFilter = 0);
        Task<IFeedback> RebuildIndexes();
    }
}