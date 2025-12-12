using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Haley.Utils;

namespace Haley.Services {
    public partial class LifeCycleStateMachine {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static LifeCycleState MapState(IDictionary<string, object> row) {
            return new LifeCycleState { 
                Id = row.GetInt("id"), 
                DisplayName = row.GetString( "display_name") ?? string.Empty, 
                DefinitionVersion = row.GetInt("def_version"), 
                Category = row.GetInt("category"), 
                Flags = (LifeCycleStateFlag)row.GetInt("flags"), 
                Created = DateTime.UtcNow };
        }

        private static LifeCycleInstance MapInstance(IDictionary<string, object> row) {
            return new LifeCycleInstance { 
                Id = row.GetLong("id"),
                DefinitionVersion = row.GetInt("def_version"), 
                CurrentState = row.GetInt("current_state"), 
                LastEvent = row.GetInt("last_event"), 
                ExternalRef = row.GetString("external_ref") ?? string.Empty, 
                Flags = (LifeCycleInstanceFlag)row.GetInt("flags"), 
                Created = DateTime.UtcNow };
        }

        private static string BuildMetadata(string? comment, object? context) {
            if (comment == null && context == null) return string.Empty;
            var payload = new Dictionary<string, object?> { 
                ["comment"] = comment, 
                ["context"] = context };
            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        private static void NormalizeDefinitionJson(LifeCycleDefinitionJson spec) {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            spec.Definition ??= new DefinitionBlock();
            spec.States ??= new List<StateBlock>();
            spec.Events ??= new List<EventBlock>();
            spec.Transitions ??= new List<TransitionBlock>();

            spec.Definition.Name = spec.Definition.Name?.Trim() ?? string.Empty;
            spec.Definition.Version = spec.Definition.Version?.Trim() ?? "1.0.0";
            spec.Definition.Description = spec.Definition.Description?.Trim();
            spec.Definition.Environment = spec.Definition.Environment?.Trim();

            foreach (var s in spec.States) {
                s.Name = s.Name?.Trim() ?? string.Empty;
                s.Category = s.Category?.Trim() ?? "business";
                s.Timeout = s.Timeout?.Trim();
                s.TimeoutMode = s.TimeoutMode?.Trim();
            }

            foreach (var e in spec.Events) {
                e.Name = e.Name?.Trim() ?? string.Empty;
                e.DisplayName = string.IsNullOrWhiteSpace(e.DisplayName) ? e.Name : e.DisplayName!.Trim();
            }

            foreach (var t in spec.Transitions) {
                t.From = t.From?.Trim() ?? string.Empty;
                t.To = t.To?.Trim() ?? string.Empty;
            }

            if (!spec.States.Any(s => s.IsInitial) && spec.States.Count > 0) spec.States[0].IsInitial = true;
        }

        private static LifeCycleStateFlag BuildStateFlags(StateBlock block) {
            var result = LifeCycleStateFlag.None;
            if (block == null) return result;
            if (block.IsInitial) result |= LifeCycleStateFlag.IsInitial;
            if (block.IsFinal) result |= LifeCycleStateFlag.IsFinal;

            if (block.Flags != null) {
                foreach (var f in block.Flags) {
                    if (string.IsNullOrWhiteSpace(f)) continue;
                    if (Enum.TryParse<LifeCycleStateFlag>(f.Trim(), true, out var parsed)) result |= parsed;
                }
            }

            return result;
        }

        private void EnsureSuccess<T>(IFeedback<T> feedback, string context) {
            if (feedback == null) throw new InvalidOperationException($"{context} returned null feedback.");
            if (feedback.Status) return;
            var message = string.IsNullOrWhiteSpace(feedback.Message) ? $"Operation '{context}' failed." : feedback.Message;
            if (ThrowExceptions || Repository.ThrowExceptions) throw new InvalidOperationException(message);
        }
    }
}
