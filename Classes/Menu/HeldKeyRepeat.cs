namespace Interstellar.Classes.Menu
{
    public sealed class HeldKeyRepeat
    {
        public const float InitialDelay = 0.4f;
        public const float Interval = 0.05f;

        private bool held;
        private bool suppressed;
        private float nextRepeat;

        public bool Poll(bool pressed, float now, bool allowRepeat = true)
        {
            if (!pressed)
            {
                Reset();
                return false;
            }
            if (suppressed) return false;
            if (!held)
            {
                held = true;
                nextRepeat = now + InitialDelay;
                return true;
            }
            if (!allowRepeat || now < nextRepeat) return false;

            nextRepeat = now + Interval;
            return true;
        }

        public void SuppressUntilRelease()
        {
            held = true;
            suppressed = true;
        }

        public void Reset()
        {
            held = false;
            suppressed = false;
            nextRepeat = 0f;
        }
    }
}
