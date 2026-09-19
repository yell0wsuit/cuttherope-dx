using System;
using System.Xml.Linq;

using CutTheRopeDX.Framework.Core;

using static CutTheRopeDX.Helpers.ParsingHelpers;

namespace CutTheRopeDX.Framework.Helpers
{
    /// <summary>
    /// Moves a point along a cyclic path with optional per-point speed settings and continuous rotation.
    /// Paths are parsed from level data, supporting both circular ("R…") and polyline ("x,y,…") definitions.
    /// </summary>
    internal class Mover : FrameworkTypes
    {
        /// <summary>
        /// Initializes a mover with path capacity, default move speed, and default rotation speed.
        /// </summary>
        /// <param name="l">Maximum number of path points.</param>
        /// <param name="m_">Default movement speed applied to each path point.</param>
        /// <param name="r_">Default rotation speed.</param>
        public Mover(int l, float m_, float r_)
        {
            int defaultMoveSpeed = (int)m_;
            int defaultRotateSpeed = (int)r_;
            pathLen = 0;
            pathCapacity = l;
            rotateSpeed = defaultRotateSpeed;
            if (pathCapacity > 0)
            {
                path = new Vector[pathCapacity];
                for (int i = 0; i < path.Length; i++)
                {
                    path[i] = default;
                }
                moveSpeed = new float[pathCapacity];
                for (int j = 0; j < moveSpeed.Length; j++)
                {
                    moveSpeed[j] = defaultMoveSpeed;
                }
            }
            IsPaused = false;
        }

        /// <summary>
        /// Sets the movement speed used for all path points.
        /// </summary>
        /// <param name="ms">Movement speed to assign.</param>
        public virtual void SetMoveSpeed(float ms)
        {
            for (int i = 0; i < pathCapacity; i++)
            {
                moveSpeed[i] = ms;
            }
        }

        /// <summary>
        /// Builds a started mover from an authored <c>path</c>, or <see langword="null"/> when none
        /// is authored. Shared so a tutorial text prompt travels exactly like any other object given
        /// the same attributes, speed scale included.
        /// </summary>
        /// <param name="xml">Element carrying <c>path</c>, <c>moveSpeed</c> and <c>rotateSpeed</c>.</param>
        /// <param name="start">World position the path is relative to.</param>
        /// <param name="angle">Starting angle in degrees.</param>
        /// <returns>The started mover, or <see langword="null"/>.</returns>
        public static Mover FromXml(XElement xml, Vector start, float angle)
        {
            string pathString = xml.Attribute("path")?.Value ?? string.Empty;
            if (pathString.Length == 0)
            {
                return null;
            }

            Mover mover = new(
                PathPointCapacity(pathString),
                ParseFloatOrZero(xml.Attribute("moveSpeed")?.Value) * ActivePhysicsConstants.MoverSpeedScale,
                ParseFloatOrZero(xml.Attribute("rotateSpeed")?.Value))
            {
                angle_ = angle,
            };
            mover.angle_initial = mover.angle_;
            mover.SetPathFromStringandStart(pathString, start);
            mover.Start();
            return mover;
        }

        /// <summary>
        /// Returns the number of path points <see cref="SetPathFromStringandStart"/> will emit for
        /// <paramref name="p"/>. Callers size the mover from this: <see cref="AddPathPoint"/>
        /// indexes its array unguarded, so a capacity derived from the unscaled radius overruns.
        /// </summary>
        /// <param name="p">The path string from level data.</param>
        /// <returns>The path point capacity required for <paramref name="p"/>.</returns>
        public static int PathPointCapacity(string p)
        {
            return string.IsNullOrEmpty(p) || p[0] != 'R'
                ? 100
                : MAX(1, CirclePathRadius(p) / 2) + 1;
        }

        /// <summary>Scales the radius of a circular ("R…") path into world units.</summary>
        /// <param name="p">The path string from level data.</param>
        /// <returns>The scaled circle radius.</returns>
        private static int CirclePathRadius(string p)
        {
            int radius = (int)RTD(ParseIntOrZero(p[2..]));
            return (int)RTD(radius * ActivePhysicsConstants.MoverPathScale);
        }

