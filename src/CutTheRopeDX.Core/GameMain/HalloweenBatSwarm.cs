using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// The bat swarm that Cut the Rope 3.3.0 bursts over a three-star result during Halloween, in
    /// place of the confetti. A port of its <c>fx/bats.zps</c> effect, using the iOS values, run
    /// the way that build's particle system runs it.
    /// </summary>
    /// <remarks>
    /// Every animated value is a formula <c>a0 + a1·x + a2·x² + a3·sin(a4·x + a5)</c> whose
    /// coefficients are rolled once per bat. Position and rotation (degrees) run on the bat's age
    /// in seconds; scale and color run on its age as a fraction of its lifetime. Each layer waits
    /// out its postpone time, then releases all its bats at once.
    /// </remarks>
    internal sealed class HalloweenBatSwarm : BaseElement
    {
        /// <summary>
        /// Converts the effect's 640x960 screen units to design units. The bat art is the 1280x1920
        /// sheet packed at 0.78, so one authored unit spans 2 × 0.78 design units.
        /// </summary>
        private const float AuthoredToDesign = 2f;

        /// <summary>Seconds each bat lives.</summary>
        private const float Lifetime = 3f;

        /// <summary>The three layers of <c>bats.zps</c>, drawn in this order.</summary>
        private static readonly LayerSpec[] Layers =
        [
            new(Count: 10, Postpone: new(0.2f, 0.1f), Quad: 1, DiveSpeedSpread: 1000f, Scale: SmallScale()),
            new(Count: 30, Postpone: new(0.1f, 0.1f), Quad: 2, DiveSpeedSpread: 1000f, Scale: SmallScale()),
            new(Count: 30, Postpone: new(0f, 0f), Quad: 3, DiveSpeedSpread: 1100f, Scale: LargeScale()),
        ];

        /// <summary>Per-layer containers, so each layer draws over the one before it.</summary>
        private readonly BaseElement[] layerGroups = new BaseElement[Layers.Length];

        /// <summary>Postpone time rolled for each layer.</summary>
        private readonly float[] postpone = new float[Layers.Length];

        /// <summary>Whether each layer has released its bats.</summary>
        private readonly bool[] released = new bool[Layers.Length];

        /// <summary>Seconds since the swarm started.</summary>
        private float elapsed;

        /// <summary>
        /// Gets whether the bats stand in for the three-star confetti: in the Halloween period,
        /// with the classic menus. The Experiments menus keep their own confetti.
        /// </summary>
        public static bool ReplacesConfetti => SpecialEvents.IsHalloween && !MenuTheme.IsExperiments;

        /// <summary>Gets the number of bats still flying.</summary>
        public int LiveBats
        {
            get
            {
                int live = 0;
                foreach (BaseElement group in layerGroups)
                {
                    for (int i = 0; i < group.ChildsCount(); i++)
                    {
                        if (group.GetChild(i) is { visible: true })
                        {
                            live++;
                        }
                    }
                }
                return live;
            }
        }

        /// <summary>Gets whether every layer has released its bats and the last one has gone.</summary>
        public bool Finished => Array.TrueForAll(released, r => r) && LiveBats == 0;

        /// <summary>
        /// Creates a swarm whose origin is the point the bats fly out from: the top center of the
        /// box it is added to.
        /// </summary>
        public HalloweenBatSwarm()
        {
            parentAnchor = 9;
            x = ViewportLayout.DesignWidth / 2f;
            y = 0f;
            touchable = false;
            for (int i = 0; i < Layers.Length; i++)
            {
                layerGroups[i] = new BaseElement { parentAnchor = 9 };
                _ = AddChild(layerGroups[i]);
                postpone[i] = Layers[i].Postpone.Sample();
            }
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);
            elapsed += delta;
            for (int i = 0; i < Layers.Length; i++)
            {
                if (released[i] || elapsed < postpone[i])
                {
                    continue;
                }
                released[i] = true;
                for (int n = 0; n < Layers[i].Count; n++)
                {
                    _ = layerGroups[i].AddChild(Bat.Create(Layers[i]));
                }
            }
        }

        /// <summary>Scale formula of the first two layers.</summary>
        /// <returns>The coefficient ranges.</returns>
        private static Formula SmallScale()
        {
            return new(new(0.5f, 0f), new(0.75f, 0.25f), new(0f, 0.5f), new(0.5f, 0.5f), new(5f, 1f), default);
        }

        /// <summary>Scale formula of the third layer, which starts larger.</summary>
        /// <returns>The coefficient ranges.</returns>
        private static Formula LargeScale()
        {
            return new(new(0.75f, 0f), new(0.75f, 0.25f), new(0.5f, 0.5f), new(0.5f, 0.5f), new(5f, 1f), default);
        }

        /// <summary>
        /// A value rolled in <c>[Value, Value + Delta]</c>; a negative delta reaches below it.
        /// </summary>
        /// <param name="Value">Start of the range.</param>
        /// <param name="Delta">Width of the range.</param>
        internal readonly record struct Range(float Value, float Delta)
        {
            /// <summary>Rolls a value in the range.</summary>
            /// <returns>The rolled value.</returns>
            public float Sample()
            {
                return Value + (RND_0_1 * Delta);
            }
        }

        /// <summary>Ranges for the six coefficients of one formula.</summary>
        /// <param name="A0">Constant term.</param>
        /// <param name="A1">Linear term.</param>
        /// <param name="A2">Quadratic term.</param>
        /// <param name="A3">Sine amplitude.</param>
        /// <param name="A4">Sine frequency, in radians per unit of the input.</param>
        /// <param name="A5">Sine phase, in radians.</param>
        internal readonly record struct Formula(Range A0, Range A1, Range A2, Range A3, Range A4, Range A5)
        {
            /// <summary>Rolls every coefficient.</summary>
            /// <returns>The rolled curve.</returns>
            public Curve Sample()
            {
                return new(A0.Sample(), A1.Sample(), A2.Sample(), A3.Sample(), A4.Sample(), A5.Sample());
            }
        }

        /// <summary>One rolled formula, <c>a0 + a1·x + a2·x² + a3·sin(a4·x + a5)</c>.</summary>
        /// <param name="A0">Constant term.</param>
        /// <param name="A1">Linear term.</param>
        /// <param name="A2">Quadratic term.</param>
        /// <param name="A3">Sine amplitude.</param>
        /// <param name="A4">Sine frequency.</param>
        /// <param name="A5">Sine phase.</param>
        internal readonly record struct Curve(float A0, float A1, float A2, float A3, float A4, float A5)
        {
            /// <summary>Evaluates the curve.</summary>
            /// <param name="t">Input: seconds or lifetime fraction, depending on the property.</param>
            /// <returns>The curve's value at <paramref name="t"/>.</returns>
            public float At(float t)
            {
                return A0 + (A1 * t) + (A2 * t * t) + (A3 * MathF.Sin((A4 * t) + A5));
            }

            /// <summary>
            /// Shifts the constant term so the curve starts at <see cref="A0"/>, as a spawned
            /// particle's path does, whatever phase its sine starts at.
            /// </summary>
            /// <returns>The shifted curve.</returns>
            public Curve StartingAtA0()
            {
                return this with { A0 = A0 - (A3 * MathF.Sin(A5)) };
            }
        }

        /// <summary>One layer of the effect.</summary>
        /// <param name="Count">Bats released at once.</param>
        /// <param name="Postpone">Seconds before the release.</param>
        /// <param name="Quad">Bat sprite in <see cref="Resources.Img.FxHalloween"/>.</param>
        /// <param name="DiveSpeedSpread">Width of the downward launch speed range, from 100.</param>
        /// <param name="Scale">Scale over the bat's lifetime fraction.</param>
        private sealed record LayerSpec(int Count, Range Postpone, int Quad, float DiveSpeedSpread, Formula Scale);

        /// <summary>
        /// One bat: swoops down from the swarm's origin, falls back up under negative gravity,
        /// drifting sideways, wobbling and growing as it comes towards the viewer.
        /// </summary>
        private sealed class Bat : Image
        {
            /// <summary>Gray tint range shared by each color channel.</summary>
            private static readonly Range Tint = new(0.501961f, 0.12549f);

            /// <summary>Horizontal path, in authored units over seconds.</summary>
            private Curve pathX;

            /// <summary>Vertical path, in authored units over seconds; positive is down.</summary>
            private Curve pathY;

            /// <summary>Rotation in degrees over seconds.</summary>
            private Curve spin;

            /// <summary>Uniform scale over the lifetime fraction.</summary>
            private Curve growth;

            /// <summary>Seconds since the bat was released.</summary>
            private float age;

            /// <summary>Creates a bat with freshly rolled coefficients, placed at its start.</summary>
            /// <param name="layer">Layer the bat belongs to.</param>
            /// <returns>The new bat.</returns>
            public static Bat Create(LayerSpec layer)
            {
                Bat bat = InitializeFromResource(new Bat(), Resources.Img.FxHalloween, layer.Quad);
                bat.anchor = 18;
                bat.parentAnchor = 9;
                bat.touchable = false;
                bat.color = new RGBAColor(Tint.Sample(), Tint.Sample(), Tint.Sample(), 1f);
                bat.pathX = new Formula(new(-200f, 400f), new(-300f, 600f), default, new(-20f, 40f), new(10f, 5f), default)
                    .Sample()
                    .StartingAtA0();
                bat.pathY = new Formula(new(100f, -200f), new(100f, layer.DiveSpeedSpread), new(-500f, 0f), new(-20f, 40f), new(10f, 2f), default)
                    .Sample()
                    .StartingAtA0();
                bat.spin = new Formula(new(-30f, 60f), new(-10f, 20f), default, new(10f, 10f), new(20f, 10f), new(10f, 5f)).Sample();
                bat.growth = layer.Scale.Sample();
                bat.Pose();
                return bat;
            }

            /// <inheritdoc />
            public override void Update(float delta)
            {
                base.Update(delta);
                Pose();
                age += delta;
                if (age > Lifetime)
                {
                    visible = false;
                    updateable = false;
                }
            }

            /// <summary>Places the bat where its curves put it at its current age.</summary>
            private void Pose()
            {
                float lifeFraction = age / Lifetime;
                x = pathX.At(age) * AuthoredToDesign;
                y = pathY.At(age) * AuthoredToDesign;
                rotation = spin.At(age);
                scaleX = scaleY = growth.At(lifeFraction);
            }
        }
    }
}
