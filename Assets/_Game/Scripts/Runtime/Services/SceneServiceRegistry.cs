using System;
using System.Collections.Generic;
using Hortensia.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Explicit registry for the services owned by the currently loaded
    /// gameplay scene. It deliberately keeps duplicate registrations visible
    /// so callers can reject or report ambiguity instead of silently choosing
    /// an arbitrary service.
    /// </summary>
    public sealed class SceneServiceRegistry
    {
        private readonly List<PlayerRegistration> players = new List<PlayerRegistration>();
        private readonly List<INarrativePresenter> presenters = new List<INarrativePresenter>();
        private readonly List<CarriedItemHolder> holders = new List<CarriedItemHolder>();
        private readonly List<VisionBleedController> visionBleeds = new List<VisionBleedController>();
        private readonly List<GifOverlayController> gifOverlays = new List<GifOverlayController>();
        private readonly List<global::LowResolutionPresenter> worldOutputs =
            new List<global::LowResolutionPresenter>();
        private readonly List<ISequencePlayer> sequencePlayers = new List<ISequencePlayer>();
        private readonly List<IPatientVisualPresenter> patientVisualPresenters =
            new List<IPatientVisualPresenter>();
        private readonly List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        private readonly List<DocumentInteractable> documents = new List<DocumentInteractable>();

        public event Action Changed;

        public void RegisterPlayer(global::FirstPersonController player, Camera camera)
        {
            if (player == null)
                return;

            PruneUnavailable();
            for (int i = 0; i < players.Count; i++)
            {
                if (!ReferenceEquals(players[i].Player, player))
                    continue;

                if (!ReferenceEquals(players[i].Camera, camera))
                {
                    players[i] = new PlayerRegistration(player, camera);
                    Changed?.Invoke();
                }
                return;
            }

            players.Add(new PlayerRegistration(player, camera));
            Changed?.Invoke();
        }

        public void UnregisterPlayer(global::FirstPersonController player)
        {
            if (RemoveMatching(players, entry => ReferenceEquals(entry.Player, player)))
                Changed?.Invoke();
        }

        public void RegisterPresenter(INarrativePresenter presenter) =>
            RegisterUnique(presenters, presenter);

        public void UnregisterPresenter(INarrativePresenter presenter) =>
            Unregister(presenters, presenter);

        public void RegisterCarriedItemHolder(CarriedItemHolder holder) =>
            RegisterUnique(holders, holder);

        public void UnregisterCarriedItemHolder(CarriedItemHolder holder) =>
            Unregister(holders, holder);

        public void RegisterVisionBleed(VisionBleedController visionBleed) =>
            RegisterUnique(visionBleeds, visionBleed);

        public void UnregisterVisionBleed(VisionBleedController visionBleed) =>
            Unregister(visionBleeds, visionBleed);

        public void RegisterGifOverlay(GifOverlayController gifOverlay) =>
            RegisterUnique(gifOverlays, gifOverlay);

        public void UnregisterGifOverlay(GifOverlayController gifOverlay) =>
            Unregister(gifOverlays, gifOverlay);

        public void RegisterWorldOutput(global::LowResolutionPresenter output) =>
            RegisterUnique(worldOutputs, output);

        public void UnregisterWorldOutput(global::LowResolutionPresenter output) =>
            Unregister(worldOutputs, output);

        public void RegisterSequencePlayer(ISequencePlayer player) =>
            RegisterUnique(sequencePlayers, player);

        public void UnregisterSequencePlayer(ISequencePlayer player) =>
            Unregister(sequencePlayers, player);

        public void RegisterPatientVisualPresenter(IPatientVisualPresenter presenter) =>
            RegisterUnique(patientVisualPresenters, presenter);

        public void UnregisterPatientVisualPresenter(IPatientVisualPresenter presenter) =>
            Unregister(patientVisualPresenters, presenter);

        public void RegisterSpawnPoint(SpawnPoint spawnPoint) =>
            RegisterUnique(spawnPoints, spawnPoint);

        public void UnregisterSpawnPoint(SpawnPoint spawnPoint) =>
            Unregister(spawnPoints, spawnPoint);

        public void RegisterDocument(DocumentInteractable document) =>
            RegisterUnique(documents, document);

        public void UnregisterDocument(DocumentInteractable document) =>
            Unregister(documents, document);

        public bool TryGetPlayer(
            out global::FirstPersonController player,
            out int matchCount)
        {
            PruneUnavailable();
            matchCount = players.Count;
            player = matchCount == 1 ? players[0].Player : null;
            return player != null;
        }

        public bool TryGetPlayerCamera(out Camera camera, out int matchCount)
        {
            PruneUnavailable();
            camera = null;
            matchCount = 0;
            for (int i = 0; i < players.Count; i++)
            {
                Camera candidate = players[i].Camera;
                if (candidate == null)
                    continue;

                camera = candidate;
                matchCount++;
            }

            if (matchCount == 1)
                return true;

            camera = null;
            return false;
        }

        public bool TryGetPresenter(out INarrativePresenter presenter, out int matchCount) =>
            TryGetUnique(presenters, out presenter, out matchCount);

        public bool TryGetCarriedItemHolder(
            out CarriedItemHolder holder,
            out int matchCount) =>
            TryGetUnique(holders, out holder, out matchCount);

        public bool TryGetVisionBleed(
            out VisionBleedController visionBleed,
            out int matchCount) =>
            TryGetUnique(visionBleeds, out visionBleed, out matchCount);

        public bool TryGetGifOverlay(
            out GifOverlayController gifOverlay,
            out int matchCount) =>
            TryGetUnique(gifOverlays, out gifOverlay, out matchCount);

        public bool TryGetWorldOutput(
            out global::LowResolutionPresenter output,
            out int matchCount) =>
            TryGetUnique(worldOutputs, out output, out matchCount);

        public bool TryGetPatientVisualPresenter(
            out IPatientVisualPresenter presenter,
            out int matchCount) =>
            TryGetUnique(patientVisualPresenters, out presenter, out matchCount);

        public bool TryGetSpawnPoint(
            string id,
            out SpawnPoint spawnPoint,
            out int matchCount)
        {
            PruneUnavailable();
            spawnPoint = null;
            matchCount = 0;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                SpawnPoint candidate = spawnPoints[i];
                if (candidate == null || !candidate.Matches(id))
                    continue;

                spawnPoint = candidate;
                matchCount++;
            }

            if (matchCount == 1)
                return true;

            spawnPoint = null;
            return false;
        }

        public bool HasActiveDocument(DocumentDefinition document)
        {
            PruneUnavailable();
            for (int i = 0; i < documents.Count; i++)
            {
                DocumentInteractable interactable = documents[i];
                if (interactable != null &&
                    interactable.isActiveAndEnabled &&
                    ReferenceEquals(interactable.Document, document))
                {
                    return true;
                }
            }

            return false;
        }

        public int FindSequencePlayers(string sequenceId, ICollection<ISequencePlayer> matches)
        {
            if (matches == null)
                throw new ArgumentNullException(nameof(matches));

            PruneUnavailable();
            int count = 0;
            for (int i = 0; i < sequencePlayers.Count; i++)
            {
                ISequencePlayer candidate = sequencePlayers[i];
                if (candidate == null ||
                    !string.Equals(candidate.SequenceId, sequenceId, StringComparison.Ordinal))
                {
                    continue;
                }

                matches.Add(candidate);
                count++;
            }

            return count;
        }

        public void Clear()
        {
            players.Clear();
            presenters.Clear();
            holders.Clear();
            visionBleeds.Clear();
            gifOverlays.Clear();
            worldOutputs.Clear();
            sequencePlayers.Clear();
            patientVisualPresenters.Clear();
            spawnPoints.Clear();
            documents.Clear();
            Changed?.Invoke();
        }

        public void PruneToScene(Scene scene)
        {
            if (!scene.IsValid())
                return;

            bool changed = false;
            changed |= RemoveMatching(players, entry =>
                IsUnavailable(entry.Player) || entry.Player.gameObject.scene != scene);
            changed |= RemoveOutsideScene(presenters, scene);
            changed |= RemoveOutsideScene(holders, scene);
            changed |= RemoveOutsideScene(visionBleeds, scene);
            changed |= RemoveOutsideScene(gifOverlays, scene);
            changed |= RemoveOutsideScene(worldOutputs, scene);
            changed |= RemoveOutsideScene(sequencePlayers, scene);
            changed |= RemoveOutsideScene(patientVisualPresenters, scene);
            changed |= RemoveOutsideScene(spawnPoints, scene);
            changed |= RemoveOutsideScene(documents, scene);
            if (changed)
                Changed?.Invoke();
        }

        private void RegisterUnique<T>(List<T> services, T service) where T : class
        {
            if (IsUnavailable(service))
                return;

            PruneUnavailable();
            for (int i = 0; i < services.Count; i++)
            {
                if (ReferenceEquals(services[i], service))
                    return;
            }

            services.Add(service);
            Changed?.Invoke();
        }

        private void Unregister<T>(List<T> services, T service) where T : class
        {
            if (RemoveMatching(services, candidate =>
                IsUnavailable(candidate) || ReferenceEquals(candidate, service)))
            {
                Changed?.Invoke();
            }
        }

        private bool TryGetUnique<T>(List<T> services, out T service, out int matchCount)
            where T : class
        {
            PruneUnavailable();
            matchCount = services.Count;
            service = matchCount == 1 ? services[0] : null;
            return service != null;
        }

        private void PruneUnavailable()
        {
            bool changed = false;
            changed |= RemoveMatching(players, entry => IsUnavailable(entry.Player));
            changed |= RemoveMatching(presenters, IsUnavailable);
            changed |= RemoveMatching(holders, IsUnavailable);
            changed |= RemoveMatching(visionBleeds, IsUnavailable);
            changed |= RemoveMatching(gifOverlays, IsUnavailable);
            changed |= RemoveMatching(worldOutputs, IsUnavailable);
            changed |= RemoveMatching(sequencePlayers, IsUnavailable);
            changed |= RemoveMatching(patientVisualPresenters, IsUnavailable);
            changed |= RemoveMatching(spawnPoints, IsUnavailable);
            changed |= RemoveMatching(documents, IsUnavailable);
            if (changed)
                Changed?.Invoke();
        }

        private static bool RemoveOutsideScene<T>(List<T> services, Scene scene)
            where T : class
        {
            return RemoveMatching(services, service =>
            {
                if (IsUnavailable(service))
                    return true;

                return service is Component component && component.gameObject.scene != scene;
            });
        }

        private static bool RemoveMatching<T>(List<T> values, Predicate<T> predicate)
        {
            bool removed = false;
            for (int i = values.Count - 1; i >= 0; i--)
            {
                if (!predicate(values[i]))
                    continue;

                values.RemoveAt(i);
                removed = true;
            }
            return removed;
        }

        private static bool IsUnavailable(object service)
        {
            if (service == null)
                return true;

            return service is UnityEngine.Object unityObject && unityObject == null;
        }

        private readonly struct PlayerRegistration
        {
            public PlayerRegistration(global::FirstPersonController player, Camera camera)
            {
                Player = player;
                Camera = camera;
            }

            public global::FirstPersonController Player { get; }
            public Camera Camera { get; }
        }
    }
}