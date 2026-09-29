using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zonk.Configs;
using Object = UnityEngine.Object;

namespace Zonk.Presentation
{
    /// <summary>Записанный бросок: позы каждой кости по кадрам и какая ось тела в конце смотрит вверх.</summary>
    public sealed class RollRecording
    {
        public int DieCount;
        public int FrameCount;
        public float FrameTime;
        public Vector3[][] Positions;
        public Quaternion[][] Rotations;
        public Vector3[] UpAxes;

        public float Duration => FrameCount * FrameTime;
    }

    /// <summary>
    /// Честный и красивый бросок. Грани выбирает ГСЧ партии (ядро правил), а здесь физика только считает,
    /// как кости летят и катятся. Бросок просчитывается мгновенно в отдельной сцене физики с копией лотка,
    /// затем проигрывается записью. Визуал каждой кости поворачивается на симметрию куба так,
    /// чтобы в конце наверху была нужная грань (DieFaces.Correction). Физика при этом та же, коллайдер — куб.
    /// </summary>
    public sealed class DiceRollSimulator : IDisposable
    {
        private const float FrameTime = 1f / 60f;
        private const float MaxDuration = 5f;
        private const int CalmFramesToStop = 12;
        private const int Attempts = 10;
        private const float FlatTolerance = 0.985f;

        private readonly DiceTrayView _tray;
        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        private Scene _scene;
        private PhysicsScene _physics;
        private PhysicsMaterial _material;

        public DiceRollSimulator(DiceTrayView tray)
        {
            _tray = tray;
        }

        /// <summary>
        /// origin: откуда высыпаются кости (горло стакана); direction: куда летят (горизонтально, к центру лотка).
        /// Бросок пересчитывается, пока все кости не лягут ровно, внутри лотка и не друг на друге (попытки невидимы
        /// и занимают миллисекунды). Если так и не вышло, берётся попытка с наименьшим числом проблем:
        /// MatchPresenter докатит такие кости на свободное место.
        /// </summary>
        public RollRecording Simulate(int count, Vector3 origin, Vector3 direction, System.Random random, RollParams style)
        {
            EnsureScene(count);

            RollRecording best = null;
            var bestProblems = int.MaxValue;
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                var recording = SimulateOnce(count, origin, direction, random, style, out var problems);
                if (problems < bestProblems)
                {
                    best = recording;
                    bestProblems = problems;
                }

                if (problems == 0)
                    break;
            }

            return best;
        }

        private RollRecording SimulateOnce(int count, Vector3 origin, Vector3 direction, System.Random random, RollParams style,
            out int problems)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            direction = Quaternion.AngleAxis(style.DirectionJitter, Vector3.up) * direction;
            var side = Vector3.Cross(Vector3.up, direction);
            var size = _tray.DieSize;

            for (var i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                var active = i < count;
                body.gameObject.SetActive(active);
                if (!active)
                    continue;

                body.GetComponent<BoxCollider>().size = Vector3.one * size;
                var spread = size * style.Spread;
                var offset = side * Range(random, -spread, spread) + Vector3.up * (i * size * 0.35f) +
                             direction * Range(random, -spread, spread) * 0.5f;

                body.position = origin + offset;
                body.rotation = RandomRotation(random);
                body.transform.SetPositionAndRotation(body.position, body.rotation);
                body.linearVelocity = direction * (style.ThrowSpeed * Range(random, 0.85f, 1.15f)) +
                                      side * Range(random, -0.8f, 0.8f) + Vector3.down * Range(random, 0.3f, 1f);
                body.angularVelocity = RandomUnit(random) * (style.SpinSpeed * Range(random, 0.8f, 1.2f));
                body.WakeUp();
            }

            var maxFrames = Mathf.CeilToInt(MaxDuration / FrameTime);
            var positions = new List<Vector3>[count];
            var rotations = new List<Quaternion>[count];
            for (var i = 0; i < count; i++)
            {
                positions[i] = new List<Vector3>(maxFrames);
                rotations[i] = new List<Quaternion>(maxFrames);
                positions[i].Add(_bodies[i].position);
                rotations[i].Add(_bodies[i].rotation);
            }