        /// <summary>
        /// Builds a path from a serialized string and prepends the supplied start point.
        /// Circular paths (<c>R</c> followed by <c>C</c> for clockwise and a radius) and polyline
        /// offsets are both scaled by <see cref="ActivePhysicsConstants.MoverPathScale"/>.
        /// </summary>
        /// <param name="p">Serialized path description.</param>
        /// <param name="s">Starting position for the generated path.</param>
        public virtual void SetPathFromStringandStart(string p, Vector s)
        {
            if (p[0] == 'R')
            {
                bool clockwise = p[1] == 'C';
                int radius = CirclePathRadius(p);
                int pointCount = radius / 2;
                if (pointCount <= 0)
                {
                    AddPathPoint(s);
                    return;
                }
                float angleStep = MathF.Tau / pointCount;
                if (!clockwise)
                {
                    angleStep = 0f - angleStep;
                }
                float theta = 0f;
                for (int i = 0; i < pointCount; i++)
                {
                    float x = s.X + (radius * Cosf(theta));
                    float y = s.Y + (radius * Sinf(theta));
                    AddPathPoint(Vect(x, y));
                    theta += angleStep;
                }
                return;
            }
            AddPathPoint(s);
            if (p[^1] == ',')
            {
                p = p[..(p.Length - 1)];
            }
            string[] list = p.Split(',');
            for (int j = 0; j < list.Length; j += 2)
            {
                string xOffsetString = list[j];
                string yOffsetString = list[j + 1];
                AddPathPoint(Vect(
                    s.X + (ParseFloatOrZero(xOffsetString) * ActivePhysicsConstants.MoverPathScale),
                    s.Y + (ParseFloatOrZero(yOffsetString) * ActivePhysicsConstants.MoverPathScale)));
            }
        }

        /// <summary>
        /// Appends a point to the movement path.
        /// </summary>
        /// <param name="v">Path point to add.</param>
        public virtual void AddPathPoint(Vector v)
        {
            Vector[] array = path;
            int insertIndex = pathLen;
            pathLen = insertIndex + 1;
            array[insertIndex] = v;
        }

        /// <summary>
        /// Starts movement from the first path point and targets the next point in sequence.
        /// </summary>
        public virtual void Start()
        {
            if (pathLen > 0)
            {
                pos = path[0];
                targetPoint = pathLen > 1 ? 1 : 0;
                // CalculateOffset();
            }
        }

        /// <summary>
        /// Pauses path and rotation updates.
        /// </summary>
        public virtual void Pause()
        {
            IsPaused = true;
        }

        /// <summary>
        /// Resumes path and rotation updates.
        /// </summary>
        public virtual void Unpause()
        {
            IsPaused = false;
        }

        /// <summary>
        /// Gets a value indicating whether movement updates are currently paused.
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Sets the continuous rotation speed.
        /// </summary>
        /// <param name="rs">Rotation speed to apply.</param>
        public virtual void SetRotateSpeed(float rs)
        {
            rotateSpeed = rs;
        }

        /// <summary>
        /// Moves immediately to the specified path point and makes it the current target.
        /// </summary>
        /// <param name="p">Path point index to jump to.</param>
        public virtual void JumpToPoint(int p)
        {
            targetPoint = p;
            pos = path[targetPoint];
            // CalculateOffset();
        }

        //public virtual void CalculateOffset()
        //{
        // Vector v = path[targetPoint];
        // offset = VectMult(VectNormalize(VectSub(v, pos)), moveSpeed[targetPoint]);
        //}

        /// <summary>
        /// Sets the movement speed for a single path point.
        /// </summary>
        /// <param name="ms">Movement speed to assign.</param>
        /// <param name="i">Path point index.</param>
        public virtual void SetMoveSpeedforPoint(float ms, int i)
        {
            moveSpeed[i] = ms;
        }

        /// <summary>
        /// Sets whether the path is traversed in reverse order.
        /// </summary>
        /// <param name="r"><see langword="true" /> to move backward through the path; otherwise <see langword="false" />.</param>
        public virtual void SetMoveReverse(bool r)
        {
            reverse = r;
        }

