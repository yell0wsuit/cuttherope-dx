using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// The butterfly that keeps the Experiments pack picker's last, bamboo-caged box company while
    /// it is locked. It wanders over whichever box the picker is on, lands on the bamboo when the
    /// picker arrives there, and flies off for good once the box opens. The iOS HD
    /// <c>Butterfly</c> class; it has no WP7 counterpart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// iOS moves it a fixed step per frame at 60 frames a second; the steps here are scaled by the
    /// real frame time instead. Its art is iOS iPad-retina units taken to the atlas's pixels; its
    /// movement is taken to DX's world instead (<see cref="IosToWorld"/>), then both to the strip's
    /// <see cref="stripScale"/>.
    /// </para>
    /// <para>
    /// The wings flap in 3D: iOS rotates them about horizontal axes under an orthographic
    /// projection. Seen through that projection a 3D rotation is only a 2D linear map, so the
    /// same result is drawn here with the renderer's 2D rotate and scale; see
    /// <see cref="ApplyRotations"/>.
    /// </para>
    /// </remarks>
    internal sealed class ExperimentsButterfly : BaseElement
    {
        /// <summary>How the butterfly is flying. Values are the iOS flag bits.</summary>
        internal enum FlightMode
        {
            /// <summary>Wandering about inside its rectangle.</summary>
            Normal = 1,

            /// <summary>Heading into its rectangle from wherever it is.</summary>
            Transition = 2,

            /// <summary>Heading for the landing point.</summary>
            Landing = 4,

            /// <summary>Perched on the landing point, flapping slowly.</summary>
            Landed = 8,

            /// <summary>Leaving for good; removed once off screen.</summary>
            Away = 16,
        }

        /// <summary>Pack atlas quads: the body and the two wings.</summary>
        private const int QuadBody = 19;
        private const int QuadRearWing = 20;
        private const int QuadFrontWing = 21;

        /// <summary>Wing flap speed while flying (iOS 5); a tenth of it while perched.</summary>
        private const float FlyingWingsVelocity = 5f;

        /// <summary>Lowest and highest wing angle, in degrees.</summary>
        private const float WingAngleLow = 10f;
        private const float WingAngleHigh = 80f;

        /// <summary>Steps per second the iOS per-frame movement was written for.</summary>
        private const float FramesPerSecond = 60f;

        /// <summary>Seconds in the rectangle before a transition becomes normal flight (iOS 0.2; 0.02 off screen).</summary>
        private const float SettleSecondsOnScreen = 0.2f;
        private const float SettleSecondsOffScreen = 0.02f;

        /// <summary>Seconds outside the rectangle before normal flight turns back into a transition (iOS 0.35).</summary>
        private const float StraySeconds = 0.35f;

        /// <summary>
        /// Scale from iOS world distances to DX's. iOS HD lays a level's height across 2048
        /// iPad-retina units; DX lays it across the design height. Movement follows the world
        /// rather than <see cref="MenuController.IosToAsset"/>, which only sizes the art.
        /// </summary>
        private const float IosToWorld = ViewportLayout.DesignHeight / 2048f;

        private readonly Image body;
        private readonly Image rearWing;
        private readonly Image frontWing;
        private readonly Random random = new();

        /// <summary>Scale from atlas pixels to the strip's logical units.</summary>
        private readonly float stripScale;

        private float wingsVelocity = FlyingWingsVelocity;
        private float wingAngle;
        private float wingVelocity;
        private float flapDelay;
        private Vector direction = new(-1f, 0f);
        private Vector landPoint;
        private Rectangle flightRect;
        private bool rectChanged;
        private bool transitionDirectionSet;
        private int transitionSteps;
        private float transitionTimeInRect;
        private float normalTimeOutOfRect;

        /// <summary>
        /// Initializes the butterfly.
        /// </summary>
        /// <param name="stripScale">Scale the pack strip is drawn at.</param>
        public ExperimentsButterfly(float stripScale)
        {
            this.stripScale = stripScale;
            body = Image.FromResource(Resources.Img.MenuExpPackSelection, QuadBody);
            body.anchor = body.parentAnchor = 18;
            body.visible = false;
            _ = AddChild(body);
            rearWing = Image.FromResource(Resources.Img.MenuExpPackSelection, QuadRearWing);
            rearWing.anchor = 34;
            rearWing.parentAnchor = 10;
            rearWing.visible = false;
            _ = body.AddChild(rearWing);
            frontWing = Image.FromResource(Resources.Img.MenuExpPackSelection, QuadFrontWing);
            frontWing.anchor = 34;
            frontWing.parentAnchor = 10;
            frontWing.visible = false;
            _ = body.AddChild(frontWing);

            width = 2 * Math.Max(body.width, Math.Max(rearWing.width, frontWing.width));
            height = body.height + rearWing.height + frontWing.height;
            anchor = 18;
            parentAnchor = 9;
            scaleX = scaleY = stripScale;
        }

        /// <summary>Gets the current flight mode.</summary>
        public FlightMode Mode { get; private set; } = FlightMode.Transition;

        /// <summary>Gets or sets the strip's left edge on screen, in the butterfly's parent's coordinates.</summary>
        public float ViewLeft { get; set; }

        /// <summary>Gets or sets the size of the screen the strip is shown on.</summary>
        public Vector ViewSize { get; set; }

        /// <summary>Raised when the butterfly is tapped.</summary>
        public Action Tapped { get; set; }

        /// <summary>Raised once the butterfly has flown away and removed itself.</summary>
        public Action Gone { get; set; }

        /// <summary>
        /// Sets where the butterfly perches, by its center.
        /// </summary>
        /// <param name="point">Landing point in the parent's coordinates.</param>
        public void SetLandingPoint(Vector point)
        {
            landPoint = point;
        }

        /// <summary>
        /// Sets the screen the butterfly wanders over, by its left edge. iOS
        /// <c>-[Butterfly setRightX:]</c>: the rectangle runs from that edge across five sixths
        /// of the screen, and from the top down to a fifth below the middle.
        /// </summary>
        /// <param name="left">Left edge of that screen in the parent's coordinates.</param>
        public void SetViewRect(float left)
        {
            flightRect = new Rectangle(left, 0f, ViewSize.X * 5f / 6f, ViewSize.Y * 0.7f);
            if (!IsInRect() && Mode == FlightMode.Normal)
            {
                Mode = FlightMode.Transition;
            }
            rectChanged = true;
        }

        /// <summary>
        /// Changes the flight mode. Leaving is final, and perching slows the wings to a tenth.
        /// </summary>
        /// <param name="mode">Mode to switch to.</param>
        public void SetFlyingMode(FlightMode mode)
        {
            if (Mode == mode || Mode == FlightMode.Away)
            {
                return;
            }
            if (mode == FlightMode.Landed)
            {
                wingsVelocity *= 0.1f;
            }
            else if (Mode == FlightMode.Landed)
            {
                wingsVelocity *= 10f;
            }
            Mode = mode;
        }

        /// <summary>Sends the butterfly away for good.</summary>
        public void FlyAway()
        {
            SetFlyingMode(FlightMode.Away);
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);
            UpdateWings(delta);
            float steps = delta * FramesPerSecond;
            switch (Mode)
            {
                case FlightMode.Normal:
                    NormalFlight(delta, steps);
                    break;
                case FlightMode.Transition:
                    TransitionFlight(delta, steps);
                    break;
                case FlightMode.Landing:
                    LandingFlight(steps);
                    break;
                case FlightMode.Landed:
                    x = landPoint.X;
                    y = landPoint.Y;
                    rotation = 0f;
                    break;
                case FlightMode.Away:
                    AwayFlight(steps);
                    break;
                default:
                    break;
            }
        }

        /// <inheritdoc />
        public override bool OnTouchDownXY(float tx, float ty)
        {
            float w = width * stripScale;
            float h = height * stripScale;
            float left = drawX + ((width - w) / 2f);
            float top = drawY + ((height - h) / 2f);
            if (Mode != FlightMode.Away && tx >= left && tx <= left + w && ty >= top && ty <= top + h)
            {
                Tapped?.Invoke();
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public override void Draw()
        {
            PreDraw();
            float cx = drawX + (width / 2f) + rotationCenterX;
            float cy = drawY + (height / 2f) + rotationCenterY;
            bool perched = Mode == FlightMode.Landed;

            Renderer.PushMatrix();
            Renderer.Translate(cx, cy, 0f);
            if (perched)
            {
                ApplyRotations(1f, (-15f, 0f, 0f, 1f), (35f, -0.5f, 0.5f, 0f));
            }
            Renderer.Translate(-cx, -cy, 0f);
            body.Draw();
            Renderer.PopMatrix();

            Renderer.PushMatrix();
            Renderer.Translate(cx, cy, 0f);
            if (perched)
            {
                ApplyRotations(0.95f - (wingAngle * 0.1f / 70f), (35f, -0.5f, 0.5f, 0f), (wingAngle, 1f, -0.5f, 0f));
            }
            else
            {
                ApplyRotations(1f, (wingAngle, 1f, 0f, 0f));
            }
            Renderer.Translate(-cx, -cy, 0f);
            rearWing.Draw();
            Renderer.PopMatrix();

            Renderer.PushMatrix();
            Renderer.Translate(cx, cy, 0f);
            if (perched)
            {
                ApplyRotations(1f, (35f, -0.5f, 0.5f, 0f), (-wingAngle, 1f, -0.5f, 0f));
            }
            else
            {
                ApplyRotations(1f, (-180f - wingAngle, 1f, 0f, 0f));
            }
            Renderer.Translate(-cx, -cy, 0f);
            frontWing.Draw();
            Renderer.PopMatrix();
            PostDraw();
        }

        /// <summary>
        /// Applies what a chain of OpenGL <c>glRotatef</c> calls does to the plane under an
        /// orthographic projection, after a uniform scale.
        /// </summary>
        /// <remarks>
        /// The rotations are multiplied as 3D matrices in call order and the depth row and
        /// column are dropped, which is all an orthographic projection keeps. What remains is a
        /// 2D linear map; it is split into a rotation, a scale that may mirror, and a second
        /// rotation (a 2x2 singular value decomposition), which the renderer applies natively.
        /// </remarks>
        /// <param name="scale">Uniform scale applied first.</param>
        /// <param name="rotations">Angle in degrees and axis for each <c>glRotatef</c>, in call order.</param>
        private static void ApplyRotations(float scale, params (float Degrees, float X, float Y, float Z)[] rotations)
        {
            float[,] m = { { scale, 0f, 0f }, { 0f, scale, 0f }, { 0f, 0f, scale } };
            foreach ((float degrees, float ax, float ay, float az) in rotations)
            {
                m = Multiply(m, RotationMatrix(degrees, ax, ay, az));
            }

            float e = (m[0, 0] + m[1, 1]) / 2f;
            float f = (m[0, 0] - m[1, 1]) / 2f;
            float g = (m[1, 0] + m[0, 1]) / 2f;
            float h = (m[1, 0] - m[0, 1]) / 2f;
            float q = MathF.Sqrt((e * e) + (h * h));
            float r = MathF.Sqrt((f * f) + (g * g));
            float a1 = MathF.Atan2(g, f);
            float a2 = MathF.Atan2(h, e);
            float first = (a2 + a1) / 2f;
            float second = (a2 - a1) / 2f;
            Renderer.Rotate(first * 180f / MathF.PI, 0f, 0f, 1f);
            Renderer.Scale(q + r, q - r, 1f);
            Renderer.Rotate(second * 180f / MathF.PI, 0f, 0f, 1f);
        }

        /// <summary>
        /// Builds the matrix <c>glRotatef</c> multiplies by, for column vectors.
        /// </summary>
        /// <param name="degrees">Rotation angle in degrees.</param>
        /// <param name="x">Axis X.</param>
        /// <param name="y">Axis Y.</param>
        /// <param name="z">Axis Z.</param>
        /// <returns>The 3x3 rotation matrix.</returns>
        private static float[,] RotationMatrix(float degrees, float x, float y, float z)
        {
            float length = MathF.Sqrt((x * x) + (y * y) + (z * z));
            x /= length;
            y /= length;
            z /= length;
            float radians = degrees * MathF.PI / 180f;
            float c = MathF.Cos(radians);
            float s = MathF.Sin(radians);
            float t = 1f - c;
            return new float[,]
            {
                { (t * x * x) + c, (t * x * y) - (s * z), (t * x * z) + (s * y) },
                { (t * x * y) + (s * z), (t * y * y) + c, (t * y * z) - (s * x) },
                { (t * x * z) - (s * y), (t * y * z) + (s * x), (t * z * z) + c },
            };
        }

        /// <summary>Multiplies two 3x3 matrices.</summary>
        /// <param name="a">Left matrix.</param>
        /// <param name="b">Right matrix.</param>
        /// <returns><paramref name="a"/> times <paramref name="b"/>.</returns>
        private static float[,] Multiply(float[,] a, float[,] b)
        {
            float[,] result = new float[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    result[i, j] = (a[i, 0] * b[0, j]) + (a[i, 1] * b[1, j]) + (a[i, 2] * b[2, j]);
                }
            }
            return result;
        }

        /// <summary>
        /// Flaps the wings between their low and high angles, pausing between flaps while perched.
        /// </summary>
        /// <param name="delta">Seconds since the last update.</param>
        private void UpdateWings(float delta)
        {
            if (flapDelay > 0f)
            {
                flapDelay = MathF.Max(0f, flapDelay - delta);
                if (flapDelay > 0f)
                {
                    return;
                }
            }

            wingAngle += wingVelocity * 100f * delta;
            if (wingAngle > WingAngleHigh)
            {
                wingVelocity = -RandomBetween(wingsVelocity * 0.7f, (wingsVelocity * 0.7f) + 1f);
                wingAngle = WingAngleHigh;
            }
            else if (wingAngle < WingAngleLow)
            {
                if (Mode == FlightMode.Landed)
                {
                    flapDelay = RandomBetween(-0.3f, 0.5f);
                }
                wingVelocity = RandomBetween(wingsVelocity, wingsVelocity + 1f);
                wingAngle = WingAngleLow;
            }
        }

        /// <summary>Wanders with a jittering heading, turning back once it strays.</summary>
        /// <param name="delta">Seconds since the last update.</param>
        /// <param name="steps">60 Hz frames the update stands for.</param>
        private void NormalFlight(float delta, float steps)
        {
            direction = Normalize(new Vector(
                direction.X + RandomBetween(-0.09f, 0.09f),
                direction.Y + RandomBetween(-0.09f, 0.09f)));
            Move(MovingVelocity * steps);
            if (IsInRect())
            {
                normalTimeOutOfRect = 0f;
            }
            else
            {
                normalTimeOutOfRect += delta;
                if (normalTimeOutOfRect > StraySeconds)
                {
                    SetFlyingMode(FlightMode.Transition);
                    normalTimeOutOfRect = 0f;
                }
            }
        }

        /// <summary>
        /// Heads for a point in the middle of the rectangle, much faster while off screen, and
        /// settles into normal flight once it has been inside long enough.
        /// </summary>
        /// <param name="delta">Seconds since the last update.</param>
        /// <param name="steps">60 Hz frames the update stands for.</param>
        private void TransitionFlight(float delta, float steps)
        {
            bool onScreen = IsOnScreenOrAdjacent();
            float velocity = MovingVelocity * (onScreen ? 1f : 30f);
            if (!transitionDirectionSet || rectChanged || transitionSteps >= 100)
            {
                Vector target = RandomPointInSmallerRect();
                direction = Normalize(new Vector(target.X - x, target.Y - y));
                transitionDirectionSet = true;
                rectChanged = false;
                transitionSteps = 0;
            }
            transitionSteps++;
            Move(velocity * steps);
            if (IsInRect())
            {
                transitionTimeInRect += delta;
                if (transitionTimeInRect > (onScreen ? SettleSecondsOnScreen : SettleSecondsOffScreen))
                {
                    SetFlyingMode(FlightMode.Normal);
                    transitionTimeInRect = 0f;
                    transitionDirectionSet = false;
                }
            }
            else
            {
                transitionTimeInRect = 0f;
            }
        }

        /// <summary>Flutters toward the landing point and perches once within reach.</summary>
        /// <param name="steps">60 Hz frames the update stands for.</param>
        private void LandingFlight(float steps)
        {
            float velocity = MovingVelocity * (IsOnScreenOrAdjacent() ? 1f : 20f);
            Vector toLand = new(landPoint.X - x, landPoint.Y - y);
            float reach = 8f * IosToWorld * stripScale;
            if (MathF.Abs(toLand.X) < reach && MathF.Abs(toLand.Y) < reach)
            {
                SetFlyingMode(FlightMode.Landed);
            }
            // Never further than the perch itself: iOS can overshoot at its off-screen speed and
            // circle the perch without settling.
            direction = Normalize(toLand);
            float remaining = MathF.Sqrt((toLand.X * toLand.X) + (toLand.Y * toLand.Y));
            x += MathF.Min(velocity * steps * PositiveRandom(), remaining) * direction.X;
            y += MathF.Min(velocity * steps * PositiveRandom(), remaining) * direction.Y;
            rotation = HeadingDegrees();
        }

        /// <summary>Flies straight on and removes itself once off screen.</summary>
        /// <param name="steps">60 Hz frames the update stands for.</param>
        private void AwayFlight(float steps)
        {
            Move(MovingVelocity * steps);
            if (!IsOnScreenOrAdjacent())
            {
                parent?.RemoveChild(this);
                Gone?.Invoke();
            }
        }

        /// <summary>Gets the distance flown per 60 Hz frame (iOS 16).</summary>
        private float MovingVelocity => 16f * IosToWorld * stripScale;

        /// <summary>Moves along the heading and turns to face it.</summary>
        /// <param name="distance">Distance to move.</param>
        private void Move(float distance)
        {
            x += direction.X * distance;
            y += direction.Y * distance;
            rotation = HeadingDegrees();
        }

        /// <summary>Gets the heading as a rotation in degrees.</summary>
        /// <returns>The heading's angle.</returns>
        private float HeadingDegrees()
        {
            return MathF.Atan2(direction.Y, direction.X) * 180f / MathF.PI;
        }

        /// <summary>Gets whether the butterfly is inside its rectangle.</summary>
        /// <returns><see langword="true"/> when inside.</returns>
        private bool IsInRect()
        {
            return x >= flightRect.x && x <= flightRect.x + flightRect.w
                && y >= flightRect.y && y <= flightRect.y + flightRect.h;
        }

        /// <summary>
        /// Gets whether the butterfly is on screen or close to it: half a screen to either side,
        /// a fifth above, a fifth below.
        /// </summary>
        /// <returns><see langword="true"/> when on or near the screen.</returns>
        private bool IsOnScreenOrAdjacent()
        {
            float screenX = x - ViewLeft;
            return screenX >= -ViewSize.X / 2f && screenX <= ViewSize.X * 1.5f
                && y >= -ViewSize.Y / 5f && y <= ViewSize.Y * 1.2f;
        }

        /// <summary>Gets a random point in the middle third of the rectangle, both ways.</summary>
        /// <returns>The point.</returns>
        private Vector RandomPointInSmallerRect()
        {
            float thirdW = flightRect.w / 3f;
            float thirdH = flightRect.h / 3f;
            return new Vector(
                RandomBetween(flightRect.x + thirdW, flightRect.x + flightRect.w - thirdW),
                RandomBetween(flightRect.y + thirdH, flightRect.y + flightRect.h - thirdH));
        }

        /// <summary>Gets a random value between two bounds.</summary>
        /// <param name="from">Lower bound.</param>
        /// <param name="to">Upper bound.</param>
        /// <returns>The value.</returns>
        private float RandomBetween(float from, float to)
        {
            return from + ((to - from) * random.Next(100) / 100f);
        }

        /// <summary>Gets a random value from 0 up to 1.</summary>
        /// <returns>The value.</returns>
        private float PositiveRandom()
        {
            return random.Next(256) / 256f;
        }

        /// <summary>Normalizes a vector, leaving a zero vector pointing left.</summary>
        /// <param name="v">Vector to normalize.</param>
        /// <returns>The unit vector.</returns>
        private static Vector Normalize(Vector v)
        {
            float length = MathF.Sqrt((v.X * v.X) + (v.Y * v.Y));
            return length > 0f ? new Vector(v.X / length, v.Y / length) : new Vector(-1f, 0f);
        }
    }
}