            var calm = 0;
            var frames = 1;
            while (frames < maxFrames && calm < CalmFramesToStop)
            {
                _physics.Simulate(FrameTime);
                frames++;

                var allCalm = true;
                for (var i = 0; i < count; i++)
                {
                    var body = _bodies[i];
                    positions[i].Add(body.position);
                    rotations[i].Add(body.rotation);
                    if (!body.IsSleeping() &&
                        (body.linearVelocity.sqrMagnitude > 0.0025f || body.angularVelocity.sqrMagnitude > 0.01f))
                    {
                        allCalm = false;
                    }
                }

                calm = allCalm ? calm + 1 : 0;
            }

            var recording = new RollRecording
            {
                DieCount = count,
                FrameCount = frames,
                FrameTime = FrameTime,
                Positions = new Vector3[count][],
                Rotations = new Quaternion[count][],
                UpAxes = new Vector3[count],
            };

            problems = 0;
            for (var i = 0; i < count; i++)
            {
                recording.Positions[i] = positions[i].ToArray();
                recording.Rotations[i] = rotations[i].ToArray();
                var rotation = recording.Rotations[i][frames - 1];
                var position = recording.Positions[i][frames - 1];
                recording.UpAxes[i] = DieFaces.UpAxis(rotation);

                // Вне лотка, на ребре или на другой кости (центр выше лежащей кости).
                var flat = Vector3.Dot(rotation * recording.UpAxes[i], Vector3.up) >= FlatTolerance;
                var onFloor = position.y <= _tray.RestHeight + size * 0.25f;
                if (!_tray.Contains(position) || !flat || !onFloor)
                    problems++;
            }

            return recording;
        }

        private void EnsureScene(int count)
        {
            if (!_scene.IsValid())
            {
                _scene = SceneManager.CreateScene("DiceRollPhysics", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                _physics = _scene.GetPhysicsScene();
                _material = new PhysicsMaterial("Dice")
                {
                    bounciness = 0.3f,
                    dynamicFriction = 0.35f,
                    staticFriction = 0.45f,
                    bounceCombine = PhysicsMaterialCombine.Average,
                };

                foreach (var source in _tray.Colliders)
                {
                    if (source == null)
                        continue;

                    var copy = new GameObject("Tray_" + source.name);
                    SceneManager.MoveGameObjectToScene(copy, _scene);
                    copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                    copy.transform.localScale = source.transform.lossyScale;
                    var collider = copy.AddComponent<BoxCollider>();
                    collider.center = source.center;
                    collider.size = source.size;
                    collider.sharedMaterial = _material;
                }
            }

            while (_bodies.Count < count)
            {
                var die = new GameObject("Die_" + _bodies.Count);
                SceneManager.MoveGameObjectToScene(die, _scene);
                var collider = die.AddComponent<BoxCollider>();
                collider.size = Vector3.one * _tray.DieSize;
                collider.sharedMaterial = _material;
                var body = die.AddComponent<Rigidbody>();
                body.mass = 0.1f;
                body.linearDamping = 0.05f;
                body.angularDamping = 0.3f;
                body.maxAngularVelocity = 40f;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _bodies.Add(body);
            }
        }

        public void Dispose()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                SceneManager.UnloadSceneAsync(_scene);

            if (_material != null)
                Object.Destroy(_material);

            _bodies.Clear();
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static Vector3 RandomUnit(System.Random random)
        {
            var vector = new Vector3(Range(random, -1f, 1f), Range(random, -1f, 1f), Range(random, -1f, 1f));
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.up;
        }

        private static Quaternion RandomRotation(System.Random random)
        {
            return Quaternion.Euler(Range(random, 0f, 360f), Range(random, 0f, 360f), Range(random, 0f, 360f));
        }
    }
}
