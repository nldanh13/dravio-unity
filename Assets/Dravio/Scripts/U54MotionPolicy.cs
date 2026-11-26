namespace Dravio
{
    // Pure guard for applying the CHAR.7 contract. Does not authorize a new medium.
    public static class U54MotionPolicy
    {
        public static bool MayMove(bool live,bool visible,bool ground,bool canTranslate,bool actionPlaying)
            =>live && visible && ground && canTranslate && !actionPlaying;
    }
}
