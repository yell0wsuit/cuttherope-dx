namespace CutTheRopeDX.Framework.Visual
{
    /// <summary>
    /// A button that confirms only after being held: it arms once held for
    /// <see cref="HoldDuration"/>, flashes once to say so, and fires on a release over it after
    /// that. Releasing earlier, or anywhere else, cancels; sliding off cancels like any button.
    /// </summary>
    internal sealed class TimedButton : Button
    {
        // Gaps between the flash's steps, in seconds: pressed, up, pressed again.
        private static readonly float[] FlashSteps = [0.1f, 0.2f, 0.1f];

        private bool holding;
        private float remaining;
        private int flashStep = -1;
        private float flashClock;

        /// <summary>Gets or sets how long the button must be held before a release confirms.</summary>
        public float HoldDuration { get; set; } = 3f;

        /// <summary>Gets whether the current hold has lasted long enough to confirm.</summary>
        public bool IsArmed => holding && remaining <= 0f;

        /// <inheritdoc />
        public override bool OnTouchDownXY(float tx, float ty)
        {
            if (!base.OnTouchDownXY(tx, ty))
            {
                return false;
            }
            holding = true;
            remaining = HoldDuration;
            flashStep = -1;
            return true;
        }

        /// <inheritdoc />
        public override bool OnTouchMoveXY(float tx, float ty)
        {
            bool held = base.OnTouchMoveXY(tx, ty);
            if (state == BUTTON_STATE.BUTTON_UP)
            {
                Cancel();
            }
            return held;
        }

        /// <inheritdoc />
        public override bool OnTouchUpXY(float tx, float ty)
        {
            if (state != BUTTON_STATE.BUTTON_DOWN)
            {
                Cancel();
                return false;
            }
            bool armed = IsArmed;
            SetState(BUTTON_STATE.BUTTON_UP);
            Cancel();
            if (armed && IsInTouchZoneXYforTouchDown(tx, ty, false))
            {
                delegateButtonDelegate?.OnButtonPressed(buttonID);
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);
            if (!holding)
            {
                return;
            }
            if (remaining > 0f)
            {
                remaining -= delta;
                if (remaining <= 0f)
                {
                    remaining = 0f;
                    flashStep = 0;
                    flashClock = 0f;
                    ShowPlate(up: true);
                }
                return;
            }
            if (flashStep < 0 || flashStep >= FlashSteps.Length)
            {
                return;
            }
            flashClock += delta;
            if (flashClock + 0.0001f >= FlashSteps[flashStep])
            {
                flashClock = 0f;
                ShowPlate(up: flashStep == 1);
                flashStep++;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// A view switch mid-hold hands the release to the view that replaced this one, so the
        /// hold is dropped here; otherwise it would arm unattended and confirm on the next tap.
        /// </remarks>
        public override void Hide()
        {
            base.Hide();
            if (holding || state == BUTTON_STATE.BUTTON_DOWN)
            {
                Cancel();
                SetState(BUTTON_STATE.BUTTON_UP);
            }
        }

        /// <summary>Ends the hold without confirming.</summary>
        private void Cancel()
        {
            holding = false;
            flashStep = -1;
        }

        /// <summary>Shows the up or the pressed plate, for the armed flash.</summary>
        /// <param name="up">Whether the up plate shows.</param>
        private void ShowPlate(bool up)
        {
            GetChild(0).SetEnabled(up);
            GetChild(1).SetEnabled(!up);
        }
    }
}
