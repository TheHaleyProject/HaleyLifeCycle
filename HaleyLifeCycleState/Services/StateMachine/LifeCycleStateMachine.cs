using Haley.Abstractions;
using Haley.Models;
using System;
using System.Threading.Tasks;

namespace Haley.Services {
    public partial class LifeCycleStateMachine : ILifeCycleStateMachine {
        public ILifeCycleStateRepository Repository { get; }
        public event Func<TransitionOccurred, Task>? TransitionRaised;
        public event Func<StateMachineError, Task>? TransitionErrorRaised;
        public event Func<TimeoutNotification, Task>? TimeoutRaised;

        public LifeCycleStateMachine(ILifeCycleStateRepository repository) {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
    }
}
