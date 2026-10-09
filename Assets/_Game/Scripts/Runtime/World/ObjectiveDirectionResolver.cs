using System;
using Hortensia.Narrative;
using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Resolves the one live world interaction that advances the pending
    /// narrative beat. Active scene components remain authoritative, so Chapter
    /// Dressing, task staging, and restored progress are reflected automatically.
    /// </summary>
    public static class ObjectiveDirectionResolver
    {
        public static bool TryResolve(
            GameSession session,
            Vector3 playerPosition,
            CarriedItemHolder holder,
            out Vector3 position)
        {
            position = default;
            if (session == null || session.State == null)
                return false;

            if (TryResolveBossObjective(session, playerPosition, holder, out position))
                return true;

            if (session.IsPlayerLocked || session.IsNarrativeInputCaptured)
                return false;

            switch (session.ActiveNarrativeBeat)
            {
                case GateBeat gate when !session.SkipGates && !gate.IsOpen(session.State):
                    return TryResolveCondition(
                        gate.Condition,
                        session.State,
                        playerPosition,
                        holder,
                        out position);

                case DocumentSequenceBeat documentSequence:
                    return TryFindDocument(
                        documentSequence.Document,
                        playerPosition,
                        out position);

                default:
                    return false;
            }
        }

        private static bool TryResolveBossObjective(
            GameSession session,
            Vector3 playerPosition,
            CarriedItemHolder holder,
            out Vector3 position)
        {
            position = default;
            if (!(session.ActiveNarrativeBeat is SequenceBeat sequence) ||
                !string.Equals(
                    sequence.SequenceId,
                    BossSequencePlayer.VerdantMirrorSequenceId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    session.ActiveSequenceId,
                    BossSequencePlayer.VerdantMirrorSequenceId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            BossSequencePlayer[] players =
                UnityEngine.Object.FindObjectsByType<BossSequencePlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                BossSequencePlayer player = players[i];
                if (player != null && player.isActiveAndEnabled &&
                    player.TryGetMirrorTargetPosition(out position))
                {
                    return true;
                }
            }

            SacrificeThornInteractable[] thorns =
                UnityEngine.Object.FindObjectsByType<SacrificeThornInteractable>(FindObjectsInactive.Exclude);
            for (int i = 0; i < thorns.Length; i++)
            {
                SacrificeThornInteractable thorn = thorns[i];
                if (thorn != null && thorn.isActiveAndEnabled &&
                    !string.IsNullOrWhiteSpace(thorn.Prompt))
                {
                    position = thorn.transform.position;
                    return true;
                }
            }

            return TryFindFlagInteraction("poison_used", playerPosition, holder, out position);
        }

        private static bool TryResolveCondition(
            BeatCondition condition,
            NarrativeState state,
            Vector3 playerPosition,
            CarriedItemHolder holder,
            out Vector3 position)
        {
            position = default;
            if (condition == null || condition.IsSatisfied(state))
                return false;

            switch (condition)
            {
                case FlagCondition flag when flag.MustBePresent:
                    return TryFindFlagInteraction(flag.Flag, playerPosition, holder, out position);

                case TaskCondition task:
                    return TryFindTaskInteraction(task.Objective, playerPosition, holder, out position);

                case AllOfCondition all:
                    return TryResolveFirstPending(
                        all.Conditions,
                        state,
                        playerPosition,
                        holder,
                        out position);

                case AnyOfCondition any:
                    return TryResolveFirstPending(
                        any.Conditions,
                        state,
                        playerPosition,
                        holder,
                        out position);

                default:
                    return false;
            }
        }

        private static bool TryResolveFirstPending(
            System.Collections.Generic.IReadOnlyList<BeatCondition> conditions,
            NarrativeState state,
            Vector3 playerPosition,
            CarriedItemHolder holder,
            out Vector3 position)
        {
            position = default;
            if (conditions == null)
                return false;

            for (int i = 0; i < conditions.Count; i++)
            {
                BeatCondition candidate = conditions[i];
                if (candidate == null || candidate.IsSatisfied(state))
                    continue;

                if (TryResolveCondition(candidate, state, playerPosition, holder, out position))
                    return true;
            }

            return false;
        }

        private static bool TryFindFlagInteraction(
            FlagId flag,
            Vector3 playerPosition,
            CarriedItemHolder holder,
            out Vector3 position)
        {
            position = default;
            if (flag == null)
                return false;

            FlagInteractable[] interactions =
                UnityEngine.Object.FindObjectsByType<FlagInteractable>(FindObjectsInactive.Exclude);
            FlagInteractable nearest = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < interactions.Length; i++)
            {
                FlagInteractable interaction = interactions[i];
                if (interaction == null || !interaction.isActiveAndEnabled ||
                    !ReferenceEquals(interaction.Flag, flag))
                {
                    continue;
                }

                float distance = HorizontalDistanceSquared(
                    playerPosition,
                    interaction.transform.position);
                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                nearest = interaction;
            }

            if (nearest != null)
            {
                CarryCategory required = nearest.RequiresCarried;
                if (required != null && (holder == null || !holder.Has(required)))
                {
                    if (holder != null && holder.HasItem)
                        return false;

                    return TryFindNearestCarryable(required, playerPosition, out position);
                }

                position = nearest.transform.position;
                return true;
            }

            DocumentInteractable[] documents =
                UnityEngine.Object.FindObjectsByType<DocumentInteractable>(FindObjectsInactive.Exclude);
            DocumentInteractable nearestDocument = null;
            closestDistance = float.PositiveInfinity;
            for (int i = 0; i < documents.Length; i++)
            {
                DocumentInteractable document = documents[i];
                if (document != null && document.isActiveAndEnabled &&
                    document.Document != null &&
                    ReferenceEquals(document.Document.SetWhenRead, flag))
                {
                    float distance = HorizontalDistanceSquared(
                        playerPosition,
                        document.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        nearestDocument = document;
                    }
                }
            }

            if (nearestDocument == null)
                return false;

            position = nearestDocument.transform.position;
            return true;
        }

        private static bool TryFindTaskInteraction(
            TaskObjective objective,
            Vector3 playerPosition,
            CarriedItemHolder holder,
            out Vector3 position)
        {
            position = default;
            if (objective == null)
                return false;

            CarryCategory carried = holder != null ? holder.Category : null;
            if (carried != null)
            {
                if (!ReferenceEquals(carried.Objective, objective))
                    return false;

                // Gardening tools are deliberately retained after use. Once
                // their matching patch has been completed, guide the player to
                // the next available tool for the same task objective.
                if (TryFindNearestReceiver(carried, playerPosition, out position))
                    return true;
            }

            return TryFindNearestCarryableForObjective(objective, playerPosition, out position);
        }

        private static bool TryFindDocument(
            DocumentDefinition definition,
            Vector3 playerPosition,
            out Vector3 position)
        {
            position = default;
            if (definition == null)
                return false;

            DocumentInteractable[] documents =
                UnityEngine.Object.FindObjectsByType<DocumentInteractable>(FindObjectsInactive.Exclude);
            DocumentInteractable nearest = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < documents.Length; i++)
            {
                DocumentInteractable document = documents[i];
                if (document != null && document.isActiveAndEnabled &&
                    ReferenceEquals(document.Document, definition))
                {
                    float distance = HorizontalDistanceSquared(
                        playerPosition,
                        document.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        nearest = document;
                    }
                }
            }

            if (nearest == null)
                return false;

            position = nearest.transform.position;
            return true;
        }

        private static bool TryFindNearestReceiver(
            CarryCategory category,
            Vector3 playerPosition,
            out Vector3 position)
        {
            position = default;
            float closestDistance = float.PositiveInfinity;
            TaskReceiver[] receivers =
                UnityEngine.Object.FindObjectsByType<TaskReceiver>(FindObjectsInactive.Exclude);
            for (int i = 0; i < receivers.Length; i++)
            {
                TaskReceiver receiver = receivers[i];
                if (receiver == null || !receiver.isActiveAndEnabled || receiver.IsCompleted ||
                    !ReferenceEquals(receiver.Accepts, category))
                {
                    continue;
                }

                float distance = HorizontalDistanceSquared(playerPosition, receiver.transform.position);
                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                position = receiver.transform.position;
            }

            return !float.IsPositiveInfinity(closestDistance);
        }

        private static bool TryFindNearestCarryableForObjective(
            TaskObjective objective,
            Vector3 playerPosition,
            out Vector3 position)
        {
            position = default;
            float closestDistance = float.PositiveInfinity;
            Carryable[] carryables =
                UnityEngine.Object.FindObjectsByType<Carryable>(FindObjectsInactive.Exclude);
            for (int i = 0; i < carryables.Length; i++)
            {
                Carryable carryable = carryables[i];
                if (carryable == null || !carryable.isActiveAndEnabled || !carryable.CanBeCarried ||
                    carryable.Category == null ||
                    !ReferenceEquals(carryable.Category.Objective, objective))
                {
                    continue;
                }

                float distance = HorizontalDistanceSquared(playerPosition, carryable.transform.position);
                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                position = carryable.transform.position;
            }

            return !float.IsPositiveInfinity(closestDistance);
        }

        private static bool TryFindNearestCarryable(
            CarryCategory category,
            Vector3 playerPosition,
            out Vector3 position)
        {
            position = default;
            float closestDistance = float.PositiveInfinity;
            Carryable[] carryables =
                UnityEngine.Object.FindObjectsByType<Carryable>(FindObjectsInactive.Exclude);
            for (int i = 0; i < carryables.Length; i++)
            {
                Carryable carryable = carryables[i];
                if (carryable == null || !carryable.isActiveAndEnabled || !carryable.CanBeCarried ||
                    !ReferenceEquals(carryable.Category, category))
                {
                    continue;
                }

                float distance = HorizontalDistanceSquared(
                    playerPosition,
                    carryable.transform.position);
                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                position = carryable.transform.position;
            }

            return !float.IsPositiveInfinity(closestDistance);
        }

        private static bool TryFindFlagInteraction(
            string flagId,
            Vector3 playerPosition,
            CarriedItemHolder holder,
            out Vector3 position)
        {
            position = default;
            FlagInteractable[] interactions =
                UnityEngine.Object.FindObjectsByType<FlagInteractable>(FindObjectsInactive.Exclude);
            FlagInteractable nearest = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < interactions.Length; i++)
            {
                FlagInteractable interaction = interactions[i];
                if (interaction == null || !interaction.isActiveAndEnabled ||
                    interaction.Flag == null ||
                    !string.Equals(interaction.Flag.Id, flagId, StringComparison.Ordinal))
                {
                    continue;
                }

                float distance = HorizontalDistanceSquared(
                    playerPosition,
                    interaction.transform.position);
                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                nearest = interaction;
            }

            if (nearest == null)
                return false;

            CarryCategory required = nearest.RequiresCarried;
            if (required != null && (holder == null || !holder.Has(required)))
            {
                if (holder != null && holder.HasItem)
                    return false;

                return TryFindNearestCarryable(required, playerPosition, out position);
            }

            position = nearest.transform.position;
            return true;
        }

        private static float HorizontalDistanceSquared(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }
    }
}