        /// <summary>
        /// Advances the mover along its path and updates rotation for one frame.
        /// </summary>
        /// <param name="delta">Elapsed frame time in seconds.</param>
        public virtual void Update(float delta)
        {
            if (IsPaused)
            {
                return;
            }
            if (pathLen > 0)
            {
                float timeRemaining = delta;
                if (overrun != 0f)
                {
                    timeRemaining += overrun;
                    overrun = 0f;
                }
                int noProgressSteps = 0;
                int maxNoProgressSteps = pathLen + 1;
                while (timeRemaining > 0f)
                {
                    Vector v = path[targetPoint];
                    Vector toTarget = VectSub(v, pos);
                    float distance = VectLength(toTarget);
                    if (distance <= 0f)
                    {
                        AdvanceTarget();
                        // CalculateOffset();
                        noProgressSteps++;
                        if (noProgressSteps > maxNoProgressSteps)
                        {
                            break;
                        }
                        continue;
                    }
                    noProgressSteps = 0;
                    float speed = moveSpeed[targetPoint];
                    if (speed <= 0f)
                    {
                        break;
                    }
                    float timeToTarget = distance / speed;
                    if (timeToTarget <= timeRemaining)
                    {
                        pos = v;
                        timeRemaining -= timeToTarget;
                        AdvanceTarget();
                        // CalculateOffset();
                        continue;
                    }
                    Vector dir = VectMult(toTarget, 1f / distance);
                    pos = VectAdd(pos, VectMult(dir, speed * timeRemaining));
                    timeRemaining = 0f;
                }
                if (timeRemaining > 0f)
                {
                    overrun = timeRemaining;
                }
            }
            if (rotateSpeed != 0f)
            {
                if (use_angle_initial && targetPoint == 0)
                {
                    angle_ = angle_initial;
                    return;
                }
                angle_ += rotateSpeed * delta;
            }
        }

        /// <summary>
        /// Advances the target-path index according to the current traversal direction.
        /// </summary>
        private void AdvanceTarget()
        {
            if (reverse)
            {
                targetPoint--;
                if (targetPoint < 0)
                {
                    targetPoint = pathLen - 1;
                }
                return;
            }
            targetPoint++;
            if (targetPoint >= pathLen)
            {
                targetPoint = 0;
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                path = null;
                moveSpeed = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Moves a scalar value toward a target at the given <paramref name="speed"/> and frame <paramref name="delta"/>.
        /// </summary>
        /// <param name="v">Value to update.</param>
        /// <param name="t">Target value.</param>
        /// <param name="speed">Movement speed in units per second.</param>
        /// <param name="delta">Elapsed frame time in seconds.</param>
        /// <returns><see langword="true" /> if the target was reached; otherwise <see langword="false" />.</returns>
        public static bool MoveVariableToTarget(ref float v, float t, float speed, float delta)
        {
            if (t != v)
            {
                if (t > v)
                {
                    v += speed * delta;
                    if (v > t)
                    {
                        v = t;
                    }
                }
                else
                {
                    v -= speed * delta;
                    if (v < t)
                    {
                        v = t;
                    }
                }
                if (t == v)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Per-path-point movement speeds.
        /// </summary>
        private float[] moveSpeed;

        /// <summary>
        /// Continuous rotation speed applied during updates.
        /// </summary>
        private float rotateSpeed;

        /// <summary>
        /// Path points traversed by the mover.
        /// </summary>
        public Vector[] path;

        /// <summary>
        /// Number of valid points currently stored in <see cref="path"/>.
        /// </summary>
        public int pathLen;

        /// <summary>
        /// Maximum number of points that can be stored in the path.
        /// </summary>
        private readonly int pathCapacity;

        /// <summary>
        /// Current position along the path.
        /// </summary>
        public Vector pos;

        /// <summary>
        /// Current rotation angle.
        /// </summary>
        public float angle_;

        /// <summary>
        /// Rotation angle used when <see cref="use_angle_initial"/> is enabled.
        /// </summary>
        public float angle_initial;

        /// <summary>
        /// Whether the initial angle should be forced while targeting the first path point.
        /// </summary>
        public bool use_angle_initial;

        /// <summary>
        /// Index of the current target path point.
        /// </summary>
        public int targetPoint;

        /// <summary>
        /// Whether path traversal proceeds in reverse order.
        /// </summary>
        private bool reverse;

        /// <summary>
        /// Unused leftover frame time carried into the next update.
        /// </summary>
        private float overrun;

        // private Vector offset;
    }
}
