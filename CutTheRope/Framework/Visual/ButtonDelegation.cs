namespace CutTheRope.Framework.Visual
{
    /// <summary>
    /// Strongly typed identifier passed alongside button press events.
    /// </summary>
    public readonly record struct ButtonId(int Value)
    {
        /// <summary>
        /// Implicitly creates a <see cref="ButtonId"/> from a raw numeric value.
        /// </summary>
        /// <param name="value">Button identifier previously represented as an <see cref="int"/>.</param>
        public static implicit operator ButtonId(int value) => new(value);

        /// <summary>
        /// Implicitly converts a <see cref="ButtonId"/> into its numeric representation.
        /// </summary>
        /// <param name="id">The button identifier to unwrap.</param>
        public static implicit operator int(ButtonId id) => id.Value;
    }

    public interface IButtonDelegation
    {
        /// <summary>
        /// Invoked when the button is pressed.
        /// </summary>
        /// <param name="buttonId">Typed button identifier.</param>
        void OnButtonPressed(ButtonId buttonId);
    }
}
